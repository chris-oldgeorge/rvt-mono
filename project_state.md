# Project State

## RVT Mono-Repository Bootstrap - 2026-07-22

- Workspace: `/Users/oldgeorge/Documents/rvt-mono`
- Status: RVT common source-reference migration final review fixes implemented.
  Active monitor and portal consumers are source-referenced, their 12 tracked
  locks reflect the source graph, and the two package-validation consumers
  restore locally packed artifacts through artifact-scoped validation locks.
- Design: `docs/superpowers/specs/2026-07-22-rvt-mono-repository-design.md`
- Plan: `docs/superpowers/plans/2026-07-22-rvt-mono-repository-bootstrap.md`
- Requested outcome: fresh unified Git history and a shared root solution for
  `rvt-monitors`, `rvtportal-spa-alpha`, `rvt-reporting`, and
  `rvt-reporting-new`.
- Intended modules: `apps/monitors`, `apps/portal`,
  `libs/rvt-monitor-common`, and `services/reporting`.
- Root solution: `Rvt.Mono.slnx`.
- Approved design: `docs/superpowers/specs/2026-07-22-rvt-common-source-reference-design.md` changes active consumers to source project references, while package-validation remains package-based against locally packed artifacts. This is an explicit decision to review if independent package consumption becomes required again.
- Implemented plan: `docs/superpowers/plans/2026-07-22-rvt-common-source-reference-migration.md`.
- Pending approved design: `docs/superpowers/specs/2026-07-22-documentation-consolidation-design.md` consolidates all module Markdown into root `docs/`, retaining root/module README and AGENTS entry points.
- Pending implementation plan: `docs/superpowers/plans/2026-07-22-documentation-consolidation.md`.
- Aggregate project count: 38 projects across all four module roots.
- Important boundary: active application consumers use the in-repository RVT
  source projects; only `libs/rvt-monitor-common/package-validation` consumes
  RVT packages. Do not merge reporting implementations or database schemas.
- Imported source snapshots:
  - `apps/monitors` from `chris-oldgeorge/rvt-monitors` at
    `5935f40614073afa6c4ef954db1308a72a5f8f2b`.
  - `apps/portal` from `chris-oldgeorge/rvtportal-spa-alpha` at
    `8355070f094a591297c9f8468057f44a6c876986`.
  - `libs/rvt-monitor-common` from `RVT-Group-LTD/rvt-reporting` at
    `f00d5b8a320945ed08e248da8641ca0c3f7e3b82`.
  - `services/reporting` from `chris-oldgeorge/rvt-reporting-new` at
    `e602e8317e35bd94a1eb4dd017759b91713ea111`.
- Import staging directory: `/private/tmp/rvt-mono-import.2w115l` (retained
  through Task 3 final verification).
- Import verification: all staged repositories were checked out detached at
  their exact manifest revisions; imported trees checksum-match the staged
  content with `.git` excluded; no nested `.git` directory exists below the
  module roots.
- Known environment note: authenticated GitHub metadata access was available;
  source clone/restore access must be verified during implementation. Never
  record credentials in this repository.
- Task 1 guard: `.gitignore` excludes generated files, environment files, and
  `.superpowers/sdd/` controller state. `docs/imports/source-manifest.md` pins
  the four approved source snapshots. Repository bootstrap commits through the
  source import are design `1327b84`, plan `0abf895`, guard `ae65789`, and
  source import `31d168f`.
- Task 3 guard: `tests/verify-mono-solution.test.sh` runs
  `scripts/verify-mono-solution.sh`. It compares normalized, sorted module
  `*.csproj` paths with the normalized, sorted `dotnet sln Rvt.Mono.slnx list`
  paths, requires matching project counts and per-module representation, and
  enforces exact project placement under `Apps/Monitors`, `Apps/Portal`,
  `Libraries/RVT Monitor Common`, and `Services/Reporting`, with test projects
  in each module's corresponding `Tests` solution folder.
- Source-reference migration Task 1: `tests/verify-rvt-common-source-boundary.test.sh`
  invokes `scripts/verify-rvt-common-source-boundary.sh`. The guard declares
  the three shared source projects, requires the approved app/portal project
  references, rejects their common-package references, and preserves
  package-only validation consumers. Each package-validation project rejects
  source references to all three shared projects while retaining its required
  package references.
- Source-reference migration Task 2: the five monitor hosts now directly
  reference `Rvt.Monitor.Common` and `Rvt.Monitor.Common.Infrastructure`; the
  five current monitor test consumers reference `Rvt.Monitor.IntegrationTesting`
  (with `ReportingMonitorTests` retaining its direct Common edge); the reporting
  messaging/storage projects directly reference Common; and the portal host
  directly references Infrastructure. MSBuild now supplies build ordering for
  these active graphs.
- Monitor central package variables `RvtCommonVersion`,
  `RvtCommonInfrastructureVersion`, and `RvtIntegrationTestingVersion`, plus
  their three `PackageVersion` entries, were removed. Active monitor and portal
  NuGet configs now retain only nuget.org; the shared library NuGet config maps
  `Rvt.*` to the root `artifacts/packages` feed for package validation.
- Package-validation remains intentionally package-based at `0.2.0-rc.1`.
  `scripts/build-mono.sh` packs exactly `Rvt.Monitor.Common`,
  `Rvt.Monitor.Common.Infrastructure`, and
  `Rvt.Monitor.IntegrationTesting` to `artifacts/packages`, validates the two
  package consumers from an isolated `artifacts/nuget-packages` cache, restores
  `Rvt.Mono.slnx`, builds with `--no-restore`, and tests with `--no-build`.
  Its legacy package-validation compatibility path is deterministically replaced
  on each run, so a stale directory or symlink from a temporary test feed cannot
  block the next build.
  Normal builds opt those two consumers into per-project locks under
  `artifacts/validation-locks`; freshly emitted NuGet archives have different
  content hashes, so this keeps their committed `0.2.0-rc.1` package-policy
  locks and all other tracked locks unchanged. The shared library NuGet
  configuration maps `Rvt.*` only to the root local feed and retains nuget.org
  for third-party packages; GitHub Packages and credentials are not used.
- Verification results:
  - `tests/verify-mono-solution.test.sh` and
    `tests/verify-mono-layout.test.sh` pass.
  - `dotnet sln Rvt.Mono.slnx list` reports all 38 module projects.
  - The source-boundary guard passes after the active-consumer conversion.
  - Both active module solutions restore successfully and their restore graphs
    reach the shared source projects. Verbose network traces contacted only
    nuget.org; the preserved shared-library config still appears in NuGet's
    configured-feed summary.
  - Portal restore reports four existing NU1903 high-severity advisories for
    `System.Security.Cryptography.Xml` 10.0.7; remediation is outside Task 2.
  - During Task 2, `dotnet restore Rvt.Mono.slnx` was blocked by private package access:
    GitHub Packages returns HTTP 401 for the RVT organization feed. Cached RVT
    `0.2.0-rc.1` packages also produce NU1403 content-hash validation errors.
  - During Task 2, `dotnet build Rvt.Mono.slnx --no-restore --nologo` exited
    with 16 errors from the same NU1301/NU1403 package state; unaffected
    projects compiled.
  - Package feeds and dependency declarations were not changed in Task 2.

## RVT Common Local Package Validation - 2026-07-22

- The missing-artifact regression check records the expected pre-restore
  failure and names `Rvt.Monitor.Common.0.2.0-rc.1.nupkg`; its mutation RED run
  catches `RuntimeConsumer` restore before artifact verification. Its GREEN run
  proves neither package-validation consumer nor aggregate restore can occur
  before all three packages exist.
- The local package sequence restores and packs the three shared projects,
  restores all 38 aggregate projects from nuget.org plus
  `artifacts/packages`, and builds the aggregate solution with 0 errors. The
  existing four NU1903 advisories for `System.Security.Cryptography.Xml`
  10.0.7 remain outside this task.
- The package artifact suite passes 8/8. RuntimeConsumer and TestConsumer stay
  package-based at `0.2.0-rc.1`; build-time artifact locks are generated under
  `artifacts/validation-locks`. The 12 active monitor consumer locks were
  regenerated from their source-reference graphs: none retain a direct RVT
  package, and normalized comparison proves all non-RVT dependency data is
  unchanged.
- Focused source-boundary architecture verification passes 12/12 for monitors
  and 7/7 for the portal. The monitor suite now enforces the approved source
  matrix, source-consumer lock shape, local validation boundary, version, and
  feed policy. The portal suite now requires the Infrastructure source project
  and a nuget.org-only credential-free configuration.
- A full build-sequence diff fingerprint is identical before and after restore,
  pack, package validation, aggregate restore/build, and the nonzero aggregate
  test stage. A normal successful run therefore introduces no tracked lockfile
  changes.
- The aggregate test stage remains nonzero for imported test assumptions that
  are outside this migration. Database-backed tests report exactly:
  `System.InvalidOperationException: Set RVT__POSTGRES_INTEGRATION_CONNECTION
  to run PostgreSQL integration tests.` Other imported architecture and
  migration-contract test assumptions still
  resolve pre-mono paths, including
  `/Users/oldgeorge/Documents/rvt-mono/reportingmonitor/ReportingMonitor/api`
  and `/Users/oldgeorge/Documents/rvt-mono/rvt-monitors.sln`, which do not exist
  in the aggregate layout. No package versions or test behavior were changed
  to mask these failures.

## Documentation Consolidation Task 1 - 2026-07-22

- Current state: the documentation move guard and exhaustive manifest are
  defined; no documentation has moved and no links have been rewritten yet.
- File structure added by this task:
  - `docs/documentation-move-manifest.md` maps all 122 tracked non-entry module
    Markdown sources to unique destinations below the root `docs/` hierarchy.
  - `scripts/verify-documentation-layout.sh` enforces the manifest, retained
    entry points, destination presence, absence of module documentation, and
    absence of stale links to moved sources.
  - `tests/verify-documentation-layout.test.sh` is the strict-mode executable
    wrapper for the guard.
- Retained entry points are the root `README.md`, the four module-root
  `README.md` files, and `apps/monitors/AGENTS.md` plus
  `apps/portal/AGENTS.md`.
- Guard variables: `repo_root` is derived from the script location;
  `manifest_path` is `docs/documentation-move-manifest.md`;
  `expected_manifest_entries=122`; `failures` counts issue groups; `sources`
  and `destinations` hold parsed manifest rows; `retained_paths` holds the seven
  required entry points; and `missing_sources` scopes stale-link checks to
  documents that have actually moved.
- Expected verification state: `tests/verify-documentation-layout.test.sh`
  exits nonzero with exactly two issue groups—122 non-entry Markdown sources
  remain below module roots and all 122 manifest destinations are absent.
  Task 2 is responsible for resolving those expected failures with Git-aware
  moves.
