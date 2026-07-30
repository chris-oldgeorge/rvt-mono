# Project State

Resume instruction: `Read project_state.md to get up to speed`.

This file holds only the **current** state. The append-only checkpoint log it
used to be (40+ dated sections) is archived at
[docs/history/project-state/2026-07-checkpoint-log.md](docs/history/project-state/2026-07-checkpoint-log.md).
Add new detail here by *replacing* stale statements, not appending; move
superseded narratives to the archive.

## Current state — 2026-07-30

### Full code-review checkpoint

- The full review covered committed `main` at
  `923184fba21d84e20ec5e8559c8ef606efec4637`. The bounded-response merge
  immediately preceding the Sonar remediation is
  `f6c06a9c98245cda144f324870727ff40370e20a`
  (`Merge bounded download limits`).
- The bounded-response remediation and its regression tests are merged into
  current `main`.
- Current top-level structure:
  - `apps/monitors`: AirQ, MyAtm, Omnidots, Reporting, Svantek and shared
    monitor-host applications.
  - `apps/portal`: Portal application, API host, React client, storage and
    integration-test projects.
  - `libs`: shared communication, integration-testing and monitor-common
    libraries.
  - `eng`, `scripts`, and `tests`: build configuration, repository guards,
    release tooling and shell contract tests.
  - `docs`: architecture, operations, development, history, plans and reviews.
- Full verification passed: locked restore; Release build with zero warnings
  and errors; 2,374/2,374 .NET tests with no skips; 163/163 Portal unit tests;
  six Portal Chromium end-to-end tests; Portal production build; all root
  guards, all `tests/*.test.sh` contracts and the complete engineering
  standards inventory. Portal lint has zero errors and one existing
  React Fast Refresh warning in `DataViewPanels.tsx`.
- The security review covered all 1,548 tracked files. Its four
  bounded-response findings are remediated in the current worktree: AirQ,
  Omnidots and Svantek JSON responses are capped at 4 MiB; Svantek recordings
  are capped at 64 MiB; and Reporting logos retain their 2 MiB contract but
  now enforce it during streaming. Declared oversized bodies are rejected
  before reading, chunked bodies stop at the boundary, and caller
  cancellation remains effective. No critical or high severity security
  issue was found.
- Highest-priority correctness findings are two Svantek UTC defects:
  `StoreMonitorsHandler` assigns `DateTime.Now` to a PostgreSQL
  `timestamp with time zone`, and the latest-notification query binds a
  `DateTime.Now` cutoff to the same type. Npgsql rejects local-kind
  `DateTime` values in both paths.
- Other open review findings: the shared Portal date formatter throws for
  malformed non-empty timestamps; malformed site-list sort directions fall
  back to ascending instead of the intended descending order; the generated
  Portal OpenAPI contract is stale enough that Help DTOs are maintained as
  local schema-gap types; and a component module exports a date helper despite
  an existing dedicated date module.
- Review-time database variables were runtime-only:
  `RVT__POSTGRES_INTEGRATION_CONNECTION` and
  `RVT__POSTGRES_INTEGRATION_CONNECTION`, both pointed at the disposable local
  PostgreSQL instance on port `55432`. No production credential was used or
  persisted. The verified toolchain is .NET SDK `10.0.302` and Node
  `24.18.0`.

### Sonar security and reliability remediation

- The Sonar remediation work was based on
  `f6c06a9c98245cda144f324870727ff40370e20a`.
- SonarCloud's latest `main` analysis is still the older
  `7976b211b27bd28de8dff4546251304c833dcc96` snapshot. It reports two
  `javascript:S4036` vulnerabilities and five reliability bugs; a new analysis
  remains pending until the manual workflow runs against updated `main`.
- The current tree remediates all seven findings: the Visual Studio Vite
  launcher invokes `robocopy.exe` by an absolute Windows system path; Portal
  problem responses observe `HttpContext.RequestAborted`; the Omnidots bounded
  reader uses an intentional unbounded control loop while retaining its
  64-KiB byte limit; repository path validation locates one invalid segment
  without a single-iteration loop; and `MyAtmMeasurementPage<T>` is owned by
  the `MyAtm.Api.Ports` namespace rather than the global namespace.
- Regression coverage includes two Portal middleware cancellation tests and a
  MyAtm namespace-ownership test. The red phase reproduced the missing
  cancellation and global-namespace defects before production changes.
- Fresh verification on this branch: locked restore passed; Release build
  passed with zero warnings and errors; the full .NET solution passed
  2,377/2,377 tests against the disposable PostgreSQL instance; Portal client
  production build passed; and Portal lint has zero errors with the one
  existing React Fast Refresh warning in `DataViewPanels.tsx`.

### Reviewable full-monorepo client release

- Current client delivery:
  [RVT-Group-LTD/rvt-monitors#1](https://github.com/RVT-Group-LTD/rvt-monitors/pull/1),
  intentionally left as a draft for client review.
- Source release commit:
  `1ba4e9d0e2c255e0b62a9c6661aff99a1fa4bbe5`.
- Source validation portability PR:
  `chris-oldgeorge/rvt-mono#80`, merged with all required checks green.
- Release changelist boundary: `a9b1bd2..eb5aa3dd`, 134 commits and 467
  changed source files.
- Approved design:
  `docs/superpowers/specs/2026-07-29-full-monorepo-client-release-design.md`.
- Implementation plan:
  `docs/superpowers/plans/2026-07-29-full-monorepo-client-release.md`.
- Client remote:
  `https://github.com/RVT-Group-LTD/rvt-monitors.git`.
- Client base branch: `release-candidate`, held at the previous release
  `7d7d8b1f74ba7c0acd77a738b29e149259f0df0f`.
- Client review branch: `agent/reviewable-build-17e2515` at
  `3b3b1f439a75129c618d3097d7c3f98c58f2fd2f`.
- Review commit tree:
  `c1716b4b41ca960aca40a3226be941a2ec19fa95`; its sole parent is the client
  base commit above.
- Prepared payload: 1,473 manifested files plus `RELEASE_MANIFEST.txt`.
- The payload is the complete committed monorepo, preserving `.github`,
  `apps`, `libs`, `eng`, `scripts`, `tests`, operational/client-facing `docs`,
  and root build files. It excludes agent/session state, internal development
  history and reviews, private release mechanics, generated output, local
  settings, and saved secrets.
- The private control-plane files are:
  - `docs/release/client-release-exclusions.txt`
  - `scripts/verify-client-release.sh`
  - `scripts/export-client-release.sh`
  - `scripts/publish-client-release.sh`
  - `tests/verify-client-release.test.sh`
  - `tests/export-client-release.test.sh`
  - `tests/publish-client-release.test.sh`
- Exporter interface:
  `scripts/export-client-release.sh --source-ref REF --export-dir DIR`.
  It reads `REF` through Git objects and emits `RELEASE_SOURCE.json` plus
  `RELEASE_MANIFEST.txt`; working-tree and untracked contents are never copied.
- Publisher interface:
  `scripts/publish-client-release.sh --target-repo URL --branch NAME
  --source-ref REF --export-dir DIR --work-dir DIR --verify-dir DIR
  [--prepare-only]`.
- Publisher defaults:
  - target repository:
    `https://github.com/RVT-Group-LTD/rvt-monitors.git`
  - branch: `release-candidate`
  - export: `/private/tmp/rvt-monorepo-client-release`
  - work: `/private/tmp/rvt-monorepo-client-publish`
  - verification: `/private/tmp/rvt-monorepo-client-verify`
- `RVT_CLIENT_RELEASE_POLICY` can point the verifier at an alternate policy
  during isolated contract tests. `RVT_CLIENT_RELEASE_BEFORE_PUSH_HOOK` is a
  test-only race-injection hook; do not set it during a real publication.
- Final client PR checks passed: .NET in 6m01s, Portal client in 52s,
  Engineering standards in 2m03s, repository guards in 27s, and both
  change-detection jobs. The prepared payload and an independent clone both
  verified the source metadata, exact parent/tree relationship, manifest,
  required monorepo paths, internal-file exclusions, and saved-secret boundary.
- In curated payloads marked by `RELEASE_SOURCE.json`, repository guards skip
  only the intentionally excluded internal documentation layout and shell
  contract fixtures. Engineering standards retain the model, configuration,
  shell-safety, and workflow-contract checks but skip the source-development
  changed-range ratchet. Product tests and shipped architecture boundaries
  remain active.

- The consolidated report of work since the monorepo source import is
  [docs/reviews/2026-07-29-monorepo-work-report.md](docs/reviews/2026-07-29-monorepo-work-report.md).
  It covers the `31d168fd..d1fe8282` range, current structure, delivery
  metrics, verification evidence, Sonar gate state, and recommended follow-up
  order.
- The latest exhaustive finding register is
  [docs/reviews/2026-07-29-full-repo-review.md](docs/reviews/2026-07-29-full-repo-review.md),
  but it predates the durable-alert, legacy-messaging-retirement, dispatch
  policy, ratchet-paydown, and `RvtConfig` endgame merges. Re-verify any item
  against the current tree before treating it as open.
- The second dead-code sweep removed ~1,600 lines: 13 never-queried EF view
  entities plus their DbSets, fluent config, model-snapshot blocks and
  canonical-name approvals; the dead `IAlertlevelRepository` port/adapter pair
  and its registration; ~12 dead service/port members (with their cascades);
  MyAtm's compat-dead direct-delivery route (removing that monitor's last
  production `GetAwaiter().GetResult()` and its `RVT0001` NoWarn); the three
  dead `IMonitorRuntimeDefaultsResolver` DI registrations; and three dead
  `LegacyMessageKind` members. `MyAtmApi` survives deliberately — it is now
  test-only (production registration deleted, `JAN1_1970` moved to
  `DateTimeUtil`), and retiring it is a test-migration exercise, not a
  deletion.
- `main` carries the bounded reliability cleanup (PR #23), recorded in
  [docs/superpowers/plans/2026-07-29-reliability-cleanup.md](docs/superpowers/plans/2026-07-29-reliability-cleanup.md):
  Omnidots trace imports propagate caller cancellation instead of recording it
  as a monitor failure; portal optional monitor summaries and site archives keep
  their fallback behavior but now log genuine failures and propagate
  cancellation; AirQ seeds a missing watermark from the injected UTC
  `TimeProvider` and no longer carries behavior-neutral aggregate rethrow
  blocks; and the Omnidots checked-in defaults no longer contain a personal
  alert recipient or a customer serial allow-list. That PR independently
  resolved several 2026-07-29 review findings (P5, P9, P10, P16) — verified
  against the merged tree. P8 (the `DateTime.Now` stopwatch idiom still used by
  three Omnidots handlers while `StoreTracesHandler` uses `TimeProvider`) is
  *not* resolved and remains open.
- Pull requests are gated by two workflows. `Engineering standards` grades the
  changed surface for source development; curated client payloads retain its
  model/configuration/shell/workflow checks and skip only that source-diff
  ratchet. `Tests` runs the whole `Rvt.Mono.slnx` suite against TimescaleDB,
  Portal client type checking and unit tests, and the shipped repository
  boundaries. Source PRs additionally run the internal documentation and shell
  contract fixtures that client releases intentionally exclude.
- `main` carries the P0 guardrail work (PR #20) from
  [docs/reviews/2026-07-28-duplication-legacy-consistency-review.md](docs/reviews/2026-07-28-duplication-legacy-consistency-review.md):
  the `Tests` workflow and its mutation-tested contract, AirQ architecture
  guards, the portal `Application → Spa.Api` shrinking baseline, the
  `MonitorHost` one-shot shutdown token, the Svantek HTTP timeout,
  cross-platform Portal SPA build targets (the `cmd.exe` wrapper broke any
  non-Windows build), and portal private static fields converged on the
  repository-wide `_camelCase` rule.
- SonarQube stays manual: `tests/verify-manual-sonarqube-workflow.test.sh`
  pins `workflow_dispatch`, so scheduling it is a deliberate product change,
  not a guardrail gap.
- `main` additionally carries the repo-wide explicit-local-types style pass
  (PR #17, merge `c6e77e3a`, 593 files) — local variables use explicit types
  everywhere; keep new code consistent with it.
- `main` carries the critical-findings remediation (PR #15, merge `6be9c90`):
  reporting consolidated onto `apps/monitors/reportingmonitor`, alert
  delivery/heatmap/contact-skipping fixes, AirQ and Omnidots import chains
  async + cancellable behind vendor ports, uniform storage-port contract,
  shared rules decoupled from the running executable, common-hub cleanup.
- `main` carries the P1 dead-code and hygiene sweep (PR #18, merge
  `adf9c824`) from
  [docs/reviews/2026-07-28-duplication-legacy-consistency-review.md](docs/reviews/2026-07-28-duplication-legacy-consistency-review.md)
  (the authoritative remaining-findings list, including the P0 guardrail backlog:
  PR test job, Svantek HTTP timeout, `MonitorHost` one-shot token, AirQ
  architecture tests, portal `Application → Spa.Api` guard).
- `main` carries the monotonic standards-baseline regeneration (PR #21), which
  remediates the six standards increases found after PR #18 and regenerates the
  engineering-standards baseline through the official updater. The baseline fell
  from
  1,994 entries / 7,709 diagnostics to 1,112 entries / 2,072 diagnostics:
  882 entries removed, 12 lowered, 5,637 diagnostic allowances retired, and
  zero increases.
- `main` also carries the PR #22 monitor-hosting consolidation, including
  per-monitor job catalogs and shared job-argument handling.
- The Sonar remediation series retires the unreferenced `RVTUtilities`
  project and its dedicated tests, removes the outdated package-validation
  expectation from the common-source-boundary verifier, and documents the
  repository guardrails added for repeated security and maintainability
  findings.
- Portal SendGrid registration now binds its enabled state through `IOptions`
  from `RVT:EMAIL_ENABLED`; the environment-variable form is
  `RVT__Email_ENABLED` (`RVT__EMAIL_ENABLED` is equivalent because ASP.NET Core
  configuration keys are case-insensitive). The setting is optional and
  defaults to `true`, but the checked-in Visual Studio `https` launch profile
  sets it to `false` so local debugging does not send mail. Setting it to
  `false` disables the adapter even when a SendGrid API key is configured.
- Monitor projects follow main's lowercase `api`, `model`, `db`, `http`, and
  `json` directory structure. Path-based architecture tests and tooling must
  preserve that exact casing. Repo-wide CA1859 and CA1873 enforcement remains
  enabled, with the inherited monitor-only debt kept visible as warnings
  through the scoped `apps/monitors/**/*.cs` editorconfig section.

## Verification environment

- Full-suite runs require the PostgreSQL/Timescale integration database:

  ```bash
  docker run -d --name rvt-integration-db -e POSTGRES_DB=rvt_integration -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -p 55432:5432 timescale/timescaledb:2.28.3-pg17
  ```

  Monitor suites use
  `RVT__POSTGRES_INTEGRATION_CONNECTION="Host=localhost;Port=55432;Database=rvt_integration;Username=postgres;Password=postgres"`.
  Portal opt-in tests use the same disposable connection through
  `RVT__POSTGRES_INTEGRATION_CONNECTION`, after applying its three EF migration chains
  and `RVT.SchemaDeploy` as documented in
  [docs/database/portal/ef-migrations.md](docs/database/portal/ef-migrations.md).
  The example is a non-secret local test credential; never substitute a
  production connection.
- Verification on current `main` passed with the disposable database prepared:
  the bounded-response state reports 2,374/2,374 with no skips and a
  zero-warning Release build. The changed-surface engineering-standards check
  passes. The preceding full-review checkpoint also passed the five root
  repository guards, all `tests/*.test.sh` contract scripts, and
  `scripts/verify-engineering-standards.sh --all`.
- Repository guards run from the root: `verify-postgresql-only.sh .`,
  `verify-mono-layout.sh`, `verify-mono-solution.sh`,
  `verify-rvt-common-source-boundary.sh`, `verify-documentation-layout.sh`.
- The engineering-standards ratchet
  (`scripts/verify-engineering-standards.sh --base origin/main --head HEAD`)
  grades the changed surface; pure deletions are safe, but any edited line
  must satisfy the standards, and whole-file reformatting (namespace
  conversion) expands the graded surface — keep style fixes line-local.
- Full standards inventory and baseline regeneration invoke Roslyn through a
  local named pipe. Sandboxed runs that prohibit local IPC fail before
  producing a report; rerun the unchanged command with local IPC permitted.

## Standing working-tree notes

- The bounded-response remediation affects the AirQ, Omnidots and Svantek
  vendor HTTP clients, the shared bounded-response reader, the Reporting
  customer-logo client, and four focused regression test files. It introduces
  no new environment variables.
- `main` carries the Windows/Parallels SPA proxy repair:
  `RvtPortal.Spa.csproj` launches
  `RvtPortal.Client/scripts/start-vite-for-visual-studio.mjs`, and
  `SpaProxyConfigurationTests` pins that boundary. The launcher installs the
  lockfile-specific Windows npm tree below
  `%LOCALAPPDATA%\RvtPortal\spa-dependencies`, mirrors the shared client source
  into a Windows-local workspace with `robocopy /MIR`, and runs Vite entirely
  from NTFS. The mirror repeats every second so edits in the shared checkout
  still trigger Vite/HMR. It uses the standard Windows `LOCALAPPDATA` and
  `ComSpec` variables; no new repository or user variable is required.
- The Windows ARM VM verification builds `RvtPortal.Spa` with zero warnings or
  errors, starts Vite 6.4.3 on port 5173, serves the HTML shell, and returns a
  transformed (non-error-overlay) `src/main.tsx` module.

## Remaining deferred work

- Continue the open remediation sequence in
  [docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md](docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md):
  production Help Admin audit receipts, `RVT.BusinessLogic` dependency cleanup,
  Portal vertical extraction, monitor narrow-port migration, synchronous
  compatibility retirement, and selective Common infrastructure extraction.
- Unify Portal blob client/service usage behind
  `IObjectStorageClientFactory`; customer-logo and reporting storage adoption
  remain explicit future decisions.
- The current consistency review still owns Veff/Vdv fleet-runner convergence,
  stale Portal OpenAPI generation and the Help Admin filter defect, container
  hygiene, and the remaining deletion/consolidation items. Do not treat the
  reliability slice as closing those broader workstreams.
