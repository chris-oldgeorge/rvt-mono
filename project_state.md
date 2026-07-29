# Project State

Resume instruction: `Read project_state.md to get up to speed`.

This file holds only the **current** state. The append-only checkpoint log it
used to be (40+ dated sections) is archived at
[docs/history/project-state/2026-07-checkpoint-log.md](docs/history/project-state/2026-07-checkpoint-log.md).
Add new detail here by *replacing* stale statements, not appending; move
superseded narratives to the archive.

## Current state — 2026-07-29

- The current branch is `codex/reliability-cleanup`. It was forked from
  `ad7a8834`, which is now an ancestor of `origin/main` after PR #20 merged.
  `origin/main` has subsequently advanced to `584bbff2`; update this branch
  from `main` only as part of the chosen integration path.
- This branch implements the bounded reliability cleanup recorded in
  [docs/superpowers/plans/2026-07-29-reliability-cleanup.md](docs/superpowers/plans/2026-07-29-reliability-cleanup.md):
  - Omnidots trace imports propagate caller cancellation instead of recording
    it as a monitor failure.
  - Portal optional monitor summaries and site archives retain their fallback
    behavior but now log genuine failures and propagate cancellation.
  - AirQ uses injected UTC `TimeProvider` time for a missing watermark and no
    longer carries behavior-neutral aggregate rethrow blocks.
  - Omnidots checked-in defaults contain no personal alert recipient or
    customer serial allow-list; deployments must supply the recipient and may
    opt into a staged serial allow-list.
- The authoritative review has been updated to distinguish the resolved slice
  from remaining work:
  [docs/reviews/2026-07-28-duplication-legacy-consistency-review.md](docs/reviews/2026-07-28-duplication-legacy-consistency-review.md).

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
- Verification on this tree passed with the disposable database prepared:
  AirQ 140/140, Omnidots 403/403, and Portal 558/558 with no skips. The five
  root repository guards, all `tests/*.test.sh` contract scripts, and
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

## Standing working-tree notes

- Preserve the pre-existing untracked `.codex/`, root `AGENTS.md`, and
  `docs/superpowers/plans/2026-07-28-sonar-security-remediation.md`; they are
  not part of this branch.
- A developer-local `apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj` variant
  (Visual Studio `npm run dev:vs` proxy + a reference to the deleted
  `RVT.Utilities`) was retired from the working tree on 2026-07-29; a backup
  copy sits in the session scratchpad. The committed proxy command from PR #8
  is the supported configuration and is pinned by `SpaProxyConfigurationTests`.

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
