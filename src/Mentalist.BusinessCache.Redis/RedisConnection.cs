using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Mentalist.BusinessCache.Redis;

public interface IRedisConnection
{
    IRedisSubscriber Create(RedisChannel channel);
}

public class RedisConnection(
    IConnectionMultiplexer multiplexer,
    ICacheLifetime lifetime,
    ILogger<RedisConnection> logger)
    : IRedisConnection
{
    private readonly ConcurrentDictionary<string, Lazy<RedisSubscriber>> _subscribers = new();
    private readonly ILogger _logger = logger;

    public IRedisSubscriber Create(RedisChannel channel)
    {
        var value = _subscribers.GetOrAdd(channel.ToString(), new Lazy<RedisSubscriber>(() => CreateSubscriber(channel)));
        return value.Value;
    }

    private RedisSubscriber CreateSubscriber(RedisChannel channel)
    {
        return new RedisSubscriber(channel, multiplexer, _logger, lifetime.ApplicationStopping);
    }
}
