using BackendSafetyKit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BackendSafetyKit.Redis.DependencyInjection;

/// <summary>
/// Provides opt-in registration for Redis-backed diagnostic events.
/// </summary>
public static class BackendSafetyRedisServiceCollectionExtensions
{
    /// <summary>
    /// Registers a bounded, asynchronous Redis Streams diagnostic sink.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configure">Optional callback to configure stream and queue limits.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// The application must register and own an <see cref="StackExchange.Redis.IConnectionMultiplexer"/>
    /// before the hosted service starts. This method does not create a connection or make a network call.
    /// </remarks>
    public static IServiceCollection AddBackendSafetyRedisDiagnostics(
        this IServiceCollection services,
        Action<BackendSafetyRedisOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<BackendSafetyRedisOptions>().ValidateOnStart();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.AddSingleton<IValidateOptions<BackendSafetyRedisOptions>, BackendSafetyRedisOptionsValidator>();
        services.AddSingleton<RedisDiagnosticQueue>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IBackendSafetyDiagnosticSink, RedisBackendSafetyDiagnosticSink>());
        services.AddHostedService<RedisDiagnosticBackgroundService>();

        return services;
    }
}
