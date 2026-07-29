# Full Monorepo Client Release Runbook

This runbook documents the private control-plane process for publishing a
reviewable RVT monorepo snapshot to the client repository.

## Target

- Source repository: `RVT-Group-LTD/rvt-mono`
- Client repository: `RVT-Group-LTD/rvt-monitors`
- Client URL: `https://github.com/RVT-Group-LTD/rvt-monitors.git`
- Client branch: `release-candidate`

The client branch contains a single orphan release commit. It does not preserve
superseded payloads or files in its Git history.

## Release Boundary

The payload includes the complete committed monorepo by default:

- `.github/`
- `apps/`
- `libs/`
- `eng/`
- `scripts/`
- `tests/`
- client-facing and operational `docs/`
- root solution, SDK, build, editor, Docker, and repository files

The private policy at
`docs/release/client-release-exclusions.txt` removes:

- agent instructions and session state
- `project_state.md`
- `.agents/`, `.codex/`, and `.codegraph/`
- internal plans, evidence, history, reviews, and release mechanics
- local environment and development settings
- private-key and certificate files
- generated build, dependency, coverage, IDE, and analysis output

The exporter reads an exact Git commit through `git archive`. Untracked files,
ignored files, unstaged edits, and staged-but-uncommitted contents cannot enter
the release.

The verifier scans the final payload for blocked paths, unsafe links, special
files, high-confidence token formats, PEM private keys, credentialed URLs,
connection-string passwords, and non-placeholder assignments to recognized
secret names. It reports only a relative path and rule; it never prints the
suspected value. Empty values, runtime environment references, and explicit
documentation placeholders remain valid.

## Prerequisites

- Clean, merged `origin/main`
- .NET SDK selected by `global.json`
- Node.js 24 and npm
- Docker with Compose support
- Authenticated Git and GitHub CLI access
- Push access to `RVT-Group-LTD/rvt-monitors`

Do not store runtime credentials in the source tree to make validation pass.
Use environment variables, user secrets, platform secrets, or ignored local
files.

## Validate the Source

From the monorepo root:

```bash
git fetch origin main
git status --short --branch
git rev-parse origin/main

bash tests/verify-client-release.test.sh
bash tests/export-client-release.test.sh
bash tests/publish-client-release.test.sh

bash scripts/verify-mono-layout.sh
bash scripts/verify-mono-solution.sh
bash scripts/verify-documentation-layout.sh
bash scripts/verify-rvt-common-source-boundary.sh
bash scripts/verify-engineering-standards.sh

dotnet restore Rvt.Mono.slnx --locked-mode
dotnet build Rvt.Mono.slnx --no-restore
dotnet test Rvt.Mono.slnx --no-build
```

Run the Portal client checks with Node.js 24:

```bash
cd apps/portal/RvtPortal.Client
npm ci
npm run lint
npm test -- --run
npm run build
```

Supply only disposable PostgreSQL/TimescaleDB configuration when integration
tests require it. Report environmental skips separately; do not describe them
as passing.

## Create a Local Export

Resolve and record the source commit once:

```bash
source_ref="$(git rev-parse origin/main)"
```

Create the export:

```bash
scripts/export-client-release.sh \
  --source-ref "$source_ref" \
  --export-dir /private/tmp/rvt-monorepo-client-release
```

Review:

```bash
cat /private/tmp/rvt-monorepo-client-release/RELEASE_SOURCE.json
wc -l /private/tmp/rvt-monorepo-client-release/RELEASE_MANIFEST.txt
find /private/tmp/rvt-monorepo-client-release -mindepth 1 -maxdepth 1 -print
```

`RELEASE_SOURCE.json` must contain the exact full source commit and its commit
timestamp. `RELEASE_MANIFEST.txt` is a sorted list of every other payload file.

## Prepare Without Publishing

The prepare-only pass observes the current remote SHA, regenerates the export,
creates the orphan commit, and prints the changed paths without pushing:

```bash
scripts/publish-client-release.sh \
  --target-repo https://github.com/RVT-Group-LTD/rvt-monitors.git \
  --branch release-candidate \
  --source-ref "$source_ref" \
  --export-dir /private/tmp/rvt-monorepo-client-release \
  --work-dir /private/tmp/rvt-monorepo-client-publish \
  --verify-dir /private/tmp/rvt-monorepo-client-verify \
  --prepare-only
```

Before publication, confirm:

- the source commit equals the intended merged `origin/main`
- the observed remote SHA is still the reviewed client branch
- the top-level tree represents the complete monorepo
- deletions are limited to obsolete client payload content
- no internal-development or secret finding remains

## Publish

Run the same command without `--prepare-only`:

```bash
scripts/publish-client-release.sh \
  --target-repo https://github.com/RVT-Group-LTD/rvt-monitors.git \
  --branch release-candidate \
  --source-ref "$source_ref" \
  --export-dir /private/tmp/rvt-monorepo-client-release \
  --work-dir /private/tmp/rvt-monorepo-client-publish \
  --verify-dir /private/tmp/rvt-monorepo-client-verify
```

The publisher uses:

```text
--force-with-lease=refs/heads/release-candidate:<observed-remote-sha>
```

If another actor updates the branch after observation, the push fails rather
than overwriting that update.

After a successful push, the publisher clones the remote branch fresh, removes
only the temporary checkout's `.git` directory, reruns the payload verifier,
checks the manifest, and confirms the source commit metadata.

## Independent Verification

The publisher performs this automatically. For a manual audit:

```bash
verify_dir=/private/tmp/rvt-monorepo-client-independent-verify
git clone --depth 1 --branch release-candidate \
  https://github.com/RVT-Group-LTD/rvt-monitors.git \
  "$verify_dir"
rm -rf "$verify_dir/.git"
scripts/verify-client-release.sh --payload-dir "$verify_dir"
```

Confirm these representative paths exist:

```bash
test -f "$verify_dir/Rvt.Mono.slnx"
test -d "$verify_dir/apps/portal"
test -d "$verify_dir/apps/monitors"
test -d "$verify_dir/libs/rvt-monitor-common"
test -d "$verify_dir/tests"
test -d "$verify_dir/.github"
```

## Rollback

Rollback is a republish, not a history restore:

1. Select the previously reviewed monorepo commit.
2. Run all source validation against that commit.
3. Run prepare-only and review the complete changed-path summary.
4. Publish the selected commit with the same explicit lease protection.
5. Verify `RELEASE_SOURCE.json` contains the rollback source commit.

## Updating the Policy

When a new internal-only or secret-bearing path family is introduced:

1. Add a failing fixture to `tests/verify-client-release.test.sh` or
   `tests/export-client-release.test.sh`.
2. Update `docs/release/client-release-exclusions.txt` or the redacted content
   scanner.
3. Run all three release-tool contract suites.
4. Export the current merged main and inspect its complete manifest.
5. Merge the policy change before publishing another client release.
