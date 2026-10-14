# Changelog

All notable changes to Backend Safety Kit are documented here. Release entries summarize consumer-visible behavior rather than internal commit details.

The format follows Keep a Changelog. Package versions follow the project's semantic-versioning policy in [docs/versioning-policy.md](docs/versioning-policy.md).

## [Unreleased]

This release train is prepared as the **0.1.0-preview.1** prerelease. It is not a stable 1.0 release. The matching tag is `v0.1.0-preview.1`; creating the tag and publishing the packages are separate release actions.

### Added

- **Core package (`BackendSafetyKit`):** dependency-light safety and diagnostics primitives, including sensitive-data masking, correlation/request IDs, structured diagnostic contracts, and request timing/tracing abstractions.
- **ASP.NET Core package (`BackendSafetyKit.AspNetCore`):** middleware and service registration for consistent exception handling with ProblemDetails, correlation IDs, structured request logging, request timing, safe diagnostic metadata, and startup option validation. The package documents both minimal API and controller usage.
- **OpenTelemetry package (`BackendSafetyKit.OpenTelemetry`):** optional OpenTelemetry integration, kept separate from the core package.
- **Redis package (`BackendSafetyKit.Redis`):** optional bounded asynchronous diagnostic delivery through Redis Streams, kept separate from the core package.
- **Entity Framework Core package (`BackendSafetyKit.EntityFrameworkCore`):** optional SaveChanges diagnostics integration, kept separate from the core package.
- Developer guides covering framework support, configuration, migration/upgrade, feature selection, troubleshooting, deployment, and release readiness.
- NuGet validation for all five shipping package IDs, including package metadata, dependency boundaries, XML documentation, README/license metadata, repository information, and matching symbol packages.

### Changed

- All five shipping packages use the same prerelease version, `0.1.0-preview.1`, so package metadata matches the intended first preview tag.
- Package metadata identifies the repository, MIT license, README, and release notes consistently.
- Pull-request CI builds, tests, packs, and validates the package artifacts; compatibility and documentation checks help catch API and packaging regressions before release.

### Security and release assurance

- Secure defaults avoid logging request bodies and query strings, restrict sensitive headers, and mask sensitive metadata.
- CodeQL, dependency vulnerability scanning, Dependabot, formatting/static-analysis checks, and package validation are part of the repository's automated maintenance and CI workflows.
- The tag-triggered NuGet release workflow is configured to validate the tag/version, restore, build, test, pack and validate all five packages, generate a CycloneDX SBOM, create build-provenance attestations, and then attempt publication through the protected `nuget-production` environment.
- **Release status:** configuring a workflow does not mean a publication succeeded. Treat the packages as unreleased until the tagged workflow completes successfully and all five package versions are verified on NuGet.org.

## Release-note guidelines

- Move only shipped changes from Unreleased into a dated release section when cutting a version.
- Describe changed public API, behavior, configuration defaults, supported frameworks, and migration impact.
- Mention security fixes without publishing exploit details before coordinated disclosure.
- Keep internal refactoring and CI-only maintenance out of the user-facing feature list unless it changes the consumer workflow.
