# Versioning

Kairos uses `Nerdbank.GitVersioning` (NBGV) for assembly and build version metadata.

## Source of truth

The version source of truth is [version.json](../version.json) at the repository root.

- `version` defines the next release line.
- `master` and `main` are treated as public-release branches.
- Tags that look like `v1.2.3` are also treated as public releases.

[Directory.Build.props](../Directory.Build.props) applies NBGV to all SDK-style projects in the solution, so project files should not set `Version`, `AssemblyVersion`, `FileVersion`, or `InformationalVersion` manually unless there is a specific exception.

## Day-to-day behavior

NBGV derives version metadata from git history.

- Builds from `master` or `main` use the stable release line from [version.json](../version.json).
- Builds from other branches remain unique and traceable through git-derived prerelease/build metadata.
- Assemblies include informational version data that can be traced back to the commit that produced them.

The app's Settings page reads the generated assembly informational version at runtime, so the displayed version now matches the actual build output.

## Bumping the version

When starting the next release line, update the `version` field in [version.json](../version.json).

Examples:

- `1.0` for the current stable line
- `1.1` for the next minor line
- `2.0` for the next major line

Commit that change before producing release builds so the version height is calculated from the correct point in history.

## Release workflow

Typical flow:

1. Merge the intended changes.
2. Update [version.json](../version.json) if you are moving to a new release line.
3. Build from `master` or `main`.
4. Optionally create a matching git tag such as `v1.0.0`.

Because `cloudBuild.buildNumber.enabled` is enabled, CI can also use the generated NBGV version in the cloud build number.

## Notes

- Keep [CHANGELOG.md](../CHANGELOG.md) aligned with release intent, but do not use it as the version source.
- Avoid hardcoded version strings in the UI or project files.
