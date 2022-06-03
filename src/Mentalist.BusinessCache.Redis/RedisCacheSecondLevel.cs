using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Mentalist.BusinessCache.Redis;

public class RedisCacheSecondLevel: ICacheSecondLevel
{
    private readonly Channel<CacheItem> _distributedChannel;
    private readonly Channel<CacheItem> _refreshChannel;
    private readonly Channel<CacheItem> _removeChannel;
    private readonly ConcurrentDictionary<string, DateTime> _duplicates = new();

    private readonly RedisCacheOptions _cacheOptions;
    private readonly IRedisConnection _connection;
    private readonly ICacheSerializer _serializer;
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<RedisConnection> _logger;
    private readonly ICacheMetrics _metrics;

    private int _setQueueSize;
    private int _refreshQueueSize;
    private int _removeQueueSize;

    public RedisCacheSecondLevel(RedisCacheOptions cacheOptions
        , IRedisConnection connection
        , ICacheSerializer serializer
        , IMemoryCache memoryCache
        , IDistributedCache distributedCache
        , ICacheLifetime lifetime
        , ILogger<RedisConnection> logger
        , ICacheMetrics metrics)
    {
        _cacheOptions = cacheOptions;
        _connection = connection;
        _serializer = serializer;
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _logger = logger;
        _metrics = metrics;

        var distributedCacheSetQueueSize = cacheOptions.DistributedCacheSetQueueSize;
        if (distributedCacheSetQueueSize <= 0)
            distributedCacheSetQueueSize = RedisCacheOptions.DefaultDistributedCacheSetQueueSize;
        _distributedChannel = Channel.CreateBounded<CacheItem>(distributedCacheSetQueueSize);

        var distributedCacheRefreshQueueSize = cacheOptions.DistributedCacheRefreshQueueSize;
        if (distributedCacheRefreshQueueSize <= 0)
            distributedCacheRefreshQueueSize = RedisCacheOptions.DefaultDistributedCacheRefreshQueueSize;
        _refreshChannel = Channel.CreateBounded<CacheItem>(distributedCacheRefreshQueueSize);

        var distributedCacheRemoveQueueSize = cacheOptions.DistributedCacheRemoveQueueSize;
        if (distributedCacheRemoveQueueSize <= 0)
            distributedCacheRemoveQueueSize = RedisCacheOptions.DefaultDistributedCacheRemoveQueueSize;
        _removeChannel = Channel.CreateBounded<CacheItem>(distributedCacheRemoveQueueSize);

        Task.Factory.StartNew(
            () => DistributedChannelConsumer(lifetime.ApplicationStopping),
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        Task.Factory.StartNew(
            () => RefreshChannelConsumer(lifetime.ApplicationStopping),
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        Task.Factory.StartNew(
            () => RemoveChannelConsumer(lifetime.ApplicationStopping),
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public void Set<T>(CacheItem<T> cacheItem)
    {
        using var timer = _metrics.SecondLevelSet<T>();

        if (_distributedChannel.Writer.TryWrite(cacheItem))
        {
            var queueSize = Interlocked.Increment(ref _setQueueSize);
            _metrics.ReportSecondLevelSetQueueSize(queueSize);
        }
        else
        {
            _metrics.SecondLevelSetFailed<T>();
        }
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken token)
    {
        using var timer = _metrics.SecondLevelGet<T>();

        try
        {
            var buffer = await _distributedCache.GetAsync(key, token);
            if (buffer != null)
            {
                var item = _serializer.Deserialize<CacheItem<T>>(buffer);
                if (item != null)
                {
                    if (!item.IsExpired())
                    {
                        _metrics.SecondLevelHit<T>();

                        _memoryCache.Set(key, item, new MemoryCacheEntryOptions { AbsoluteExpiration = item.AbsoluteExpiration });
                        return item.Value;
                    }
                }
            }
        }
        catch (Exception e)
        {
            _metrics.SecondLevelGetFailed<T>();

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(e, "Unable to get {CacheItemType} key {CacheKey} from distributed cache", typeof(T), key);
        }

        _metrics.SecondLevelMis<T>();

        return default;
    }

    public T? Get<T>(string key)
    {
        using var timer = _metrics.SecondLevelGet<T>();

        try
        {
            var buffer = _distributedCache.Get(key);
            if (buffer != null)
            {
                var item = _serializer.Deserialize<CacheItem<T>>(buffer);
                if (item != null)
                {
                    if (!item.IsExpired())
                    {
                        _metrics.SecondLevelHit<T>();

                        _memoryCache.Set(key, item, new MemoryCacheEntryOptions { AbsoluteExpiration = item.AbsoluteExpiration });
                        return item.Value;
                    }
                }
            }
        }
        catch (Exception e)
        {
            _metrics.SecondLevelGetFailed<T>();

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(e, "Unable to get {CacheItemType} key {CacheKey} from distributed cache", typeof(T), key);
        }

        _metrics.SecondLevelMis<T>();

        return default;
    }

    public void Remove<T>(string key)
    {
        var cacheItem = new CacheItem {Key = key};
        if (_removeChannel.Writer.TryWrite(cacheItem))
        {
            var queueSize = Interlocked.Increment(ref _removeQueueSize);
            _metrics.ReportSecondLevelRemoveQueueSize(queueSize);
        }
    }

    public void Refresh<T>(CacheItem<T> cacheItem)
    {
        RefreshInternal(cacheItem);
    }

    private void RefreshInternal(CacheItem cacheItem)
    {
        if (_refreshChannel.Writer.TryWrite(cacheItem))
        {
            var queueSize = Interlocked.Increment(ref _refreshQueueSize);
            _metrics.ReportSecondLevelRefreshQueueSize(queueSize);
        }
    }

    private async Task DistributedChannelConsumer(CancellationToken cancellationToken)
    {
        try
        {
            var channel = _connection.Create(_cacheOptions.RedisSubscriptionChannel);
            channel.Subscribe(redis =>
            {
                var message = redis.ToString();
                var remove = char.ToUpper(message[0]) == 'R';
                var id = message[1..33];
                var key = message[33..];

                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Notification received. Cache key {CacheKey}. Removal = {IsRemoval}.", key, remove.ToString());

                var item = _memoryCache.Get<CacheItem>(key);
                if (item != null && (item.Id != id || remove))
                {
                    _memoryCache.Remove(key);
                    _logger.LogInformation("Subscription evicted memory cache item with key {CacheKey}", key);

                    if (!remove)
                    {
                        RefreshInternal(item);
                    }
                }
            });

            await foreach (var item in _distributedChannel.Reader.ReadAllAsync(cancellationToken))
            {
                var id = item.Id;
                var key = item.Key;
                var expiration = item.AbsoluteExpiration;

                try
                {
                    var queueSize = Interlocked.Decrement(ref _setQueueSize);
                    _metrics.ReportSecondLevelSetQueueSize(queueSize);

                    var buffer = _serializer.Serialize(item);
                    var entryOptions = new DistributedCacheEntryOptions { AbsoluteExpiration = expiration };

                    await Executor.Execute(
                        () => Update(id, key, buffer, entryOptions, channel, cancellationToken),
                        _logger, cancellationToken,
                        "Unable to set distributed cache value"
                    );
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _logger.LogError(e, "Unable to update distributed cache key {CacheKey}", key);
                }
            }
        }
        catch (OperationCanceledException e)
        {
            _logger.LogWarning(e, "Distributed channel consumer cancelled");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Distributed channel consumer crashed");
        }
        finally
        {
            _distributedChannel.Writer.TryComplete();
        }
    }

    private async Task Update(string id, string key, byte[] buffer, DistributedCacheEntryOptions entryOptions, IRedisSubscriber channel, CancellationToken cancellationToken)
    {
        await _distributedCache.SetAsync(key, buffer, entryOptions, cancellationToken);

        if (!_duplicates.TryGetValue(id, out var lastPublication) || lastPublication < DateTime.UtcNow.AddMinutes(-1))
        {
            _duplicates[id] = DateTime.UtcNow;

            var message = $"U{id}{key}";
            channel.Publish(new RedisValue(message));
        }
    }

    private async Task RefreshChannelConsumer(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in _refreshChannel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    var queueSize = Interlocked.Decrement(ref _refreshQueueSize);
                    _metrics.ReportSecondLevelRefreshQueueSize(queueSize);

                    var key = item.Key;
                    var buffer = await _distributedCache.GetAsync(key, cancellationToken);
                    if (buffer != null)
                    {
                        var clone = item.Create(_serializer, buffer);
                        if (clone != null && clone.Timestamp < item.Timestamp)
                        {
                            // distributed cache contains older item
                            _distributedChannel.Writer.TryWrite(item);
                            _logger.LogDebug("Scheduled refresh of distributed cache item with key {CacheKey} from memory cache", key);
                        }
                        else if (clone != null && !clone.IsExpired())
                        {
                            _memoryCache.Set(key, clone, new MemoryCacheEntryOptions { AbsoluteExpiration = item.AbsoluteExpiration });
                            _logger.LogDebug("Refreshed memory cache item with key {CacheKey} from distributed cache", key);
                        }
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _logger.LogError(e, "Unable to refresh to distributed cache");
                }
            }
        }
        catch (OperationCanceledException e)
        {
            _logger.LogWarning(e, "Distributed cache refresh consumer cancelled");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Distributed cache refresh consumer crashed");
        }
        finally
        {
            _refreshChannel.Writer.TryComplete();
        }
    }

    private async Task RemoveChannelConsumer(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in _removeChannel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    var queueSize = Interlocked.Decrement(ref _refreshQueueSize);
                    _metrics.ReportSecondLevelRefreshQueueSize(queueSize);

                    var id = item.Id;
                    var key = item.Key;
                    await _distributedCache.RemoveAsync(key, cancellationToken);
                    
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Removed item with key {CacheKey}", key);

                    var message = $"R{id}{key}";
                    var channel = _connection.Create(_cacheOptions.RedisSubscriptionChannel);
                    channel.Publish(new RedisValue(message));
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _logger.LogError(e, "Unable to remove from distributed cache");
                }
            }
        }
        catch (OperationCanceledException e)
        {
            _logger.LogWarning(e, "Distributed cache remove consumer cancelled");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Distributed cache remove consumer crashed");
        }
        finally
        {
            _removeChannel.Writer.TryComplete();
        }
    }
}