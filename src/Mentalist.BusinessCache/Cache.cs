using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Mentalist.BusinessCache;

public interface ICache
{
    void Set<T>(string key, T value, TimeSpan? relativeExpiration = null);
    Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default);
    T Get<T>(string key, T defaultValue);
    void Remove<T>(string key);
}

public class Cache : ICache
{
    private readonly CacheOptions _options;
    private readonly IMemoryCache _memoryCache;
    private readonly ICacheSecondLevel _secondLevel;
    private readonly ILogger<Cache> _logger;
    private readonly ICacheMetrics _metrics;

    public Cache(CacheOptions options
        , IMemoryCache memoryCache
        , ICacheSecondLevel secondLevel
        , ILogger<Cache> logger
        , ICacheMetrics metrics)
    {
        _options = options;
        _memoryCache = memoryCache;
        _secondLevel = secondLevel;
        _logger = logger;
        _metrics = metrics;
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

            _secondLevel.Set(cacheItem);
        }
        catch (Exception e)
        {
            _metrics.SetFailed<T>();
            _logger.LogDebug(e, "Unable to set value for key {CacheKey}", key);
        }
    }

    public async Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
    {
        using var timer = _metrics.Get<T>();

        try
        {
            var item = _memoryCache.Get<CacheItem<T>>(key);
            if (item != null && !item.IsExpired())
            {
                _metrics.Hit<T>();
                _secondLevel.Refresh(item);
                return item.Value;
            }
        }
        catch (Exception e)
        {
            _metrics.GetFailed<T>();
            _logger.LogDebug(e, "Unable to get value for key {CacheKey}", key);
        }

        _metrics.Mis<T>();

        try
        {
            var value = await _secondLevel
                .GetAsync<T>(key, cancellationToken)
                .ConfigureAwait(false);

            return value ?? defaultValue;
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "Unable to get {CacheItemType} value from second level cache using key {CacheKey}", typeof(T), key);
            return defaultValue;
        }
    }

    public T Get<T>(string key, T defaultValue)
    {
        using var timer = _metrics.Get<T>();

        try
        {
            var item = _memoryCache.Get<CacheItem<T>>(key);
            if (item != null && !item.IsExpired())
            {
                _metrics.Hit<T>();
                _secondLevel.Refresh(item);
                return item.Value;
            }
        }
        catch (Exception e)
        {
            _metrics.GetFailed<T>();
            _logger.LogDebug(e, "Unable to get {CacheItemType} value for key {CacheKey}", typeof(T), key);
        }

        _metrics.Mis<T>();

        try
        {
            var value = _secondLevel.Get<T>(key);

            return value ?? defaultValue;
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "Unable to get {CacheItemType} value from second level cache using key {CacheKey}", typeof(T), key);
            return defaultValue;
        }
    }

    public void Remove<T>(string key)
    {
        using var timer = _metrics.Remove<T>();
        _memoryCache.Remove(key);
        _secondLevel.Remove<T>(key);
    }
}
