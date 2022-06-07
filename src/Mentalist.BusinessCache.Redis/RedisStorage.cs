using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Mentalist.BusinessCache.Redis;

public class RedisStorage: ICacheStorage
{
    private readonly Channel<CacheItem> _distributedChannel;
    private readonly Channel<CacheItem> _refreshChannel;
    private readonly Channel<CacheItem> _removeChannel;
    private readonly ConcurrentDictionary<string, DateTime> _duplicates = new();

    private readonly RedisCacheOptions _cacheOptions;
    private readonly IRedisConnection _connection;
    private readonly ICacheSerializer _serializer;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<RedisConnection> _logger;
    private readonly ICacheMetrics _metrics;

    private int _setQueueSize;
    private int _refreshQueueSize;
    private int _removeQueueSize;

    private bool _circuitBreakerOpen;
    private DateTime _circuitBreakerOpenTimestamp = DateTime.MinValue;
    private int _consecutiveExceptions;

    public RedisStorage(RedisCacheOptions cacheOptions
        , IRedisConnection connection
        , ICacheSerializer serializer
        , IDistributedCache distributedCache
        , ICacheLifetime lifetime
        , ILogger<RedisConnection> logger
        , ICacheMetrics metrics)
    {
        _cacheOptions = cacheOptions;
        _connection = connection;
        _serializer = serializer;
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

    public event CacheItemUpdateEventHandler? Updated;
    public event CacheItemEvictedEventHandler? Evicted;

    public void Set<T>(CacheItem<T> cacheItem)
    {
        using var timer = _metrics.SecondLevelSet<T>();

        if (!SetInternal(cacheItem))
        {
            _metrics.SecondLevelSetFailed<T>();
        }
    }

    private bool SetInternal(CacheItem cacheItem)
    {
        if (_distributedChannel.Writer.TryWrite(cacheItem))
        {
            var queueSize = Interlocked.Increment(ref _setQueueSize);
            _metrics.ReportSecondLevelSetQueueSize(queueSize);
            return true;
        }

        return false;
    }

    public async Task<CacheItem<T>?> GetAsync<T>(string key, CancellationToken token)
    {
        using var timer = _metrics.SecondLevelGet<T>();

        if (_cacheOptions.CircuitBreakerEnabled && 
            _circuitBreakerOpen && 
            _circuitBreakerOpenTimestamp > DateTime.UtcNow.Subtract(_cacheOptions.CircuitBreakerDuration))
        {
            _metrics.ReportSecondLevelCircuitBreaker(true);
            return null;
        }

        if (_circuitBreakerOpen)
        {
            _circuitBreakerOpen = false;
            _logger.LogWarning("Cache circuit breaker closed");
        }

        _metrics.ReportSecondLevelCircuitBreaker(false);

        try
        {
            byte[]? buffer = null;
            var finished = false;
            var counter = 0;
            while (!finished)
            {
                counter += 1;

                if (counter > 1)
                {
                    _metrics.SecondLevelRetry<T>();
                }

                try
                {
                    buffer = await _distributedCache.GetAsync(key, token);
                    finished = true;

                    if (_cacheOptions.CircuitBreakerEnabled)
                    {
                        Interlocked.Exchange(ref _consecutiveExceptions, 0);
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    if (!_cacheOptions.RetriesEnabled || counter > _cacheOptions.RetriesCount)
                    {
                        throw;
                    }

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(e,
                            "Unable to get {CacheItemType} key {CacheKey} from distributed cache. Will retry in {CacheRetryDelay}ms.",
                            typeof(T), key, _cacheOptions.RetriesDelay
                        );
                    }

                    if (_cacheOptions.RetriesDelay > 0)
                    {
                        Thread.Sleep(_cacheOptions.RetriesDelay);
                    }
                }
            }

            if (buffer != null)
            {
                var item = _serializer.Deserialize<CacheItem<T>>(buffer);
                if (item != null)
                {
                    _metrics.SecondLevelHit<T>();
                    return item;
                }
            }
        }
        catch (Exception e)
        {
            _metrics.SecondLevelGetFailed<T>();

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(e, "Unable to get {CacheItemType} key {CacheKey} from distributed cache", typeof(T), key);

            if (_cacheOptions.CircuitBreakerEnabled)
            {
                var exceptionCount = Interlocked.Increment(ref _consecutiveExceptions);
                if (exceptionCount >= _cacheOptions.CircuitBreakerExceptionCount)
                {
                    _circuitBreakerOpenTimestamp = DateTime.UtcNow;
                    _circuitBreakerOpen = true;
                    _metrics.ReportSecondLevelCircuitBreaker(true);
                    _logger.LogWarning("Cache circuit breaker opened");
                    return null;
                }
            }
        }

        _metrics.SecondLevelMis<T>();

        return null;
    }

    public CacheItem<T>? Get<T>(string key)
    {
        using var timer = _metrics.SecondLevelGet<T>();

        if (_cacheOptions.CircuitBreakerEnabled &&
            _circuitBreakerOpen &&
            _circuitBreakerOpenTimestamp > DateTime.UtcNow.Subtract(_cacheOptions.CircuitBreakerDuration))
        {
            _metrics.ReportSecondLevelCircuitBreaker(true);
            return null;
        }

        if (_circuitBreakerOpen)
        {
            _circuitBreakerOpen = false;
            _logger.LogWarning("Cache circuit breaker closed");
        }

        _metrics.ReportSecondLevelCircuitBreaker(false);

        try
        {
            byte[]? buffer = null;
            var finished = false;
            var counter = 0;
            while (!finished)
            {
                counter += 1;

                if (counter > 1)
                {
                    _metrics.SecondLevelRetry<T>();
                }

                try
                {
                    buffer = _distributedCache.Get(key);
                    finished = true;

                    if (_cacheOptions.CircuitBreakerEnabled)
                    {
                        Interlocked.Exchange(ref _consecutiveExceptions, 0);
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    if (!_cacheOptions.RetriesEnabled || counter > _cacheOptions.RetriesCount)
                    {
                        throw;
                    }

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(e,
                            "Unable to get {CacheItemType} key {CacheKey} from distributed cache. Will retry in {CacheRetryDelay}ms.",
                            typeof(T), key, _cacheOptions.RetriesDelay
                        );
                    }

                    if (_cacheOptions.RetriesDelay > 0)
                    {
                        Thread.Sleep(_cacheOptions.RetriesDelay);
                    }
                }
            }

            if (buffer != null)
            {
                var item = _serializer.Deserialize<CacheItem<T>>(buffer);
                if (item != null)
                {
                    _metrics.SecondLevelHit<T>();
                    return item;
                }
            }
        }
        catch (Exception e)
        {
            _metrics.SecondLevelGetFailed<T>();

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(e, "Unable to get {CacheItemType} key {CacheKey} from distributed cache", typeof(T), key);

            if (_cacheOptions.CircuitBreakerEnabled)
            {
                var exceptionCount = Interlocked.Increment(ref _consecutiveExceptions);
                if (exceptionCount >= _cacheOptions.CircuitBreakerExceptionCount)
                {
                    _circuitBreakerOpenTimestamp = DateTime.UtcNow;
                    _circuitBreakerOpen = true;
                    _metrics.ReportSecondLevelCircuitBreaker(true);
                    _logger.LogWarning("Cache circuit breaker opened");
                    return null;
                }
            }
        }

        _metrics.SecondLevelMis<T>();

        return null;
    }

    public void Remove<T>(string key)
    {
        using var timer = _metrics.SecondLevelRemove<T>();

        var cacheItem = new CacheItem {Key = key};
        if (_removeChannel.Writer.TryWrite(cacheItem))
        {
            var queueSize = Interlocked.Increment(ref _removeQueueSize);
            _metrics.ReportSecondLevelRemoveQueueSize(queueSize);
        }
    }

    public void Refresh(CacheItem cacheItem)
    {
        if (_refreshChannel.Writer.TryWrite(cacheItem))
        {
            var queueSize = Interlocked.Increment(ref _refreshQueueSize);
            _metrics.ReportSecondLevelRefreshQueueSize(queueSize);
        }
    }

    public Task ValidateStorageAsync(CancellationToken token)
    {
        var value = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
        var options = new DistributedCacheEntryOptions {AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)};
        return _distributedCache.SetAsync(Guid.NewGuid().ToString(), value, options, token);
    }

    private async Task DistributedChannelConsumer(CancellationToken cancellationToken)
    {
        try
        {
            var channel = _connection.Create(_cacheOptions.RedisSubscriptionChannel);
            channel.Subscribe(redis =>
            {
                var message = redis.ToString();
                var action = char.ToUpper(message[0]);
                var remove = action == 'R';
                var id = message[1..33];
                var key = message[33..];

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Notification received. Cache key {CacheKey}. Removed = {IsCacheRemove}/{CacheAction}.", key, remove.ToString(), action);
                }

                Evicted?.Invoke(this, new CacheItemEvictedEventArgs(id, key, remove));
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

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Refreshing item with key {CacheKey}", item.Key);
                    }

                    await Executor.Execute(
                        () => Refresh(item, cancellationToken),
                        _logger, cancellationToken,
                        "Unable to refresh distributed cache value"
                    );
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

    private async Task Refresh(CacheItem item, CancellationToken cancellationToken)
    {
        var key = item.Key;
        if (key == "51e694a9-75ef-44bb-b51e-67fbe57ecc5d.OperationContextAccessor.Users")
        {
            System.Diagnostics.Trace.WriteLine("ouch");
        }
        var buffer = await _distributedCache.GetAsync(key, cancellationToken);
        if (buffer != null)
        {
            var clone = item.Create(_serializer, buffer);
            if (clone != null && clone.Timestamp < item.Timestamp)
            {
                // distributed cache contains older item (lets update it)
                SetInternal(item);

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Scheduled refresh of distributed cache item with key {CacheKey} from memory cache", key);
                }
            }
            else if (clone != null && !clone.IsExpired())
            {
                Updated?.Invoke(this, new CacheItemUpdateEventArgs(clone, item.AbsoluteExpiration));
            }
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
                    var queueSize = Interlocked.Decrement(ref _removeQueueSize);
                    _metrics.ReportSecondLevelRemoveQueueSize(queueSize);

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Removing item with key {CacheKey}", item.Key);
                    }

                    await Executor.Execute(
                        () => Remove(item, cancellationToken),
                        _logger, cancellationToken,
                        "Unable to remove distributed cache value"
                    );
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

    private async Task Remove(CacheItem item, CancellationToken cancellationToken)
    {
        var id = item.Id;
        var key = item.Key;
        await _distributedCache.RemoveAsync(key, cancellationToken);

        var message = $"R{id}{key}";
        var channel = _connection.Create(_cacheOptions.RedisSubscriptionChannel);
        channel.Publish(new RedisValue(message));
    }
}