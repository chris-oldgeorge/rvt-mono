# Project State

Resume instruction: `Read project_state.md to get up to speed`.

This file holds only the **current** state. The append-only checkpoint log it
used to be (40+ dated sections) is archived at
[docs/history/project-state/2026-07-checkpoint-log.md](docs/history/project-state/2026-07-checkpoint-log.md).
Add new detail here by *replacing* stale statements, not appending; move
superseded narratives to the archive.

## Current state — 2026-07-29

- The active isolated branch is `codex/sonar-release-remediation` in
  `/private/tmp/rvt-sonar-remediation`. Do not modify or clean the unrelated
  dirty linked worktree at `/Users/oldgeorge/Documents/rvt-mono`.
- The remediation branch is committed through `1a61acc`
  (`guard repeated shell literals`). The latest checkpoints are:
  `10fdfc1` (S3267, S2699, S1192, S101 and data-result mapping),
  `5028177` (S107 refactors/suppressions and auth result mapping), and
  `1a61acc` (S3776 and shell S1192 guardrails).
- The C# Sonar rules S101, S107, S1192, S2699, S3267, and S3776 are promoted
  to error severity in the root `.editorconfig`. Compatibility/framework
  suppressions are symbol-scoped and justified.
- Repeated shell literals in the verifier fixtures were extracted into named
  values. Root `AGENTS.md` now requires this for any shell literal used three
  or more times.
- The current Release solution build succeeds with zero warnings and errors.
  Focused verification passes: reporting 16/16, alert commit store 3/3,
  portal auth/monitor/release-audit 27/27, and the formerly failing data-view
  endpoint regression. The three modified shell harnesses all pass.
- The working-tree engineering-standards verifier passes. Before handoff,
  rerun the definitive committed-range verifier and the broad non-database
  test suite.
- A real pre-existing 500 response was fixed while verifying this wave:
  auth workflow statuses now map to their intended 4xx responses instead of
  `NotImplementedException`. A similar data-view `DeploymentNotFound` mapping
  was corrected in the preceding checkpoint.
- A full Omnidots test invocation cannot pass without
  `RVT__POSTGRES_INTEGRATION_CONNECTION`; its database-backed classes fail
  deliberately when the variable is absent. Do not treat those failures as a
  regression. The security-sensitive endpoint subset is green.
- The branch has not been pushed. A prior push was blocked because the private
  `origin` destination was not authorized as verified source egress. Do not
  work around that safeguard; obtain explicit authorization for the exact
  remote or use a verified connector before pushing.
- Sonar's `main` analysis is stale (`2026-07-28T22:45:10Z`) and still reports
  findings already proven clean locally. Live CLI code analysis is unavailable
  because the organization lacks the Vortex license (403). A fresh traditional
  branch analysis is required to establish the true residual issue count.
- The uploaded analysis has no open vulnerabilities or unreviewed security
  hotspots. Traditional authenticated Sonar API access works through
  `/Users/oldgeorge/.local/share/sonarqube-cli/bin/sonar`; never print or store
  its token.

## Relevant structure

- `.github/workflows/sonarqube.yml` — release analysis pipeline.
- `apps/monitors/` — AirQ, MyAtm, Omnidots, reporting, and Svantek services
  plus tests.
- `apps/portal/` — Portal backend, SPA host, and React client.
- `libs/rvt-monitor-common/` — shared communications, monitoring, storage,
  and tests.
- `eng/standards/` and `scripts/engineering-standards/` — monotonic
  repository ratchet and baseline implementation.
- `project_state.md` — this current-state handoff.

## Verification environment

- Full-suite runs require the PostgreSQL/Timescale integration database:

  ```bash
  docker run -d --name rvt-integration-db -e POSTGRES_DB=rvt_integration -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -p 55432:5432 timescale/timescaledb:2.28.3-pg17
  ```

  and `RVT__POSTGRES_INTEGRATION_CONNECTION="Host=localhost;Port=55432;Database=rvt_integration;Username=postgres;Password=postgres"`
  (a non-secret local test credential). Without it, the PostgreSQL
  integration tests fail by design rather than silently passing.
- Sonar authentication is held by the local CLI/keychain; do not write tokens
  into the repository or command output. In scratch worktrees invoke
  `/Users/oldgeorge/.local/share/sonarqube-cli/bin/sonar` explicitly.
- Repository guards run from the root: `verify-postgresql-only.sh .`,
  `verify-mono-layout.sh`, `verify-mono-solution.sh`,
  `verify-rvt-common-source-boundary.sh`, `verify-documentation-layout.sh`.
- The engineering-standards ratchet
  (`scripts/verify-engineering-standards.sh --base origin/main --head HEAD`)
  grades the changed surface. This remediation intentionally converted legacy
  namespaces and then fixed the resulting whole-file graded surface. Pure
  deletions are safe, but any edited line must satisfy the standards.
- Full standards inventory and baseline regeneration invoke Roslyn through a
  local named pipe. Sandboxed runs that prohibit local IPC fail before
  producing a report; rerun the unchanged command with local IPC permitted.

## Standing working-tree notes

- The original linked worktree contains a large unrelated formatting
  migration on `codex/monotonic-standards-baseline`; preserve it exactly.
- The isolated remediation worktree should be clean except for the intentional
  `project_state.md` handoff update.
- Resume by reading this file, switching to
  `/private/tmp/rvt-sonar-remediation`, checking `git status`, committing the
  state update if needed, and then running the definitive committed-range
  verifier. Apply the repository pre-read secrets policy to every file before
  reading it and after modifying it.
- Do not push until the exact private remote is explicitly authorized or a
  verified connector is available. After upload, use the fresh Sonar analysis
  rather than the stale `main` line numbers for any further remediation.
