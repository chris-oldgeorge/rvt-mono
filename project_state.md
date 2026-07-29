# Project State

Resume instruction: `Read project_state.md to get up to speed`.

This file holds only the **current** state. The append-only checkpoint log it
used to be (40+ dated sections) is archived at
[docs/history/project-state/2026-07-checkpoint-log.md](docs/history/project-state/2026-07-checkpoint-log.md).
Add new detail here by *replacing* stale statements, not appending; move
superseded narratives to the archive.

## Current state — 2026-07-29

- `main` is at merge `3f9f0fc` and includes PR #21's monotonic standards
  baseline, plus PR #19's verifier fix for generated `.sonar/` tool inputs.
- The active isolated branch is `codex/sonar-release-remediation` in
  `/private/tmp/rvt-sonar-remediation`. Do not modify or clean the unrelated
  dirty linked worktree at `/Users/oldgeorge/Documents/rvt-mono`.
- The latest release Sonar workflow was run against `main`:
  GitHub run `30405522709`, analysis
  `d39ffb79-d732-40d1-a312-e3e81450c3cd`. Upload succeeded; the workflow
  failed only because the quality gate failed.
- The uploaded analysis has no open vulnerabilities and no unreviewed
  security hotspots. It reported 1,589 maintainability-impact issues,
  26 reliability-impact issues, and 18.9% new-code coverage against an
  80% gate.
- Local remediation is committed through `b7dfbdb` (`replace deprecated React
  form events`). The complete local-only sequence after the original three
  remediation commits is:
  `8ad33a0`, `dcec0d7`, `56c141c`, `3e4a7a1`, `1b72bd6`, `ed7a301`,
  `4ec01e6`, `649bc0f`, and `b7dfbdb`.
- Completed waves remove the dominant MSTest, file-scoped namespace,
  cancellation-token, unnecessary-allocation, React testing, inefficient
  logging, sanitized catch-logging, and deprecated React event findings.
  Repeated rules are guarded in `.editorconfig` or ESLint. S6667 uses four
  narrow `SuppressMessage` attributes because passing raw exceptions failed
  the endpoint's no-sensitive-log-leakage tests.
- The committed-range standards verifier passes through `ed7a301`; the full
  standards baseline was then reduced and committed in `4ec01e6`. Working-tree
  verification passed after the S6667 and React event waves. Rerun the
  definitive committed-range verifier before handoff.
- Current verified evidence before the paused, uncommitted wave:
  Release solution build succeeds with zero warnings/errors;
  `Rvt.Monitor.CommonTests` passes 387/387; Omnidots endpoint tests pass 27/27;
  frontend lint has zero errors and two pre-existing Fast Refresh warnings;
  frontend production build passes; focused frontend tests pass 76/76.
- A full Omnidots test invocation cannot pass without
  `RVT__POSTGRES_INTEGRATION_CONNECTION`; its database-backed classes fail
  deliberately when the variable is absent. Do not treat those failures as a
  regression. The security-sensitive endpoint subset is green.
- The working tree is intentionally paused with five uncommitted reliability
  edits:
  `.editorconfig`, `MyAtmHttpGateway.cs`, `BoundedJsonRequestReader.cs`,
  `ApiInfrastructure.cs`, and `MqttAlertDeliveryAdapter.cs`.
  They address Sonar S8949 (three cancellation paths, with an error-level
  guardrail), S3903 (unnamespaced record), and S2583 (always-true loop
  condition). Required secret scans and `git diff --check` passed; the MyAtm
  file was converted to the enforced file-scoped namespace. Build, focused
  tests, standards verification, and commit are still pending for this wave.
- The branch has not been pushed. A prior push was blocked because the private
  `origin` destination was not authorized as verified source egress. Do not
  work around that safeguard; obtain explicit authorization for the exact
  remote or use a verified connector before pushing.
- Remaining maintainability work must be driven from the stale Sonar analysis
  until a new branch analysis can be uploaded. The last queried reliability
  set contained only S8949 x3, S3903 x1, and S2583 x1; the paused wave targets
  all five.
- The repository-local `AGENTS.md` now permits two exact Omnidots test-fixture
  false-positive patterns after their paths, rule IDs, and finding counts were
  independently verified. Any deviation remains blocking.
- `main` also carries the explicit-local-types pass (PR #17), critical-findings
  remediation (PR #15), P1 dead-code and hygiene sweep (PR #18), and PR #21's
  baseline reduction from 1,994 entries / 7,709 diagnostics to 1,112 entries /
  2,072 diagnostics.

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
- Resume by reading this file, switching to
  `/private/tmp/rvt-sonar-remediation`, verifying the five paused reliability
  edits, committing them, and then continuing the Sonar remediation plan.
  Apply the repository's pre-read secrets policy to every file before reading
  it and after modifying it.
