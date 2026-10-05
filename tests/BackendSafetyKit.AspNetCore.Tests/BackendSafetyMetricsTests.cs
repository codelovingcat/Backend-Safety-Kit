using System.Diagnostics.Metrics;
using BackendSafetyKit;
using BackendSafetyKit.AspNetCore.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BackendSafetyKit.AspNetCore.Tests;

public sealed class BackendSafetyMetricsTests
{
    [Fact]
    public async Task HttpRequestMetricsAreEmittedWithSafeBoundedTags()
    {
        using var listener = new MeterListener();
        var requestCount = 0L;
        var errorCount = 0L;
        var slowRequestCount = 0L;
        double? durationSeconds = null;

        listener.InstrumentPublished = static (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == BackendSafetyMetrics.MeterName)
            {
                publishedInstruments.Add(instrument.Name);
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, _) =>
            {
                if (instrument.Name ==
                    "backend_safety_kit.http.server.request.count")
                {
                    requestCount += measurement.Value;
                }
                else if (instrument.Name ==
                         "backend_safety_kit.http.server.request.error.count")
                {
                    errorCount += measurement.Value;
                }
                else if (instrument.Name ==
                         "backend_safety_kit.http.server.request.slow.count")
                {
                    slowRequestCount += measurement.Value;
                }
            });

        listener.SetMeasurementEventCallback<double>(
            (instrument, measurement, _) =>
            {
                if (instrument.Name ==
                    "backend_safety_kit.http.server.request.duration")
                {
                    durationSeconds = measurement.Value;
                }
            });

        listener.Start();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety(options =>
        {
            options.RequestTiming.SlowRequestThreshold = TimeSpan.Zero;
        });

        var provider = services.BuildServiceProvider();
        var builder = new ApplicationBuilder(provider);

        builder.UseBackendSafety();
        builder.Run(context =>
        {
            context.Response.StatusCode = 599;
            return Task.CompletedTask;
        });

        var app = builder.Build();

        var context = new DefaultHttpContext
        {
            Request =
            {
                Method = HttpMethods.Get,
                Path = "/users/42"
            }
        };

        context.RequestServices = provider;

        await app(context);

        Assert.Equal(1, requestCount);
        Assert.Equal(1, errorCount);
        Assert.Equal(1, slowRequestCount);
        Assert.True(durationSeconds >= 0);

        Assert.Equal(
            BackendSafetyMetrics.Meter,
            Assert.IsType<Counter<long>>(
                GetInstrument(
                    BackendSafetyMetrics.Meter,
                    "backend_safety_kit.http.server.request.count")).Meter);

        listener.Dispose();
    }

    [Fact]
    public void MetricsMeterUsesStablePublicName()
    {
        Assert.Equal("BackendSafetyKit", BackendSafetyMetrics.Meter.Name);
    }

    private static Instrument GetInstrument(Meter meter, string name)
    {
        using var listener = new MeterListener();
        Instrument? observed = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter == meter && instrument.Name == name)
            {
                observed = instrument;
                meterListener.DisableMeasurementEvents(instrument);
            }
        };

        listener.Start();
        Assert.NotNull(observed);

        return observed!;
    }
}
