# Client Release Runbook

This runbook documents the repeatable process for preparing and publishing a curated RVT Monitors source package to the client/audit repository.

## Target Repository

- GitHub repository: `RVT-Group-LTD/rvt-monitors`
- Default branch: `main`
- Recommended release candidate branch: `release-candidate`

## Release Policy

The release package is built from Git-tracked files only. It includes monitor source code, tests, Docker configuration, observability configuration, operational documentation, and the root `README.md`.

The package excludes internal agent/session state, planning material, memory files, local secrets, generated output, and release tooling. The canonical exclusion list is:

- `docs/release/client-release-exclusions.txt`

The export script also validates the generated package and fails if blocked paths remain.
The curated payload retains `NuGet.config` and `Directory.Packages.props`, which are
required to resolve the exact private `Rvt.Monitor.*` package versions during builds.
It must contain no retired Common source path and must restore those dependencies only
through GitHub Packages; a local source fallback is a release-policy failure.

## Prepare Source

From the native macOS working copy:

```bash
cd /Users/oldgeorge/Documents/rvt-monitors/rvt-monitors
git status --short --branch
dotnet test rvt-monitors.sln --no-build
```

If the source tree contains intended release-process changes, commit them before publishing the client package.

## Generate A Local Curated Export

```bash
scripts/export-client-release.sh /private/tmp/rvt-monitors-client-release
```

Expected result:

- The export folder is recreated.
- Files are copied from `git ls-files`.
- Excluded internal paths are omitted.
- `RELEASE_MANIFEST.txt` is generated.
- The validation scan reports success.

## Publish To Client Repository

```bash
scripts/publish-client-release.sh \
  --target-repo https://github.com/RVT-Group-LTD/rvt-monitors.git \
  --branch release-candidate \
  --export-dir /private/tmp/rvt-monitors-client-release
```

The publisher:

1. Regenerates the curated export.
2. Clones the target repository into `/private/tmp`.
3. Creates the requested release branch as a fresh orphan history by default, so the client/audit branch does not retain previously excluded files in Git history.
4. Replaces the branch contents with the curated export.
5. Commits the payload.
6. Force-updates the release branch with `--force-with-lease`.

## Verification

After publishing, verify the remote branch:

```bash
tmp=/private/tmp/rvt-monitors-client-verify
git clone --branch release-candidate https://github.com/RVT-Group-LTD/rvt-monitors.git "$tmp"
find "$tmp" -type f \( \
  -name AGENTS.md -o \
  -name project_state.md -o \
  -path '*/docs/superpowers/*' -o \
  -path '*/docs/monitor-data-access-migration.md' -o \
  -path '*/docs/release/*' -o \
  -path '*/.codegraph/*' \
\) -print
test -f "$tmp/README.md"
test -f "$tmp/rvt-monitors.sln"
test -f "$tmp/docker-compose.yml"
test -f "$tmp/NuGet.config"
test -f "$tmp/Directory.Packages.props"
```

The blocked-path scan must print nothing. The `test -f` commands must exit successfully,
and the payload must contain no retired Common source directory.
Before running package restores or container builds on the release branch, confirm that
`RVT-Group-LTD/rvt-monitors` has read access to all three private packages:
`Rvt.Monitor.Common`, `Rvt.Monitor.Common.Infrastructure`, and
`Rvt.Monitor.IntegrationTesting`. GitHub Actions should use the repository's authorized
`GITHUB_TOKEN`; if a separate token is necessary, store it only as the
`RVT_PACKAGES_READ_TOKEN` Actions secret with `read:packages`, and store its account name
as `RVT_PACKAGES_READ_USER`. The personal `chris-oldgeorge/rvt-monitors` mirror uses
these two repository secrets because its repository `GITHUB_TOKEN` cannot read packages
owned by `RVT-Group-LTD`.

## Updating The Release Policy

When a new internal-only directory or file type is introduced:

1. Add the pattern to `docs/release/client-release-exclusions.txt`.
2. Re-run `scripts/export-client-release.sh`.
3. Confirm `RELEASE_MANIFEST.txt` contains only intended client/audit files.
4. Commit the policy change in the source repository before publishing.
