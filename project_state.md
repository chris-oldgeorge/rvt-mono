# Project State

Resume instruction: `Read project_state.md to get up to speed`.

This file holds only the **current** state. The append-only checkpoint log it
used to be (40+ dated sections) is archived at
[docs/history/project-state/2026-07-checkpoint-log.md](docs/history/project-state/2026-07-checkpoint-log.md).
Add new detail here by *replacing* stale statements, not appending; move
superseded narratives to the archive.

## Current state — 2026-07-29

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

## Verification environment

- Full-suite runs require the PostgreSQL/Timescale integration database:

  ```bash
  docker run -d --name rvt-integration-db -e POSTGRES_DB=rvt_integration -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -p 55432:5432 timescale/timescaledb:2.28.3-pg17
  ```

  and `RVT__POSTGRES_INTEGRATION_CONNECTION="Host=localhost;Port=55432;Database=rvt_integration;Username=postgres;Password=postgres"`
  (a non-secret local test credential). Without it, the PostgreSQL
  integration tests fail by design rather than silently passing.
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

- A developer-local `apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj` variant
  (Visual Studio `npm run dev:vs` proxy + a reference to the deleted
  `RVT.Utilities`) was retired from the working tree on 2026-07-29; a backup
  copy sits in the session scratchpad. The committed proxy command from PR #8
  is the supported configuration and is pinned by `SpaProxyConfigurationTests`.
