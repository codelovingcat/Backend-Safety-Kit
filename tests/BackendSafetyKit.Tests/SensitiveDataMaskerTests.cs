using BackendSafetyKit;
using System.Reflection;
using Xunit;

namespace BackendSafetyKit.Tests;

public sealed class SensitiveDataMaskerTests
{
    [Fact]
    public void DefaultSensitiveFieldNamesAreMatchedCaseInsensitively()
    {
        var masker = new SensitiveDataMasker(new SensitiveDataMaskingOptions());

        var masked = masker.MaskString("super-secret", "PaSsWoRd");

        Assert.Equal("[REDACTED]", masked);
        Assert.DoesNotContain("super-secret", masked, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultSecretHeadersAreMasked()
    {
        var masker = new SensitiveDataMasker(new SensitiveDataMaskingOptions());
        var source = new Dictionary<string, object?>
        {
            ["Authorization"] = "Bearer very-secret-token",
            ["X-Api-Key"] = "api-key-secret",
            ["Content-Type"] = "application/json"
        };

        var masked = Assert.IsType<Dictionary<string, object?>>(masker.Mask(source));

        Assert.Equal("[REDACTED]", masked["Authorization"]);
        Assert.Equal("[REDACTED]", masked["X-Api-Key"]);
        Assert.Equal("application/json", masked["Content-Type"]);
    }

    [Fact]
    public void CustomExactRuleMasksDomainSpecificField()
    {
        var options = new SensitiveDataMaskingOptions();
        options.AddRule("CustomerSecret");

        var masker = new SensitiveDataMasker(options);

        var masked = masker.MaskString("customer-secret-42", "CUSTOMERSECRET");

        Assert.Equal("[REDACTED]", masked);
    }

    [Fact]
    public void CustomContainsRuleMasksMatchingFieldNames()
    {
        var options = new SensitiveDataMaskingOptions();
        options.AddRule(
            "credential",
            SensitiveDataMatchMode.Contains);

        var masker = new SensitiveDataMasker(options);

        var masked = masker.MaskString("credential-secret", "DatabaseCredential");

        Assert.Equal("[REDACTED]", masked);
    }

    [Fact]
    public void PartialMaskingRequiresExplicitOptIn()
    {
        var options = new SensitiveDataMaskingOptions();
        options.AddRule(
            "sessionToken",
            SensitiveDataMatchMode.Exact,
            SensitiveDataMaskMode.Partial,
            visiblePrefixLength: 2,
            visibleSuffixLength: 2);

        var masker = new SensitiveDataMasker(options);

        Assert.Equal("[REDACTED]", masker.MaskString("secret-token", "sessionToken"));

        options.AllowPartialMasking = true;
        var partiallyConfiguredMasker = new SensitiveDataMasker(options);
        var partiallyMasked = partiallyConfiguredMasker.MaskString(
            "secret-token",
            "sessionToken");

        Assert.Equal("se********en", partiallyMasked);
        Assert.DoesNotContain("secret-token", partiallyMasked, StringComparison.Ordinal);
    }

    [Fact]
    public void SingletonMaskerUsesStartupSnapshotAfterOptionsAreMutated()
    {
        var options = new SensitiveDataMaskingOptions();
        options.AddRule(
            "sessionToken",
            SensitiveDataMatchMode.Exact,
            SensitiveDataMaskMode.Partial,
            visiblePrefixLength: 2,
            visibleSuffixLength: 2);

        var masker = new SensitiveDataMasker(options);

        options.AllowPartialMasking = true;
        options.MaskValue = "[CHANGED]";
        options.Rules[^1].Name = "different";

        Assert.Equal(
            "[REDACTED]",
            masker.MaskString("secret-token", "sessionToken"));
    }

    [Fact]
    public void SingletonMaskerCanBeUsedConcurrently()
    {
        var masker = new SensitiveDataMasker(
            new SensitiveDataMaskingOptions());

        var results = new string[128];

        Parallel.For(
            0,
            results.Length,
            index =>
            {
                results[index] =
                    masker.MaskString(
                        $"secret-{index}",
                        "password")
                    ?? string.Empty;
            });

        Assert.All(results, result => Assert.Equal("[REDACTED]", result));
    }

    [Fact]
    public void ReflectionMetadataIsCachedPerType()
    {
        var masker = new SensitiveDataMasker(new SensitiveDataMaskingOptions());
        var source = new CacheProbe("secret");

        var cacheField = typeof(SensitiveDataMasker).GetField(
            "PropertyMetadataCache",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(cacheField);

        var cache = cacheField.GetValue(null);

        Assert.NotNull(cache);

        var cacheType = cache.GetType();
        var countProperty = cacheType.GetProperty("Count");
        var containsKeyMethod = cacheType.GetMethod("ContainsKey");

        Assert.NotNull(countProperty);
        Assert.NotNull(containsKeyMethod);

        var initialCount = Assert.IsType<int>(countProperty!.GetValue(cache));

        masker.Mask(source);
        masker.Mask(source);

        var finalCount = Assert.IsType<int>(countProperty.GetValue(cache));
        var containsProbe = Assert.IsType<bool>(
            containsKeyMethod!.Invoke(cache, [typeof(CacheProbe)]));

        Assert.Equal(initialCount + 1, finalCount);
        Assert.True(containsProbe);
    }

    [Fact]
    public void NestedDictionariesAndArraysAreMaskedWithoutMutation()
    {
        var options = new SensitiveDataMaskingOptions();
        var masker = new SensitiveDataMasker(options);
        var source = new Dictionary<string, object?>
        {
            ["Profile"] = new Dictionary<string, object?>
            {
                ["Name"] = "Ada",
                ["Password"] = "nested-secret"
            },
            ["Tokens"] = new object?[]
            {
                new Dictionary<string, object?>
                {
                    ["AccessToken"] = "array-secret"
                },
                "public-value"
            }
        };

        var masked = Assert.IsType<Dictionary<string, object?>>(masker.Mask(source));
        var profile = Assert.IsType<Dictionary<string, object?>>(masked["Profile"]);
        var tokens = Assert.IsType<List<object?>>(masked["Tokens"]);
        var tokenObject = Assert.IsType<Dictionary<string, object?>>(tokens[0]);

        Assert.Equal("Ada", profile["Name"]);
        Assert.Equal("[REDACTED]", profile["Password"]);
        Assert.Equal("[REDACTED]", tokenObject["AccessToken"]);
        Assert.Equal("public-value", tokens[1]);

        var originalProfile = Assert.IsType<Dictionary<string, object?>>(source["Profile"]);
        Assert.Equal("nested-secret", originalProfile["Password"]);
        var originalTokens = Assert.IsType<object?[]>(source["Tokens"]);
        var originalTokenObject =
            Assert.IsType<Dictionary<string, object?>>(originalTokens[0]);
        Assert.Equal("array-secret", originalTokenObject["AccessToken"]);
    }

    [Fact]
    public void NestedPocoPropertiesAreCopiedAndMasked()
    {
        var masker = new SensitiveDataMasker(new SensitiveDataMaskingOptions());
        var source = new LoginRequest(
            new Credentials(
                "ada",
                "poco-secret"));

        var masked = Assert.IsType<Dictionary<string, object?>>(masker.Mask(source));
        var credentials = Assert.IsType<Dictionary<string, object?>>(masked["Credentials"]);

        Assert.Equal("ada", credentials["Username"]);
        Assert.Equal("[REDACTED]", credentials["Password"]);
        Assert.Equal("poco-secret", source.Credentials.Password);
    }

    private sealed record LoginRequest(Credentials Credentials);

    private sealed record Credentials(string Username, string Password);

    private sealed record CacheProbe(string Password);
}
