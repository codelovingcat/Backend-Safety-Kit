# Migration and Upgrade Guide

This guide helps teams adopt Backend Safety Kit alongside existing ASP.NET Core behavior and upgrade package versions without accidental policy changes.

## Introduce the package incrementally

1. Add `BackendSafetyKit.AspNetCore` to the application.
2. Register `AddBackendSafety()` during service setup.
3. Add `UseBackendSafety()` to the request pipeline after trusted forwarded-header processing, if applicable.
4. Start with defaults in a non-production environment.
5. Compare response headers, error payloads, correlation IDs, and log events with your current implementation.
6. Remove duplicate middleware only after confirming behavior and ownership.
7. Add integration tests for expected failures and keep the old path available for rollback until rollout succeeds.

Do not register multiple exception handlers, timing middleware, or request loggers accidentally. Framework components such as authentication, authorization, CORS, routing, model validation, and application-owned domain responses remain under the host application's control.

## Adopt safe defaults without breaking consumers

The package does not return exception messages in production by default. If clients relied on detailed error text, replace that accidental contract with stable ProblemDetails titles, application error codes, and documented status codes. Treat any deliberate change in status mapping or response extensions as a client-facing API change.

Request bodies and query strings are not logged. Header logging is deny-by-default and only allowlisted values can be captured. Add metadata selectively; never reproduce a legacy logger's habit of writing all headers or request content.

HTTPS redirection, HSTS, frame options, Referrer-Policy, CSP, and request-size limits should be evaluated against the host's deployment and application needs. Do not switch them all on as a blind migration step.

## Upgrade checklist

For every package upgrade:

- Read the release notes and compare the package's public API.
- Keep the package family on a consistent version.
- Restore and build using the supported .NET SDK.
- Run unit and HTTP integration tests.
- Recheck custom exception mappings and ProblemDetails callbacks.
- Recheck middleware order behind proxies and gateways.
- Review logging allowlists/denylists and masking rules.
- Pack and validate artifacts if you build from source.
- Test configuration failures and rollback before production rollout.

The repository version policy follows SemVer, with additional caution while versions remain in the 0.x range. Treat changes to defaults, validation rules, error response shape, log schema, middleware order, or package dependencies as potentially breaking even when the C# signatures are unchanged.

## Move from one package to optional integrations

The core primitives are dependency-light. ASP.NET Core middleware and observability integrations are separate packages so a service only adds the dependencies it uses. Provider-specific integrations should remain optional and must not make the core package depend on a database or cache provider.

For a minimal setup, see the [minimal API guide](minimal-api-guide.md) or [controller guide](controller-guide.md). For configuration changes, use the [configuration reference](configuration-reference.md).
