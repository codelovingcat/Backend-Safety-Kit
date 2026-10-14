# SBOM and Release Provenance

## Inventory and validation

The release SBOM is generated from the five shipped `.nupkg` files, their embedded NuGet `.nuspec` dependency declarations, and the resolved .NET 10 dependency graphs in each package project's `obj/project.assets.json`. The generator follows dependencies reachable from each shipped package's declared NuGet dependencies, so build-only tools are not reported as consumer dependencies unless they are also reachable from a published package's declared dependency graph.

The generator validates the SBOM against the official CycloneDX 1.6 JSON Schema and fails if a shipping package is missing, the SBOM's package SHA-256 does not match its actual `.nupkg`, a direct NuGet dependency is missing, a dependency graph edge points at a missing component, or the restored graph cannot resolve an expected package dependency.

Run it after restoring, building, and packing the solution:

~~~bash
dotnet restore BackendSafetyKit.slnx
dotnet build BackendSafetyKit.slnx --configuration Release --no-restore
dotnet pack BackendSafetyKit.slnx --configuration Release --no-build --output ./artifacts
python -m pip install --disable-pip-version-check -r scripts/requirements-sbom.txt
curl --fail --silent --show-error --location https://cyclonedx.org/schema/bom-1.6.schema.json --output /tmp/cyclonedx-bom-1.6.schema.json
python scripts/generate_release_sbom.py ./artifacts --schema /tmp/cyclonedx-bom-1.6.schema.json
(cd ./artifacts && sha256sum ./*.nupkg ./*.snupkg ./backend-safety-kit.sbom.json > SHA256SUMS.txt && sha256sum --check SHA256SUMS.txt)
~~~

## Artifact verification after release

The protected tag workflow generates `backend-safety-kit.sbom.json` and `SHA256SUMS.txt`, then creates GitHub build-provenance attestations for the files under `artifacts/` before publishing the NuGet packages. It does not publish from ordinary pull requests.

Download the SBOM/checksum artifact and all package artifacts from the same release workflow run into one `artifacts/` directory, then run:

~~~bash
cd artifacts
sha256sum --check SHA256SUMS.txt
gh attestation verify BackendSafetyKit.0.1.0-preview.1.nupkg --repo codelovingcat/Backend-Safety-Kit
gh attestation verify backend-safety-kit.sbom.json --repo codelovingcat/Backend-Safety-Kit
gh attestation verify SHA256SUMS.txt --repo codelovingcat/Backend-Safety-Kit
~~~

Replace the example NuGet filename with the package version being inspected. Verification requires a GitHub CLI version that supports `gh attestation verify` and access to the repository's artifact attestations. Confirm the tag workflow succeeded and verify each package ID/version on NuGet.org; configuration alone does not prove publication completed.
