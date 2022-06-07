namespace Mentalist.BusinessCache;

public class CacheOptions
{
    public TimeSpan DefaultRelativeExpiration { get; set; }
    public bool RefreshAfterEviction { get; set; }
    public bool RefreshAfterGet { get; set; }
    public TimeSpan RefreshAfterGetDelay { get; set; }
}