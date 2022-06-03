using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Mentalist.BusinessCache.Redis;

public interface IRedisConnection
{
    IRedisSubscriber Create(string channel);
}

public class RedisConnection: IRedisConnection, IDisposable
{
    private readonly TaskCompletionSource<ConnectionMultiplexer> _connectionCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentDictionary<string, Lazy<RedisSubscriber>> _subscribers = new();
    private readonly ICacheLifetime _lifetime;
    private readonly ILogger _logger;
    private readonly ConfigurationOptions _configurationOptions;
    
    private ConnectionMultiplexer? _connection;

    public RedisConnection(RedisConnectionOptions options, ICacheLifetime lifetime, ILogger<RedisConnection> logger)
    {
        _lifetime = lifetime;
        _logger = logger;
        _configurationOptions = ConfigurationOptions.Parse(options.ConnectionString);

        Task.Factory.StartNew(
            () => Connect(lifetime.ApplicationStopping),
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    ~RedisConnection()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Dispose();
        }
    }

    private async Task Connect(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        try
        {
            await Executor.Execute(() => ConnectInternal(token), _logger, token, "Unable to connect to Redis");
        }
        catch (OperationCanceledException e)
        {
            _logger.LogWarning(e, "Redis connect loop was aborted");
            _connectionCreated.SetCanceled(token);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to create Redis connection");
            _connectionCreated.SetException(e);
        }
    }

    private async Task ConnectInternal(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        _connection = await ConnectionMultiplexer
            .ConnectAsync(_configurationOptions)
            .ConfigureAwait(false);

        _connectionCreated.SetResult(_connection);
    }

    public IRedisSubscriber Create(string channel)
    {
        var value = _subscribers.GetOrAdd(channel, new Lazy<RedisSubscriber>(() => CreateSubscriber(channel)));
        return value.Value;
    }

    private RedisSubscriber CreateSubscriber(string channel)
    {
        return new RedisSubscriber(channel, _connectionCreated.Task, _logger, _lifetime.ApplicationStopping);
    }
}
