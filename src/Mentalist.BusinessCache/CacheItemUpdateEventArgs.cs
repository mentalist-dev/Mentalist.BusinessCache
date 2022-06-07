namespace Mentalist.BusinessCache;

public class CacheItemUpdateEventArgs: EventArgs
{
    public CacheItem CacheItem { get; }
    public DateTimeOffset? AbsoluteExpiration { get; }

    public CacheItemUpdateEventArgs(CacheItem cacheItem, DateTimeOffset? absoluteExpiration = null)
    {
        CacheItem = cacheItem;
        AbsoluteExpiration = absoluteExpiration;
    }
}

public delegate void CacheItemUpdateEventHandler(object sender, CacheItemUpdateEventArgs e);
