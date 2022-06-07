using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mentalist.BusinessCache;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCache(this IServiceCollection services, Action<ICacheConfiguration>? configure = null)
    {
        services.AddMemoryCache();

        var configuration = new CacheConfiguration(services);
        if (configure != null)
        {
            configure(configuration);
        }
        else
        {
            configuration.DefaultAbsoluteExpiration(TimeSpan.FromHours(1));
        }

        services.TryAddSingleton<ICacheSerializer, CacheSerializer>();
        services.TryAddSingleton<ICache, Cache>();

        configuration.Complete();

        return services;
    }
}

public interface ICacheConfiguration
{
    internal IServiceCollection Services { get; }

    ICacheConfiguration DefaultAbsoluteExpiration(TimeSpan absoluteExpiration);
    ICacheConfiguration RefreshFromStorageAfterEviction(bool enabled = true);
    ICacheConfiguration RefreshFromStorageAfterGet(bool enabled = true, int delayRefreshInMilliseconds = 10000);
    ICacheConfiguration Lifetime<TLifetime>() where TLifetime : class, ICacheLifetime;
    ICacheConfiguration Storage<TStorage>() where TStorage : class, ICacheStorage;
    ICacheConfiguration Metrics<TMetrics>() where TMetrics : class, ICacheMetrics;
    ICacheConfiguration Serializer<TSerializer>() where TSerializer : class, ICacheSerializer;
}

internal class CacheConfiguration: ICacheConfiguration
{
    private readonly IServiceCollection _services;
    private TimeSpan _defaultAbsoluteExpiration;
    private bool _refreshAfterEviction;
    private bool _refreshAfterGet;
    private TimeSpan? _refreshAfterGetDelay;
    private Action<IServiceCollection> _configureCacheLifetime = null!;
    private Action<IServiceCollection> _configureCacheSecondLevel = null!;
    private Action<IServiceCollection> _configureCacheMetrics = null!;
    private Action<IServiceCollection> _configureSerializer = null!;

    public CacheConfiguration(IServiceCollection services)
    {
        _services = services;

        Lifetime<CacheLifetime>();
        Storage<CacheStorage>();
        Metrics<CacheMetrics>();
        Serializer<CacheSerializer>();
    }

    IServiceCollection ICacheConfiguration.Services => _services;

    public ICacheConfiguration DefaultAbsoluteExpiration(TimeSpan absoluteExpiration)
    {
        _defaultAbsoluteExpiration = absoluteExpiration;
        return this;
    }

    public ICacheConfiguration RefreshFromStorageAfterEviction(bool enabled = true)
    {
        _refreshAfterEviction = enabled;
        return this;
    }

    public ICacheConfiguration RefreshFromStorageAfterGet(bool enabled = true, int delayRefreshInMilliseconds = 10000)
    {
        _refreshAfterGet = enabled;
        if (delayRefreshInMilliseconds >= 0)
        {
            _refreshAfterGetDelay = TimeSpan.FromMilliseconds(delayRefreshInMilliseconds);
        }
        else
        {
            _refreshAfterGetDelay = TimeSpan.FromSeconds(10);
        }
        return this;
    }

    public ICacheConfiguration Lifetime<TLifetime>() where TLifetime : class, ICacheLifetime
    {
        _configureCacheLifetime = s => s.TryAddSingleton<ICacheLifetime, TLifetime>();
        return this;
    }

    public ICacheConfiguration Storage<TSecondLevelLayer>() where TSecondLevelLayer : class, ICacheStorage
    {
        _configureCacheSecondLevel = s => s.TryAddSingleton<ICacheStorage, TSecondLevelLayer>();
        return this;
    }

    public ICacheConfiguration Metrics<TMetrics>() where TMetrics : class, ICacheMetrics
    {
        _configureCacheMetrics = s => s.TryAddSingleton<ICacheMetrics, TMetrics>();
        return this;
    }

    public ICacheConfiguration Serializer<TSerializer>() where TSerializer : class, ICacheSerializer
    {
        _configureSerializer = s => s.TryAddSingleton<ICacheSerializer, TSerializer>();
        return this;
    }

    internal void Complete()
    {
        var cacheOptions = new CacheOptions
        {
            DefaultRelativeExpiration = _defaultAbsoluteExpiration,
            RefreshAfterEviction = _refreshAfterEviction,
            RefreshAfterGet = _refreshAfterGet,
            RefreshAfterGetDelay = _refreshAfterGetDelay.GetValueOrDefault(TimeSpan.FromSeconds(10))
        };

        _services.AddSingleton(cacheOptions);

        _configureCacheLifetime(_services);
        _configureCacheSecondLevel(_services);
        _configureCacheMetrics(_services);
        _configureSerializer(_services);
    }
}