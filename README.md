# Backend Safety Kit

Production-ready safety and diagnostics infrastructure for ASP.NET Core applications.

Backend Safety Kit is designed to remove repetitive backend infrastructure work by providing secure, predictable, configurable defaults through a small NuGet package.

## What problem does it solve?

A typical ASP.NET Core API repeatedly needs the same infrastructure:

- Global exception handling
- Consistent error responses
- Correlation/request IDs
- Structured request logging
- Sensitive-data protection
- Request timing and diagnostics
- Safe HTTP defaults
- Health and diagnostics integration

These concerns are important, but they should not force every development team to implement the same middleware, options, validation, logging rules, and tests from scratch.

Backend Safety Kit packages those concerns into a reusable layer while staying out of business logic.

## Intended Developer Experience

The common case should be close to this:

```bash
dotnet add package BackendSafetyKit.AspNetCore
```

Then:

```csharp
builder.Services.AddBackendSafety();

var app = builder.Build();

app.UseBackendSafety();
```

The package should work safely with minimal configuration. Advanced applications can configure individual features when needed.

## Initial Feature Set

### 1. Global Exception Handling

Unhandled exceptions are captured at one consistent boundary.

Goals:

- Return safe HTTP errors.
- Do not expose stack traces in production by default.
- Preserve exceptions for diagnostics.
- Support custom exception-to-status-code mappings.
- Work with controllers and minimal APIs.

### 2. Standardized ProblemDetails

Handled API exceptions are returned as standards-aligned ProblemDetails responses using the `application/problem+json` content type.

The default response includes:

- `type`
- `title`
- `status`
- `instance` when enabled
- `traceId` when enabled

Exception messages are not returned by default.

Example:

```json
{
  "type": "about:blank",
  "title": "An unexpected error occurred.",
  "status": 500,
  "instance": "/api/orders/42",
  "traceId": "00-..."
}
```

Applications can customize safe details, titles, application-specific error codes, and arbitrary ProblemDetails extensions:

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.ExceptionHandling.Map<KeyNotFoundException>(404);

    options.ProblemDetails
        .MapErrorCode<KeyNotFoundException>("order_not_found")
        .MapTitle<KeyNotFoundException>("Order was not found.");

    options.ProblemDetails.Customize = context =>
    {
        context.Detail = "The requested order does not exist.";
        context.Extensions["errors"] = new { orderId = "order-42" };
    };
});
```

Exception details can be enabled explicitly for development environments with `IncludeExceptionDetailInDevelopment`. Stack traces are never added automatically.

### 3. Correlation ID / Request ID

Every request receives one effective identifier.

By default the middleware uses this precedence:

```text
X-Correlation-ID
      |
      +--> valid --> use it
      |
      +--> missing/invalid
                 |
                 v
          X-Request-ID
                 |
                 +--> valid --> use it
                 |
                 +--> missing/invalid --> generate ID
```

The effective identifier is:

- Stored in the current request through ICorrelationIdAccessor.
- Assigned to ASP.NET Core's HttpContext.TraceIdentifier.
- Added to the standard logging scope for downstream logs.
- Returned through X-Correlation-ID by default.
- Configurable without changing application code.

Incoming values are untrusted input. IDs longer than the configured limit, IDs containing unsupported characters, and multi-valued ID headers are ignored and safely replaced or resolved through the fallback header.

Example:

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.Correlation.MaxLength = 128;
    options.Correlation.IncludeResponseHeader = true;
});

var app = builder.Build();

app.UseBackendSafety();
```

Custom header names and ID generation are supported through CorrelationIdOptions.

### 4. Structured Request Logging

The library writes one structured completion log for each request through standard `Microsoft.Extensions.Logging` abstractions.

The default completion event includes:

- HTTP method and path
- HTTP status code
- elapsed duration measured with a monotonic clock
- correlation/request ID
- request host
- failure indicator and unhandled exception type when one escapes the pipeline
- selected request/response headers only when explicitly allowlisted

Security defaults are intentionally restrictive:

- Request bodies are never read or logged.
- Query strings are never logged.
- Authorization, Cookie, Proxy-Authorization, and Set-Cookie are denied by default.
- Header logging is disabled until a header is explicitly added to an allowlist.
- Logged metadata values are length-limited.

Header allowlists and denylists can be configured when an application has a safe diagnostic need:

    builder.Services.AddBackendSafety(options =>
    {
        options.RequestLogging.AllowedRequestHeaders.Add("X-Tenant");
        options.RequestLogging.AllowedResponseHeaders.Add("Content-Type");
        options.RequestLogging.MaxMetadataValueLength = 256;
    });

A denylist entry takes precedence over an allowlist entry. Remove a default denylist entry only when the application explicitly accepts the data-handling risk.

### 5. Sensitive Data Masking

Sensitive values should be protected before they can reach logs.

Default protection should cover common values such as:

- Passwords
- API keys
- Access tokens
- Refresh tokens
- Authorization headers
- Cookies

Consumers must be able to add custom sensitive field names.

The masking system must not mutate the application's original objects.

### 6. Request Timing and Diagnostics

The package measures request duration using an appropriate monotonic elapsed-time mechanism.

It should support:

- Request duration
- Configurable slow-request threshold
- Optional slow-request logging
- Correlation identifiers
- Diagnostic extension points

The package should remain lightweight and should not attempt to become a complete metrics platform.

### 7. Secure-by-Default HTTP Configuration

The library may provide carefully selected HTTP hardening defaults.

Each behavior must be independently configurable and documented.

The package must not silently create or replace:

- Authentication schemes
- Authorization policies
- CORS policies
- Reverse-proxy trust configuration
- Cookie authentication configuration

Security features should improve defaults without taking ownership of the application's security architecture.

### 8. Health and Diagnostics

Integration should remain compatible with standard ASP.NET Core diagnostics and health infrastructure.

Health endpoints must not expose secrets or internal exception details.

Optional integrations should not require external infrastructure.

## Request Pipeline

The implemented request pipeline is:

    Incoming Request
           |
           v
    Correlation / Request ID
           |
           v
    Structured Request Logging
           |
           v
    Exception Boundary
           |
           v
    Application Pipeline
           |
           +---- exception ----> ProblemDetails
           |
           v
    Completion Log
           |
           v
    Outgoing Response

The request logging middleware wraps the exception boundary so handled failures are logged with their final HTTP status code.

## Architecture

The project deliberately separates framework-independent code from ASP.NET Core integration.

### BackendSafetyKit

Contains reusable, framework-independent primitives and contracts.

Examples:

- Options contracts
- Masking rules
- Diagnostic abstractions
- Shared policies
- Reusable utilities

### BackendSafetyKit.AspNetCore

Contains ASP.NET Core-specific behavior.

Examples:

- Dependency injection extensions
- Middleware
- HTTP context integration
- ProblemDetails integration
- Request/response diagnostics

### Future Optional Packages

Provider-specific integrations should be separate packages.

Possible examples:

```text
BackendSafetyKit.OpenTelemetry
BackendSafetyKit.Redis
BackendSafetyKit.EntityFrameworkCore
```

The core packages must not become dependency magnets.

## Dependency Philosophy

Backend Safety Kit should be useful immediately after installation.

Rules:

- Prefer BCL and Microsoft.Extensions abstractions.
- Keep third-party dependencies minimal.
- Do not require Redis, PostgreSQL, SQL Server, or an external SaaS for core functionality.
- Optional infrastructure integrations belong in separate packages.
- No telemetry.
- No hidden network calls.
- No tracking or analytics.

## Security Principles

Security is a primary responsibility of the library.

The project follows these rules:

- Never expose stack traces in production by default.
- Never log secrets by default.
- Never log request bodies by default.
- Treat incoming IDs and headers as untrusted input.
- Apply sensible size limits.
- Do not leak internal implementation details through errors.
- Do not silently weaken application authentication or authorization.
- Every security-sensitive behavior must have regression tests.

When convenience and security conflict, the secure behavior wins unless the consumer explicitly opts into a different behavior.

## Public API Design

Public APIs should be:

- Small
- Discoverable
- Strongly typed
- Nullable-aware
- Documented
- Predictable

Prefer simple registration and focused options:

```csharp
builder.Services.AddBackendSafety(options =>
{
    // feature-specific settings
});
```

Avoid:

- Global mutable state
- Static service locators
- Hidden singleton state
- Reflection on hot paths
- Magic configuration where typed options are practical
- APIs that expose internal middleware implementation details

## Performance

Backend Safety Kit runs on the request path, so performance matters.

Rules:

- Avoid unnecessary allocations.
- Avoid reflection on every request.
- Do not serialize data that is not needed.
- Do not read request/response bodies unless explicitly enabled.
- Avoid expensive work for disabled features.
- Never make network calls in the core request pipeline.
- Benchmark expensive code when a feature introduces measurable overhead.

Safety should not come at the cost of careless runtime overhead.

## Testing Strategy

Tests are part of every feature.

### Unit Tests

Use unit tests for framework-independent behavior such as:

- Masking
- Identifier validation
- Exception mappings
- Options validation
- Reusable utilities

### Integration Tests

Use real ASP.NET Core test hosts for:

- Middleware ordering
- HTTP responses
- ProblemDetails
- Headers
- Exception handling
- Logging
- Timing
- Health/diagnostics

### Security Regression Tests

Every security bug or information-disclosure bug becomes a permanent regression test.

Tests should be deterministic, isolated, readable, and independent of external infrastructure.

## Sample Application

The sample API is part of the product documentation.

It should demonstrate:

- Minimal installation
- Minimal registration
- A successful endpoint
- An endpoint that throws
- Correlation ID behavior
- Slow-request diagnostics
- Health/diagnostics behavior

The sample should remain simple enough for a developer discovering the package for the first time.

## CI

CI validates the project in this order:

```text
restore
  |
  v
build
  |
  v
unit tests
  |
  v
integration tests
  |
  v
pack
```

Future CI may add:

- Formatting
- Static analysis
- CodeQL
- Dependency/security scanning
- Package validation

## NuGet Quality

Before a stable release:

- Package metadata must be complete.
- Package README must render correctly.
- Public APIs should have XML documentation.
- Semantic versioning must be followed.
- Package dependencies must be reviewed.
- Security scanning must run in CI.
- Package contents must be checked before publishing.
- Release notes must explain user-visible changes.

The package must earn trust rather than simply compile.

## Versioning

Use Semantic Versioning:

```text
MAJOR.MINOR.PATCH
```

Until the first stable release, the package may remain in the 0.x range.

Breaking public API changes require deliberate review.

## Development Workflow

Issues define the development order.

For each implementation issue:

1. Create a focused feature branch.
2. Implement only the issue's scope.
3. Add or update tests.
4. Update documentation when behavior changes.
5. Run the relevant test suite.
6. Open a pull request.
7. Review the pull request.
8. The repository maintainer performs the final merge.

PRs should not be merged automatically.

Avoid unrelated refactoring inside feature issues.

## Definition of Done

A feature is done when:

- Implementation is complete.
- Public API is intentional.
- Expected and failure behavior are tested.
- Security implications are considered.
- Documentation is updated.
- Sample usage is updated when relevant.
- CI is green.
- No unnecessary dependency was introduced.
- The change stays within the issue scope.

## Initial Roadmap

The initial implementation order is intentionally dependency-driven:

```text
#1  Solution and package foundation
 |
 v
#2  Global exception handling
 |
 v
#3  ProblemDetails
 |
 v
#4  Correlation / Request IDs
 |
 v
#5  Structured request logging
 |
 v
#6  Sensitive-data masking
 |
 v
#7  Request timing and diagnostics
 |
 v
#8  Secure HTTP defaults
 |
 v
#9  Health and diagnostics integration
 |
 v
#10 Integration tests + sample application
```

## Project Standard

Backend Safety Kit should feel like a small package that quietly removes a large amount of repetitive backend work.

A developer should be able to:

1. Install it quickly.
2. Understand exactly what it changes.
3. Trust the defaults.
4. Configure only what is necessary.
5. Integrate it without restructuring the application.
6. Remove it without rewriting business logic.

The ideal reaction is:

> "Why was I implementing all of this manually in every API?"

## License

The license will be finalized before the first stable NuGet release.
