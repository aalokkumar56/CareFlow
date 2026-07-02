using System.Collections;
using System.Reflection;

namespace CureFlow.Infrastructure.Persistence.Dapper;

/// <summary>
/// Merges Dapper SQL parameters from anonymous objects, dictionaries, or other POCOs.
/// Skips indexer properties (e.g. Dictionary.Item) to avoid TargetParameterCountException.
/// </summary>
public static class SqlParam
{
    public static Dictionary<string, object?> Merge(params object?[] sources)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
            CopyInto(dict, source);
        return dict;
    }

    public static void CopyInto(Dictionary<string, object?> target, object? source)
    {
        if (source is null)
            return;

        if (source is IDictionary<string, object?> genericDict)
        {
            foreach (var kv in genericDict)
                target[kv.Key] = kv.Value;
            return;
        }

        if (source is IDictionary nonGeneric)
        {
            foreach (DictionaryEntry entry in nonGeneric)
            {
                if (entry.Key is string key)
                    target[key] = entry.Value;
            }
            return;
        }

        foreach (var prop in source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                continue;

            target[prop.Name] = prop.GetValue(source);
        }
    }
}
