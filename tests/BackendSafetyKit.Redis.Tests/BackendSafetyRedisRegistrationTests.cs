using BackendSafetyKit.Abstractions;
using BackendSafetyKit.Redis;
using BackendSafetyKit.Redis.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BackendSafetyKit.Redis.Tests;

public sealed class BackendSafetyRedisRegistrationTests
{
    [Fact]
    public void RegistrationProvidesDiagnosticSinkWithoutOpeningRedisConnection()
    {
        var services = new ServiceCollection();
        services.AddBackendSafetyRedisDiagnostics();

        using var provider = services.BuildServiceProvider();
        var sink = provider.GetRequiredService<IBackendSafetyDiagnosticSink>();

        Assert.NotNull(sink);
    }

    [Fact]
    public void InvalidQueueCapacityFailsOptionsValidation()
    {
        var services = new ServiceCollection();
        services.AddBackendSafetyRedisDiagnostics(options => options.QueueCapacity = 0);

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<BackendSafetyRedisOptions>>().Value);
    }

    [Fact]
    public void InvalidStreamKeyFailsOptionsValidation()
    {
        var services = new ServiceCollection();
        services.AddBackendSafetyRedisDiagnostics(options => options.StreamKey = " ");

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<BackendSafetyRedisOptions>>().Value);
    }
}
