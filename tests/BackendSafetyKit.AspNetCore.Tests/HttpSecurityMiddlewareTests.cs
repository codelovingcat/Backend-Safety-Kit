using BackendSafetyKit;
using BackendSafetyKit.AspNetCore.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BackendSafetyKit.AspNetCore.Tests;

public sealed class HttpSecurityMiddlewareTests
{
    [Fact]
    public async Task NoSniffHeaderIsAddedByDefault()
    {
        var services = CreateServices();
        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            });

        var context = CreateContext();

        await app(context);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
    }

    [Fact]
    public async Task SecurityHeadersCanBeIndividuallyEnabled()
    {
        var services = CreateServices(options =>
        {
            options.HttpSecurity.EnableFrameOptionsHeader = true;
            options.HttpSecurity.FrameOptions = "SAMEORIGIN";
            options.HttpSecurity.EnableReferrerPolicyHeader = true;
            options.HttpSecurity.ReferrerPolicy = "strict-origin-when-cross-origin";
            options.HttpSecurity.EnableContentSecurityPolicyHeader = true;
            options.HttpSecurity.ContentSecurityPolicy = "default-src 'self'";
        });

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            });

        var context = CreateContext();

        await app(context);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("SAMEORIGIN", context.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal(
            "strict-origin-when-cross-origin",
            context.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal(
            "default-src 'self'",
            context.Response.Headers["Content-Security-Policy"].ToString());
    }

    [Fact]
    public async Task ExistingSecurityHeadersArePreserved()
    {
        var services = CreateServices(options =>
            options.HttpSecurity.EnableFrameOptionsHeader = true);

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            });

        var context = CreateContext();

        await app(context);

        Assert.Equal("SAMEORIGIN", context.Response.Headers["X-Frame-Options"].ToString());
    }

    [Fact]
    public async Task SecurityHeadersCanBeDisabled()
    {
        var services = CreateServices(options =>
            options.HttpSecurity.EnableNoSniffHeader = false);

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            });

        var context = CreateContext();

        await app(context);

        Assert.False(context.Response.Headers.ContainsKey("X-Content-Type-Options"));
    }

    [Fact]
    public async Task ConfiguredRequestBodySizeIsAppliedToServerFeature()
    {
        var services = CreateServices(options =>
            options.HttpSecurity.MaxRequestBodySize = 4096);

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            });

        var context = CreateContext();
        var feature = new TestMaxRequestBodySizeFeature();
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(feature);

        await app(context);

        Assert.Equal(4096, feature.MaxRequestBodySize);
    }

    [Fact]
    public async Task RequestBodyLimitCanBeDisabledByDefault()
    {
        var services = CreateServices();
        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            });

        var context = CreateContext();
        var feature = new TestMaxRequestBodySizeFeature();

        context.Features.Set<IHttpMaxRequestBodySizeFeature>(feature);
        feature.MaxRequestBodySize = 1024 * 1024;

        await app(context);

        Assert.Equal(1024 * 1024, feature.MaxRequestBodySize);
    }

    [Fact]
    public void InvalidRequestBodySizeConfigurationFailsFast()
    {
        var services = CreateServices(options =>
            options.HttpSecurity.MaxRequestBodySize = 0);

        var exception = Assert.Throws<OptionsValidationException>(
            () => BuildPipeline(
                services,
                context => Task.CompletedTask));

        Assert.Contains("HttpSecurity", exception.Message);
        Assert.Contains("maximum request body size", exception.Message);
    }

    [Fact]
    public void InvalidCustomSecurityHeaderValuesFailValidation()
    {
        var services = CreateServices(options =>
        {
            options.HttpSecurity.EnableReferrerPolicyHeader = true;
            options.HttpSecurity.ReferrerPolicy = "bad\r\nvalue";
        });

        var exception = Assert.Throws<OptionsValidationException>(
            () => BuildPipeline(
                services,
                context => Task.CompletedTask));

        Assert.Contains("HttpSecurity", exception.Message);
        Assert.Contains("carriage return or line feed", exception.Message);
    }

    private static ServiceCollection CreateServices(
        Action<BackendSafetyOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety(configure);
        return services;
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

    private static DefaultHttpContext CreateContext() =>
        new();

    private sealed class TestMaxRequestBodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        private long? maxRequestBodySize = 30 * 1024 * 1024;

        public long? MaxRequestBodySize
        {
            get => maxRequestBodySize;
            set => maxRequestBodySize = value;
        }

        public bool IsReadOnly => false;
    }
}
