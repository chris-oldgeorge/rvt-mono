# Source-reference compatibility-feed symlink fix

## Scope

- `scripts/build-mono.sh` now removes the package-validation compatibility
  path before recreating it as a link to the active `RVT_PACKAGE_FEED_DIR`.
  This replaces either a stale directory or a dangling/stale symlink.
- The package-sequence test saves and restores any pre-existing compatibility
  path. It creates a temporary-feed link, removes that feed to make the link
  dangling, then starts a second build against a replacement feed. Its cleanup
  removes only the test-created path before restoring the original path, so a
  fake feed cannot poison a later real build.
- `project_state.md` records the deterministic compatibility-path behavior and
  distinguishes the remaining imported architecture and migration-contract
  pre-mono path assumptions.

## Regression evidence

The new second-startup assertion was run before the shell-script change. It
failed at `ln` with `File exists`, which is the reported dangling-link failure.
After replacement, the same test runs the source-boundary guard followed by
two complete fake-dotnet build-script sequences; the second starts from the
dangling temporary link and ends with the compatibility path targeting the
replacement feed.

## Verification

- `tests/verify-rvt-common-source-boundary.test.sh` — passes the source-boundary
  guard and local package prerequisite/clean-path sequence.
- `tests/verify-rvt-common-source-boundary-regression.test.sh` — passes.
- `dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj --no-restore --nologo`
  — 8 passed, 0 failed.
- `bash -n scripts/build-mono.sh tests/verify-rvt-common-source-boundary.test.sh tests/verify-rvt-common-source-boundary-regression.test.sh`
  and `git diff --check` — pass.
