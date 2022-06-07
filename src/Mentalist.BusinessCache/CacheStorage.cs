namespace Mentalist.BusinessCache;

public interface ICacheStorage
{
    event CacheItemUpdateEventHandler? Updated;
    event CacheItemEvictedEventHandler? Evicted;

    void Set<T>(CacheItem<T> cacheItem);
    Task<CacheItem<T>?> GetAsync<T>(string key, CancellationToken token);
    CacheItem<T>? Get<T>(string key); 
    void Remove<T>(string key);
    void Refresh(CacheItem cacheItem);

    Task ValidateStorageAsync(CancellationToken token);
}

public class CacheStorage: ICacheStorage
{
    public event CacheItemUpdateEventHandler? Updated;
    public event CacheItemEvictedEventHandler? Evicted;

    public void Set<T>(CacheItem<T> cacheItem)
    {
        //
    }

    public Task<CacheItem<T>?> GetAsync<T>(string key, CancellationToken token)
    {
        return Task.FromResult((CacheItem<T>?)null);
    }

    public CacheItem<T>? Get<T>(string key)
    {
        return null;
    }

    public void Remove<T>(string key)
    {
        //
    }

    public void Refresh(CacheItem cacheItem)
    {
        // 
    }

    public Task ValidateStorageAsync(CancellationToken token)
    {
        return Task.CompletedTask;
    }
}