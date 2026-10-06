using System.Diagnostics;
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
    public void AddBackendSafetyOpenTelemetryRegistersBothSignals()
    {
        var services = new ServiceCollection();

        services.AddBackendSafetyOpenTelemetry();

        var exportedActivities = new List<Activity>();
        var exportedMetrics = new List<Metric>();

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddInMemoryExporter(exportedActivities))
            .WithMetrics(metrics => metrics.AddInMemoryExporter(exportedMetrics));

        using var provider = services.BuildServiceProvider();
        using var tracerProvider = provider.GetRequiredService<TracerProvider>();
        using var meterProvider = provider.GetRequiredService<MeterProvider>();

        using (BackendSafetyDiagnostics.ActivitySource.StartActivity(
                   "backend-safety-kit.test.trace",
                   ActivityKind.Internal))
        {
        }

        var counter = BackendSafetyMetrics.Meter.CreateCounter<long>(
            "backend_safety_kit.test.enabled.count");
        counter.Add(1);

        Assert.True(tracerProvider.ForceFlush());
        Assert.True(meterProvider.ForceFlush());

        Assert.Single(exportedActivities);
        Assert.Contains(
            exportedMetrics,
            metric => metric.Name == "backend_safety_kit.test.enabled.count");
    }

    [Fact]
    public void AddBackendSafetyOpenTelemetryCanDisableTracing()
    {
        var services = new ServiceCollection();

        services.AddBackendSafetyOpenTelemetry(options =>
        {
            options.EnableTracing = false;
        });

        var exportedActivities = new List<Activity>();
        var exportedMetrics = new List<Metric>();

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddInMemoryExporter(exportedActivities))
            .WithMetrics(metrics => metrics.AddInMemoryExporter(exportedMetrics));

        using var provider = services.BuildServiceProvider();
        using var tracerProvider = provider.GetRequiredService<TracerProvider>();
        using var meterProvider = provider.GetRequiredService<MeterProvider>();

        using (BackendSafetyDiagnostics.ActivitySource.StartActivity(
                   "backend-safety-kit.test.tracing-disabled",
                   ActivityKind.Internal))
        {
        }

        var counter = BackendSafetyMetrics.Meter.CreateCounter<long>(
            "backend_safety_kit.test.tracing-disabled.count");
        counter.Add(1);

        Assert.True(tracerProvider.ForceFlush());
        Assert.True(meterProvider.ForceFlush());

        Assert.Empty(exportedActivities);
        Assert.Contains(
            exportedMetrics,
            metric => metric.Name == "backend_safety_kit.test.tracing-disabled.count");
    }

    [Fact]
    public void AddBackendSafetyOpenTelemetryCanDisableMetrics()
    {
        var services = new ServiceCollection();

        services.AddBackendSafetyOpenTelemetry(options =>
        {
            options.EnableMetrics = false;
        });

        var exportedActivities = new List<Activity>();
        var exportedMetrics = new List<Metric>();

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddInMemoryExporter(exportedActivities))
            .WithMetrics(metrics => metrics.AddInMemoryExporter(exportedMetrics));

        using var provider = services.BuildServiceProvider();
        using var tracerProvider = provider.GetRequiredService<TracerProvider>();
        using var meterProvider = provider.GetRequiredService<MeterProvider>();

        using (BackendSafetyDiagnostics.ActivitySource.StartActivity(
                   "backend-safety-kit.test.metrics-disabled",
                   ActivityKind.Internal))
        {
        }

        var counter = BackendSafetyMetrics.Meter.CreateCounter<long>(
            "backend_safety_kit.test.metrics-disabled.count");
        counter.Add(1);

        Assert.True(tracerProvider.ForceFlush());
        Assert.True(meterProvider.ForceFlush());

        Assert.Single(exportedActivities);
        Assert.DoesNotContain(
            exportedMetrics,
            metric => metric.Name == "backend_safety_kit.test.metrics-disabled.count");
    }

    [Fact]
    public void AddBackendSafetyOpenTelemetryUsesExistingBackendSafetyDiagnosticsNames()
    {
        Assert.Equal("BackendSafetyKit", BackendSafetyDiagnostics.ActivitySource.Name);
        Assert.Equal("BackendSafetyKit", BackendSafetyMetrics.Meter.Name);
    }
}
