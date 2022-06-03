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
        services.TryAddSingleton<ICacheMetrics, CacheMetrics>();

        configuration.Complete();

        return services;
    }
}

public interface ICacheConfiguration
{
    internal IServiceCollection Services { get; }

    ICacheConfiguration DefaultAbsoluteExpiration(TimeSpan absoluteExpiration);
    ICacheConfiguration CacheLifetime<TLifetime>() where TLifetime : class, ICacheLifetime;
    ICacheConfiguration SecondLevel<TSecondLevelLayer>() where TSecondLevelLayer : class, ICacheSecondLevel;
    ICacheConfiguration Metrics<TMetrics>() where TMetrics : class, ICacheMetrics;
}

internal class CacheConfiguration: ICacheConfiguration
{
    private readonly IServiceCollection _services;
    private TimeSpan _defaultAbsoluteExpiration;
    private Action<IServiceCollection> _configureCacheLifetime;
    private Action<IServiceCollection> _configureCacheSecondLevel;
    private Action<IServiceCollection> _configureCacheMetrics;

    public CacheConfiguration(IServiceCollection services)
    {
        _services = services;
        _configureCacheLifetime = s => s.TryAddSingleton<ICacheLifetime, CacheLifetime>();
        _configureCacheSecondLevel = s => s.TryAddSingleton<ICacheSecondLevel, CacheSecondLevel>();
        _configureCacheMetrics = s => s.TryAddSingleton<ICacheMetrics, CacheMetrics>();
    }

    IServiceCollection ICacheConfiguration.Services => _services;

    public ICacheConfiguration DefaultAbsoluteExpiration(TimeSpan absoluteExpiration)
    {
        _defaultAbsoluteExpiration = absoluteExpiration;
        return this;
    }

    public ICacheConfiguration CacheLifetime<TLifetime>() where TLifetime : class, ICacheLifetime
    {
        _configureCacheLifetime = s => s.TryAddSingleton<ICacheLifetime, TLifetime>();
        return this;
    }

    public ICacheConfiguration SecondLevel<TSecondLevelLayer>() where TSecondLevelLayer : class, ICacheSecondLevel
    {
        _configureCacheSecondLevel = s => s.TryAddSingleton<ICacheSecondLevel, TSecondLevelLayer>();
        return this;
    }

    public ICacheConfiguration Metrics<TMetrics>() where TMetrics : class, ICacheMetrics
    {
        _configureCacheMetrics = s => s.TryAddSingleton<ICacheMetrics, TMetrics>();
        return this;
    }

    internal void Complete()
    {
        var cacheOptions = new CacheOptions {DefaultRelativeExpiration = _defaultAbsoluteExpiration};
        _services.AddSingleton(cacheOptions);

        _configureCacheLifetime(_services);
        _configureCacheSecondLevel(_services);
    }
}