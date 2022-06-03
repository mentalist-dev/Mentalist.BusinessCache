using System.Text.Json;

namespace Mentalist.BusinessCache;

public interface ICacheSerializer
{
    byte[] Serialize<T>(T value);
    T? Deserialize<T>(byte[] buffer);
}

public class CacheSerializer : ICacheSerializer
{
    private readonly ICacheMetrics _metrics;

    public CacheSerializer(ICacheMetrics metrics)
    {
        _metrics = metrics;
    }

    public byte[] Serialize<T>(T value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        using var timer = _metrics.Serialize<T>();

        using var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, value, value.GetType());

        return stream.ToArray();
    }

    public T? Deserialize<T>(byte[] buffer)
    {
        using var timer = _metrics.Serialize<T>();
        using var stream = new MemoryStream(buffer);
        return JsonSerializer.Deserialize<T>(stream);
    }
}