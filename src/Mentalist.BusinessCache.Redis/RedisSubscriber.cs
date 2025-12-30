using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Mentalist.BusinessCache.Redis;

public interface IRedisSubscriber
{
    void Subscribe(Action<RedisValue> onMessage);
    void Publish(RedisValue value);
}

internal class RedisSubscriber: IRedisSubscriber
{
    private readonly Channel<RedisValue> _publications = Channel.CreateBounded<RedisValue>(100_000);
    private readonly List<Action<RedisValue>> _subscriptions = [];
    private readonly object _subscriptionsLock = new();
    private readonly RedisChannel _channel;
    private readonly IConnectionMultiplexer _connection;
    private readonly ILogger _logger;

    public RedisSubscriber(RedisChannel channel, IConnectionMultiplexer connection, ILogger logger, CancellationToken token)
    {
        _channel = channel;
        _connection = connection;
        _logger = logger;
        _connection = connection;

        _ = Task.Run(() => Connect(token));
    }

    public void Subscribe(Action<RedisValue> onMessage)
    {
        lock (_subscriptionsLock)
        {
            _subscriptions.Add(onMessage);
        }
    }

    public void Publish(RedisValue value)
    {
        _publications.Writer.TryWrite(value);
    }

    private async Task Connect(CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();

            var subscriber = _connection.GetSubscriber();

            token.Register(() => subscriber.Unsubscribe(_channel));

            await Executor.Execute(() => ConnectInternal(subscriber, token), _logger, token, "Unable to subscribe to Redis channel {RedisChannel}!", _channel);

            _logger.LogInformation("Subscribed to Redis channel {RedisChannel}", _channel);

            _ = Task.Run(() => Publish(subscriber, token), CancellationToken.None);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to subscribe to Redis channel {RedisChannel}!", _channel);
        }
    }

    private async Task ConnectInternal(ISubscriber subscriber, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        await subscriber.SubscribeAsync(_channel, (_, value) => { ExecuteSubscriptions(value); });
    }

    private async Task Publish(ISubscriber subscriber, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in _publications.Reader.ReadAllAsync(cancellationToken))
            {
                await Executor.Execute(
                    () => subscriber.PublishAsync(_channel, item),
                    _logger, cancellationToken,
                    "Unable to publish notification to redis channel {RedisChannel}", _channel
                );
            }
        }
        catch (OperationCanceledException e)
        {
            _logger.LogWarning(e, "Publication consumer for channel {RedisChannel} cancelled!", _channel);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Publication consumer for channel {RedisChannel} crashed!", _channel);
        }
        finally
        {
            _publications.Writer.TryComplete();
        }
    }

    private void ExecuteSubscriptions(RedisValue value)
    {
        if (!value.HasValue) return;

        Action<RedisValue>[] snapshot;
        lock (_subscriptionsLock)
        {
            snapshot = _subscriptions.ToArray();
        }

        foreach (var onMessage in snapshot)
        {
            try
            {
                onMessage(value);
            }
            catch (Exception e)
            {
                //Console.WriteLine(e);
            }
        }
    }
}