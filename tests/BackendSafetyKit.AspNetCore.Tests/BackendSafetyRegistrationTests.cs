using BackendSafetyKit;
using BackendSafetyKit.AspNetCore.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BackendSafetyKit.AspNetCore.Tests;

public sealed class BackendSafetyRegistrationTests
{
    [Fact]
    public void AddBackendSafetyRegistersOptions()
    {
        var services = new ServiceCollection();

        services.AddBackendSafety();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BackendSafetyOptions>>();

        Assert.NotNull(options.Value);
        Assert.IsType<SensitiveDataMasker>(
            provider.GetRequiredService<ISensitiveDataMasker>());
    }

    [Fact]
    public void AddBackendSafetyValidatesOptionsOnStartup()
    {
        var services = new ServiceCollection();

        services.AddBackendSafety(options =>
        {
            options.ExceptionHandling.DefaultStatusCode = StatusCodes.Status200OK;
        });

        using var provider = services.BuildServiceProvider();
        var startupValidator = provider.GetRequiredService<IStartupValidator>();

        var exception = Assert.Throws<OptionsValidationException>(
            startupValidator.Validate);

        Assert.Contains("between 400 and 599", exception.Message);
    }

    [Fact]
    public async Task DisabledCorrelationDoesNotAddResponseHeader()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety(options =>
            options.Features.EnableCorrelationId = false);

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            });

        var context = new DefaultHttpContext();
        await app(context);

        Assert.False(context.Response.Headers.ContainsKey("X-Correlation-ID"));
    }

    [Fact]
    public async Task DisabledHttpSecurityDoesNotAddSecurityHeaders()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety(options =>
            options.Features.EnableHttpSecurity = false);

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            });

        var context = new DefaultHttpContext();
        await app(context);

        Assert.False(context.Response.Headers.ContainsKey("X-Content-Type-Options"));
    }

    [Fact]
    public async Task DisabledExceptionHandlingLetsExceptionEscape()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety(options =>
            options.Features.EnableExceptionHandling = false);

        var app = BuildPipeline(
            services,
            _ => throw new InvalidOperationException("expected"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => app(new DefaultHttpContext()));

        Assert.Equal("expected", exception.Message);
    }

    [Fact]
    public async Task DisabledRequestTimingDoesNotRunCompletionCallback()
    {
        var callbackInvoked = false;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety(options =>
        {
            options.Features.EnableRequestTiming = false;
            options.RequestTiming.OnCompleted = _ => callbackInvoked = true;
        });

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            });

        await app(new DefaultHttpContext());

        Assert.False(callbackInvoked);
    }

    [Fact]
    public async Task AllMiddlewareFeaturesCanBeDisabled()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety(options =>
        {
            options.Features.EnableCorrelationId = false;
            options.Features.EnableRequestLogging = false;
            options.Features.EnableRequestTiming = false;
            options.Features.EnableHttpSecurity = false;
            options.Features.EnableExceptionHandling = false;
        });

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            });

        var context = new DefaultHttpContext();
        await app(context);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("X-Correlation-ID"));
        Assert.False(context.Response.Headers.ContainsKey("X-Content-Type-Options"));
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

    [Fact]
    public async Task UseBackendSafetyAllowsRequestToReachEndpoint()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety();

        var builder = new ApplicationBuilder(services.BuildServiceProvider());

        var reachedEndpoint = false;

        builder.UseBackendSafety();
        builder.Run(context =>
        {
            reachedEndpoint = true;
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        var app = builder.Build();
        var context = new DefaultHttpContext();
        await app(context);

        Assert.True(reachedEndpoint);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }
}
