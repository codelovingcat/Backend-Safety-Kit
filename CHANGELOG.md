# Changelog

All notable changes to Backend Safety Kit are documented here. Release entries summarize consumer-visible behavior rather than internal commit details.

The format follows Keep a Changelog, and released versions follow the project's semantic-versioning policy in [docs/versioning-policy.md](docs/versioning-policy.md).

## [Unreleased]

### Added

- Automated public API compatibility checks for pull requests.
- XML documentation enforcement for packable projects.
- NuGet artifact validation for metadata, dependency boundaries, XML docs, README, license metadata, repository revision, and symbol packages.
- Source Link and portable symbol-package generation for the distributable libraries.
- Documented .NET framework support and package release-readiness procedures.

### Changed

- NuGet package metadata now declares explicit project/repository details and an SPDX MIT license expression.
- CI treats build, API compatibility, packaging, and artifact-validation failures as release-blocking checks.

### Security

- Added a documented framework for future CodeQL and dependency-supply-chain checks; findings should be triaged before a release is published.

## Release-note guidelines

- Move only shipped changes from Unreleased into a dated release section when cutting a version.
- Describe changed public API, behavior, configuration defaults, supported frameworks, and migration impact.
- Mention security fixes without publishing exploit details before coordinated disclosure.
- Keep internal refactoring and CI-only maintenance out of the user-facing feature list unless it changes the consumer workflow.
