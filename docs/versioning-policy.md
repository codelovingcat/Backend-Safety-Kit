# Semantic Versioning and Release Policy

All shipping package projects included in the solution's release set are versioned together and must have the same version. The foundational package IDs are BackendSafetyKit, BackendSafetyKit.AspNetCore, and BackendSafetyKit.OpenTelemetry; optional provider packages such as BackendSafetyKit.Redis and BackendSafetyKit.EntityFrameworkCore remain separate dependencies. The authoritative project/package set is maintained in `scripts/check_package_versions.py` and `scripts/validate_nuget_packages.py`. A release tag uses the matching vMAJOR.MINOR.PATCH format, with optional SemVer prerelease or build metadata.

The repository uses Semantic Versioning 2.0.0:

- Before 1.0.0, patch releases are reserved for backward-compatible fixes and minor increments introduce features or behavior changes. Breaking API changes are documented prominently and should trigger a minor increment before 1.0.
- From 1.0.0 onward, patch releases contain backward-compatible fixes, minor releases add backward-compatible functionality, and major releases signal public API or behavior breaks.
- Prerelease identifiers such as -rc.1 are for candidate packages. Do not label a prerelease as a stable release.
- Build metadata is for traceability; it does not affect version precedence.

Public API removals, signature changes, changed configuration defaults, altered validation, and dependency-boundary changes must be reviewed against the API compatibility check and release notes. Do not bump a version only to bypass compatibility failures.

## Single release version

Keep the Version field aligned in all three package project files. scripts/check_package_versions.py validates the exact package set, Semantic Versioning syntax, and version consistency. When a tag is supplied, it also verifies that the tag matches the project version.

Run the policy check locally:

~~~bash
python scripts/check_package_versions.py
python scripts/check_package_versions.py --tag v0.1.0
~~~

The publishing workflow must reject a tag whose version does not match the package project files. Package validation and tests remain mandatory for every release tag; publishing is not triggered by ordinary pull requests.
