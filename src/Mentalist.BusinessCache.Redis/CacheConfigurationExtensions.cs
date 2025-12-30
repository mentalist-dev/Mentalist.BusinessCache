using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Mentalist.BusinessCache.Redis;

public static class CacheConfigurationExtensions
{
    public static ICacheConfiguration UseRedis(this ICacheConfiguration configuration, RedisCacheOptions options)
    {
        configuration.Services.TryAddSingleton(options);

        var opt = ConfigurationOptions.Parse(options.RedisConnectionString);
        var redisConnection = CreateRedisConnection(opt);

        configuration.Services.AddSingleton(redisConnection);

        configuration.Services.AddStackExchangeRedisCache(cacheOptions =>
        {
            cacheOptions.ConnectionMultiplexerFactory = () => Task.FromResult(redisConnection);
        });

        configuration.Services.TryAddSingleton(new RedisConnectionOptions { ConnectionString = options.RedisConnectionString });
        configuration.Services.TryAddSingleton<IRedisConnection>(sp => new RedisConnection(
            redisConnection,
            sp.GetRequiredService<ICacheLifetime>(),
            sp.GetRequiredService<ILogger<RedisConnection>>())
        );

        return configuration.Storage<RedisStorage>();
    }

    private static IConnectionMultiplexer CreateRedisConnection(ConfigurationOptions opt)
    {
        for (var i = 1; i <= 10; i++)
        {
            try
            {
                var multiplexer = ConnectionMultiplexer.Connect(opt);
                return multiplexer;
            }
            catch (Exception e)
            {
                // we cannot get logger here, but for startup code - its fine
                Console.WriteLine(e);

                if (i == 10)
                {
                    throw;
                }

                Thread.Sleep(500); // startup only; acceptable
            }
        }

        throw new Exception($"Unable to connect to Redis..");
    }
}