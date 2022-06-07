namespace Mentalist.BusinessCache.Redis;

public class RedisCacheOptions
{
    public const int DefaultDistributedCacheSetQueueSize = 100_000;
    public const int DefaultDistributedCacheRefreshQueueSize = 100_000;
    public const int DefaultDistributedCacheRemoveQueueSize = 100_000;

    public RedisCacheOptions(string redisConnectionString)
    {
        RedisConnectionString = redisConnectionString;
    }

    public string RedisConnectionString { get; }
    public string RedisSubscriptionChannel { get; private set; } = "mentalist-business-cache";

    public int DistributedCacheSetQueueSize { get; private set; }
    public int DistributedCacheRefreshQueueSize { get; private set; }
    public int DistributedCacheRemoveQueueSize { get; private set; }
    public bool RetriesEnabled { get; private set; }
    public int RetriesCount { get; private set; }
    public int RetriesDelay { get; private set; }
    public bool CircuitBreakerEnabled { get; private set; }
    public int CircuitBreakerExceptionCount { get; private set; }
    public TimeSpan CircuitBreakerDuration { get; private set; }

    public RedisCacheOptions SubscriptionChannel(string subscriptionChannel)
    {
        RedisSubscriptionChannel = subscriptionChannel;
        return this;
    }

    public RedisCacheOptions CacheSetQueueSize(int queueSize)
    {
        DistributedCacheSetQueueSize = queueSize;
        return this;
    }
    
    public RedisCacheOptions CacheRefreshQueueSize(int queueSize)
    {
        DistributedCacheRefreshQueueSize = queueSize;
        return this;
    }
 
    public RedisCacheOptions CacheRemoveQueueSize(int queueSize)
    {
        DistributedCacheRemoveQueueSize = queueSize;
        return this;
    }

    public RedisCacheOptions EnableGetRetries(bool enabled = true, int retryCount = 10, int delayIntervalMilliseconds = 100)
    {
        RetriesEnabled = enabled;
        RetriesCount = retryCount;
        RetriesDelay = delayIntervalMilliseconds;
        return this;
    }

    public RedisCacheOptions EnableCircuitBreaker(bool enabled = true, int exceptionCount = 10, int breakDurationInMilliseconds = 10000)
    {
        CircuitBreakerEnabled = enabled;
        CircuitBreakerExceptionCount = exceptionCount;
        CircuitBreakerDuration = TimeSpan.FromMilliseconds(breakDurationInMilliseconds);
        return this;
    }
}