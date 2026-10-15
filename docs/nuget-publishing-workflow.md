# NuGet Publishing Workflow

The NuGet publishing workflow runs only when a v-prefixed version tag is pushed to the upstream repository. It validates that the tag agrees with all five publishable package project versions, then restores, builds, tests, packs, validates, and publishes both package and symbol archives.

Before the first release, configure the GitHub Actions environment named `nuget-production` and apply appropriate deployment tag and reviewer restrictions. On nuget.org, create a Trusted Publishing policy for package owner `Urem`, GitHub owner `codelovingcat`, repository `Backend-Safety-Kit`, workflow file `publish-nuget.yml`, and environment `nuget-production`. Limit the policy to the five package IDs listed below and the required Push scope. The workflow uses GitHub OIDC to obtain a short-lived NuGet API key immediately before publishing; no long-lived `NUGET_API_KEY` secret is stored in GitHub.

The job has read-only repository permissions and does not run for forks. A tag is a release action: review the release notes, changelog, package version, CI results, and dependency/security findings before pushing it. The workflow uses --skip-duplicate so retrying an already uploaded package does not fail solely because NuGet.org already has that version; a different package with the same version cannot be overwritten.

If a release fails before upload, fix the source and push a new valid version tag after review. If a package has already been published, follow the project versioning policy rather than trying to replace that version.
