# Production Deployment Guide

Backend Safety Kit provides request-pipeline safety and diagnostics; it is not a substitute for application identity, authorization, firewall, secret-management, or infrastructure controls.

## Startup configuration

Register all package services before building the host, and configure settings through reviewed code or your host's configuration system. Invalid security options fail validation at startup. Treat configuration as immutable after startup.

```csharp
builder.Services.AddBackendSafety(options =>
{
    options.HttpSecurity.EnableNoSniffHeader = true;
    options.HttpSecurity.EnableFrameOptionsHeader = true;
    options.HttpSecurity.FrameOptions = "DENY";

    options.RequestLogging.AllowedRequestHeaders.Add("X-Tenant");
    options.RequestLogging.DeniedRequestHeaders.Add("X-Tenant");
});
```

The example explicitly demonstrates deny-overrides-allow; do not allowlist secrets, tokens, cookies, or personally sensitive headers. Request/response bodies and query strings are not logged.

## Reverse proxies and HTTPS

If TLS terminates at a load balancer or proxy, configure ASP.NET Core forwarded-header processing before Backend Safety Kit. Trust only known proxy addresses/networks; do not clear trusted-proxy lists or accept forwarded headers from arbitrary clients.

```csharp
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.10")); // Replace with your trusted proxy.
    options.ForwardLimit = 1;
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseBackendSafety();
```

The proxy address and number of hops are deployment-specific. Do not copy the example IP literally. Misconfigured scheme forwarding can create redirect loops or cause an application to believe a secure request is HTTP.

HTTPS redirection and HSTS are opt-in because the package cannot infer the actual TLS topology. Configure a valid HTTPS port for redirection. Enable HSTS only when all relevant traffic is HTTPS; use preload only after every subdomain meets preload requirements and the owning team accepts its long-lived impact.

## Health and rollout

```csharp
builder.Services.AddBackendSafety();
builder.Services.AddBackendSafetyHealthChecks();

var app = builder.Build();
app.UseBackendSafety();
app.MapHealthChecks("/health");
```

The built-in health check verifies Backend Safety Kit configuration. It does not check database readiness or other infrastructure. Compose it with application-specific checks and expose liveness/readiness endpoints according to your platform's policy. Protect diagnostic endpoints if their combined output could reveal operational state.

Roll out restrictive Content Security Policy in report-only mode first. Observe browser behavior and add the enforcement policy only after verifying the application’s required resources. Keep security-header behavior opt-in where compatibility depends on the application.

## Containers and operations

- Run as a non-root user and use a minimal supported .NET runtime image.
- Keep the runtime image and SDK patched; do not ship the SDK merely to run the API.
- Inject secrets at runtime through the deployment platform's secret mechanism. Never commit secrets to source or image layers.
- Configure CPU/memory limits, health probes, log retention, and graceful shutdown for the host.
- Do not trust arbitrary forwarded headers, hosts, or client IP values.
- Review high-cardinality values before exporting logs, metrics, or traces to an external provider.
- Keep exporters and transports in explicitly chosen integrations. The core package makes no hidden network calls.

## Release readiness

Restore, build, run unit and integration tests, pack, and validate the package artifacts before promotion. Use the repository's protected tag-only release workflow, verify package versions and release notes, and retain the SBOM/provenance artifact alongside the release records.
