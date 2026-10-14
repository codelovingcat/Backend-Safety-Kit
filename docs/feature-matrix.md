# Feature Matrix and Package Boundaries

Backend Safety Kit deliberately separates reusable primitives from ASP.NET Core middleware and optional vendor integrations.

| Capability | Core package `BackendSafetyKit` | ASP.NET Core package `BackendSafetyKit.AspNetCore` | OpenTelemetry package `BackendSafetyKit.OpenTelemetry` |
|---|---|---|---|
| Typed configuration contracts | Yes | Consumes core contracts | Optional configuration for registration |
| Sensitive-data masking | Yes | Uses core masker where applicable | No |
| Standard diagnostic event contracts | Yes | Can publish timing events to registered sinks | No direct export |
| Correlation/request ID middleware | No middleware | Yes | No |
| Global exception boundary and ProblemDetails | Configuration contracts | Yes | No |
| Structured HTTP request completion logs | No HTTP pipeline | Yes, via standard logging abstractions | No |
| Request timing and slow-request diagnostics | Contracts | Yes | Can be observed through configured instrumentation providers |
| Secure HTTP headers, HTTPS/HSTS, request-size policy | Options contracts | Yes | No |
| Health-check registration | No hosting integration | Yes, configuration health check | No |
| ActivitySource and Meter identities | Yes | Produces request instrumentation | Registers the existing sources with OpenTelemetry providers |
| Exporters, collectors, or transport | No | No | No; chosen by the host application |

## Optional integration boundary

Provider-specific packages belong outside the core dependency graph. Installing an optional integration should not pull that provider into applications that install only `BackendSafetyKit`. Optional integrations must be explicit, documented, independently tested, and should not make hidden network calls.

Redis, Entity Framework Core, and other provider-specific packages are not part of the three foundational packages listed above. Treat each provider integration as a separately versioned/packaged capability and verify its own package metadata, dependencies, supported framework, and tests before adopting it. A package's existence does not mean that a Redis server, database, exporter, or external service is provisioned for you.

## What Backend Safety Kit does not do

- It does not configure authentication, authorization, CORS, API keys, or user identity.
- It does not infer trusted reverse proxies or rewrite forwarded headers.
- It does not log request bodies, query strings, or arbitrary header values by default.
- It does not export telemetry, choose sampling, configure exporters, or call home.
- It does not create a database schema, migration, Redis keyspace, or infrastructure resource.
- It does not replace the application's deployment health/readiness policy.
- It does not promise that a secure response header or CSP is compatible with every frontend without testing.

## Choosing package references

Start with only the package needed for the job. Most ASP.NET Core APIs use `BackendSafetyKit.AspNetCore`, which references the core package. Add `BackendSafetyKit.OpenTelemetry` only when the application uses the OpenTelemetry SDK integration. Review the [configuration reference](configuration-reference.md) and the [production deployment guide](production-deployment.md) before enabling deployment-sensitive behavior.
