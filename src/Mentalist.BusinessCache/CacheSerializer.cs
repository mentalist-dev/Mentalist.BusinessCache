﻿using System.Diagnostics;
using System.Text.Json;

namespace Mentalist.BusinessCache;

public interface ICacheSerializer
{
    byte[] Serialize<T>(T value) where T: CacheItem;
    T? Deserialize<T>(byte[] buffer) where T : CacheItem;
}

public class CacheSerializer : ICacheSerializer
{
    private readonly ICacheMetrics _metrics;

    public CacheSerializer(ICacheMetrics metrics)
    {
        _metrics = metrics;
    }

    public byte[] Serialize<T>(T value) where T : CacheItem
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        using var timer = _metrics.Serialize(value.Type);

        using var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, value, value.GetType());

        return stream.ToArray();
    }

    public T? Deserialize<T>(byte[] buffer) where T : CacheItem
    {
        var timer = Stopwatch.StartNew();
        using var stream = new MemoryStream(buffer);
        var result = JsonSerializer.Deserialize<T>(stream);
        if (result != null)
        {
            _metrics.Deserialized(result.Type, timer.Elapsed);
        }
        return result;
    }
}