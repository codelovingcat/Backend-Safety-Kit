using System.Collections;
using System.Reflection;

namespace BackendSafetyKit;

/// <summary>
/// Provides non-mutating sensitive data masking for scalar and structured values.
/// </summary>
public sealed class SensitiveDataMasker : ISensitiveDataMasker
{
    private readonly SensitiveDataMaskingOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SensitiveDataMasker"/> class.
    /// </summary>
    /// <param name="options">The masking configuration.</param>
    public SensitiveDataMasker(SensitiveDataMaskingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        this.options = options;
    }

    /// <inheritdoc />
    public object? Mask(object? value, string? fieldName = null)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        return MaskCore(value, fieldName, depth: 0, visited);
    }

    /// <inheritdoc />
    public string? MaskString(string? value, string? fieldName = null)
    {
        if (value is null)
        {
            return null;
        }

        var rule = FindRule(fieldName);
        return rule is null
            ? value
            : ApplyRule(value, rule);
    }

    private object? MaskCore(
        object? value,
        string? fieldName,
        int depth,
        HashSet<object> visited)
    {
        if (value is null)
        {
            return null;
        }

        var rule = FindRule(fieldName);

        if (rule is not null)
        {
            return ApplyRule(value, rule);
        }

        if (value is string)
        {
            return value;
        }

        if (IsSimpleValue(value.GetType()))
        {
            return value;
        }

        if (depth >= options.MaxDepth)
        {
            return "[TRUNCATED]";
        }

        if (value is IDictionary dictionary)
        {
            return MaskDictionary(dictionary, depth, visited);
        }

        if (value is IEnumerable enumerable)
        {
            return MaskEnumerable(enumerable, depth, visited);
        }

        return MaskObjectProperties(value, depth, visited);
    }

    private Dictionary<string, object?> MaskDictionary(
        IDictionary dictionary,
        int depth,
        HashSet<object> visited)
    {
        if (!visited.Add(dictionary))
        {
            return new Dictionary<string, object?>
            {
                ["$masked"] = "[CYCLE]"
            };
        }

        try
        {
            var result = new Dictionary<string, object?>(
                StringComparer.OrdinalIgnoreCase);
            var count = 0;

            foreach (DictionaryEntry entry in dictionary)
            {
                if (count++ >= options.MaxCollectionItems)
                {
                    result["$truncated"] = true;
                    break;
                }

                var key = entry.Key as string ?? string.Empty;
                result[key] = MaskCore(
                    entry.Value,
                    key,
                    depth + 1,
                    visited);
            }

            return result;
        }
        finally
        {
            visited.Remove(dictionary);
        }
    }

    private List<object?> MaskEnumerable(
        IEnumerable enumerable,
        int depth,
        HashSet<object> visited)
    {
        if (!visited.Add(enumerable))
        {
            return new List<object?> { "[CYCLE]" };
        }

        try
        {
            var result = new List<object?>();
            var count = 0;

            foreach (var item in enumerable)
            {
                if (count++ >= options.MaxCollectionItems)
                {
                    result.Add("[TRUNCATED]");
                    break;
                }

                result.Add(MaskCore(item, null, depth + 1, visited));
            }

            return result;
        }
        finally
        {
            visited.Remove(enumerable);
        }
    }

    private Dictionary<string, object?> MaskObjectProperties(
        object value,
        int depth,
        HashSet<object> visited)
    {
        if (!visited.Add(value))
        {
            return new Dictionary<string, object?>
            {
                ["$masked"] = "[CYCLE]"
            };
        }

        try
        {
            var result = new Dictionary<string, object?>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var property in value.GetType().GetProperties(
                         BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead ||
                    property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                var propertyValue = property.GetValue(value);

                result[property.Name] = MaskCore(
                    propertyValue,
                    property.Name,
                    depth + 1,
                    visited);
            }

            return result;
        }
        finally
        {
            visited.Remove(value);
        }
    }

    private SensitiveDataMaskingRule? FindRule(string? fieldName)
    {
        if (string.IsNullOrEmpty(fieldName))
        {
            return null;
        }

        foreach (var rule in options.Rules)
        {
            if (rule.Matches(fieldName))
            {
                return rule;
            }
        }

        return null;
    }

    private string ApplyRule(object value, SensitiveDataMaskingRule rule)
    {
        if (rule.MaskMode == SensitiveDataMaskMode.Full ||
            !options.AllowPartialMasking)
        {
            return options.MaskValue;
        }

        if (value is not string stringValue || stringValue.Length == 0)
        {
            return options.MaskValue;
        }

        return ApplyPartialMask(stringValue, rule);
    }

    private static string ApplyPartialMask(
        string value,
        SensitiveDataMaskingRule rule)
    {
        if (value.Length == 0)
        {
            return new string(rule.MaskCharacter, 1);
        }

        var prefixLength = Math.Min(rule.VisiblePrefixLength, value.Length);
        var suffixLength = Math.Min(
            rule.VisibleSuffixLength,
            value.Length - prefixLength);

        if (prefixLength + suffixLength >= value.Length)
        {
            suffixLength = Math.Max(0, value.Length - prefixLength - 1);
        }

        var maskLength = value.Length - prefixLength - suffixLength;

        return string.Concat(
            value.AsSpan(0, prefixLength),
            new string(rule.MaskCharacter, Math.Max(1, maskLength)),
            suffixLength == 0
                ? string.Empty
                : value.AsSpan(value.Length - suffixLength, suffixLength).ToString());
    }

    private static bool IsSimpleValue(Type type) =>
        type.IsPrimitive ||
        type.IsEnum ||
        type == typeof(decimal) ||
        type == typeof(Guid) ||
        type == typeof(DateTime) ||
        type == typeof(DateTimeOffset) ||
        type == typeof(TimeSpan) ||
        type == typeof(Uri);
}
