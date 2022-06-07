namespace Mentalist.BusinessCache;

public interface ITimer : IDisposable
{
}

public interface ICacheMetrics
{
    ITimer Serialize(string valueType);
    void Deserialized(string valueType, TimeSpan duration);

    ITimer Set<T>();
    void SetFailed<T>();

    ITimer Get<T>();
    void GetFailed<T>();

    ITimer Remove<T>();

    void Hit<T>();
    void Mis<T>();

    ITimer SecondLevelSet<T>();
    void SecondLevelSetFailed<T>();

    ITimer SecondLevelGet<T>();
    void SecondLevelGetFailed<T>();
    ITimer SecondLevelRemove<T>();

    void SecondLevelHit<T>();
    void SecondLevelMis<T>();
    void SecondLevelRetry<T>();

    void EvictedByNotification(string type);

    void ReportSecondLevelSetQueueSize(int size);
    void ReportSecondLevelRefreshQueueSize(int size);
    void ReportSecondLevelRemoveQueueSize(int size);
    void ReportSecondLevelCircuitBreaker(bool isOpen);
}

public class CacheMetrics: ICacheMetrics
{
    public ITimer Serialize(string valueType)
    {
        return new Timer();
    }

    public void Deserialized(string valueType, TimeSpan duration)
    {
        //
    }

    public ITimer Set<T>()
    {
        return new Timer();
    }

    public void SetFailed<T>()
    {
        //
    }

    public ITimer Get<T>()
    {
        return new Timer();
    }

    public void GetFailed<T>()
    {
        //
    }

    public ITimer Remove<T>()
    {
        return new Timer();
    }

    public void Hit<T>()
    {
        //
    }

    public void Mis<T>()
    {
        //
    }

    public ITimer SecondLevelSet<T>()
    {
        return new Timer();
    }

    public void SecondLevelSetFailed<T>()
    {
        //
    }

    public ITimer SecondLevelGet<T>()
    {
        return new Timer();
    }

    public void SecondLevelGetFailed<T>()
    {
        //
    }

    public ITimer SecondLevelRemove<T>()
    {
        return new Timer();
    }

    public void SecondLevelHit<T>()
    {
        //
    }

    public void SecondLevelMis<T>()
    {
        //
    }

    public void SecondLevelRetry<T>()
    {
        //
    }

    public void EvictedByNotification(string type)
    {
        //
    }

    public void ReportSecondLevelSetQueueSize(int size)
    {
        //
    }

    public void ReportSecondLevelRefreshQueueSize(int size)
    {
        //
    }

    public void ReportSecondLevelRemoveQueueSize(int size)
    {
        //
    }

    public void ReportSecondLevelCircuitBreaker(bool isOpen)
    {
        //
    }

    private sealed class Timer: ITimer
    {
        public void Dispose() { /**/ }
    }
}