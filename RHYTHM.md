# RHYTHM

## Backend Safety Kit — Project Working Agreement

This file is the project's long-lived technical and product reference.

It defines how Backend Safety Kit should behave, how it should be designed, how changes should be implemented, and what "done" means.

---

## 1. Project Mission

Backend Safety Kit is a NuGet library for ASP.NET Core applications that removes repetitive production-safety and diagnostics plumbing.

The intended developer experience is:

~~~bash
dotnet add package BackendSafetyKit.AspNetCore
~~~

followed by a very small setup surface:

~~~csharp
builder.Services.AddBackendSafety();

var app = builder.Build();

app.UseBackendSafety();
~~~

The library should make a new or existing API safer and easier to diagnose without forcing the application to adopt a particular business architecture, database, logging provider, hosting platform, or external SaaS.

The package is infrastructure. It must stay out of business logic.

---

## 2. Initial Product Scope

The first milestone focuses on common infrastructure that appears in many ASP.NET Core backends:

1. Global exception handling
2. Standardized ProblemDetails responses
3. Correlation ID / Request ID propagation
4. Structured request logging
5. Sensitive-data masking
6. Request timing and lightweight diagnostics
7. Secure-by-default HTTP/API hardening
8. Health and diagnostics integration
9. Strong automated tests
10. A runnable sample application

These features must work together as one coherent request pipeline.

Future ideas such as audit trails, distributed caching, queues, outbox processing, webhook infrastructure, or provider-specific integrations are out of scope until intentionally added to the roadmap.

---

## 3. Target Runtime and Compatibility

### Primary target

- .NET 10
- ASP.NET Core 10

.NET 10 is the current LTS release and is supported through November 14, 2028.

### Compatibility policy

- Use the latest patch version of the selected .NET SDK in CI.
- Avoid obsolete APIs when an appropriate supported alternative exists.
- Do not add compatibility shims for unsupported runtimes unless explicitly planned.
- Consider multi-targeting only when there is a concrete user benefit and the maintenance cost is justified.

---

## 4. Package Architecture

### BackendSafetyKit

Framework-independent contracts, options, primitives, and reusable logic.

It should contain things that do not need ASP.NET Core HTTP types.

Examples:

- configuration contracts
- masking rules
- diagnostic abstractions
- shared result/value objects
- constants
- policies
- reusable utilities

### BackendSafetyKit.AspNetCore

ASP.NET Core integration.

Examples:

- dependency-injection extensions
- middleware
- HTTP context access
- ProblemDetails integration
- request/response diagnostics
- ASP.NET Core logging integration

### Optional future packages

Provider-specific integrations belong in separate packages.

Examples:

~~~text
BackendSafetyKit.OpenTelemetry
BackendSafetyKit.Redis
BackendSafetyKit.EntityFrameworkCore
BackendSafetyKit.SomeProvider
~~~

The core package must never become a dependency magnet.

---

## 5. Dependency Philosophy

The package should be useful immediately after installation.

Rules:

- Prefer BCL and Microsoft.Extensions abstractions.
- Keep third-party dependencies to an absolute minimum.
- Never introduce a dependency solely for syntactic convenience.
- Never require Redis, PostgreSQL, SQL Server, a logging provider, or an external SaaS for core functionality.
- Optional integrations must be separate packages.
- The library must not phone home.
- No telemetry, tracking, analytics, or hidden network calls.

A consumer should be able to run the initial feature set in a local ASP.NET Core application with no infrastructure beyond the app itself.

---

## 6. Security Is a Default, Not an Option

The library exists to make backend applications safer.

Security rules:

- Never expose stack traces in production by default.
- Never log passwords, tokens, API keys, authorization credentials, or cookies by default.
- Never log request bodies by default.
- Never trust arbitrary incoming identifiers without validation.
- Apply sensible length limits to values accepted from headers or configuration.
- Avoid leaking implementation details through error messages.
- Never silently weaken authentication, authorization, CORS, antiforgery, or proxy configuration.
- Security behavior must be explicit, documented, and independently configurable.
- Any feature that can leak information must have regression tests.

When convenience and security conflict, security wins unless the consumer explicitly opts into the less-safe behavior.

---

## 7. Request Pipeline

The initial middleware pipeline should conceptually behave like this:

~~~text
Incoming Request
       |
       v
Correlation / Request ID
       |
       v
Request Diagnostics / Timing
       |
       v
Exception Boundary
       |
       v
Application Pipeline
       |
       +---- failure ----> ProblemDetails
       |
       v
Response Diagnostics
       |
       v
Structured Logging
       |
       v
Outgoing Response
~~~

The exact registration order can evolve as implementation details become clearer, but the final behavior must preserve:

- correlation identifiers are available to downstream components
- exceptions are caught centrally
- safe error responses are produced
- timing survives both success and failure
- completion logging happens consistently
- sensitive values are masked before they reach log output

Every middleware should have one clear responsibility.

---

## 8. Public API Design

The library is intended for developers who want a small amount of setup.

Prefer:

~~~csharp
builder.Services.AddBackendSafety(options =>
{
    // only the settings the application actually needs
});
~~~

over large, deeply nested configuration structures.

Public APIs should be:

- small
- discoverable
- strongly typed
- nullable-aware
- documented
- backwards-compatible where practical

Avoid:

- magic strings when a typed alternative is reasonable
- global mutable state
- static service locators
- hidden singleton state
- reflection when a simpler mechanism exists
- APIs that require consumers to understand internal middleware details

Extension methods should follow normal ASP.NET Core conventions.

---

## 9. Configuration Philosophy

Defaults should make the common case safe.

Configuration requirements:

- Every option has a documented default.
- Every security-sensitive option is named clearly.
- Invalid configuration should fail fast where possible.
- Options should be validated during startup when validation can be deterministic.
- Enabling a feature must not silently enable unrelated features.
- Configuration objects must not contain secrets unnecessarily.
- Environment-specific behavior should be explicit.

The library should work with the standard .NET options pattern.

---

## 10. Error Handling

Error handling is a central feature.

The external HTTP contract should be predictable.

Default errors should use ProblemDetails-compatible output and include useful diagnostic correlation information without exposing internal implementation details.

Conceptually:

~~~json
{
  "type": "...",
  "title": "...",
  "status": 500,
  "detail": "...",
  "instance": "...",
  "traceId": "..."
}
~~~

Rules:

- Production responses must be safe.
- Development diagnostics may be richer when explicitly enabled.
- Application-specific exceptions may be mapped to intentional status codes.
- Consumers must be able to customize mappings.
- The package must not force a proprietary exception hierarchy.

---

## 11. Correlation and Request IDs

Every handled request should be traceable.

Behavior:

1. Read the configured incoming identifier when present.
2. Validate it.
3. Generate a new identifier when missing or invalid.
4. Make it available to application code and logging.
5. Return it in the configured response header.
6. Use the same identifier in error responses.

Incoming values are untrusted input.

The library should make it easy to correlate one request across:

~~~text
HTTP request
    |
    +--> logs
    |
    +--> exception
    |
    +--> ProblemDetails
    |
    +--> downstream work
~~~

---

## 12. Logging Rules

Use Microsoft.Extensions.Logging abstractions.

The package must not require Serilog, NLog, or another logging implementation.

Structured logging should prefer named properties over interpolated text.

Good:

~~~csharp
logger.LogInformation(
    "Request completed with status code {StatusCode} in {DurationMs} ms",
    statusCode,
    durationMs);
~~~

Avoid logging:

- request/response bodies by default
- Authorization headers
- cookies
- secrets
- complete query strings when they may contain credentials or personal information

When headers are logged, use explicit allowlists or safe defaults rather than blindly dumping every header.

---

## 13. Sensitive Data Masking

Sensitive-data masking is a first-class component, not an afterthought.

The masking system should support:

- default sensitive field names
- case-insensitive matching
- custom field names
- full redaction
- optional partial masking where explicitly configured
- nested structured data when supported

The implementation must not mutate the consumer's original object.

The important invariant is:

> A secret value must not accidentally survive into a log line just because it appeared under a slightly different casing or nested structure.

Every new sensitive-data path needs a regression test.

---

## 14. Diagnostics and Timing

Timing should be lightweight.

Measure elapsed time with a monotonic timing mechanism suitable for duration calculations.

Support:

- request duration
- configurable slow-request threshold
- optional slow-request warning logging
- correlation identifiers
- diagnostic hooks

Do not build a metrics platform into the package.

The package may expose extension points so OpenTelemetry or another system can consume diagnostics without making that provider a required dependency.

---

## 15. Secure HTTP Defaults

Security hardening should be conservative.

The package may provide individually configurable defaults for appropriate HTTP/API hardening.

It must NOT silently create or change:

- authentication schemes
- authorization policies
- CORS policies
- reverse-proxy trust
- CSRF/antiforgery behavior
- cookie authentication behavior

Any header or behavior we add must be justified by a concrete security benefit and tested for compatibility.

---

## 16. Health and Diagnostics

Health integration must remain standard-ASP.NET-Core friendly.

Rules:

- Do not expose secrets through health endpoints.
- Do not return raw exception messages that reveal internal systems.
- Keep health checks independent of the package's own internal implementation.
- Optional integrations should be modular.
- Diagnostic failures should be actionable for developers.

---

## 17. Testing Strategy

Tests are not a final step. They are part of the feature.

### Unit tests

Use unit tests for:

- masking rules
- identifier validation/generation
- exception mapping
- options validation
- reusable framework-independent logic

### Integration tests

Use real ASP.NET Core test hosts for:

- middleware ordering
- HTTP status behavior
- ProblemDetails payloads
- headers
- logging behavior
- exception handling
- timing
- health/diagnostics behavior

### Security regression tests

Every bug involving information disclosure, unsafe defaults, or secret handling must become a permanent regression test.

Tests must be:

- deterministic
- isolated
- independent of external infrastructure
- readable enough to explain the expected security behavior

---

## 18. Sample Application

The sample application is part of the product documentation.

It should demonstrate the smallest useful setup:

~~~csharp
builder.Services.AddBackendSafety();

var app = builder.Build();

app.UseBackendSafety();
~~~

The sample should include deliberately useful endpoints such as:

- successful request
- endpoint that throws an exception
- endpoint that demonstrates correlation ID
- endpoint whose request is considered slow
- diagnostics/health endpoint

The sample must remain simple enough for someone discovering the NuGet package for the first time.

---

## 19. NuGet Package Quality

Before a stable package release:

- package IDs must be stable
- package metadata must be complete
- README must render correctly on NuGet
- XML documentation should be enabled for public APIs
- symbols/source strategy should be deliberate
- semantic versioning must be followed
- release notes must explain user-visible changes
- package dependencies must be reviewed
- security scanning must run in CI
- package contents must be checked before publishing

Do not publish a package simply because it builds.

The release must be understandable and trustworthy to a developer evaluating it among many NuGet alternatives.

---

## 20. CI/CD

CI should validate at minimum:

~~~text
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
~~~

Future CI may also include:

- formatting checks
- static analysis
- CodeQL
- dependency/security scanning
- package validation

A green build means the repository is technically healthy, not that the feature is automatically correct.

---

## 21. Versioning

Follow Semantic Versioning:

~~~text
MAJOR.MINOR.PATCH
~~~

Until the first stable release, the package may remain in the 0.x range.

Rules:

- Breaking public API changes require deliberate review.
- Bug fixes should not unnecessarily break consumers.
- New functionality should prefer additive APIs.
- Deprecations need migration guidance.

---

## 22. Performance

The package is middleware infrastructure and therefore sits on the hot path.

Performance rules:

- Avoid unnecessary allocations.
- Avoid reflection on every request.
- Avoid serializing data that is not needed.
- Avoid reading request/response bodies unless explicitly enabled.
- Avoid expensive work for disabled features.
- Do not make network calls in the core request pipeline.
- Benchmark expensive code when a feature adds measurable per-request overhead.

Safety must not become an excuse for careless overhead.

---

## 23. Observability Without Vendor Lock-In

The package should expose useful diagnostics through standard abstractions.

Prefer:

~~~text
Microsoft.Extensions.Logging
Microsoft.Extensions.Options
Microsoft.Extensions.Diagnostics.HealthChecks
Activity / System.Diagnostics
~~~

over provider-specific APIs.

OpenTelemetry support may be added later as an optional package or integration.

---

## 24. Documentation

Every major feature should answer:

1. What problem does it solve?
2. What happens by default?
3. How do I enable it?
4. How do I customize it?
5. What security implications exist?
6. How do I disable it?
7. How is it tested?

Documentation examples should be copy-paste friendly.

Do not document hypothetical APIs that do not exist.

---

## 25. Development Workflow

Issues define the development order.

Rules:

- Implement issues in dependency order.
- Do not skip foundational issues unless explicitly justified.
- Each issue should have a focused change.
- Create a feature branch for implementation work.
- Open a pull request for completed work.
- Run tests before presenting the PR for review.
- Do not merge PRs automatically.
- The repository maintainer performs the final merge after review.
- Keep commits focused and descriptive.
- Avoid unrelated refactoring inside feature issues.

When an implementation exposes a better architectural direction, update the relevant documentation and issue before expanding scope.

---

## 26. Definition of Done

A feature is not done when the code compiles.

A feature is done when:

- implementation is complete
- public API is intentional
- tests cover expected and failure behavior
- security implications are considered
- documentation is updated
- sample usage is updated when relevant
- CI is green
- no unnecessary dependency was introduced
- the change is isolated to its issue
- a reviewer can understand why the implementation is shaped this way

---

## 27. Initial Roadmap

The initial roadmap follows this dependency order:

~~~text
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
~~~

Later milestones can build on this foundation with additional production infrastructure.

---

## 28. Final Product Principle

Backend Safety Kit should feel like a small addition that quietly removes a large amount of backend boilerplate.

A developer should be able to install it quickly, understand what it changes, trust its defaults, override what they need, and remove it without rewriting their application architecture.

The ideal reaction is:

> "Why was I implementing all of this manually in every API?"

That is the standard this project should aim for.
