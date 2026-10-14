# Security Policy and Threat Model

## Reporting a Vulnerability

Please do not report a suspected vulnerability in a public issue, discussion, or pull request.

Use GitHub's private vulnerability reporting flow for this repository: open the repository's **Security** tab and choose **Report a vulnerability** when the feature is available. If private reporting is unavailable, contact the repository maintainer privately through GitHub rather than publishing exploit details.

Please include the affected package and version, .NET/ASP.NET Core version, deployment topology, impact, and a minimal reproducible example. Remove credentials, real customer data, tokens, and other secrets from logs and reproduction steps. Do not attach production dumps or request bodies containing private data.

There is no published response-time SLA yet. Coordinated disclosure is preferred: allow the maintainer an opportunity to investigate and prepare a fix before public details are shared.

## Supported Versions

Backend Safety Kit is pre-1.0 and does not yet publish a formal long-term-support matrix. Reports should target the latest available package and, where practical, include whether the issue reproduces on the current main branch. Do not assume older package versions receive a backported fix.

## Threat Model

Backend Safety Kit provides defensive infrastructure for ASP.NET Core applications. It reduces common implementation risks, but it is not a substitute for application-specific security controls.

### In scope

The project aims to reduce risks such as:

- Accidental disclosure of exception details or sensitive metadata in standard error responses and package-generated logs.
- Exposure of common secrets when applications opt into structured-header logging and masking.
- Unsafe or unbounded processing of nested values by the sensitive-data masker.
- Untrusted correlation and tracing identifiers being propagated without validation.
- Requests exceeding an explicitly configured request-body size policy.
- Missing or inconsistent opt-in HTTP hardening headers.
- Misuse of forwarded headers when deployment trust boundaries are clearly configured.

### Trust assumptions and application responsibilities

- Request headers, query strings, payloads, correlation IDs, and tracing headers are untrusted input.
- Reverse proxies and forwarded headers are trusted only when the host application configures known proxies/networks and places forwarded-header middleware before middleware that depends on the resulting scheme or client address.
- HTTPS redirection and HSTS depend on deployment topology. Configure HTTPS ports and HSTS intentionally; HSTS can persist in clients for the configured duration.
- Custom diagnostic sinks, logging providers, telemetry exporters, and reporting endpoints are separate data-handling boundaries. Review their access controls, retention, and redaction behavior.
- Applications remain responsible for authentication, authorization, tenant isolation, input/business validation, CSRF policy, secret management, TLS certificate configuration, dependency patching, and infrastructure hardening.
- A feature disabled by the host application does not enforce the corresponding protection. Review options and middleware order as part of deployment.

### Design constraints

- No hidden telemetry, tracking, or network calls are introduced by the core package.
- Request and response bodies are not logged by default.
- Sensitive-data masking and response hardening prefer explicit opt-in for behavior that could affect application compatibility.
- Existing application-supplied security headers should not be overwritten.
- Forwarded headers and proxy trust are not enabled automatically.
- Security-sensitive behavior should have deterministic regression tests.

## Safe Reproduction and Disclosure

When reproducing an issue, use synthetic credentials and data. Avoid public exploit payloads against production systems. Report the smallest relevant source location, request shape, configured options, observed result, expected result, and impact. Public advisories and release notes should be coordinated with the maintainer.
