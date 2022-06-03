namespace Mentalist.BusinessCache.Redis;

public class RedisCacheOptions
{
    public const int DefaultDistributedCacheSetQueueSize = 100_000;
    public const int DefaultDistributedCacheRefreshQueueSize = 100_000;
    public const int DefaultDistributedCacheRemoveQueueSize = 100_000;

    public string RedisConnectionString { get; set; } = string.Empty;
    public string RedisSubscriptionChannel { get; set; } = "mentalist-business-cache";

    public int DistributedCacheSetQueueSize { get; set; }
    public int DistributedCacheRefreshQueueSize { get; set; }
    public int DistributedCacheRemoveQueueSize { get; set; }
}