# MVC Controller Integration Guide

Backend Safety Kit wraps the ASP.NET Core request pipeline; it does not replace MVC model validation, routing, authentication, authorization, or controller filters.

## 1. Install and register

```bash
dotnet add package BackendSafetyKit.AspNetCore
```

```csharp
using BackendSafetyKit.AspNetCore.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddBackendSafety();
builder.Services.AddBackendSafetyHealthChecks();

var app = builder.Build();

app.UseBackendSafety();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
```

Place `UseBackendSafety()` after `UseForwardedHeaders()` when the application is behind a trusted reverse proxy. Preserve the host application's existing authentication, authorization, CORS, and routing order; Backend Safety Kit does not enable or replace those policies.

## 2. Controller example

```csharp
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    [HttpGet("{id:int}")]
    public IActionResult Get(int id)
    {
        if (id <= 0)
        {
            return BadRequest(new { error = "invalid_order_id" });
        }

        // Load the real resource using the application's normal data layer.
        return NotFound();
    }
}
```

MVC's explicit results and validation responses remain the application's responsibility. Unhandled exceptions reaching the configured exception boundary become safe ProblemDetails responses. Avoid throwing exceptions for ordinary validation paths when a clear HTTP result is more appropriate.

## 3. Map domain exceptions

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.ExceptionHandling.Map<KeyNotFoundException>(404);
    options.ProblemDetails
        .MapErrorCode<KeyNotFoundException>("order_not_found")
        .MapTitle<KeyNotFoundException>("Order was not found.");
});
```

Error codes and titles are stable contracts for clients. Do not place sensitive details in them. Production responses do not include exception messages by default.

## Diagnostics and security

Request logs intentionally exclude bodies and query strings. Headers are captured only through explicit allowlists, and deny rules take precedence. Correlation IDs are treated as untrusted input and regenerated when invalid. For deployment concerns, read [Production Deployment](production-deployment.md); for known failure modes, see [Troubleshooting](troubleshooting.md).
