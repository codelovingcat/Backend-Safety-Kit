# NuGet Publishing Workflow

The NuGet publishing workflow runs only when a v-prefixed version tag is pushed to the upstream repository. It validates that the tag agrees with all three shipping package project versions, then restores, builds, tests, packs, validates, and publishes both package and symbol archives.

Before the first release, configure a GitHub Actions environment named nuget-production, add NUGET_API_KEY as an environment secret, and apply appropriate reviewer/branch restrictions to that environment. Do not put the API key in the repository, workflow YAML, command output, or issue comments.

The job has read-only repository permissions and does not run for forks. A tag is a release action: review the release notes, changelog, package version, CI results, and dependency/security findings before pushing it. The workflow uses --skip-duplicate so retrying an already uploaded package does not fail solely because NuGet.org already has that version; a different package with the same version cannot be overwritten.

If a release fails before upload, fix the source and push a new valid version tag after review. If a package has already been published, follow the project versioning policy rather than trying to replace that version.
