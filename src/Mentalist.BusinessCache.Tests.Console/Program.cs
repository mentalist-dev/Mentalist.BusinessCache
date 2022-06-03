using Mentalist.BusinessCache;
using Mentalist.BusinessCache.Prometheus;
using Mentalist.BusinessCache.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var redisConnectionString = "redis:6389,connectTimeout=10000,connectRetry=5,keepAlive=60,syncTimeout=200,abortConnect=false";

var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
});

services.AddCache(config => config
    .UseRedis(new RedisCacheOptions {RedisConnectionString = redisConnectionString})
    .UsePrometheusMetrics()
);

var provider = services.BuildServiceProvider();
var cache = provider.GetRequiredService<ICache>();

var finished = false;
while (!finished)
{
    var key = Console.ReadKey(true);
    switch (key.Key)
    {
        case ConsoleKey.S:
        {
            var dateTime = DateTime.UtcNow;
            Console.WriteLine($"[{DateTime.UtcNow:O}] set: {dateTime:O}");
            cache.Set("s3", dateTime, TimeSpan.FromMinutes(60));
            break;
        }
        case ConsoleKey.G:
        {
            var dateTime = await cache.GetAsync("s3", DateTime.MinValue);
            Console.WriteLine($"[{DateTime.UtcNow:O}] get: {dateTime:O}");
            break;
        }
        case ConsoleKey.R:
        {
            cache.Remove<DateTime>("s3");
            Console.WriteLine($"[{DateTime.UtcNow:O}] rem");
            break;
        }
        case ConsoleKey.Escape:
            finished = true;
            break;
    }
}