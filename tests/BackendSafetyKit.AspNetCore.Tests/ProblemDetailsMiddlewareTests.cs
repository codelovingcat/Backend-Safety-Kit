using BackendSafetyKit.AspNetCore.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using Xunit;

namespace BackendSafetyKit.AspNetCore.Tests;

public sealed class ProblemDetailsMiddlewareTests
{
    [Fact]
    public async Task ExceptionReturnsStandardProblemDetailsWithoutSensitiveDetail()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety();

        var app = BuildPipeline(
            services,
            _ => throw new InvalidOperationException("super-secret-exception-message"));

        var context = CreateContext();
        context.TraceIdentifier = "trace-123";

        await app(context);

        var body = await ReadResponseAsync(context);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal("application/problem+json", context.Response.ContentType?.Split(';')[0]);
        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("An unexpected error occurred.", root.GetProperty("title").GetString());
        Assert.Equal("about:blank", root.GetProperty("type").GetString());
        Assert.Equal("/test", root.GetProperty("instance").GetString());
        Assert.Equal("trace-123", root.GetProperty("traceId").GetString());
        Assert.False(root.TryGetProperty("detail", out _));
        Assert.DoesNotContain("super-secret-exception-message", body);
    }

    [Fact]
    public async Task MappedExceptionSupportsCustomCodeTitleAndExtensions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety(options =>
        {
            options.ExceptionHandling.Map<KeyNotFoundException>(404);
            options.ProblemDetails
                .MapErrorCode<KeyNotFoundException>("order_not_found")
                .MapTitle<KeyNotFoundException>("Order was not found.");

            options.ProblemDetails.Customize = context =>
            {
                context.Detail = "The requested order does not exist.";
                context.Extensions["errors"] = new Dictionary<string, string[]>
                {
                    ["orderId"] = ["order-42"]
                };
            };
        });

        var app = BuildPipeline(
            services,
            _ => throw new KeyNotFoundException("order-42"));

        var context = CreateContext();

        await app(context);

        var body = await ReadResponseAsync(context);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal("Order was not found.", root.GetProperty("title").GetString());
        Assert.Equal("The requested order does not exist.", root.GetProperty("detail").GetString());
        Assert.Equal("order_not_found", root.GetProperty("code").GetString());
        Assert.Equal(
            "order-42",
            root.GetProperty("errors").GetProperty("orderId")[0].GetString());
    }

    [Fact]
    public async Task TraceAndInstanceCanBeDisabled()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety(options =>
        {
            options.ProblemDetails.IncludeTraceId = false;
            options.ProblemDetails.IncludeInstance = false;
        });

        var app = BuildPipeline(
            services,
            _ => throw new InvalidOperationException("hidden"));

        var context = CreateContext();
        context.TraceIdentifier = "trace-disabled";

        await app(context);

        var body = await ReadResponseAsync(context);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.False(root.TryGetProperty("traceId", out _));
        Assert.False(root.TryGetProperty("instance", out _));
    }

    private static RequestDelegate BuildPipeline(
        IServiceCollection services,
        RequestDelegate terminal)
    {
        var serviceProvider = services.BuildServiceProvider();
        var builder = new ApplicationBuilder(serviceProvider);

        builder.UseBackendSafety();
        builder.Run(terminal);

        return builder.Build();
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };

        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/test";

        return context;
    }

    private static async Task<string> ReadResponseAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;

        using var reader = new StreamReader(
            context.Response.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        return await reader.ReadToEndAsync();
    }
}
