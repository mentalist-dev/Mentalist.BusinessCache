namespace Mentalist.BusinessCache;

public interface ITimer : IDisposable
{
}

public interface ICacheMetrics
{
    ITimer Serialize<T>();
    ITimer Deserialize<T>();

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

    void SecondLevelEvictedByNotification<T>();

    void ReportSecondLevelSetQueueSize(int size);
    void ReportSecondLevelRefreshQueueSize(int size);
    void ReportSecondLevelRemoveQueueSize(int size);
}

public class CacheMetrics: ICacheMetrics
{
    public ITimer Serialize<T>()
    {
        return new Timer();
    }

    public ITimer Deserialize<T>()
    {
        return new Timer();
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

    public void SecondLevelEvictedByNotification<T>()
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

    private sealed class Timer: ITimer
    {
        public void Dispose() { /**/ }
    }
}