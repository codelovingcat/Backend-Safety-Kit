using BackendSafetyKit;
using BackendSafetyKit.AspNetCore.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

    [Fact]
    public async Task HttpsRedirectionIsDisabledByDefault()
    {
        var services = CreateServices();
        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            });

        var context = CreateContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("api.example.test", 8080);

        await app(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
        Assert.False(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
    }

    [Fact]
    public async Task HttpsRedirectionUsesConfiguredPortAndPreservesPathAndQuery()
    {
        var services = CreateServices(options =>
        {
            options.HttpSecurity.EnableHttpsRedirection = true;
            options.HttpSecurity.HttpsPort = 8443;
        });
        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            });

        var context = CreateContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("api.example.test", 8080);
        context.Request.PathBase = "/base";
        context.Request.Path = "/resource";
        context.Request.QueryString = new QueryString("?return=%2Fhome");

        await app(context);

        Assert.Equal(StatusCodes.Status307TemporaryRedirect, context.Response.StatusCode);
        Assert.Equal(
            "https://api.example.test:8443/base/resource?return=%2Fhome",
            context.Response.Headers.Location.ToString());
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
    }

    [Fact]
    public async Task HttpsRedirectionUsesPermanentMethodPreservingStatusWhenConfigured()
    {
        var services = CreateServices(options =>
        {
            options.HttpSecurity.EnableHttpsRedirection = true;
            options.HttpSecurity.HttpsPort = 443;
            options.HttpSecurity.HttpsRedirectStatusCode = 308;
        });
        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            });

        var context = CreateContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("api.example.test");

        await app(context);

        Assert.Equal(StatusCodes.StatusPermanentRedirect, context.Response.StatusCode);
        Assert.Equal("https://api.example.test/", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task HstsIsAddedOnlyToHttpsResponses()
    {
        var services = CreateServices(options =>
        {
            options.HttpSecurity.EnableHsts = true;
            options.HttpSecurity.HstsIncludeSubDomains = true;
            options.HttpSecurity.HstsPreload = true;
        });
        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            });

        var httpContext = CreateContext();
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("api.example.test");

        await app(httpContext);

        Assert.False(httpContext.Response.Headers.ContainsKey("Strict-Transport-Security"));

        var httpsContext = CreateContext();
        httpsContext.Request.Scheme = "https";
        httpsContext.Request.Host = new HostString("api.example.test");

        await app(httpsContext);

        Assert.Equal(
            "max-age=31536000; includeSubDomains; preload",
            httpsContext.Response.Headers["Strict-Transport-Security"].ToString());
    }

    [Fact]
    public async Task ExistingHstsHeaderIsPreserved()
    {
        var services = CreateServices(options =>
        {
            options.HttpSecurity.EnableHsts = true;
            options.HttpSecurity.HstsMaxAge = TimeSpan.FromDays(365);
        });
        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.Headers["Strict-Transport-Security"] = "max-age=10";
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            });

        var context = CreateContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("api.example.test");

        await app(context);

        Assert.Equal("max-age=10", context.Response.Headers["Strict-Transport-Security"].ToString());
    }

    [Fact]
    public void HttpsRedirectionRequiresAnExplicitValidPort()
    {
        var services = CreateServices(options =>
        {
            options.HttpSecurity.EnableHttpsRedirection = true;
            options.HttpSecurity.HttpsPort = null;
        });

        var exception = Assert.Throws<OptionsValidationException>(
            () => BuildPipeline(services, _ => Task.CompletedTask));

        Assert.Contains("HttpsPort", exception.Message);
    }

    [Fact]
    public void HstsPreloadRequiresSubdomainsAndAtLeastOneYear()
    {
        var services = CreateServices(options =>
        {
            options.HttpSecurity.EnableHsts = true;
            options.HttpSecurity.HstsPreload = true;
            options.HttpSecurity.HstsIncludeSubDomains = false;
        });

        var exception = Assert.Throws<OptionsValidationException>(
            () => BuildPipeline(services, _ => Task.CompletedTask));

        Assert.Contains("HSTS preload requires includeSubDomains", exception.Message);
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
