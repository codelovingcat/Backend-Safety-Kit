# Backend Safety Kit

Production-ready safety and diagnostics infrastructure for ASP.NET Core applications.

Backend Safety Kit aims to remove repetitive API infrastructure work by providing secure, predictable, configurable defaults through a small NuGet package.

## Goals

The library is designed to make common backend concerns easy to enable without forcing a specific architecture, database, logging provider, or business domain.

Initial capabilities:

- Global exception handling
- Standardized ProblemDetails responses
- Correlation ID and request ID propagation
- Structured request logging
- Sensitive-data masking
- Request timing and diagnostics
- Secure-by-default ASP.NET Core configuration
- Health and diagnostics integration

Future capabilities may include audit hooks, observability integrations, extensibility points, and optional infrastructure providers.

## Design Principles

- Secure by default
- Minimal configuration
- Minimal dependencies
- No telemetry
- No external service required for core functionality
- ASP.NET Core integration isolated from framework-independent abstractions
- Small and predictable public API
- Strong test coverage
- Backward-compatible evolution

## Planned Package Structure

```text
src/
  BackendSafetyKit/
  BackendSafetyKit.AspNetCore/

tests/
  BackendSafetyKit.Tests/
  BackendSafetyKit.AspNetCore.Tests/

samples/
  BackendSafetyKit.SampleApi/
```

Optional infrastructure integrations will be introduced as separate packages when they become necessary.

## Example Direction

The intended developer experience is deliberately small:

```csharp
builder.Services.AddBackendSafety();

var app = builder.Build();

app.UseBackendSafety();
```

The exact public API will be finalized during the initial architecture issue.

## Status

Early development.

The first milestone focuses on the core ASP.NET Core pipeline, predictable error handling, request correlation, safe logging, diagnostics, testing, and documentation.

## License

License will be finalized before the first NuGet release.
