using Prometheus;

namespace Mentalist.BusinessCache.Prometheus;

public class PrometheusCacheMetrics: ICacheMetrics
{
    private readonly Histogram _serializeHistogram = Metrics.CreateHistogram(
        "mentalist_cache_serialize",
        "Serialization duration",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 10),
            LabelNames = new[] { "type" }
        });

    private readonly Histogram _deserializeHistogram = Metrics.CreateHistogram(
        "mentalist_cache_deserialize",
        "Deserialization duration",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 10),
            LabelNames = new[] { "type" }
        });

    private readonly Histogram _setHistogram = Metrics.CreateHistogram(
        "mentalist_cache_set",
        "Cache set duration",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 10),
            LabelNames = new[] { "type" }
        });

    private readonly Counter _setFailedCounter = Metrics.CreateCounter(
        "mentalist_cache_set_failed",
        "Cache set failures",
        new CounterConfiguration { LabelNames = new[] { "type" } });

    private readonly Histogram _getHistogram = Metrics.CreateHistogram(
        "mentalist_cache_get",
        "Cache get duration",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 10),
            LabelNames = new[] { "type" }
        });

    private readonly Counter _getFailedCounter = Metrics.CreateCounter(
        "mentalist_cache_get_failed",
        "Cache get failures",
        new CounterConfiguration { LabelNames = new[] { "type" } });

    private readonly Histogram _removeHistogram = Metrics.CreateHistogram(
        "mentalist_cache_remove",
        "Cache remove duration",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 10),
            LabelNames = new[] { "type" }
        });

    private readonly Counter _hitCounter = Metrics.CreateCounter(
        "mentalist_cache_hit",
        "Cache hits",
        new CounterConfiguration { LabelNames = new[] { "type" } });

    private readonly Counter _misCounter = Metrics.CreateCounter(
        "mentalist_cache_mis",
        "Cache misses",
        new CounterConfiguration { LabelNames = new[] { "type" } });

    private readonly Histogram _secondLevelSetHistogram = Metrics.CreateHistogram(
        "mentalist_second_level_cache_set",
        "Second level cache set duration",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 10),
            LabelNames = new[] { "type" }
        });

    private readonly Counter _secondLevelSetFailedCounter = Metrics.CreateCounter(
        "mentalist_second_level_cache_set_failed",
        "Second level cache set failures",
        new CounterConfiguration { LabelNames = new[] { "type" } });

    private readonly Histogram _secondLevelGetHistogram = Metrics.CreateHistogram(
        "mentalist_second_level_cache_get",
        "Second level cache get duration",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 10),
            LabelNames = new[] { "type" }
        });

    private readonly Counter _secondLevelGetFailedCounter = Metrics.CreateCounter(
        "mentalist_second_level_cache_get_failed",
        "Second level cache get failures",
        new CounterConfiguration { LabelNames = new[] { "type" } });

    private readonly Histogram _secondLevelRemoveHistogram = Metrics.CreateHistogram(
        "mentalist_second_level_cache_remove",
        "Second level cache remove duration",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 10),
            LabelNames = new[] { "type" }
        });

    private readonly Counter _secondLevelHitCounter = Metrics.CreateCounter(
        "mentalist_second_level_cache_hit",
        "Second level cache hits",
        new CounterConfiguration { LabelNames = new[] { "type" } });

    private readonly Counter _secondLevelMisCounter = Metrics.CreateCounter(
        "mentalist_second_level_cache_mis",
        "Second level cache misses",
        new CounterConfiguration { LabelNames = new[] { "type" } });

    private readonly Counter _secondLevelEvictedByNotificationCounter = Metrics.CreateCounter(
        "mentalist_second_level_cache_evicted_by_notification",
        "Second level cache evicted by notification",
        new CounterConfiguration { LabelNames = new[] { "type" } });

    private readonly Gauge _secondLevelSetQueueSizeGauge = Metrics.CreateGauge(
        "mentalist_second_level_cache_set_queue_size",
        "Second level cache set queue size");

    private readonly Gauge _secondLevelRefreshQueueSizeGauge = Metrics.CreateGauge(
        "mentalist_second_level_cache_refresh_queue_size",
        "Second level cache set queue size");

    private readonly Gauge _secondLevelRemoveQueueSizeGauge = Metrics.CreateGauge(
        "mentalist_second_level_cache_remove_queue_size",
        "Second level cache remove queue size");

    public ITimer Serialize<T>()
    {
        return new PrometheusTimer(_serializeHistogram, typeof(T).Name);
    }

    public ITimer Deserialize<T>()
    {
        return new PrometheusTimer(_deserializeHistogram, typeof(T).Name);
    }

    public ITimer Set<T>()
    {
        return new PrometheusTimer(_setHistogram, typeof(T).Name);
    }

    public void SetFailed<T>()
    {
        _setFailedCounter.Labels(typeof(T).Name).Inc();
    }

    public ITimer Get<T>()
    {
        return new PrometheusTimer(_getHistogram, typeof(T).Name);
    }

    public void GetFailed<T>()
    {
        _getFailedCounter.Labels(typeof(T).Name).Inc();
    }

    public ITimer Remove<T>()
    {
        return new PrometheusTimer(_removeHistogram, typeof(T).Name);
    }

    public void Hit<T>()
    {
        _hitCounter.Labels(typeof(T).Name).Inc();
    }

    public void Mis<T>()
    {
        _misCounter.Labels(typeof(T).Name).Inc();
    }

    public ITimer SecondLevelSet<T>()
    {
        return new PrometheusTimer(_secondLevelSetHistogram, typeof(T).Name);
    }

    public void SecondLevelSetFailed<T>()
    {
        _secondLevelSetFailedCounter.Labels(typeof(T).Name).Inc();
    }

    public ITimer SecondLevelGet<T>()
    {
        return new PrometheusTimer(_secondLevelGetHistogram, typeof(T).Name);
    }

    public void SecondLevelGetFailed<T>()
    {
        _secondLevelGetFailedCounter.Labels(typeof(T).Name).Inc();
    }

    public ITimer SecondLevelRemove<T>()
    {
        return new PrometheusTimer(_secondLevelRemoveHistogram, typeof(T).Name);
    }

    public void SecondLevelHit<T>()
    {
        _secondLevelHitCounter.Labels(typeof(T).Name).Inc();
    }

    public void SecondLevelMis<T>()
    {
        _secondLevelMisCounter.Labels(typeof(T).Name).Inc();
    }

    public void SecondLevelEvictedByNotification<T>()
    {
        _secondLevelEvictedByNotificationCounter.Labels(typeof(T).Name).Inc();
    }

    public void ReportSecondLevelSetQueueSize(int size)
    {
        _secondLevelSetQueueSizeGauge.Set(size);
    }

    public void ReportSecondLevelRefreshQueueSize(int size)
    {
        _secondLevelRefreshQueueSizeGauge.Set(size);
    }

    public void ReportSecondLevelRemoveQueueSize(int size)
    {
        _secondLevelRemoveQueueSizeGauge.Set(size);
    }

    private sealed class PrometheusTimer : ITimer
    {
        private readonly global::Prometheus.ITimer _timer;

        public PrometheusTimer(Histogram histogram, params string[] labelValues)
        {
            _timer = histogram.Labels(labelValues).NewTimer();
        }

        ~PrometheusTimer()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
            }
        }
    }
}