namespace Mentalist.BusinessCache;

public class CacheItem
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Key { get; init; } = null!;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public DateTimeOffset? AbsoluteExpiration { get; init; }
    public bool IsExpired() => AbsoluteExpiration != null && AbsoluteExpiration < DateTimeOffset.UtcNow;

    public virtual CacheItem? Create(ICacheSerializer serializer, byte[] buffer)
    {
        throw new NotImplementedException();
    }
}

public sealed class CacheItem<T> : CacheItem
{
    public T Value { get; init; } = default!;

    public override CacheItem? Create(ICacheSerializer serializer, byte[] buffer)
    {
        return serializer.Deserialize<CacheItem<T>>(buffer);
    }
}