using BackendSafetyKit.AspNetCore.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using Xunit;

namespace BackendSafetyKit.AspNetCore.Tests;

public sealed class ProblemDetailsHardeningTests
{
    [Fact]
    public async Task MoreSpecificExceptionMappingWinsRegardlessOfRegistrationOrder()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety(options =>
        {
            options.ExceptionHandling.Map<Exception>(500);
            options.ExceptionHandling.Map<ApplicationException>(422);

            options.ProblemDetails
                .MapErrorCode<Exception>("generic_error")
                .MapErrorCode<ApplicationException>("application_error")
                .MapTitle<Exception>("Generic error")
                .MapTitle<ApplicationException>("Application error");
        });

        var app = BuildPipeline(
            services,
            _ => throw new SpecificApplicationException("test"));

        var context = CreateContext();

        await app(context);

        var body = await ReadResponseAsync(context);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(422, context.Response.StatusCode);
        Assert.Equal("application_error", root.GetProperty("code").GetString());
        Assert.Equal("Application error", root.GetProperty("title").GetString());
    }

    [Fact]
    public async Task BaseExceptionMappingAppliesWhenNoMoreSpecificMappingExists()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety(options =>
        {
            options.ExceptionHandling.Map<Exception>(400);
        });

        var app = BuildPipeline(
            services,
            _ => throw new InvalidOperationException("test"));

        var context = CreateContext();

        await app(context);

        Assert.Equal(400, context.Response.StatusCode);
    }

    [Fact]
    public async Task CustomizedProblemDetailsTypeCannotBeEmpty()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety(options =>
        {
            options.ProblemDetails.Customize = context =>
            {
                context.Type = " ";
            };
        });

        var app = BuildPipeline(
            services,
            _ => throw new InvalidOperationException("test"));

        var context = CreateContext();

        await Assert.ThrowsAsync<ArgumentException>(() => app(context));
    }

    [Fact]
    public async Task CustomizedProblemDetailsExtensionNameCannotBeEmpty()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety(options =>
        {
            options.ProblemDetails.Customize = context =>
            {
                context.Extensions[""] = "invalid";
            };
        });

        var app = BuildPipeline(
            services,
            _ => throw new InvalidOperationException("test"));

        var context = CreateContext();

        await Assert.ThrowsAsync<ArgumentException>(() => app(context));
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
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
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

    private sealed class SpecificApplicationException(string message)
        : ApplicationException(message);
}
