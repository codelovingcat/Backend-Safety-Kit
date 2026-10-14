using System.Text.Json;
using BackendSafetyKit;
using Xunit;

namespace BackendSafetyKit.Tests;

public sealed class SensitiveDataMaskerRegressionTests
{
    [Fact]
    public void SelfReferencingDictionaryIsReplacedWithCycleMarkerWithoutMutation()
    {
        var masker = new SensitiveDataMasker(new SensitiveDataMaskingOptions());
        var source = new Dictionary<string, object?>();

        source["self"] = source;

        var masked = Assert.IsType<Dictionary<string, object?>>(masker.Mask(source));
        var cycle = Assert.IsType<Dictionary<string, object?>>(masked["self"]);

        Assert.Equal("[CYCLE]", cycle["$masked"]);
        Assert.Same(source, source["self"]);
    }

    [Fact]
    public void ValuesBeyondConfiguredDepthAreTruncatedBeforeTheirContentsAreCopied()
    {
        var options = new SensitiveDataMaskingOptions
        {
            MaxDepth = 2
        };
        var masker = new SensitiveDataMasker(options);
        var source = new Dictionary<string, object?>
        {
            ["level1"] = new Dictionary<string, object?>
            {
                ["level2"] = new Dictionary<string, object?>
                {
                    ["Password"] = "deep-secret-must-not-appear"
                }
            }
        };

        var masked = Assert.IsType<Dictionary<string, object?>>(masker.Mask(source));
        var level1 = Assert.IsType<Dictionary<string, object?>>(masked["level1"]);

        Assert.Equal("[TRUNCATED]", level1["level2"]);
        Assert.DoesNotContain("deep-secret-must-not-appear", JsonSerializer.Serialize(masked));
        Assert.Contains("deep-secret-must-not-appear",
            JsonSerializer.Serialize(source));
    }

    [Fact]
    public void DictionaryCopyStopsAtConfiguredItemLimitAndDoesNotLeakMaskedData()
    {
        var options = new SensitiveDataMaskingOptions
        {
            MaxCollectionItems = 1
        };
        var masker = new SensitiveDataMasker(options);
        var source = new Dictionary<string, object?>
        {
            ["Password"] = "dictionary-secret",
            ["Public"] = "visible",
            ["Extra"] = "not-copied"
        };

        var masked = Assert.IsType<Dictionary<string, object?>>(masker.Mask(source));

        Assert.True((bool)masked["$truncated"]!);
        Assert.True(masked.Count <= 2);
        Assert.DoesNotContain("dictionary-secret", JsonSerializer.Serialize(masked));
        Assert.Contains("dictionary-secret", JsonSerializer.Serialize(source));
    }

    [Fact]
    public void EnumerableCopyUsesTruncationMarkerInsteadOfEnumeratingPastLimit()
    {
        var options = new SensitiveDataMaskingOptions
        {
            MaxCollectionItems = 2
        };
        var masker = new SensitiveDataMasker(options);
        var source = new object?[]
        {
            "visible-one",
            "visible-two",
            new Dictionary<string, object?>
            {
                ["Password"] = "enumerable-secret"
            }
        };

        var masked = Assert.IsType<List<object?>>(masker.Mask(source));

        Assert.Equal(3, masked.Count);
        Assert.Equal("[TRUNCATED]", masked[^1]);
        Assert.DoesNotContain("enumerable-secret", JsonSerializer.Serialize(masked));
        Assert.Contains("enumerable-secret", JsonSerializer.Serialize(source));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(33)]
    public void MaskingRejectsDepthOutsideTheSupportedBounds(int maxDepth)
    {
        var options = new SensitiveDataMaskingOptions
        {
            MaxDepth = maxDepth
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SensitiveDataMasker(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4097)]
    public void MaskingRejectsCollectionLimitOutsideTheSupportedBounds(int maxCollectionItems)
    {
        var options = new SensitiveDataMaskingOptions
        {
            MaxCollectionItems = maxCollectionItems
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SensitiveDataMasker(options));
    }
}
