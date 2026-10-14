# Minimal API Integration Guide

This guide shows the smallest setup for a .NET 10 minimal API using Backend Safety Kit.

## 1. Install the integration package

```bash
dotnet add package BackendSafetyKit.AspNetCore
```

Keep the core primitives package and OpenTelemetry integration optional according to your needs. The ASP.NET Core package is the entry point for middleware and health-check integration.

## 2. Register and add the middleware

```csharp
using BackendSafetyKit.AspNetCore.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBackendSafety(options =>
{
    options.RequestTiming.EnableSlowRequestLogging = true;
    options.RequestTiming.SlowRequestThreshold = TimeSpan.FromSeconds(1);
});

builder.Services.AddBackendSafetyHealthChecks();

var app = builder.Build();

app.UseBackendSafety();

app.MapGet("/", () => Results.Ok(new { status = "ok" }));
app.MapHealthChecks("/health");

app.Run();
```

Register services before building the application and place `UseBackendSafety()` after trusted forwarded-header processing, if your deployment uses a reverse proxy. Backend Safety Kit handles enabled correlation, logging, tracing, timing, HTTP security, and exception middleware in its own documented order.

## 3. Return safe errors

Exceptions are translated to standardized ProblemDetails. Responses do not expose exception details by default. Map known application exceptions intentionally:

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.ExceptionHandling.Map<KeyNotFoundException>(404);
    options.ProblemDetails
        .MapErrorCode<KeyNotFoundException>("resource_not_found")
        .MapTitle<KeyNotFoundException>("The requested resource was not found.");
});
```

Do not return raw exception messages, credentials, or database details in a custom ProblemDetails callback.

## 4. Health and readiness

`AddBackendSafetyHealthChecks()` registers a configuration diagnostic check. Expose it through the standard ASP.NET Core Health Checks APIs, and keep liveness/readiness policy specific to your host. The built-in check does not test Redis, a database, DNS, or other external dependencies.

## 5. Custom configuration

Use the [configuration reference](configuration-reference.md) and [configuration recipes](configuration-recipes.md) for request logging allowlists, masking rules, HTTPS/HSTS, request-size limits, and tracing. Configuration is validated at startup, so invalid values should fail deployment rather than remain latent in production.

## Verification

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

For controller-based apps, continue with [Controller Integration Guide](controller-guide.md).
