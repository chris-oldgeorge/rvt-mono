# RVT Portal Review Remediation Implementation Plan

**Status:** Active. Restored for the PostgreSQL-only architecture on
2026-07-26.

**Goal:** Close the confirmed Portal cutover, security, correctness, client,
deployment, and maintainability findings without a broad rewrite.

**Architecture:** Stabilize observable behavior and executable release gates
before restructuring. Preserve the three-context shared-transaction design and
introduce application boundaries incrementally.

**Stack:** .NET 10, ASP.NET Core, EF Core/Npgsql, PostgreSQL/TimescaleDB,
React 19, TypeScript, Vite, Vitest, Testing Library, Playwright, Bash,
PowerShell, and GitHub Actions.

## How to read status

- Tasks 2, 3, and 4 are complete.
- Tasks 5 and 6 are implemented; their live PostgreSQL evidence remains open
  until a suitable connection is available.
- The database-provider portion of Task 16 is complete: PostgreSQL/Npgsql is
  the sole contract and selection-era configuration is retired.
- Task 15 is partial because the application project now exists, but the full
  boundary extraction and dependency gates remain open.
- Tasks 1 and 7 through 14 remain open.
- Task 16's non-provider cleanup and client-contract work remain open.
- A completed implementation does not close an unchecked release gate. Every
  gate at the end of this plan remains unchecked until fresh evidence is
  recorded.

## Global constraints

- Work from the monorepo root; Portal paths are rooted at `apps/portal/`.
- Preserve the three `DbContext` split and single shared
  `DbConnection`/unit-of-work transaction pipeline.
- Keep active Common consumers source-referenced; package-consumer fixtures
  remain isolated.
- Treat `timestamp without time zone` telemetry as UTC by contract: use
  `DateTimeKind.Unspecified` only at the Npgsql query boundary and restore UTC
  before API serialization.
- Treat contract hire fields as calendar dates and convert them to UTC midnight
  only at persistence.
- Every P0/P1 production fix needs a regression test that fails first.
- Real PostgreSQL tests are mandatory for Npgsql time-kind, schema repair, and
  schema-qualification behavior.
- Cross-tenant resource reads return `404`. Forgot-password responses remain
  indistinguishable for existing and nonexistent accounts.
- Do not retry non-idempotent outbound operations automatically. Establish
  timeouts and typed failure translation first.
- Help Admin title-focus work stays deferred unless that temporary UI is
  confirmed for release.
- What3Words requires a product decision: remove it and its secret, or retain it
  behind a typed outbound port with header-based authentication.

## Normalized finding register

### P0/P1

| ID | Contract | Status |
| --- | --- | --- |
| R01 | Vibration traces query the mapped entity end to end. | Complete |
| R02 | Search time bounds and serialized values honor the UTC telemetry contract. | Implemented; live PostgreSQL evidence open |
| R03 | Installer monitor-picture reads enforce company ownership. | Complete |
| R04 | `TimeProvider` is resolvable from dependency injection. | Complete |
| R05 | Site access uses one inclusive active-assignment window. | Complete |
| R06 | Public auth links use a validated configured SPA origin outside development. | Complete |
| R07 | Contract date-only inputs persist as valid UTC instants. | Implemented; live PostgreSQL evidence open |
| R08 | Existing-database column-default repair ships and executes safely. | Implemented; live PostgreSQL evidence open |
| R09 | Monitor option lists are actor-scoped. | Complete |
| R10 | Self-service email changes require a confirmation flow or are prohibited. | Complete |
| R11 | Forgot-password failures remain publicly generic and internally observable. | Complete |
| R12 | Liveness is process-only and readiness proves database access. | Open |
| R13 | Trusted forwarded-header processing precedes origin, auth, and rate-limit use. | Complete |
| R14 | Site archives use the shared storage client factory in every supported mode. | Open |
| R15 | Schema validation keys metadata by schema, relation, and column. | Open |
| R16 | Calendar padding cells send the full selected ISO date. | Open |
| R17 | Local-day defaults use local calendar components rather than UTC conversion. | Open |
| R18 | Client requests cannot apply stale responses. | Open |
| R19 | Outbound clients have named timeouts and typed failure containment. | Open |
| R20 | An active root workflow runs Portal static, PostgreSQL, and browser gates. | Open |

### P2 and decision-gated work

| Contract | Status |
| --- | --- |
| Development restore must propagate restore failure after destructive setup. | Open |
| What3Words must be removed or moved behind a safe port after product decision. | Open |
| Help Admin must use a stable key if retained for release. | Decision-gated |
| All telemetry mappings must declare the intended PostgreSQL column type. | Implemented; live evidence open |
| Unused blob and HTTP dependencies must be removed after caller proof. | Open |
| Development-secret values must not be exposed as process arguments. | Open |
| Million-row reads require bounded paging/streaming and deterministic order. | Open |
| Sonar lifecycle findings need an explicit reviewed `npm ci` policy. | Open |

Superseded observations remain closed: repository state is current, imported
workspace debris is absent, SendGrid client construction is shared, and the
runtime container user observation is obsolete. Existing release-export and
architecture guards must be preserved.

## Ordered action sequence and ownership

Tasks are deliberately ordered. Owners are functional roles; a named delegate
may implement a task, but the listed role owns acceptance evidence.

### Task 1 — Activate root Portal CI with PostgreSQL evidence

**Status:** Open. **Owner:** Build/release.

- Add an active root workflow with `portal-static`, `portal-postgres`, and
  `portal-client-e2e` jobs.
- Give PostgreSQL tests an ephemeral supported service and a runtime-only
  connection.
- Produce TRX and fail if any required PostgreSQL test is skipped.
- Run Vitest and a real Playwright Chromium journey.
- Align the cutover runbook and mono-layout checks with the active workflow.

### Task 2 — Fix service wiring and vibration trace mapping

**Status:** Complete. **Owner:** Portal backend.

- Resolve `TimeProvider.System` through dependency injection.
- Use the mapped vibration trace entity throughout the service/data path.
- Retain focused host and data-view regression tests.

### Task 3 — Close tenant and assignment authorization gaps

**Status:** Complete. **Owner:** Portal security/backend.

- Apply company ownership to installer detail, picture, and list reads.
- Reuse one inclusive active-assignment predicate.
- Pass the actor to monitor options and return only visible sites/contracts.
- Preserve regression cases for cross-company, expired, and future access.

### Task 4 — Harden public auth origins and account workflows

**Status:** Complete. **Owner:** Identity/security.

- Require and validate the configured public SPA origin outside development.
- Configure trusted forwarded headers before consumers.
- Use a confirmation-token flow or prohibit direct profile email replacement.
- Keep forgot-password output generic when outbound delivery fails.

### Task 5 — Enforce UTC search and calendar-date contracts

**Status:** Implemented; live evidence open. **Owner:** Portal data.

- Normalize query bounds only at the Npgsql boundary and restore UTC kind on
  returned telemetry.
- Persist hire/off-hire dates as UTC midnight while retaining calendar meaning.
- Audit telemetry mappings for explicit PostgreSQL types.
- Record real PostgreSQL/DST evidence before closing the task.

### Task 6 — Make schema deployment complete and failure-aware

**Status:** Implemented; live evidence open. **Owner:** Database/release.

- Ship existing-database repair assets in source, build, and publish output.
- Execute scripts in deterministic order and fail without partial success.
- Keep dry-run, idempotency, prerequisite, and rollback evidence.
- Make destructive development restore propagate failure.
- Rehearse twice on real PostgreSQL before closing the task.

### Task 7 — Split liveness from readiness

**Status:** Open. **Owner:** Portal operations.

- Keep liveness process-only.
- Make readiness prove the configured database is reachable with a bounded
  timeout.
- Return `503` while unavailable and `200` only when ready.
- Gate deployment and traffic switching on readiness, not liveness.

### Task 8 — Harden storage and outbound integration boundaries

**Status:** Open. **Owner:** Integrations/platform.

- Route site archives through the shared storage client factory.
- Give report and vendor clients named timeouts and typed
  configuration/URI/cancellation/network failures.
- Resolve What3Words through the retain-or-remove decision.
- Prevent development-secret values from appearing in process arguments.
- Add retries only where an operation is proven idempotent.

### Task 9 — Correct client dates and stale-response behavior

**Status:** Open. **Owner:** Portal client.

- Send a complete ISO date from every calendar cell, including padding days.
- Build local-day defaults from local year/month/day components.
- Cancel superseded requests or guard them with request generations.
- Test month boundaries, time-zone boundaries, rapid filter changes, and
  company switching.

### Task 10 — Resolve Help Admin identity and focus

**Status:** Decision-gated. **Owner:** Product and Portal client.

- First decide whether the temporary UI ships.
- If retained, key rows/assets by immutable ID rather than editable title.
- Restore predictable focus after edit/create/delete without title lookup.
- If removed, delete its route, assets, and dead client contracts together.

### Task 11 — Complete schema-safety and delete contracts

**Status:** Open. **Owner:** Database and Portal backend.

- Validate metadata by schema plus relation plus column.
- Reject same-named objects in an unintended schema.
- Define delete behavior for referenced entities and test the full matrix.
- Resolve model/database mismatches through generated migrations and reviewed
  schema assets, never ad hoc startup mutation.

### Task 12 — Bound and stabilize large reads

**Status:** Open. **Owner:** Portal backend/performance.

- Replace `Take(1000000)` with explicit bounded paging, keyset paging, or
  streaming according to the caller.
- Add deterministic tie-break ordering to every paged query.
- Carry page size, continuation, total/count semantics, cancellation, and safe
  maximums through API and generated client contracts.
- Test boundary sizes, ties, empty pages, cancellation, and query shape.

### Task 13 — Remove dead dependencies and strengthen guards

**Status:** Open. **Owner:** Architecture.

- Prove no callers before removing unused blob and HTTP dependencies.
- Remove dead registrations, wrappers, imports, and configuration as one
  change.
- Add architecture checks that prevent adapter dependencies leaking into the
  application core.
- Keep Common source-reference and package-validation boundaries intact.

### Task 14 — Make release orchestration executable

**Status:** Open. **Owner:** Build/release.

- Make the root workflow the single authoritative Portal verification entry.
- Produce versioned backend/client/schema artifacts with checksums and evidence.
- Run real browser smoke coverage against a deployed candidate.
- Document and enforce the reviewed lifecycle-script allowlist.
- Require release notes, rollback assets, Sonar disposition, and zero skipped
  required tests.

### Task 15 — Establish the application boundary incrementally

**Status:** Partial. **Owner:** Architecture and Portal backend.

- Keep the existing application project, then move Auth, Sites, and Monitors
  use cases behind narrow ports in reviewable slices.
- Keep domain/application code free of ASP.NET, EF, Npgsql, storage, email, and
  vendor SDK dependencies.
- Retain infrastructure adapters in outer projects.
- Preserve the three-context shared unit-of-work transaction.
- Add compile-time dependency tests before each extraction is accepted.

### Task 16 — Finish scope cleanup and contract alignment

**Status:** Provider portion complete; other work open.
**Owner:** Cross-functional Portal.

- Preserve the sole PostgreSQL/Npgsql database contract and its configuration
  guards.
- Remove proven dead code only with caller and test evidence.
- Align OpenAPI, generated TypeScript, DTO nullability, paging, cancellation,
  and error contracts in one reviewed change.
- Correct remaining display defects, development binding assumptions, and FFT
  semantics with focused tests.
- Re-run static, database, client, browser, architecture, and documentation
  gates after the cleanup.

## Release, rollout, rollback, and evidence

1. Finish the relevant regression test before each production change.
2. Run static and architecture gates, then real PostgreSQL gates, then client
   and browser gates.
3. Back up the target and retain the previous application and schema artifacts.
4. Run SchemaDeploy dry-run and review its ordered output.
5. Deploy application and required schema as one release unit.
6. Check liveness, then database-backed readiness, before switching traffic.
7. Observe authentication, tenant authorization, report delivery, error rate,
   latency, and database health during progressive rollout.
8. Roll back traffic and the application first. Apply only the reviewed
   rollback asset for a reversible schema change; restore the backup when
   rollback would otherwise lose data.
9. Do not restore old-name compatibility objects as a rollback mechanism.

Each closed finding must link a regression test, implementation commit, command
output, and operational evidence where applicable. Secrets and connection
strings must never appear in logs or evidence files.

## Final readiness gates

- [ ] Every P0/P1 register row links its test and implementation commit.
- [ ] The active root workflow is green on the release candidate.
- [ ] Required PostgreSQL tests report zero skipped/not-executed results.
- [ ] Cross-tenant, expired-assignment, and future-assignment cases pass.
- [ ] Public auth links, email change, and forgot-password cases pass.
- [ ] UTC, London/DST, calendar-date, and serialization cases pass.
- [ ] Contract and search behavior passes against real PostgreSQL.
- [ ] Schema dry-run, repair, schema qualification, and second-run idempotency
  evidence is recorded.
- [ ] Liveness and readiness demonstrate the expected `200`/`503` transitions.
- [ ] Vitest, Playwright, backend, release-export, Sonar, and documentation
  gates pass.
- [ ] Mono layout, solution membership, Common boundary, application boundary,
  local-link, and documentation-layout checks pass.
- [ ] Paging is bounded and deterministically ordered; generated client
  contracts match the API.
- [ ] Cutover, rollout, rollback, and evidence runbooks describe real,
  executable gates.
