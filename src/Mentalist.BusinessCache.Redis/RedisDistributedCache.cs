using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;

namespace Mentalist.BusinessCache.Redis;

internal class RedisDistributedCache: IDistributedCache
{
    private readonly RedisCache _cache;

    public RedisDistributedCache(RedisCache cache)
    {
        _cache = cache;
    }

    public byte[] Get(string key)
    {
        return _cache.Get(key);
    }

    public Task<byte[]> GetAsync(string key, CancellationToken token = new())
    {
        return _cache.GetAsync(key, token);
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        _cache.Set(key, value, options);
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = new())
    {
        return _cache.SetAsync(key, value, options, token);
    }

    public void Refresh(string key)
    {
        _cache.Refresh(key);
    }

    public Task RefreshAsync(string key, CancellationToken token = new())
    {
        return _cache.RefreshAsync(key, token);
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
    }

    public Task RemoveAsync(string key, CancellationToken token = new())
    {
        return _cache.RemoveAsync(key, token);
    }
}