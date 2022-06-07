using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Mentalist.BusinessCache;

public interface ICache
{
    void Set<T>(string key, T value, TimeSpan? relativeExpiration = null);

    Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default);
    T Get<T>(string key, T defaultValue);

    Task<CacheItem<T>?> GetCacheItemAsync<T>(string key, CancellationToken cancellationToken = default);
    CacheItem<T>? GetCacheItem<T>(string key);

    void Remove<T>(string key);
}

public class Cache : ICache
{
    private readonly CacheOptions _options;
    private readonly IMemoryCache _memoryCache;
    private readonly ICacheStorage _storage;
    private readonly ILogger<Cache> _logger;
    private readonly ICacheMetrics _metrics;

    public Cache(CacheOptions options
        , IMemoryCache memoryCache
        , ICacheStorage storage
        , ILogger<Cache> logger
        , ICacheMetrics metrics)
    {
        _options = options;
        _memoryCache = memoryCache;
        _storage = storage;
        _logger = logger;
        _metrics = metrics;

        _storage.Updated += StorageCacheUpdated;
        _storage.Evicted += StorageCacheEvicted;
    }

    private void StorageCacheUpdated(object sender, CacheItemUpdateEventArgs e)
    {
        var item = e.CacheItem;
        var cacheKey = item.Key;

        _memoryCache.Set(cacheKey, item, new MemoryCacheEntryOptions {AbsoluteExpiration = item.AbsoluteExpiration});

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Refreshed memory cache item with key {CacheKey} from distributed cache", cacheKey);
        }
    }

    private void StorageCacheEvicted(object sender, CacheItemEvictedEventArgs e)
    {
        var cacheKey = e.Key;

        var item = _memoryCache.Get<CacheItem>(cacheKey);
        if (item != null && (item.Id != e.Id || e.Removed))
        {
            _memoryCache.Remove(cacheKey);

            if (!string.IsNullOrWhiteSpace(item.Type))
            {
                _metrics.EvictedByNotification(item.Type);
            }
            else
            {
                _metrics.EvictedByNotification("-");
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Subscription evicted memory cache item with key {CacheKey}", cacheKey);
            }

            if (!e.Removed && _options.RefreshAfterEviction)
            {
                _storage.Refresh(item);
            }
        }
    }

    public void Set<T>(string key, T value, TimeSpan? relativeExpiration = null)
    {
        if (value == null)
            return;

        using var timer = _metrics.Set<T>();

        try
        {
            var expiration = relativeExpiration ?? _options.DefaultRelativeExpiration;
            DateTimeOffset? absoluteExpiration = null;
            if (expiration > TimeSpan.Zero)
            {
                absoluteExpiration = DateTimeOffset.UtcNow.Add(expiration);
            }

            var cacheItem = new CacheItem<T> { Key = key, Value = value, AbsoluteExpiration = absoluteExpiration };
            _memoryCache.Set(key, cacheItem, new MemoryCacheEntryOptions { AbsoluteExpiration = absoluteExpiration });

            _storage.Set(cacheItem);
        }
        catch (Exception e)
        {
            _metrics.SetFailed<T>();
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(e, "Unable to set value for key {CacheKey}", key);
            }
        }
    }

    public async Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
    {
        var cacheItem = await GetCacheItemAsync<T>(key, cancellationToken);
        if (cacheItem == null)
            return defaultValue;
        return cacheItem.Value;
    }

    public T Get<T>(string key, T defaultValue)
    {
        var cacheItem = GetCacheItem<T>(key);
        if (cacheItem == null)
            return defaultValue;
        return cacheItem.Value;
    }

    public async Task<CacheItem<T>?> GetCacheItemAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        using var timer = _metrics.Get<T>();

        try
        {
            var item = _memoryCache.Get<CacheItem<T>>(key);
            if (item != null && !item.IsExpired())
            {
                _metrics.Hit<T>();

                RefreshAfterGet(item);

                return item;
            }
        }
        catch (Exception e)
        {
            _metrics.GetFailed<T>();
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(e, "Unable to get value for key {CacheKey}", key);
            }
        }

        _metrics.Mis<T>();

        try
        {
            var item = await _storage.GetAsync<T>(key, cancellationToken);

            if (item != null)
            {
                _memoryCache.Set(key, item, new MemoryCacheEntryOptions { AbsoluteExpiration = item.AbsoluteExpiration });

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Refreshed memory cache item with key {CacheKey} from distributed cache", key);
                }

                return item;
            }
        }
        catch (Exception e)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(e, "Unable to get {CacheItemType} value from second level cache using key {CacheKey}", typeof(T), key);
            }
        }

        return null;
    }

    public CacheItem<T>? GetCacheItem<T>(string key)
    {
        using var timer = _metrics.Get<T>();

        try
        {
            var item = _memoryCache.Get<CacheItem<T>>(key);
            if (item != null && !item.IsExpired())
            {
                _metrics.Hit<T>();

                RefreshAfterGet(item);

                return item;
            }
        }
        catch (Exception e)
        {
            _metrics.GetFailed<T>();
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(e, "Unable to get value for key {CacheKey}", key);
            }
        }

        _metrics.Mis<T>();

        try
        {
            var item = _storage.Get<T>(key);

            if (item != null)
            {
                _memoryCache.Set(key, item, new MemoryCacheEntryOptions { AbsoluteExpiration = item.AbsoluteExpiration });

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Refreshed memory cache item with key {CacheKey} from distributed cache", key);
                }

                return item;
            }
        }
        catch (Exception e)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(e, "Unable to get {CacheItemType} value from second level cache using key {CacheKey}", typeof(T), key);
            }
        }

        return null;
    }

    public void Remove<T>(string key)
    {
        using var timer = _metrics.Remove<T>();
        _memoryCache.Remove(key);
        _storage.Remove<T>(key);
    }

    private readonly ConcurrentDictionary<string, Delay> _refreshDelays = new();

    private void RefreshAfterGet<T>(CacheItem<T> item)
    {
        if (_options.RefreshAfterGet)
        {
            if (_options.RefreshAfterGetDelay <= TimeSpan.Zero)
            {
                _storage.Refresh(item);
            }
            else
            {
                var delay = new Delay {Timestamp = DateTime.UtcNow};
                var value = _refreshDelays.AddOrUpdate(item.Key, delay, (_, current) =>
                    current.Timestamp < DateTime.UtcNow.Subtract(_options.RefreshAfterGetDelay)
                        ? delay
                        : current
                );

                if (value.Id == delay.Id)
                {
                    _storage.Refresh(item);
                }
            }
        }
    }

    private sealed class Delay
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTime Timestamp { get; init; }
    }
}
