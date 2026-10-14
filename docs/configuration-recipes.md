# Configuration Recipes

These recipes show common combinations of Backend Safety Kit options. Apply them during startup and change only the settings your application needs.

## 1. Small safe default setup

```csharp
builder.Services.AddBackendSafety();
```

This enables the package's configured middleware defaults. It does not enable authentication, CORS, telemetry export, or external service connections.

## 2. Map application exceptions to stable errors

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.ExceptionHandling
        .Map<KeyNotFoundException>(404)
        .Map<UnauthorizedAccessException>(403);

    options.ProblemDetails
        .MapErrorCode<KeyNotFoundException>("resource_not_found")
        .MapTitle<KeyNotFoundException>("The requested resource was not found.");
});
```

Keep response titles and error codes stable. Avoid returning database messages, stack traces, or untrusted exception text.

## 3. Customize correlation headers

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.Correlation.CorrelationIdHeaderName = "X-Correlation-ID";
    options.Correlation.RequestIdHeaderName = "X-Request-ID";
    options.Correlation.ResponseHeaderName = "X-Correlation-ID";
    options.Correlation.MaxLength = 96;
});
```

Incoming identifiers are still validated. Header values that are malformed, oversized, or multi-valued are not trusted.

## 4. Log only explicit safe headers

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.RequestLogging.AllowedRequestHeaders.Add("X-Tenant");
    options.RequestLogging.AllowedResponseHeaders.Add("Content-Type");

    // Deny rules override allow rules.
    options.RequestLogging.DeniedRequestHeaders.Add("X-Tenant");

    options.RequestLogging.MaxHeaderCount = 12;
    options.RequestLogging.MaxMetadataValueLength = 128;
});
```

Do not allowlist Authorization, Cookie, Proxy-Authorization, Set-Cookie, tokens, or personal data. Request bodies and query strings are not logged.

## 5. Add a secret masking rule

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.SensitiveDataMasking.AddRule("tenantSecret");
    options.SensitiveDataMasking.AddRule(
        "email",
        matchMode: SensitiveDataMatchMode.Exact,
        maskMode: SensitiveDataMaskMode.Full);
});
```

Partial masking remains disabled unless explicitly enabled. Validate rules against the structures your application processes, and do not assume that every arbitrary object is automatically safe to log.

## 6. Configure slow request diagnostics

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.RequestTiming.SlowRequestThreshold = TimeSpan.FromMilliseconds(800);
    options.RequestTiming.EnableSlowRequestLogging = true;
    options.RequestTiming.SlowRequestLogLevel = RequestTimingLogLevel.Warning;
});
```

Thresholds are application-specific. Keep callbacks short and avoid synchronous remote work on the request path.

## 7. Enable selected HTTP headers

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.HttpSecurity.EnableFrameOptionsHeader = true;
    options.HttpSecurity.FrameOptions = "DENY";

    options.HttpSecurity.EnableReferrerPolicyHeader = true;
    options.HttpSecurity.ReferrerPolicy = "strict-origin-when-cross-origin";

    options.HttpSecurity.EnableContentSecurityPolicyReportOnlyHeader = true;
    options.HttpSecurity.ContentSecurityPolicyReportOnly =
        "default-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'";
});
```

Start CSP in report-only mode, assess real application resource needs, and enable enforcement only after testing. Existing application headers are preserved.

## 8. Enable HTTPS redirection and HSTS deliberately

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.HttpSecurity.EnableHttpsRedirection = true;
    options.HttpSecurity.HttpsPort = 443;
    options.HttpSecurity.HttpsRedirectStatusCode = 307;

    options.HttpSecurity.EnableHsts = true;
    options.HttpSecurity.HstsMaxAge = TimeSpan.FromDays(365);
    options.HttpSecurity.HstsIncludeSubDomains = true;
    options.HttpSecurity.HstsPreload = false;
});
```

Only use this configuration after confirming the public HTTPS topology, trusted proxy forwarding, and subdomain readiness. HSTS preload is intentionally omitted; it has long-lived domain-wide consequences.

## 9. Apply a request body limit

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.HttpSecurity.MaxRequestBodySize = 10 * 1024 * 1024;
});
```

The value is in bytes. The active server must support changing the request-size feature at the point where the middleware runs. An unsupported or already read-only feature causes configuration to fail rather than silently ignore the limit.

## 10. Disable unused middleware explicitly

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.Features.EnableRequestLogging = false;
    options.Features.EnableDistributedTracing = false;
    options.Features.EnableHttpSecurity = false;
});
```

Disabling a feature removes that middleware. Confirm that the application does not depend on its response headers, diagnostics, trace context, or logging before switching it off.

For every setting and valid range, see the [configuration reference](configuration-reference.md). For deployment-specific settings, see [Production Deployment](production-deployment.md).
