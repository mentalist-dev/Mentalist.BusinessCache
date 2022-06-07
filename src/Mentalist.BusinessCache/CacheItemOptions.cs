namespace Mentalist.BusinessCache;

public interface ICacheItemOptions
{
    ICacheItemOptions DisableStorage();
    ICacheItemOptions DisableRefreshAfterGet();
}

internal class CacheItemOptions: ICacheItemOptions
{
    internal bool? StorageEnabled { get; set; }
    internal bool? RefreshAfterGetEnabled { get; set; }

    public ICacheItemOptions DisableStorage()
    {
        StorageEnabled = false;
        return this;
    }

    public ICacheItemOptions DisableRefreshAfterGet()
    {
        RefreshAfterGetEnabled = false;
        return this;
    }
}