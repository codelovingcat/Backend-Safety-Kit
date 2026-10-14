# Changelog and Release Notes Workflow

## During development

1. Keep user-visible changes under the Unreleased heading in CHANGELOG.md.
2. Organize entries by Added, Changed, Deprecated, Removed, Fixed, and Security.
3. Write entries for package consumers, not as a raw list of commit messages.
4. Include configuration and migration notes when public APIs, defaults, dependencies, or target frameworks change.

## Preparing a release

1. Review merged pull requests and the automatically generated draft notes from GitHub's release generator.
2. Reconcile the draft with CHANGELOG.md: remove internal maintenance, combine duplicate descriptions, and call out behavior changes and security fixes.
3. Move the verified Unreleased entries into a dated version heading, add a fresh empty Unreleased section, and review links and spelling.
4. Ensure the tag and package versions agree with the [versioning policy](versioning-policy.md).
5. Publish release notes only after package CI and security checks are green.

## GitHub configuration

The repository's .github/release.yml groups pull requests into Breaking Changes, Security, Features, Bug Fixes, Dependencies, and Documentation/Developer Experience. Use the labels when opening or merging pull requests. The generated notes are a draft for maintainer review; they do not replace the curated changelog.

Do not publish a NuGet package automatically from a changelog edit. Package publishing is an explicit release operation and has its own workflow and credentials.
