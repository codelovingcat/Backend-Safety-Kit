using BackendSafetyKit;
using BackendSafetyKit.OpenTelemetry.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace BackendSafetyKit.OpenTelemetry.Tests;

public sealed class BackendSafetyOpenTelemetryServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBackendSafetyOpenTelemetryRegistersTracingAndMetricsProviders()
    {
        var services = new ServiceCollection();

        services.AddBackendSafetyOpenTelemetry();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<TracerProvider>());
        Assert.NotNull(provider.GetService<MeterProvider>());
    }

    [Fact]
    public void AddBackendSafetyOpenTelemetryCanDisableEachSignalIndependently()
    {
        var tracingDisabledServices = new ServiceCollection();

        tracingDisabledServices.AddBackendSafetyOpenTelemetry(options =>
        {
            options.EnableTracing = false;
        });

        using var tracingDisabledProvider = tracingDisabledServices.BuildServiceProvider();

        Assert.Null(tracingDisabledProvider.GetService<TracerProvider>());
        Assert.NotNull(tracingDisabledProvider.GetService<MeterProvider>());

        var metricsDisabledServices = new ServiceCollection();

        metricsDisabledServices.AddBackendSafetyOpenTelemetry(options =>
        {
            options.EnableMetrics = false;
        });

        using var metricsDisabledProvider = metricsDisabledServices.BuildServiceProvider();

        Assert.NotNull(metricsDisabledProvider.GetService<TracerProvider>());
        Assert.Null(metricsDisabledProvider.GetService<MeterProvider>());
    }

    [Fact]
    public void AddBackendSafetyOpenTelemetryUsesExistingBackendSafetyDiagnosticsNames()
    {
        var services = new ServiceCollection();

        services.AddBackendSafetyOpenTelemetry();

        using var provider = services.BuildServiceProvider();
        using var tracerProvider = provider.GetRequiredService<TracerProvider>();
        using var meterProvider = provider.GetRequiredService<MeterProvider>();

        Assert.Equal("BackendSafetyKit", BackendSafetyDiagnostics.ActivitySource.Name);
        Assert.Equal("BackendSafetyKit", BackendSafetyMetrics.Meter.Name);
    }
}
