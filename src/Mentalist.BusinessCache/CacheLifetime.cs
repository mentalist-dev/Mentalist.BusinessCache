namespace Mentalist.BusinessCache;

public interface ICacheLifetime
{
    CancellationToken ApplicationStopping { get; }
}

public class CacheLifetime : ICacheLifetime, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public CacheLifetime()
    {
        ApplicationStopping = _cancellationTokenSource.Token;
    }

    public CancellationToken ApplicationStopping { get; }

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
    }
}