# Public API Review

This document records the intentional public surface of Backend Safety Kit and the review rules used when changing it. It is a human-reviewed inventory, not a machine-generated API diff. Automated compatibility enforcement is tracked separately.

## Package boundaries

| Package | Public purpose | Intended entry points |
|---|---|---|
| `BackendSafetyKit` | Framework-light options, masking, and diagnostic primitives | `BackendSafetyOptions`, `ISensitiveDataMasker`, `SensitiveDataMasker`, `BackendSafetyDiagnostics`, `BackendSafetyMetrics` |
| `BackendSafetyKit.AspNetCore` | ASP.NET Core registration and request-pipeline integration | `AddBackendSafety(...)`, `UseBackendSafety()`, `AddBackendSafetyHealthChecks(...)`, `ICorrelationIdAccessor` |
| `BackendSafetyKit.OpenTelemetry` | Optional registration of the existing activity source and meter | `AddBackendSafetyOpenTelemetry(...)`, `BackendSafetyOpenTelemetryOptions` |

The core and ASP.NET Core projects should not gain an exporter or provider-specific runtime dependency as a side effect of an observability feature. Provider integrations belong in the optional package.

## Core public contracts

### Root options

`BackendSafetyOptions` is the root configuration object. Its public option groups are:

- `Features` — middleware feature switches.
- `ExceptionHandling` — exception mapping and safe error behavior.
- `Correlation` — identifier validation and propagation.
- `ProblemDetails` — public error response customization.
- `RequestLogging` — structured request-completion logging policy.
- `DistributedTracing` — activity naming and trace context behavior.
- `SensitiveDataMasking` — matching and redaction rules.
- `RequestTiming` — duration thresholds and completion callback.
- `HttpSecurity` — opt-in response headers and body-size policy.

Option properties and enum values are consumer configuration. Changes to names, defaults, validation behavior, or enum meanings need deliberate compatibility review and updated tests/documentation.

### Supported primitives and value types

The intended reusable API includes:

- `ISensitiveDataMasker` and `SensitiveDataMasker` for non-mutating masking.
- `SensitiveDataMaskingOptions` and `SensitiveDataMaskingRule` for redaction rules.
- `SensitiveDataMatchMode` and `SensitiveDataMaskMode` for rule behavior.
- `ProblemDetailsCustomizationContext` and `ProblemDetailsOptions` for safe error-response customization.
- `RequestTimingContext` and `RequestTimingOptions` for request-duration diagnostics.
- `RequestLoggingOptions`, `RequestLoggingLogLevel`, and `RequestTimingLogLevel` for logging controls.
- `BackendSafetyDiagnostics` and `BackendSafetyMetrics` as stable instrumentation source/meter owners.

`CorrelationIdOptions`, `DistributedTracingOptions`, `ExceptionHandlingOptions`, `HttpSecurityOptions`, and `BackendSafetyFeatureOptions` are public configuration contracts nested under `BackendSafetyOptions`.

### ASP.NET Core integration

The application-facing API should remain centered on service registration and middleware registration:

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.Features.EnableRequestLogging = false;
});

builder.Services.AddBackendSafetyHealthChecks();

var app = builder.Build();
app.UseBackendSafety();
```

Applications should not need to instantiate implementation middleware directly. Middleware classes, validators, cache helpers, and writer implementations should remain internal unless an issue documents a concrete consumer use case.

### Optional OpenTelemetry integration

`AddBackendSafetyOpenTelemetry(...)` registers the library's existing instrumentation sources with an application's configured OpenTelemetry providers. It must not silently select an exporter, destination, credentials, sampling policy, or network transport.

## API review checklist

Before merging a public API change, reviewers should answer:

- **Necessity:** Does a consumer need this type/member, or can it stay internal?
- **Consistency:** Do naming, nullability, defaults, and exception behavior match the adjacent options APIs?
- **Security:** Does the API expose request data, exception details, headers, secrets, or trust decisions by default?
- **Lifecycle and concurrency:** Is mutable state request-scoped, or is startup configuration snapshotted for safe shared use?
- **Dependency boundary:** Does a core package gain a third-party or provider-specific dependency unnecessarily?
- **Documentation:** Are public types/members XML-documented and are examples aligned with the actual default behavior?
- **Compatibility:** Could removal, rename, overload ambiguity, changed default, altered validation, or changed enum semantics break existing consumers?
- **Tests:** Are both successful and invalid/edge-case behaviors covered?

## Findings and follow-up

- Public option groups are mutable during registration; the package documents startup configuration as immutable after the application starts. Runtime mutation is not a supported reconfiguration mechanism.
- Security-sensitive behaviors such as HTTPS redirection, HSTS, CSP, forwarded-header trust, and request-size limits need explicit configuration or host-application decisions; avoid changing these defaults without a compatibility and deployment review.
- Request and metric dimensions must remain bounded; path, query string, correlation IDs, request/response bodies, and arbitrary headers must not become default metric tags.
- The package intentionally separates the core, ASP.NET Core integration, and OpenTelemetry integration so installing the core package does not opt into web hosting or telemetry export.

Next step: maintain an automated shipped/unshipped public API baseline in the dedicated API compatibility work item. The baseline should be generated from the built assemblies, reviewed on intentional public changes, and checked in CI rather than relying on this narrative alone.
