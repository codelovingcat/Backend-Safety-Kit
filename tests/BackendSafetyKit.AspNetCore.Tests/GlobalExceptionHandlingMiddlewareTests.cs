using BackendSafetyKit.AspNetCore.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using Xunit;

namespace BackendSafetyKit.AspNetCore.Tests;

public sealed class GlobalExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task UnhandledException_ReturnsInternalServerErrorWithoutExceptionDetails()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddProvider(loggerProvider));
        services.AddBackendSafety();

        var app = BuildPipeline(
            services,
            _ => throw new InvalidOperationException("super-secret-exception-message"));

        var context = CreateContext();

        await app(context);

        var response = await ReadResponseAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
        Assert.DoesNotContain("super-secret-exception-message", response);
        Assert.DoesNotContain("InvalidOperationException", response);
        Assert.Contains("An unexpected error occurred.", response);
        Assert.Equal(1, loggerProvider.ErrorCount);
    }

    [Fact]
    public async Task MappedException_ReturnsConfiguredStatusCode()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddProvider(loggerProvider));
        services.AddBackendSafety(options =>
        {
            options.ExceptionHandling.Map<KeyNotFoundException>(
                StatusCodes.Status404NotFound);
        });

        var app = BuildPipeline(
            services,
            _ => throw new KeyNotFoundException("order-42"));

        var context = CreateContext();

        await app(context);

        var response = await ReadResponseAsync(context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Contains("An error occurred while processing the request.", response);
        Assert.DoesNotContain("order-42", response);
        Assert.Equal(1, loggerProvider.ErrorCount);
    }

    [Fact]
    public async Task StartedResponse_RethrowsException()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddProvider(loggerProvider));
        services.AddBackendSafety();

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.FromException(new InvalidOperationException("after-response-start"));
            });

        var context = CreateContext();
        context.Features.Set<IHttpResponseFeature>(
            new StartedResponseFeature(context.Response.Body));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => app(context));

        Assert.Equal("after-response-start", exception.Message);
        Assert.Equal(1, loggerProvider.ErrorCount);
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

    private sealed class StartedResponseFeature(Stream body) : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = StatusCodes.Status200OK;

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } =
            new HeaderDictionary();

        public Stream Body { get; set; } = body;

        public bool HasStarted => true;

        public bool HasCompleted => false;

        public void OnStarting(Func<object, Task> callback, object state)
        {
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly RecordingLogger _logger = new();

        public int ErrorCount => _logger.ErrorCount;

        public ILogger CreateLogger(string categoryName) => _logger;

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        private int _errorCount;

        public int ErrorCount => _errorCount;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                Interlocked.Increment(ref _errorCount);
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
