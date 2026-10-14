using System.Globalization;
using BackendSafetyKit.AspNetCore.Correlation;
using BackendSafetyKit.AspNetCore.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BackendSafetyKit.AspNetCore.Tests;

public sealed class MiddlewareConcurrencyStressTests
{
    [Fact]
    public async Task DefaultPipelineKeepsCorrelationAndResponseStateIsolatedAcrossConcurrentRequests()
    {
        const int requestCount = 128;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety();

        await using var rootProvider = services.BuildServiceProvider();
        var builder = new ApplicationBuilder(rootProvider);

        builder.UseBackendSafety();
        builder.Run(context =>
        {
            var accessor = context.RequestServices.GetRequiredService<ICorrelationIdAccessor>();
            context.Response.Headers["X-Observed-Correlation-ID"] =
                accessor.CorrelationId ?? string.Empty;
            context.Response.StatusCode =
                context.Request.Headers["X-Expected-Status"] == "404"
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status204NoContent;

            return Task.CompletedTask;
        });

        var pipeline = builder.Build();
        var responses = await Task.WhenAll(
            Enumerable.Range(0, requestCount).Select(async index =>
            {
                var expectedCorrelationId = $"stress-request-{index}";
                var expectedStatus = index % 2 == 0
                    ? StatusCodes.Status204NoContent
                    : StatusCodes.Status404NotFound;

                await using var scope = rootProvider.CreateAsyncScope();
                var context = new DefaultHttpContext
                {
                    RequestServices = scope.ServiceProvider
                };
                context.Request.Method = HttpMethods.Get;
                context.Request.Path = $"/stress/{index}";
                context.Request.Headers["X-Correlation-ID"] = expectedCorrelationId;
                context.Request.Headers["X-Expected-Status"] = expectedStatus.ToString(CultureInfo.InvariantCulture);

                await pipeline(context);

                return new RequestResult(
                    expectedCorrelationId,
                    context.TraceIdentifier,
                    context.Response.Headers["X-Correlation-ID"].ToString(),
                    context.Response.Headers["X-Observed-Correlation-ID"].ToString(),
                    expectedStatus,
                    context.Response.StatusCode);
            }));

        Assert.Equal(
            requestCount,
            responses.Select(result => result.TraceIdentifier)
                .Distinct(StringComparer.Ordinal)
                .Count());

        Assert.All(responses, result =>
        {
            Assert.Equal(result.ExpectedCorrelationId, result.TraceIdentifier);
            Assert.Equal(result.ExpectedCorrelationId, result.ResponseCorrelationId);
            Assert.Equal(result.ExpectedCorrelationId, result.ObservedCorrelationId);
            Assert.Equal(result.ExpectedStatus, result.ActualStatus);
        });
    }

    private sealed record RequestResult(
        string ExpectedCorrelationId,
        string TraceIdentifier,
        string ResponseCorrelationId,
        string ObservedCorrelationId,
        int ExpectedStatus,
        int ActualStatus);
}
