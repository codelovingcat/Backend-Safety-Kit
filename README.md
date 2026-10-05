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

The kit provides a non-mutating masking component through `ISensitiveDataMasker`. It can redact sensitive scalar values and copy nested dictionaries, collections, and public object properties into a safe representation.

The default rules cover common sensitive names such as:

- Passwords
- API keys
- Access tokens
- Refresh tokens
- Client secrets
- Authorization and proxy-authorization values
- Cookies and set-cookie values
- Common authentication token headers

Matching is case-insensitive and exact by default. Applications can add contains, starts-with, or ends-with rules for domain-specific names:

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.SensitiveDataMasking.AddRule("CustomerSecret");

    options.SensitiveDataMasking.AddRule(
        "credential",
        SensitiveDataMatchMode.Contains);
});
```

Full redaction is the default:

```text
customer-secret-42 -> [REDACTED]
```

Partial masking is disabled by default and requires explicit opt-in on both the options and the individual rule:

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.SensitiveDataMasking.AllowPartialMasking = true;

    options.SensitiveDataMasking.AddRule(
        "accessToken",
        SensitiveDataMatchMode.Exact,
        SensitiveDataMaskMode.Partial,
        visiblePrefixLength: 2,
        visibleSuffixLength: 4);
});
```

The masker never changes the original application object. When structured data is masked, a new dictionary/list representation is returned.

The request logging middleware uses the masker for allowlisted headers. This provides a second protection layer when an application explicitly removes a header from the logging denylist.

### 6. Request Timing and Diagnostics

Request timing is measured with a monotonic clock so elapsed duration is not affected by wall-clock changes.

The timing middleware provides:

- Request duration
- Configurable slow-request threshold
- Optional slow-request logging at a configurable log level
- Correlation/request identifier
- A completion callback for applications that want to export diagnostics elsewhere

Slow-request logging is disabled by default. A hook can be registered without introducing a metrics provider:

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.RequestTiming.SlowRequestThreshold =
        TimeSpan.FromMilliseconds(750);

    options.RequestTiming.EnableSlowRequestLogging = true;
    options.RequestTiming.SlowRequestLogLevel =
        RequestTimingLogLevel.Warning;

    options.RequestTiming.OnCompleted = timing =>
    {
        // Export timing.Duration to your own metrics system.
    };
});
```

The completion callback receives only safe diagnostic metadata: method, path, status code, correlation ID, duration, and slow-request state. Request bodies and query strings are not included.

### 7. Secure-by-Default HTTP Configuration

The kit applies a deliberately small set of HTTP hardening defaults through `HttpSecurityOptions`.

The default behavior is intentionally conservative:

- `X-Content-Type-Options: nosniff` is enabled.
- Request body size is not changed unless `MaxRequestBodySize` is explicitly configured.
- `X-Frame-Options`, `Referrer-Policy`, and `Content-Security-Policy` are disabled by default because they can affect legitimate browser-facing applications.
- Existing response headers are preserved and are never overwritten by the package.

Example:

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.HttpSecurity.EnableFrameOptionsHeader = true;
    options.HttpSecurity.FrameOptions = "SAMEORIGIN";

    options.HttpSecurity.EnableReferrerPolicyHeader = true;
    options.HttpSecurity.ReferrerPolicy = "strict-origin-when-cross-origin";

    options.HttpSecurity.EnableContentSecurityPolicyHeader = true;
    options.HttpSecurity.ContentSecurityPolicy =
        "default-src 'self'";

    options.HttpSecurity.MaxRequestBodySize = 10 * 1024 * 1024;
});
```

A configured request body limit is applied through ASP.NET Core's `IHttpMaxRequestBodySizeFeature`. If the active server does not expose that feature or the server has already made the limit read-only, configuration fails instead of silently doing nothing.

The package does not automatically configure HSTS, HTTPS redirection, authentication, authorization, CORS, cookies, or reverse-proxy trust. Those behaviors depend on the host application's deployment and security architecture.

Existing ProblemDetails behavior remains safe by default: exception details are not returned unless explicitly enabled for development, and development-only details are still suppressed outside the Development environment.

### 8. Health and Diagnostics

The kit can integrate with the standard ASP.NET Core Health Checks infrastructure without adding external services.

Register the package diagnostic check with:

```csharp
builder.Services.AddBackendSafety();
builder.Services.AddBackendSafetyHealthChecks();
```

Then expose it through the standard ASP.NET Core endpoint mapping:

```csharp
app.MapHealthChecks("/health");
```

The built-in check validates the package configuration without returning configuration values or exception details.

A healthy check reports:

```text
Backend Safety Kit configuration is valid.
```

Invalid configuration reports an unhealthy result:

```text
Backend Safety Kit configuration is invalid.
```

The underlying validation exception is written to structured logging (event ID `4001`) for operators, but it is not included in the health response.

Readiness or liveness filtering can use standard Health Checks tags:

```csharp
builder.Services.AddBackendSafetyHealthChecks(
    "backend-safety-readiness",
    "ready");
```

The package does not create authentication, authorization, CORS, or external dependency health checks. Applications can compose the package check with their own checks using the standard `AddHealthChecks` and `MapHealthChecks` APIs.

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
    Request Timing / Diagnostics
           |
           v
    Secure HTTP Defaults
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

The structured logging middleware wraps request timing so the completion log uses the same monotonic duration measurement produced by the timing middleware. The timing middleware wraps the exception boundary so handled failures receive their final HTTP status code.

## Architecture

The project deliberately separates framework-independent code from ASP.NET Core integration.

### BackendSafetyKit

Contains reusable, framework-independent primitives and contracts.

Examples:

- Options contracts
- Sensitive-data masking rules
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
