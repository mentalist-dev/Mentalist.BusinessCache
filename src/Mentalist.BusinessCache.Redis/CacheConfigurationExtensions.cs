using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mentalist.BusinessCache.Redis;

public static class CacheConfigurationExtensions
{
    public static ICacheConfiguration UseRedis(this ICacheConfiguration configuration, RedisCacheOptions options)
    {
        configuration.Services.AddStackExchangeRedisCache(cacheOptions => cacheOptions.Configuration = options.RedisConnectionString);

        configuration.Services.TryAddSingleton(options);
        configuration.Services.TryAddSingleton(new RedisConnectionOptions { ConnectionString = options.RedisConnectionString });
        configuration.Services.TryAddSingleton<IRedisConnection, RedisConnection>();
        
        return configuration.SecondLevel<RedisCacheSecondLevel>();
    }
}