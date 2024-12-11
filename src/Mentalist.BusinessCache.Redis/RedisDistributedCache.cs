using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;

namespace Mentalist.BusinessCache.Redis;

internal class RedisDistributedCache(RedisCache cache) : IDistributedCache
{
    public byte[]? Get(string key)
    {
        return cache.Get(key);
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken token = new())
    {
        return cache.GetAsync(key, token);
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        cache.Set(key, value, options);
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = new())
    {
        return cache.SetAsync(key, value, options, token);
    }

    public void Refresh(string key)
    {
        cache.Refresh(key);
    }

    public Task RefreshAsync(string key, CancellationToken token = new())
    {
        return cache.RefreshAsync(key, token);
    }

    public void Remove(string key)
    {
        cache.Remove(key);
    }

    public Task RemoveAsync(string key, CancellationToken token = new())
    {
        return cache.RemoveAsync(key, token);
    }
}