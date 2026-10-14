# NuGet Release Readiness

Each shipping package must provide stable package identity, a meaningful description, authors, tags, a project URL, Git repository metadata, the MIT SPDX license expression and a packaged README. Optional integrations remain separate packages: the core package must not acquire ASP.NET Core, OpenTelemetry, Redis, or Entity Framework Core runtime dependencies merely because those integrations exist elsewhere in the solution.

The CI pack validator's expected package set is maintained in `scripts/validate_nuget_packages.py`. It checks each configured package ID, its net10.0 assembly, XML documentation, README, MIT license metadata, repository URL and commit, declared dependency boundary, and matching .snupkg with portable PDB symbols. Optional integrations must be added to the project/version/package-validator set together so their artifacts cannot bypass validation.

## Local validation

~~~bash
dotnet restore BackendSafetyKit.slnx
dotnet build BackendSafetyKit.slnx --configuration Release --no-restore
dotnet test BackendSafetyKit.slnx --configuration Release --no-build
dotnet pack BackendSafetyKit.slnx --configuration Release --no-build --output ./artifacts
python scripts/validate_nuget_packages.py ./artifacts
python scripts/test_nuget_consumers.py ./artifacts
python -m pip install --disable-pip-version-check -r scripts/requirements-sbom.txt
curl --fail --silent --show-error --location https://cyclonedx.org/schema/bom-1.6.schema.json --output /tmp/cyclonedx-bom-1.6.schema.json
python scripts/generate_release_sbom.py ./artifacts --schema /tmp/cyclonedx-bom-1.6.schema.json
(cd ./artifacts && sha256sum ./*.nupkg ./*.snupkg ./backend-safety-kit.sbom.json > SHA256SUMS.txt && sha256sum --check SHA256SUMS.txt)
~~~

The validators use Python's standard library only. The consumer smoke test then creates five temporary .NET 10 projects, each referencing exactly one packed Backend Safety Kit package. It restores the product packages from a temporary local feed, restores their third-party dependencies from NuGet.org, compiles each project against public APIs, and inspects each `project.assets.json` file to catch unintended Redis/EF Core dependency leakage. No test package is published, and no NuGet.org credentials are needed.

## SBOM and release provenance

`scripts/generate_release_sbom.py` builds a CycloneDX 1.6 SBOM from each shipping package's actual `.nuspec` metadata and restored .NET 10 `project.assets.json` graph. It follows dependencies reachable from each package's declared NuGet dependencies, records SHA-256 checksums for every shipped `.nupkg`, checks all five package IDs and direct-dependency edges, and validates the document against the official CycloneDX 1.6 JSON Schema. Build-only packages that are not reachable from the shipped dependency declarations are not mislabeled as consumer dependencies.

The SBOM workflow uploads `backend-safety-kit.sbom.json` and `SHA256SUMS.txt` as CI artifacts. On a protected release tag, the publishing workflow creates the same SBOM/checksum set and attests the files under `artifacts/` with GitHub build provenance. Ordinary pull requests and branch builds do not publish NuGet packages.

After downloading the SBOM/checksum artifact and the package artifacts from the same release workflow run into one `artifacts/` directory, verify the hashes and attestations with Git Bash, Linux, or macOS:

~~~bash
cd artifacts
sha256sum --check SHA256SUMS.txt
gh attestation verify BackendSafetyKit.0.1.0-preview.1.nupkg --repo codelovingcat/Backend-Safety-Kit
gh attestation verify backend-safety-kit.sbom.json --repo codelovingcat/Backend-Safety-Kit
gh attestation verify SHA256SUMS.txt --repo codelovingcat/Backend-Safety-Kit
~~~

Use the exact package version and downloaded filename for the release being inspected. A configured workflow or successful local build is not proof that public NuGet publication succeeded; verify the tagged workflow and package versions on NuGet.org separately.
