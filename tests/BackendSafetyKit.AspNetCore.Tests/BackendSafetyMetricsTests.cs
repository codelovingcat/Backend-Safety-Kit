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
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, _) =>
            {
                if (!HasStatusCode(tags, 599))
                {
                    return;
                }

                if (instrument.Name ==
                    "backend_safety_kit.http.server.request.count")
                {
                    requestCount += measurement;
                }
                else if (instrument.Name ==
                         "backend_safety_kit.http.server.request.error.count")
                {
                    errorCount += measurement;
                }
                else if (instrument.Name ==
                         "backend_safety_kit.http.server.request.slow.count")
                {
                    slowRequestCount += measurement;
                }
            });

        listener.SetMeasurementEventCallback<double>(
            (instrument, measurement, tags, _) =>
            {
                if (instrument.Name ==
                        "backend_safety_kit.http.server.request.duration" &&
                    HasStatusCode(tags, 599))
                {
                    durationSeconds = measurement;
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
        Assert.True(durationSeconds is >= 0);
        Assert.Equal("BackendSafetyKit", BackendSafetyMetrics.Meter.Name);
    }

    private static bool HasStatusCode(
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        int expectedStatusCode)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == "http.response.status_code" &&
                tag.Value is int statusCode)
            {
                return statusCode == expectedStatusCode;
            }
        }

        return false;
    }
}
