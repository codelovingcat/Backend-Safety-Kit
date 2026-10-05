using System.Diagnostics;
using BackendSafetyKit;
using BackendSafetyKit.AspNetCore.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BackendSafetyKit.AspNetCore.Tests;

public sealed class DistributedTracingMiddlewareTests
{
    private const string TraceParent =
        "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    [Fact]
    public async Task ValidTraceParentCreatesServerActivityWithRemoteParent()
    {
        using var listener = CreateListener();
        Activity? observedActivity = null;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety();

        var app = BuildPipeline(
            services,
            _ =>
            {
                observedActivity = Activity.Current;
                return Task.CompletedTask;
            });

        var context = new DefaultHttpContext();
        context.Request.Headers["traceparent"] = TraceParent;

        await app(context);

        Assert.NotNull(observedActivity);
        Assert.Equal(ActivityKind.Server, observedActivity!.Kind);
        Assert.Equal("BackendSafetyKit.HttpRequest", observedActivity.OperationName);
        Assert.Equal(ActivityTraceId.CreateFromString(
            "4bf92f3577b34da6a3ce929d0e0e4736"), observedActivity.TraceId);
        Assert.Equal(ActivitySpanId.CreateFromString(
            "00f067aa0ba902b7"), observedActivity.ParentSpanId);
        Assert.True(observedActivity.ParentId?.Contains(
            "00f067aa0ba902b7",
            StringComparison.OrdinalIgnoreCase));
        Assert.True(observedActivity.IsAllDataRequested);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task InvalidTraceParentDoesNotPreventRequestProcessing()
    {
        using var listener = CreateListener();
        Activity? observedActivity = null;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety();

        var app = BuildPipeline(
            services,
            _ =>
            {
                observedActivity = Activity.Current;
                return Task.CompletedTask;
            });

        var context = new DefaultHttpContext();
        context.Request.Headers["traceparent"] = "malformed";
        context.Request.Headers["tracestate"] = "also-malformed";

        await app(context);

        Assert.NotNull(observedActivity);
        Assert.Equal(ActivityKind.Server, observedActivity!.Kind);
        Assert.NotEqual(
            ActivityTraceId.CreateFromString(
                "4bf92f3577b34da6a3ce929d0e0e4736"),
            observedActivity.TraceId);
        Assert.Equal(default, observedActivity.ParentSpanId);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task ExistingActivityIsReusedWithoutCreatingNestedServerActivity()
    {
        using var upstream = new Activity("upstream").Start();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety();

        Activity? observedActivity = null;
        var app = BuildPipeline(
            services,
            _ =>
            {
                observedActivity = Activity.Current;
                return Task.CompletedTask;
            });

        await app(new DefaultHttpContext());

        Assert.Same(upstream, observedActivity);
    }

    [Fact]
    public async Task DistributedTracingCanBeDisabled()
    {
        using var listener = CreateListener();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety(
            options => options.Features.EnableDistributedTracing = false);

        Activity? observedActivity = null;

        var app = BuildPipeline(
            services,
            _ =>
            {
                observedActivity = Activity.Current;
                return Task.CompletedTask;
            });

        await app(new DefaultHttpContext());

        Assert.Null(observedActivity);
    }

    private static ActivityListener CreateListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source =>
                source.Name == "BackendSafetyKit",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData
        };

        ActivitySource.AddActivityListener(listener);

        return listener;
    }

    private static RequestDelegate BuildPipeline(
        IServiceCollection services,
        RequestDelegate terminal)
    {
        var provider = services.BuildServiceProvider();
        var builder = new ApplicationBuilder(provider);

        builder.UseBackendSafety();
        builder.Run(terminal);

        return builder.Build();
    }
}
