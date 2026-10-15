# SBOM and Software Supply-Chain Metadata

Backend Safety Kit creates a CycloneDX JSON software bill of materials (SBOM) from the validated NuGet artifact directory. The dedicated GitHub Actions workflow runs for pull requests and pushes to `main`, and makes the SBOM available as the `backend-safety-kit-sbom` workflow artifact.

The SBOM workflow first restores, builds, tests, packs, and validates the NuGet packages. It does not treat an SBOM as a replacement for dependency vulnerability scanning or package validation. Review the SBOM together with the package validator output and the dependency vulnerability scan.

## Release provenance

The protected `nuget-production` release workflow generates an SBOM for its release artifacts and creates GitHub build-provenance attestations for those files before publishing. A release requires a matching SemVer tag, passing tests and package validation, and a NuGet.org Trusted Publishing policy bound to `codelovingcat/Backend-Safety-Kit`, `publish-nuget.yml`, and the `nuget-production` environment; no long-lived NuGet API key is stored in GitHub. Ordinary pull-request CI never publishes packages.

Consumers can inspect attestations in the GitHub repository's artifact attestations interface and verify package checksums against the exact release artifacts. Treat a valid attestation as evidence of the recorded build provenance, not as a guarantee that a package is free from vulnerabilities.

## Maintenance

- Keep the SBOM workflow in step with the package build and validation commands.
- Review direct and transitive dependency changes from Dependabot.
- Investigate any discrepancy between an SBOM, a `.nuspec`, and the source project files before releasing.
- Do not add credentials, request data, environment values, or other secrets to generated metadata.
