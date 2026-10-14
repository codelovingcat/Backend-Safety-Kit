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
~~~

The validator uses Python's standard library only. It inspects the actual archives rather than assuming that a successful dotnet pack necessarily produced complete consumer packages.

Package publishing credentials and publishing actions are intentionally outside the build and validation workflow. CI checks artifacts but does not publish packages or make release-time network calls.
