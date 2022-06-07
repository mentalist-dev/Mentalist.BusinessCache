namespace Mentalist.BusinessCache.AspNetCore;

public static class CacheConfigurationExtensions
{
    public static ICacheConfiguration UseAspNetCoreCacheLifetime(this ICacheConfiguration configuration) =>
        configuration.Lifetime<AspNetCoreCacheLifetime>();
}