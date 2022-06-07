namespace Mentalist.BusinessCache;

public class CacheItemEvictedEventArgs : EventArgs
{
    public string Id { get; }
    public string Key { get; }
    public bool Removed { get; }

    public CacheItemEvictedEventArgs(string id, string key, bool removed)
    {
        Id = id;
        Key = key;
        Removed = removed;
    }
}

public delegate void CacheItemEvictedEventHandler(object sender, CacheItemEvictedEventArgs e);
