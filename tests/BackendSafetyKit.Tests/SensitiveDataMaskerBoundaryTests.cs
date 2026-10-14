using System.Collections.Specialized;
using System.Text.Json;
using BackendSafetyKit;
using Xunit;

namespace BackendSafetyKit.Tests;

public sealed class SensitiveDataMaskerBoundaryTests
{
    [Fact]
    public void MaskerStopsEnumeratingWhenCollectionLimitIsReached()
    {
        const int maxItems = 3;
        var options = new SensitiveDataMaskingOptions
        {
            MaxCollectionItems = maxItems
        };
        var masker = new SensitiveDataMasker(options);
        var enumeratedCount = 0;
        var source = CountedValues(() => enumeratedCount++);

        var masked = Assert.IsType<List<object?>>(masker.Mask(source));

        Assert.Equal(maxItems + 1, enumeratedCount);
        Assert.Equal(maxItems + 1, masked.Count);
        Assert.Equal(0, masked[0]);
        Assert.Equal(1, masked[1]);
        Assert.Equal(2, masked[2]);
        Assert.Equal("[TRUNCATED]", masked[^1]);
    }

    [Fact]
    public void MaskerDoesNotCopyDictionaryItemsBeyondCollectionLimit()
    {
        const int maxItems = 16;
        const string secret = "must-not-be-copied-after-limit";
        var options = new SensitiveDataMaskingOptions
        {
            MaxCollectionItems = maxItems
        };
        var masker = new SensitiveDataMasker(options);
        var source = new OrderedDictionary(StringComparer.Ordinal);

        for (var index = 0; index < maxItems; index++)
        {
            source[$"safe-{index:D2}"] = index;
        }

        source["99-Password"] = secret;

        var masked = Assert.IsType<Dictionary<string, object?>>(masker.Mask(source));

        Assert.Equal(maxItems + 1, masked.Count);
        Assert.True(Assert.IsType<bool>(masked["$truncated"]));
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(masked));
        Assert.Equal(secret, source["99-Password"]);
    }

    [Fact]
    public void MaskerTruncatesDeepObjectsBeforeCopyingDeepSecrets()
    {
        const string secret = "deep-boundary-secret";
        var options = new SensitiveDataMaskingOptions
        {
            MaxDepth = 8
        };
        var masker = new SensitiveDataMasker(options);
        var source = new Dictionary<string, object?>();
        var current = source;

        for (var depth = 0; depth < 100; depth++)
        {
            var child = new Dictionary<string, object?>();
            current[$"level-{depth}"] = child;
            current = child;
        }

        current["Password"] = secret;

        var masked = masker.Mask(source);
        var serialized = JsonSerializer.Serialize(masked);

        Assert.Contains("[TRUNCATED]", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, serialized, StringComparison.Ordinal);
        Assert.Equal(secret, current["Password"]);
    }

    [Fact]
    public void MaskerPreservesLargeUnmatchedStringWithoutTruncatingItsContents()
    {
        var masker = new SensitiveDataMasker(new SensitiveDataMaskingOptions());
        var source = new string('x', 1024 * 1024);

        var result = masker.Mask(source);

        Assert.Same(source, result);
        Assert.Equal(1024 * 1024, Assert.IsType<string>(result).Length);
    }

    private static IEnumerable<int> CountedValues(Action onMoveNext)
    {
        var value = 0;

        while (true)
        {
            onMoveNext();
            yield return value++;
        }
    }
}
