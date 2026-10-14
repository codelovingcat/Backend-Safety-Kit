# NuGet Release Readiness

Each shipping package must provide stable package identity, a meaningful description, authors, tags, a project URL, Git repository metadata, an explicit license file and a packaged README. Optional integrations remain separate packages: the core package must not acquire ASP.NET Core or OpenTelemetry runtime dependencies merely because those integrations exist elsewhere in the solution.

The CI pack validation checks the three expected package IDs, their net10.0 assemblies, XML documentation, README, license, repository URL and commit, and dependency boundaries. It also requires a matching .snupkg with portable PDB symbols for each package.

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
