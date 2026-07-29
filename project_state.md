# Project State

Resume instruction: `Read project_state.md to get up to speed`.

This file holds only the **current** state. The append-only checkpoint log it
used to be (40+ dated sections) is archived at
[docs/history/project-state/2026-07-checkpoint-log.md](docs/history/project-state/2026-07-checkpoint-log.md).
Add new detail here by *replacing* stale statements, not appending; move
superseded narratives to the archive.

## Current state — 2026-07-29

### Reviewable full-monorepo client release

- Completed implementation branch:
  `chore/full-monorepo-client-release-v2`.
- Source-tooling PR `chris-oldgeorge/rvt-mono#43` merged to `main` as
  `a9b1bd2a2de3e0db79fb543f4bde629dbcdf555c`.
- Isolated worktree:
  `/Users/oldgeorge/Developer/rvt-mono/.worktrees/full-monorepo-client-release`.
- Approved design:
  `docs/superpowers/specs/2026-07-29-full-monorepo-client-release-design.md`.
- Implementation plan:
  `docs/superpowers/plans/2026-07-29-full-monorepo-client-release.md`.
- Client remote:
  `https://github.com/RVT-Group-LTD/rvt-monitors.git`.
- Client branch: `release-candidate`.
- Published client commit:
  `7d7d8b1f74ba7c0acd77a738b29e149259f0df0f`.
- Published source commit:
  `a9b1bd2a2de3e0db79fb543f4bde629dbcdf555c`.
- Published payload: 1,445 manifested files plus `RELEASE_MANIFEST.txt`.
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
- Publication verification completed with .NET SDK `10.0.302`: restore/build
  succeeded with zero warnings, the TimescaleDB-backed suite passed
  2,367/2,367 with no skips, all shell contracts and repository guards passed,
  Portal passed 163/163 tests and its production build, and all Compose inputs
  validated. GitHub PR checks passed. The publisher and a second independent
  clone both verified the source metadata, root/orphan history, manifest,
  required monorepo paths, internal-file exclusions, and saved-secret boundary.

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
  changed surface; `Tests` (added by PR #20) runs the whole `Rvt.Mono.slnx`
  suite against a TimescaleDB service container, the Portal client type check
  and unit tests, the five repository guards, and every `tests/*.test.sh`
  contract test as a glob. Before PR #20 no workflow ran any test on a pull
  request, and the guards and contract tests were wired into nothing.
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
  `RVT_TEST_POSTGRES_CONNECTION`, after applying its three EF migration chains
  and `RVT.SchemaDeploy` as documented in
  [docs/database/portal/ef-migrations.md](docs/database/portal/ef-migrations.md).
  The example is a non-secret local test credential; never substitute a
  production connection.
- Verification on the merged PR tree passed with the disposable database
  prepared: the full solution reported 2,379/2,379, including AirQ 140/140,
  Omnidots 403/403, and Portal 560/560, with no skips. The five root repository
  guards, all `tests/*.test.sh` contract scripts, and
  `scripts/verify-engineering-standards.sh --working-tree` also passed. One
  engineering-standards contract scenario needed an isolated retry after its
  0.4-second process-ownership timing check flaked; the retry passed.
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

- Preserve the pre-existing untracked `.codex/`, root `AGENTS.md`, and
  `docs/superpowers/plans/2026-07-28-sonar-security-remediation.md`; they are
  not part of this branch.
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
