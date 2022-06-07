namespace Mentalist.BusinessCache;

public class CacheItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Key { get; init; } = null!;
    public string Type { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public DateTimeOffset? AbsoluteExpiration { get; init; }
    public bool IsExpired() => AbsoluteExpiration != null && AbsoluteExpiration < DateTimeOffset.UtcNow;
    
    internal CacheItemOptions? Options { get; init; }

    public virtual CacheItem? Create(ICacheSerializer serializer, byte[] buffer)
    {
        throw new NotImplementedException();
    }
}

public class CacheItem<T> : CacheItem
{
    public CacheItem()
    {
        Type = typeof(T).GetTypeName();
    }

    public T Value { get; init; } = default!;

    public override CacheItem? Create(ICacheSerializer serializer, byte[] buffer)
    {
        return serializer.Deserialize<CacheItem<T>>(buffer);
    }
}