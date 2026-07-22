# RVT Common Monitor Source Removal Design

**Status:** Approved for implementation planning
**Date:** 2026-07-17
**Monitor branch:** `codex/rvt-common-private-nuget-migration`
**Authoritative package repository:** `RVT-Group-LTD/rvt-reporting`
**Package train:** exact immutable `0.2.0-rc.1`

## Context

The existing migration branch has already converted the monitor consumers from local project references to the private `Rvt.Monitor.Common`, `Rvt.Monitor.Common.Infrastructure`, and `Rvt.Monitor.IntegrationTesting` packages. It also contains credential-free source mapping, exact central versions, lock files, secure BuildKit restores, package-consumer CI, release-export checks, runtime package inventory, and accepted monitor RC evidence.

The retained `rvt-monitor-common/` directory is now a rollback checkpoint rather than an active consumer dependency. The earlier cross-repository migration plan deferred deleting it until stable `0.2.0` promotion after portal validation. The user has now explicitly selected source removal while the monitor repository remains pinned to the already validated immutable `0.2.0-rc.1` train.

## Goals

- Make every actual Common consumer use the exact private packages.
- Remove all monitor-owned Common, Infrastructure, and IntegrationTesting source projects.
- Remove local common projects from every active solution file.
- Prevent project-reference, conditional-source, version-drift, and test-package-leakage regressions.
- Keep local, CI, Docker, and curated-release restores credential-safe and package-only.
- Update active documentation to point to `RVT-Group-LTD/rvt-reporting` and immutable package/release artifacts.
- Preserve historical specifications, evidence, and Git history as the audit trail.

## Non-goals

- Adding RVT packages to projects that do not consume Common APIs.
- Changing public APIs, monitor behavior, configuration keys, entity mappings, database schemas, or provider behavior.
- Publishing a replacement package version.
- Promoting `0.2.0-rc.1` to stable `0.2.0`.
- Modifying the portal repositories or their package adoption state.
- Deleting unrelated worktrees or branches.
- Adding a source/package fallback switch.

## Dependency Boundary

Only direct consumers receive package references:

| Consumers | Required package references |
| --- | --- |
| AirQ, MyATM, Omnidots, Svantek, and Reporting runtime applications | `Rvt.Monitor.Common` and `Rvt.Monitor.Common.Infrastructure` |
| `Rvt.Reporting.Messaging` and `Rvt.Reporting.Storage` | `Rvt.Monitor.Common` |
| AirQ, MyATM, Omnidots, and Svantek test projects | `Rvt.Monitor.IntegrationTesting` with `PrivateAssets="all"` |
| Reporting tests | `Rvt.Monitor.Common` and `Rvt.Monitor.IntegrationTesting` with `PrivateAssets="all"` |
| Reporting Core, Reporting PDF, and other non-consumers | No RVT Common package reference |

The existing root `Directory.Packages.props` remains the sole version authority for the three synchronized packages. Root `NuGet.config` continues mapping `Rvt.Monitor.*` exclusively to GitHub Packages and public dependencies to NuGet.org. No consumer project may contain a version on its RVT package reference.

## Authentication and Build Paths

Tracked files contain package endpoints and versions but no credentials. Local restores receive `NuGetPackageSourceCredentials_rvt` only in the invoking process. Package-consumer CI uses repository-scoped `GITHUB_TOKEN` access with `packages: read`. Each production Dockerfile exposes the same NuGet credential only through its BuildKit `nuget_credentials` secret mount and publishes without persisting the value in an image layer, environment, build argument, file, or log.

The curated client release includes `NuGet.config`, `Directory.Packages.props`, lock files, build scripts, and application source. It excludes `rvt-monitor-common`, internal migration documents, project state, and credentials. Missing package credentials fail explicitly; normal builds never fall back to local source.

## Source Removal

Implementation starts with a red architecture test that asserts the local `rvt-monitor-common` directory does not exist. The test must fail while the rollback checkpoint remains. The boundary suite is then updated to inspect only the active checkout and to ignore `.worktrees`, `.git`, `bin`, and `obj` directories.

All six retired projects are removed from `rvt-monitors.sln` and from any monitor-specific solution that still lists them:

- `Rvt.Monitor.Common`
- `Rvt.Monitor.Common.Infrastructure`
- `Rvt.Monitor.CommonTests`
- `Rvt.Monitor.Common.InfrastructureTests`
- `Rvt.Monitor.IntegrationTesting`
- `Rvt.Monitor.IntegrationTesting.Tests`

The complete `rvt-monitor-common/` tree is then deleted. Common-owned implementation, tests, migrations, and package construction remain authoritative in `RVT-Group-LTD/rvt-reporting` and its immutable release artifacts; none are copied back into the monitor repository.

Active READMEs, observability guidance, container-build guidance, migration instructions, and the client-release runbook are updated when they currently direct users to local Common source. Historical specifications, plans, and evidence remain unchanged except for explicit superseding state notes.

## Architecture Enforcement

The final boundary checks must prove:

- `rvt-monitor-common/` is absent from the active checkout.
- No consumer `.csproj`, `.props`, or `.targets` file references that path.
- No active solution lists a Common or IntegrationTesting project.
- No `UseLocalRvtCommon` or equivalent conditional source switch exists.
- Every direct consumer has the package references in the approved dependency matrix.
- All three package versions are exact and synchronized at `0.2.0-rc.1`.
- `Rvt.Monitor.IntegrationTesting` occurs only in test projects and always has `PrivateAssets="all"`.
- Runtime outputs contain Common and Infrastructure at `0.2.0-rc.1` and exclude IntegrationTesting.
- Scans exclude sibling worktrees so unrelated historical branches cannot create false violations.

## Verification

The final release gate runs from the active migration worktree with runtime-only package credentials:

1. Locked restore of `rvt-monitors.sln`.
2. Repository formatter verification without restore.
3. Root solution build without restore, with zero warnings and errors.
4. Full solution tests, including package-boundary tests.
5. Private-package policy verification.
6. All five production container builds through BuildKit secrets.
7. Runtime `.deps.json` inventory for synchronized versions and test-package exclusion.
8. Docker Compose configuration validation.
9. Curated client-release export and absence scan.
10. Solution/reference scans and `git diff --check`.

No live vendor, email, SMS, production database, migration, or deployment operation is part of this change.

## Error Handling

A restore authentication failure stops verification and reports the missing runtime credential without printing it. A package hash or lock mismatch stops the cutover rather than regenerating locks against an unknown artifact. Any build, test, container, runtime-inventory, solution-scan, or release-export failure blocks source-removal completion and merge. Package versions are never overwritten or floated.

## Rollback

The package conversion and source deletion remain separate Git commits. Before merge, the source-reference rollback is the recorded pre-conversion commit `924ed20deee37fba17452612ae40eae8e0fe6168`, whose restored solution was already verified to build. After merge, rollback reverts the source-removal commit and, if necessary, the package-conversion sequence; normal forward development does not restore a permanent local-source switch.

This design deliberately accepts the operational limitation that `0.2.0-rc.1` is a prerelease package. Stable promotion remains a later cross-repository release task after separately accepted portal evidence.

## Acceptance Criteria

- All actual consumers match the approved package-reference matrix.
- All three private packages resolve exactly to `0.2.0-rc.1` from GitHub Packages.
- No unrelated project receives an RVT package reference.
- The active checkout and every active solution contain no local Common project or source tree.
- Local, CI, Docker, and curated-release paths are package-only and credential-safe.
- Architecture checks prevent local-source and version-drift regressions.
- Locked restore, formatting, build, tests, containers, runtime inventory, Compose, release export, scans, and diff checks pass with fresh evidence.
- Active documentation identifies `RVT-Group-LTD/rvt-reporting` as the source authority and records the intentional RC deletion decision.
- Historical design, implementation, release, and rollback evidence remains available without secret values.
