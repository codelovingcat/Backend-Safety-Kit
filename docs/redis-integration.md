# Redis Diagnostics Integration

The optional `BackendSafetyKit.Redis` package stores safe Backend Safety Kit diagnostic events in a Redis Stream. Redis is not a dependency of the core package and is never connected automatically by the core package.

## Install and register

```bash
dotnet add package BackendSafetyKit.Redis
```

Create and register an `IConnectionMultiplexer` through your host's own configuration/secret mechanism, then register the sink:

```csharp
using BackendSafetyKit.Redis.DependencyInjection;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("A Redis connection string is required.");
    return ConnectionMultiplexer.Connect(connectionString);
});

builder.Services.AddBackendSafetyRedisDiagnostics(options =>
{
    options.StreamKey = "backend-safety:diagnostics";
    options.QueueCapacity = 512;
    options.MaxStreamLength = 10000;
});

// Register the normal ASP.NET Core integration separately when needed.
builder.Services.AddBackendSafety();
```

The application owns the connection multiplexer lifecycle and must provide a Redis connection before the hosted writer starts. Do not store credentials in source control or log connection strings.

## Delivery behavior

The sink implements the core `IBackendSafetyDiagnosticSink` contract. Writes from request processing enqueue an event in a bounded in-memory channel; they do not perform Redis network I/O on the request thread. A hosted service asynchronously writes entries to the configured Redis Stream.

The queue defaults to 512 events and drops the oldest pending event on overflow. The Redis stream defaults to `backend-safety:diagnostics` with an approximate maximum length of 10,000 entries. Both bounds are configurable and validated. Events that cannot be serialized or written are logged as warnings and dropped; this is best-effort diagnostics, not a durable audit ledger.

Only put operational metadata in diagnostic events. Do not include credentials, request/response bodies, query strings, raw headers, or personal data in event properties. Restrict Redis network access and permissions to the minimum required key/stream operations. The integration does not provision Redis, choose persistence settings, or expose a health endpoint.

## Tests

The package has isolated tests for dependency registration and invalid options. They do not require a live Redis server. Run the full solution tests and package validator before publishing.
