namespace Mentalist.BusinessCache;

public interface ICacheSecondLevel
{
    void Set<T>(CacheItem<T> cacheItem);
    Task<T?> GetAsync<T>(string key, CancellationToken token);
    T? Get<T>(string key); 
    void Remove<T>(string key);
    void Refresh<T>(CacheItem<T> cacheItem);
}

public class CacheSecondLevel: ICacheSecondLevel
{
    public void Set<T>(CacheItem<T> cacheItem)
    {
        //
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken token)
    {
        var value = default(T?);
        return Task.FromResult(value);
    }

    public T? Get<T>(string key)
    {
        var value = default(T?);
        return value;
    }

    public void Remove<T>(string key)
    {
        //
    }

    public void Refresh<T>(CacheItem<T> cacheItem)
    {
        // 
    }
}