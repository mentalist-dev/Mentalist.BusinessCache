using Microsoft.Extensions.Hosting;

namespace Mentalist.BusinessCache.AspNetCore;

public class AspNetCoreCacheLifetime : ICacheLifetime
{
    public AspNetCoreCacheLifetime(IHostApplicationLifetime lifetime)
    {
        ApplicationStopping = lifetime.ApplicationStopping;
    }

    public CancellationToken ApplicationStopping { get; }
}