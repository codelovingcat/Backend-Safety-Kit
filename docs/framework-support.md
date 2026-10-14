# Framework Support Policy

**Last reviewed:** 14 October 2026

## Supported target framework

All three distributable packages currently target **.NET 10 (net10.0)**:

| Package | Support boundary |
|---|---|
| **BackendSafetyKit** | .NET 10 |
| **BackendSafetyKit.AspNetCore** | .NET 10 and the ASP.NET Core shared framework for that target |
| **BackendSafetyKit.OpenTelemetry** | .NET 10; OpenTelemetry integration remains a separate optional package |

The repository deliberately does not claim compatibility with net8.0, net9.0, .NET Framework, or .NET Standard. Those target frameworks are not part of the current build and test matrix.

## Servicing policy

Backend Safety Kit follows Microsoft's support lifecycle for the .NET runtime and ASP.NET Core:

- Prefer active Long Term Support (LTS) releases for production-facing package targets.
- Keep the target framework on a supported release and test against the latest available servicing SDK/runtime in CI.
- Review target-framework changes annually when a new .NET release is available. Do not add a target framework until restore, build, tests, package validation, API compatibility, and documentation all pass for it.
- Remove a target framework from the supported matrix when Microsoft support ends unless there is an explicit, maintained exception.
- Support only the package versions and target frameworks published in the release metadata; the library does not promise servicing for unsupported runtime versions.

As of this review, Microsoft lists .NET 10 as an active LTS release with support through **14 November 2028**. See the [official .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) for the current lifecycle dates. These dates can change; the Microsoft policy is authoritative.

## SDK policy

global.json pins the repository to the .NET 10 SDK feature band beginning at 10.0.100 and allows patch roll-forward. This pin is for reproducible development and CI; it does not promise that every runtime version can consume the packages.

## Validation

The CI workflow restores, builds, tests, packs and validates all shipping packages on .NET 10. Pull requests also compare the compiled public APIs with their base commit. A new target framework is not considered supported until that complete validation path covers it.
