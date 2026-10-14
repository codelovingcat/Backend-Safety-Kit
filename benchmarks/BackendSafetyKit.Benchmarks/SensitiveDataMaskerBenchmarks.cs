using BackendSafetyKit;
using BenchmarkDotNet.Attributes;

namespace BackendSafetyKit.Benchmarks;

[MemoryDiagnoser]
public class SensitiveDataMaskerBenchmarks
{
    private SensitiveDataMasker _masker = null!;
    private Dictionary<string, object?> _flatPayload = null!;
    private Dictionary<string, object?> _nestedPayload = null!;
    private object?[] _collection = [];
    private LoginRequest _loginRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _masker = new SensitiveDataMasker(new SensitiveDataMaskingOptions());

        _flatPayload = new Dictionary<string, object?>
        {
            ["userName"] = "ada",
            ["accessToken"] = "secret-token-value",
            ["role"] = "reader",
            ["active"] = true
        };

        _nestedPayload = new Dictionary<string, object?>
        {
            ["profile"] = new Dictionary<string, object?>
            {
                ["displayName"] = "Ada Lovelace",
                ["password"] = "secret-password"
            },
            ["credentials"] = new Dictionary<string, object?>
            {
                ["clientSecret"] = "client-secret-value",
                ["region"] = "eu-west"
            }
        };

        _collection =
        [
            new Dictionary<string, object?>
            {
                ["id"] = 1,
                ["refreshToken"] = "secret-refresh-token"
            },
            new Dictionary<string, object?>
            {
                ["id"] = 2,
                ["name"] = "reader"
            },
            new Dictionary<string, object?>
            {
                ["id"] = 3,
                ["apiKey"] = "secret-api-key"
            }
        ];

        _loginRequest = new LoginRequest(
            "ada",
            new Credentials("user-secret", "password-secret"));
    }

    [Benchmark(Baseline = true)]
    public string? MaskSingleSecret() =>
        _masker.MaskString("secret-access-token", "accessToken");

    [Benchmark]
    public object? MaskFlatPayload() =>
        _masker.Mask(_flatPayload);

    [Benchmark]
    public object? MaskNestedPayload() =>
        _masker.Mask(_nestedPayload);

    [Benchmark]
    public object? MaskCollection() =>
        _masker.Mask(_collection);

    [Benchmark]
    public object? MaskPoco() =>
        _masker.Mask(_loginRequest);

    public sealed record LoginRequest(string UserName, Credentials Credentials);

    public sealed record Credentials(string ClientSecret, string Password);
}
