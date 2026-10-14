# Troubleshooting Guide

This guide covers common setup, runtime, and CI problems. Backend Safety Kit intentionally fails fast for invalid options and avoids returning internal exceptions to API clients.

## Startup fails while validating options

**Symptoms:** the application stops during host startup, before serving requests.

**Check:** correlation header names and maximum length; invalid HTTP header values; exception status codes outside 400–599; request logging limits; masking limits; HTTPS port/status code; HSTS preloading requirements; or a missing required CSP/referrer value when that feature is enabled.

**Fix:** reduce the configuration to a known-good setup, then add settings one group at a time. Do not suppress startup validation to get a deployment through.

## An exception returns generic ProblemDetails

That is the secure default. Exception details are not included in production responses. Map known exceptions to stable status codes, titles, and error codes. Enable development-only details only in a protected Development environment, never as a production debugging shortcut.

If an expected exception is not mapped, inspect the exception type and mapping registration. The nearest matching mapped exception type is used when mappings are assignable.

## A correlation ID is not reused

The effective ID is resolved from the configured primary header, then the fallback header. Invalid, too-long, or multi-valued incoming IDs are treated as untrusted and may be replaced. Check custom header names and reverse-proxy behavior; do not weaken ID validation just to preserve arbitrary client input.

The response header can be disabled with `Correlation.IncludeResponseHeader = false`, but this does not disable internal correlation handling when the middleware feature remains enabled.

## Request headers are missing from logs

The default is a deny-by-default allowlist: no request or response header values are logged unless explicitly allowlisted. Authorization, Cookie, Proxy-Authorization, and Set-Cookie are denied by default, and deny rules win over allow rules. Query strings and bodies are not captured.

Verify `AllowedRequestHeaders`, `AllowedResponseHeaders`, `DeniedRequestHeaders`, and `DeniedResponseHeaders`. Never add credentials or sensitive personal data to a diagnostic allowlist.

## HTTPS redirects loop behind a proxy

Make sure forwarded-header middleware runs before `UseBackendSafety()`, configure only the headers actually set by the proxy, and trust only known proxy addresses/networks. Set the intended HTTPS port. Do not accept forwarded scheme headers from arbitrary clients.

## HSTS or CSP affects the application

HSTS, frame options, Referrer-Policy, and Content Security Policy are opt-in except `X-Content-Type-Options: nosniff`. Roll CSP out with report-only mode before enforcement, and test legitimate scripts, styles, images, fonts, APIs, and frames. Do not enable HSTS preload until all subdomains and requirements are verified.

## The configured request body size is not applied

`HttpSecurity.MaxRequestBodySize` must be positive. It is applied using the active server's request-size feature; configuration throws when the feature is unavailable or already read-only. Check server configuration and middleware ordering, and test both requests with a declared Content-Length and streaming/chunked requests.

## Slow request diagnostics do not appear

Slow request logging is disabled by default. Enable `RequestTiming.EnableSlowRequestLogging`, check `SlowRequestThreshold` and `SlowRequestLogLevel`, and confirm the timing feature is enabled. A custom `OnCompleted` callback runs on the request path; keep it fast and handle failures internally.

## Health check reports unhealthy

The built-in health check validates Backend Safety Kit options and intentionally does not expose configuration values. Inspect server-side logs for the validation failure. It does not probe databases, Redis, external HTTP endpoints, or any infrastructure you have not separately registered.

## CI or package validation fails

Run the pipeline locally:

```bash
dotnet restore BackendSafetyKit.slnx
dotnet build BackendSafetyKit.slnx --configuration Release --no-restore
dotnet test BackendSafetyKit.slnx --configuration Release --no-build
dotnet pack BackendSafetyKit.slnx --configuration Release --no-build --output ./artifacts
python scripts/validate_nuget_packages.py ./artifacts
python scripts/check_package_versions.py
```

If API compatibility fails, review the baseline and the public API diff; do not bypass the gate by changing a version alone. If a pack artifact is missing XML docs, README, repository metadata, license, or matching symbols, fix the package project and rerun validation.

## Get a useful bug report

Include the .NET SDK version, package versions, environment (Development/Production), sanitized options, response status/content type, correlation ID, and minimal reproduction. Remove tokens, cookies, personal data, request bodies, connection strings, and raw exception details before filing an issue.
