using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;

namespace Mentalist.BusinessCache.NewtonsoftJson;

public class NewtonsoftJsonCacheSerializer: ICacheSerializer
{
    private readonly ICacheMetrics _metrics;

    public NewtonsoftJsonCacheSerializer(ICacheMetrics metrics)
    {
        _metrics = metrics;
    }

    public byte[] Serialize<T>(T value) where T : CacheItem
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        using var timer = _metrics.Serialize(value.Type);

        var content = JsonConvert.SerializeObject(value);
        return Encoding.UTF8.GetBytes(content);
    }

    public T? Deserialize<T>(byte[] buffer) where T : CacheItem
    {
        var timer = Stopwatch.StartNew();

        var result = JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(buffer));
        if (result != null)
        {
            _metrics.Deserialized(result.Type, timer.Elapsed);
        }
        
        return result;
    }
}