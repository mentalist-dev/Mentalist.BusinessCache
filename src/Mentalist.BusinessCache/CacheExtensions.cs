using System.Collections.Concurrent;

namespace Mentalist.BusinessCache;

internal static class CacheExtensions
{
    private static readonly ConcurrentDictionary<Type, string> TypeNames = new();

    public static string GetTypeName(this Type type)
    {
        return TypeNames.GetOrAdd(type, t =>
        {
            var typeName = t.Name;

            if (t.IsGenericType)
            {
                var index = typeName.IndexOf('`');
                if (index > 0)
                {
                    typeName = typeName.Substring(0, index);

                    var arguments = t.GenericTypeArguments.Select(GetTypeName);
                    var joined = string.Join(',', arguments);

                    if (!string.IsNullOrWhiteSpace(joined))
                    {
                        typeName += $"<{joined}>";
                    }
                }
            }

            return typeName;
        });
    }
}