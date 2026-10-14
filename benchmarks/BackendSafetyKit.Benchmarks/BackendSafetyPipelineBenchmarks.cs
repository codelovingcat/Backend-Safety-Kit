using BackendSafetyKit.AspNetCore.DependencyInjection;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BackendSafetyKit.Benchmarks;

[MemoryDiagnoser]
public class BackendSafetyPipelineBenchmarks
{
    private ServiceProvider _rootProvider = null!;
    private RequestDelegate _baselinePipeline = null!;
    private RequestDelegate _backendSafetyPipeline = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety();
        _rootProvider = services.BuildServiceProvider();

        var baselineBuilder = new ApplicationBuilder(_rootProvider);
        baselineBuilder.Run(CompleteRequest);
        _baselinePipeline = baselineBuilder.Build();

        var backendSafetyBuilder = new ApplicationBuilder(_rootProvider);
        backendSafetyBuilder.UseBackendSafety();
        backendSafetyBuilder.Run(CompleteRequest);
        _backendSafetyPipeline = backendSafetyBuilder.Build();
    }

    [Benchmark(Baseline = true)]
    public Task MinimalPipeline() =>
        RunRequestAsync(_baselinePipeline);

    [Benchmark]
    public Task BackendSafetyPipeline() =>
        RunRequestAsync(_backendSafetyPipeline);

    [GlobalCleanup]
    public async Task Cleanup() =>
        await _rootProvider.DisposeAsync();

    private async Task RunRequestAsync(RequestDelegate pipeline)
    {
        await using var scope = _rootProvider.CreateAsyncScope();
        var context = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };

        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/benchmark";

        await pipeline(context);
    }

    private static Task CompleteRequest(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status204NoContent;
        return Task.CompletedTask;
    }
}
