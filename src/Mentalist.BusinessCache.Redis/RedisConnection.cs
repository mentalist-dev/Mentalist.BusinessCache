using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Mentalist.BusinessCache.Redis;

public interface IRedisConnection
{
    IRedisSubscriber GetOrCreate(RedisChannel channel);
}

public sealed class RedisConnection(
    IConnectionMultiplexer multiplexer,
    ICacheLifetime lifetime,
    ILogger<RedisConnection> logger)
    : IRedisConnection
{
    private readonly ConcurrentDictionary<RedisChannel, Lazy<RedisSubscriber>> _subscribers = new();

    public IRedisSubscriber GetOrCreate(RedisChannel channel)
    {
        var lazy = _subscribers.GetOrAdd(
            channel,
            static (ch, state) => new Lazy<RedisSubscriber>(
                () => state.CreateSubscriber(ch),
                LazyThreadSafetyMode.ExecutionAndPublication),
            this);

        return lazy.Value;
    }

    private RedisSubscriber CreateSubscriber(RedisChannel channel)
    {
        return new RedisSubscriber(channel, multiplexer, logger, lifetime.ApplicationStopping);
    }
}
