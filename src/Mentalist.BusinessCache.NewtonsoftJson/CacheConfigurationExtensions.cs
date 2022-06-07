namespace Mentalist.BusinessCache.NewtonsoftJson;

public static class CacheConfigurationExtensions
{
    public static ICacheConfiguration UseNewtonsoftJson(this ICacheConfiguration configuration) =>
        configuration.Serializer<NewtonsoftJsonCacheSerializer>();
}