namespace Mentalist.BusinessCache.Prometheus;

public static class CacheConfigurationExtensions
{
    public static ICacheConfiguration UsePrometheusMetrics(this ICacheConfiguration configuration) =>
        configuration.Metrics<PrometheusCacheMetrics>();
}