using System.Net;
using BackendSafetyKit.AspNetCore.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BackendSafetyKit.AspNetCore.Tests;

public sealed class ForwardedHeadersSecurityTests
{
    [Fact]
    public async Task TrustedForwardedProtoIsAppliedWhenMiddlewareRunsBeforeBackendSafety()
    {
        var observedScheme = await RunPipelineAsync(IPAddress.Loopback);

        Assert.Equal("https", observedScheme);
    }

    [Fact]
    public async Task ForwardedProtoFromUntrustedProxyIsIgnored()
    {
        var observedScheme = await RunPipelineAsync(IPAddress.Parse("203.0.113.10"));

        Assert.Equal("http", observedScheme);
    }

    private static async Task<string> RunPipelineAsync(IPAddress remoteAddress)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety(options =>
        {
            options.Features.EnableCorrelationId = false;
            options.Features.EnableDistributedTracing = false;
            options.Features.EnableRequestLogging = false;
            options.Features.EnableRequestTiming = false;
            options.Features.EnableHttpSecurity = false;
            options.Features.EnableExceptionHandling = false;
        });
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
        });

        var provider = services.BuildServiceProvider();
        var builder = new ApplicationBuilder(provider);
        builder.UseForwardedHeaders();
        builder.UseBackendSafety();

        var observedScheme = string.Empty;
        builder.Run(context =>
        {
            observedScheme = context.Request.Scheme;
            return Task.CompletedTask;
        });

        var app = builder.Build();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = remoteAddress;
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("api.example.test");
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.25";
        context.Request.Headers["X-Forwarded-Proto"] = "https";

        await app(context);

        return observedScheme;
    }
}
