# Entity Framework Core Integration

The optional `BackendSafetyKit.EntityFrameworkCore` package adds a `SaveChangesInterceptor` that emits structured diagnostic events through the core `IBackendSafetyDiagnosticSink` contract. It does not change entity values, create schema objects, enable SQL logging, or select a database provider.

## Install and configure

```bash
dotnet add package BackendSafetyKit.EntityFrameworkCore
```

```csharp
using BackendSafetyKit.Abstractions;
using BackendSafetyKit.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

builder.Services.AddSingleton<IBackendSafetyDiagnosticSink, ApplicationDiagnosticSink>();

builder.Services.AddDbContext<ApplicationDbContext>((services, options) =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Application")
        ?? throw new InvalidOperationException("The database connection string is required."));

    options.AddBackendSafetyEntityFrameworkCore(
        services.GetServices<IBackendSafetyDiagnosticSink>());
});
```

Replace the provider with the one your application already uses. The package does not add a database provider, migrate the schema, or open a database connection by itself. Do not hard-code or log connection strings.

## Diagnostic event contract

Successful saves emit `BackendSafetyKit.EntityFrameworkCore.SaveChanges.Completed` with the context type, elapsed milliseconds, and affected-entry count. Failed saves emit `BackendSafetyKit.EntityFrameworkCore.SaveChanges.Failed` with the context type, elapsed time, and exception type.

The event does not include entity values, SQL text, command parameters, connection strings, or exception messages. Diagnostic sinks should preserve that boundary; do not attach sensitive values to event properties. Sink failures are isolated from the persistence operation so diagnostics do not turn a successful database write into an application failure.

## Scope and limitations

- This package targets EF Core 10 and follows the repository's .NET 10 framework policy.
- It observes `SaveChanges` and `SaveChangesAsync`; it does not observe arbitrary queries unless the application adds another interceptor.
- It does not implement retries, transaction management, auditing of entity changes, data masking before persistence, migrations, or a database health check.
- Applications remain responsible for database credentials, provider configuration, access control, and diagnostics retention.

The SaveChanges interceptor tests use an in-memory SQLite relational database. They cover synchronous and asynchronous success/failure events and verify that entity values, SQL, provider error messages, and connection strings are not included in diagnostics. They also verify that a throwing diagnostic sink cannot change the underlying save outcome. No external database server is required. Run the full solution test suite and package validator before publishing.
