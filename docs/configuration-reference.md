# Configuration Reference

Backend Safety Kit options are configured during application startup through `AddBackendSafety`. Treat them as immutable after the host starts. Invalid configuration is checked during startup; it is not silently accepted.

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.Features.EnableRequestLogging = true;
    options.Correlation.MaxLength = 128;
    options.RequestTiming.SlowRequestThreshold = TimeSpan.FromMilliseconds(750);
});
```

## Feature switches

All switches below default to `true`. Disabling a switch removes that middleware from the pipeline; the package does not leave a no-op middleware behind.

| Option | Default | Behavior |
|---|---:|---|
| `Features.EnableCorrelationId` | `true` | Resolve and propagate the effective request/correlation ID. |
| `Features.EnableRequestLogging` | `true` | Write structured request completion events. |
| `Features.EnableDistributedTracing` | `true` | Create a Backend Safety activity when no server activity already exists. |
| `Features.EnableRequestTiming` | `true` | Measure elapsed request time and optionally report slow requests. |
| `Features.EnableHttpSecurity` | `true` | Apply enabled HTTP security headers, redirects, and request-size policy. |
| `Features.EnableExceptionHandling` | `true` | Convert otherwise-unhandled exceptions into safe ProblemDetails responses. |

## Exception handling

| Option | Default | Notes |
|---|---|---|
| `ExceptionHandling.DefaultStatusCode` | `500` | Must be an HTTP error status from 400 through 599. |
| `ExceptionHandling.StatusCodeMappings` | empty | Map exception types with `Map<TException>(statusCode)`; the most specific matching type wins. |

Example:

```csharp
options.ExceptionHandling
    .Map<KeyNotFoundException>(404)
    .Map<UnauthorizedAccessException>(403);
```

## ProblemDetails

| Option | Default | Notes |
|---|---|---|
| `ProblemDetails.IncludeTraceId` | `true` | Include the request trace ID in the response extensions. |
| `ProblemDetails.IncludeInstance` | `true` | Include the request path as the instance. |
| `ProblemDetails.IncludeExceptionDetailInDevelopment` | `false` | Exception messages may be included only in Development when explicitly enabled; details are not automatically exposed in production. |
| `ProblemDetails.DefaultType` | `about:blank` | Default RFC ProblemDetails type; must be non-empty. |
| `ProblemDetails.ErrorCodeMappings` | empty | Map exception types to application error codes with `MapErrorCode<TException>(code)`. |
| `ProblemDetails.TitleMappings` | empty | Map exception types to safe titles with `MapTitle<TException>(title)`. |
| `ProblemDetails.Customize` | `null` | Optional callback for response customization. Customization is validated before writing. |

Do not add secrets, connection strings, stack traces, SQL statements, or raw exception messages to custom extensions.

## Correlation and request IDs

| Option | Default | Validated behavior |
|---|---|---|
| `Correlation.CorrelationIdHeaderName` | `X-Correlation-ID` | Primary inbound ID header. |
| `Correlation.RequestIdHeaderName` | `X-Request-ID` | Fallback inbound ID header. |
| `Correlation.ResponseHeaderName` | `X-Correlation-ID` | Response header for the effective ID. |
| `Correlation.IncludeResponseHeader` | `true` | Return the effective ID in a response header. |
| `Correlation.MaxLength` | `128` | Accepted IDs are bounded to 1–1024 characters. |
| `Correlation.Generator` | random GUID in N format | Used when no valid incoming value is available. |

The primary header wins when valid, then the fallback is considered. Invalid, oversized, or multi-valued input is not trusted; the middleware resolves or generates a safe ID instead. Header names must be non-empty HTTP token strings no longer than 256 characters.

## Structured request logging

| Option | Default | Notes |
|---|---|---|
| `RequestLogging.IncludeHost` | `true` | Include the request host in completion metadata. |
| `RequestLogging.AllowedRequestHeaders` | empty | Header values are never logged unless explicitly allowlisted. |
| `RequestLogging.DeniedRequestHeaders` | `Authorization`, `Cookie`, `Proxy-Authorization` | Deny rules take precedence over allow rules. |
| `RequestLogging.AllowedResponseHeaders` | empty | Response headers are never logged unless explicitly allowlisted. |
| `RequestLogging.DeniedResponseHeaders` | `Set-Cookie` | Deny rules take precedence over allow rules. |
| `RequestLogging.SuccessfulRequestLogLevel` | `Information` | Used for successful responses. |
| `RequestLogging.ClientErrorLogLevel` | `Warning` | Used for HTTP 400–499. |
| `RequestLogging.ServerErrorLogLevel` | `Error` | Used for HTTP 500+ and unhandled exceptions. |
| `RequestLogging.MaxMetadataValueLength` | `256` | Valid range: 1–4096 characters. |
| `RequestLogging.MaxHeaderCount` | `16` | Valid range: 0–128 headers per log entry. |

Set a request log level to `RequestLoggingLogLevel.None` to disable that category. Request bodies, query strings, and denied headers must not be added to logs.

## Distributed tracing

| Option | Default | Notes |
|---|---|---|
| `DistributedTracing.ActivityName` | `BackendSafetyKit.HttpRequest` | Non-empty name up to 256 characters. |

The middleware consumes standard W3C trace context and avoids creating a duplicate server activity when an appropriate `Activity.Current` already exists. The core package does not export telemetry or make network calls.

## Sensitive-data masking

| Option | Default | Notes |
|---|---|---|
| `SensitiveDataMasking.Rules` | built-in secret-oriented rules | Ordered rules; the first match wins. |
| `SensitiveDataMasking.MaskValue` | `[REDACTED]` | Must be non-empty and at most 64 characters. |
| `SensitiveDataMasking.AllowPartialMasking` | `false` | Partial masking requires an explicit opt-in. |
| `SensitiveDataMasking.MaxDepth` | `16` | Valid range: 1–32 nested levels. |
| `SensitiveDataMasking.MaxCollectionItems` | `256` | Valid range: 1–4096 items. |

Add rules with `AddRule(name, matchMode, maskMode, visiblePrefixLength, visibleSuffixLength)`. Up to 256 rules are supported. Default rules include password, secret, token, authorization, cookie, and common API-key header variants. Review custom rules against your own schema and do not assume masking makes arbitrary data safe to persist.

## Request timing and diagnostics

| Option | Default | Notes |
|---|---|---|
| `RequestTiming.SlowRequestThreshold` | 1 second | Must be between zero and one day. A request at or above the threshold is slow. |
| `RequestTiming.EnableSlowRequestLogging` | `false` | Enables automatic slow-request logs. |
| `RequestTiming.SlowRequestLogLevel` | `Warning` | Level used for slow requests. |
| `RequestTiming.OnCompleted` | `null` | Optional callback invoked with a `RequestTimingContext`. |

The callback runs on the request path. Keep it fast, avoid blocking I/O, and handle its own failures. Diagnostic sink events should contain safe operational metadata only, never credentials, bodies, query strings, or raw header values.

## HTTP security

| Option | Default | Notes |
|---|---|---|
| `HttpSecurity.EnableNoSniffHeader` | `true` | Adds `X-Content-Type-Options: nosniff`. |
| `HttpSecurity.EnableFrameOptionsHeader` | `false` | Opt in when appropriate; value is `DENY` or `SAMEORIGIN`. |
| `HttpSecurity.FrameOptions` | `DENY` | Used when frame options is enabled. |
| `HttpSecurity.EnableReferrerPolicyHeader` | `false` | Opt-in response header. |
| `HttpSecurity.ReferrerPolicy` | `no-referrer` | Must be non-empty and contain no CR/LF characters. |
| `HttpSecurity.EnableContentSecurityPolicyHeader` | `false` | Enforcement policy; opt in after testing the application. |
| `HttpSecurity.ContentSecurityPolicy` | restrictive CSP string | Used when enforcement is enabled. |
| `HttpSecurity.EnableContentSecurityPolicyReportOnlyHeader` | `false` | Emits a non-enforcing report-only policy when enabled. |
| `HttpSecurity.ContentSecurityPolicyReportOnly` | restrictive report-only policy | Used when report-only mode is enabled. |
| `HttpSecurity.EnableHttpsRedirection` | `false` | Enable only after defining deployment/proxy topology. |
| `HttpSecurity.HttpsPort` | `null` | Required and must be 1–65535 when redirection is enabled. |
| `HttpSecurity.HttpsRedirectStatusCode` | `307` | Only 307 or 308 is accepted to preserve HTTP method semantics. |
| `HttpSecurity.EnableHsts` | `false` | Emits HSTS only on HTTPS responses. |
| `HttpSecurity.HstsMaxAge` | 365 days | If enabled, valid range is one second through ten years. |
| `HttpSecurity.HstsIncludeSubDomains` | `false` | Applies HSTS to subdomains when enabled. |
| `HttpSecurity.HstsPreload` | `false` | Requires includeSubDomains and max-age of at least one year. |
| `HttpSecurity.MaxRequestBodySize` | `null` | Optional positive byte limit. Null leaves server limits unchanged. |

Existing application response headers are preserved. Request-size enforcement depends on the active server feature; configuration fails if a requested limit cannot be applied. HSTS preload should only be enabled after validating every subdomain and preload eligibility.

## Lifecycle and middleware ordering

Configure options during startup; runtime mutation is unsupported. The package adds enabled middleware in a deliberate order so correlation metadata is available downstream, timing can observe handled status codes, logging can include timing, and HTTP security can apply to handled errors. Configure trusted forwarded headers before `UseBackendSafety()` when TLS terminates at a proxy.

For runnable recipes, see [Configuration Recipes](configuration-recipes.md). For minimal hosting setups, see the [Minimal API Guide](minimal-api-guide.md).
