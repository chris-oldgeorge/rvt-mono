# RVT monorepo delivery report

Date: 2026-07-29
Repository: `chris-oldgeorge/rvt-mono`
Reporting head: `d1fe828241ad04cbc2f825e1519eb699748ec433`

## Executive summary

The RVT source snapshots were imported into the monorepo at commit
`31d168fd9e07f80695d2fb1b09feb0a885e5f52d` on 2026-07-22. From that import
through the reporting head, the repository accumulated 470 subsequent commits,
including 58 merge commits and 32 first-parent GitHub pull-request merges.

The work converted a collection of imported applications and shared source
into one governed .NET 10 monorepo:

- one 42-project aggregate solution;
- a Portal SPA host and React client with explicit application, adapter,
  persistence, release-audit, and schema-deployment boundaries;
- five monitor hosts plus a consolidated reporting subsystem;
- provider-neutral communication and storage libraries with explicit
  SendGrid, Microsoft Graph, TransmitSMS, Local, Azure Blob, and S3 adapters;
- a PostgreSQL/TimescaleDB-only persistence contract;
- durable, cancellable monitor alert delivery with the legacy synchronous
  messaging path removed;
- pull-request tests, engineering-standard ratchets, repository guards, and a
  manual SonarCloud analysis pipeline;
- centralized architecture, development, operations, release, and historical
  documentation.

The latest recorded full-suite verification reports 2,379 passing tests with
no failures or skips when both PostgreSQL integration variables are supplied.
The latest SonarCloud analysis was processed successfully, but its quality gate
failed on new-code reliability, security, and coverage. That analysis predates
the reporting head and must be rerun before the current `main` can be treated as
measured by Sonar.

## Scope and method

This report treats the source import as the monorepo merge boundary:

| Boundary | Commit | Timestamp |
| --- | --- | --- |
| Imported source baseline | `31d168fd` | 2026-07-22 10:18:53 +03:00 |
| Reporting head | `d1fe8282` | 2026-07-29 19:33:26 +03:00 |

Activity metrics use the Git range `31d168fd..d1fe8282`, excluding the initial
292,000-line snapshot import so that subsequent engineering work is not hidden
by the import itself. Findings and outcomes were cross-checked against:

- `project_state.md`;
- the repository Git history and first-parent merge history;
- the current aggregate solution and tracked-file inventory;
- the architecture and operations documentation;
- the 2026-07-27 through 2026-07-29 repository reviews;
- the engineering-standards baseline;
- the latest SonarCloud quality-gate response.

This is a consolidated delivery report rather than a 470-entry commit
changelog. Every commit is included in the quantitative range; related commits
are grouped below by outcome.

## Delivery metrics

| Measure | Result |
| --- | ---: |
| Subsequent commits | 470 |
| Merge commits | 58 |
| First-parent GitHub PR merges | 32 |
| Files touched | 1,491 |
| Files added | 358 |
| Files modified | 764 |
| Files deleted | 260 |
| Files renamed | 109 |
| Insertions after import | 106,040 |
| Deletions after import | 82,141 |
| Net change after import | +23,899 lines |
| Current tracked files | 1,541 |
| Current .NET projects | 42 |
| Current test projects | 16 |
| Current C# files | 1,043 |
| Current TypeScript/JavaScript files | 56 |
| Current Markdown files | 140 |
| GitHub workflows | 3 |
| Root verification scripts | 7 |
| Root contract-test programs | 18 |

The file and line totals include generated migrations, model snapshots,
test fixtures, documentation, mechanical formatting, and renames. They describe
scope, not developer productivity.

The 32 first-parent PR merges were:
`#1`, `#5`, `#6`, `#8`–`#30` where present, `#33`, `#36`, and
`#38`–`#41`. Specifically:

`#1, #5, #6, #8, #9, #10, #11, #12, #13, #14, #15, #16, #17, #18,
#19, #20, #21, #22, #23, #24, #25, #26, #27, #28, #29, #30, #33,
#36, #38, #39, #40, #41`.

## Current repository structure

### Applications

- `apps/portal`
  - `RvtPortal.Spa`: ASP.NET Core startup host, API, Identity, health checks,
    adapters, and SPA serving boundary.
  - `RvtPortal.Client`: React/Vite client.
  - `RvtPortal.Application`: application use cases and ports.
  - `RVT.BusinessLogic`, `RVT.DataAccess`, and `RVT.Entities`: retained domain,
    persistence, and model layers.
  - `RVT.ReleaseAudit`: deployment/readiness audit tooling.
  - `RVT.SchemaDeploy`: PostgreSQL database-script deployment.
  - two .NET test projects plus client unit and end-to-end tests.
- `apps/monitors`
  - AirQ, MyAtm, Omnidots, ReportingMonitor, and Svantek hosts.
  - one dedicated test project per monitor.
  - Reporting is decomposed into Core, Messaging, PDF, and Storage projects.
  - shared Docker Compose, observability, database, and operational assets.

### Shared libraries

`libs/rvt-monitor-common` contains:

- communication abstractions and workflow;
- SendGrid, Microsoft Graph, and TransmitSMS adapters;
- monitor runtime, hosting, scheduling, data, durable-alert, MQTT, HTTP, and
  diagnostic infrastructure;
- storage abstractions plus Local, Azure Blob, and S3 adapters;
- shared PostgreSQL integration-test infrastructure;
- eight shared-library test projects.

### Governance and documentation

- `Rvt.Mono.slnx`: exact 42-project aggregate solution.
- `.github/workflows`: tests, engineering standards, and manual SonarQube.
- `scripts`: build and seven repository verification entry points.
- `tests`: contract tests and mutation/regression fixtures.
- `eng/standards`: the monotonic engineering-debt baseline.
- `docs`: architecture, database, development, history, module, operations,
  release, review, specification, and implementation-plan material.

The former top-level `services/reporting` implementation has been removed; the
active reporting implementation lives under `apps/monitors/reportingmonitor`.

## Work delivered

### 1. Monorepo foundation and dependency model

- Imported the Portal, monitor, reporting, and shared-common source snapshots
  into an `apps`/`libs` layout.
- Added the aggregate `Rvt.Mono.slnx` solution and guards that enforce exact
  project membership and folder placement.
- Replaced internal package-feed coupling with direct project references.
- Added source-boundary tests to prevent reintroduction of stale
  `Rvt.Monitor.Common` package or source-copy dependencies.
- Removed obsolete package-validation expectations once the internal package
  release boundary was retired.
- Consolidated central package/version configuration and locked restores.
- Defined a common .NET build policy with nullable analysis, implicit usings,
  deterministic builds, and ratcheted analysis.

Outcome: the repository builds and tests as one source graph; internal RVT
components no longer depend on a locally staged package feed.

### 2. Documentation consolidation

- Moved imported module documentation into one root `docs` hierarchy.
- Added a root documentation index and a move manifest.
- Added a repository-wide documentation-layout verifier and regression tests
  for stale source-relative links.
- Split current state from historical checkpoints:
  - `project_state.md` holds the live resumable state;
  - `docs/history/project-state/2026-07-checkpoint-log.md` holds the append-only
    history.
- Produced architecture records, implementation plans, release runbooks,
  readiness matrices, database standards, operational guides, and three
  progressively deeper full-repository reviews.
- Documented the Portal SendGrid runtime toggle, including its Visual Studio
  launch-profile behavior and environment-variable form.

Outcome: documentation has a governed location, a link/layout guard, a current
state entry point, and historical traceability.

### 3. Portal authentication, authorization, and release hardening

- Enforced active tenant and assignment authorization at Portal boundaries.
- Hardened public authentication routes and completed confirmed-email
  transitions.
- Made authorization tests deterministic through an injected clock.
- Defined UTC, local-time, and PostgreSQL timestamp boundaries.
- Added required database repairs and made incomplete schema deployment fail
  readiness.
- Hardened public hosting, forwarded-header, host-filtering, rate-limit,
  readiness, and data-protection configuration.
- Added deployment-safe report-content and report-generation trust boundaries.
- Added Help asset release auditing and production cutover evidence tooling.
- Kept credentials out of tracked configuration and documented user-secrets,
  deployment secrets, and environment-variable mappings.
- Added an `IOptions`-registered Portal SendGrid enabled state:
  `RVT:EMAIL_ENABLED`, supplied as `RVT__Email_ENABLED`; it defaults to enabled,
  while the checked-in Visual Studio profile disables local delivery.

Outcome: Portal startup and protected operations now fail closed around tenant,
host, schema, release, and internal-service boundaries.

### 4. Portal application and adapter boundaries

- Created `RvtPortal.Application`.
- Moved current-user and site-access policies out of the host layer.
- Extracted site reads, transactional mutations, archives, and logo workflows
  behind application ports.
- Added application tests for policy, ordering, validation, and transaction
  behavior.
- Extracted the Help Admin application boundary, persistence adapters, and
  release-audit requirements.
- Added shrinking architecture baselines that prevent Portal application code
  from regaining host/API dependencies.
- Added query-validation helpers and standardized invalid-sort behavior across
  most APIs.
- Consolidated repeated frontend grid, formatting, and request-lifecycle
  behavior where completed.

Outcome: major Portal workflows now depend on explicit ports, with adapters in
the host and architecture tests pinning the direction of dependencies.

### 5. Portal client modernization

- Updated linting, TypeScript, Vite, and React test configuration.
- Cleared npm advisories and restored a clean audit at the time of the
  remediation.
- Added or strengthened client unit and end-to-end gates.
- Removed dead exports, components, state, and CSS identified by review.
- Repaired the Visual Studio SPA proxy for Windows/Parallels:
  dependencies and the Vite workspace run from an NTFS-local mirror while
  source edits continue to synchronize from the shared checkout.
- Standardized on Node.js 24 through `.nvmrc`, package-engine constraints, and
  all active workflows.
- Added current environment-variable instructions for disabling or enabling
  Portal outbound mail while debugging.

Outcome: the client has reproducible Node 24 tooling, active type/lint/test
gates, and a verified Visual Studio startup path.

### 6. Communication provider split

- Created provider-neutral communication requests, results, failures, and
  email/SMS delivery ports.
- Extracted the provider-neutral delivery workflow.
- Extracted dedicated adapters for:
  - SendGrid;
  - Microsoft Graph;
  - TransmitSMS.
- Added options validation, startup validation, HTTP timeouts, cancellation,
  safe error translation, and adapter contract tests.
- Added Microsoft Graph app-only mail support, including bounded large
  attachment upload sessions.
- Made each monitor composition root explicitly select SendGrid or Microsoft
  Graph and always compose TransmitSMS.
- Kept Portal deliberately SendGrid-only behind its own adapter.
- Migrated Reporting messaging to depend on communication abstractions.
- Removed the obsolete `Rvt.Monitor.Common.Infrastructure` communication
  project and guarded its absence.

Outcome: applications depend on provider-neutral ports, while credentials,
SDKs, and provider selection stay in explicit adapters and composition roots.

### 7. Storage provider split

- Created streaming object-storage abstractions.
- Extracted Local, Azure Blob, and S3 adapters.
- Added parity, failure, cancellation, timeout, and dependency-boundary tests.
- Migrated Svantek audio storage to the object-storage contract.
- Migrated reporting output storage and URI resolution.
- Removed the former shared/common storage implementations.
- Added configuration documentation for provider, container, prefix, endpoint,
  region, service URI, and path-style behavior.

Outcome: monitor and reporting consumers use provider-neutral streaming
storage without direct Azure or AWS dependencies.

### 8. PostgreSQL and TimescaleDB consolidation

- Declared PostgreSQL as the only supported application database.
- Removed Portal SQL Server dialects, packages, migrations/assets, and runtime
  provider branches.
- Converted shared monitor persistence and all vendor monitors to PostgreSQL.
- Consolidated canonical PostgreSQL mapping, schema validation, and routine
  deployment.
- Added and strengthened the PostgreSQL-only repository guard and regression
  fixtures.
- Standardized database naming on lowercase, singular, snake_case identifiers,
  with `id` primary keys and canonical foreign-key naming.
- Added TimescaleDB-backed integration testing and made missing monitor
  integration configuration fail rather than silently skip.
- Documented the two integration variables:
  `RVT__POSTGRES_INTEGRATION_CONNECTION` for monitor suites and
  `RVT_TEST_POSTGRES_CONNECTION` for Portal opt-in tests.

Outcome: one database contract now drives application code, migrations,
deployment, integration tests, and CI.

### 9. Monitor hosting, vendor HTTP, and job execution

- Consolidated per-monitor job names and argument parsing into shared job
  catalogs.
- Threaded shutdown cancellation through one-shot jobs.
- Added bounded Svantek HTTP behavior and regression coverage.
- Extracted a shared `VendorHttpTransport` and request policy, leaving thin
  vendor-specific gateways.
- Added or completed vendor ports for AirQ, Omnidots, Svantek, and MyAtm.
- Consolidated Omnidots fleet imports and corrected cancellation so a stopped
  run is not recorded as a monitor failure.
- Strengthened failure aggregation, safe logging, timeout classification, and
  cancellation propagation.
- Added test infrastructure shared across monitor composition and integration
  suites.

Outcome: monitor hosts share execution and transport policy while retaining
vendor-specific contracts and failure semantics.

### 10. Durable alerting and legacy messaging retirement

- Built the shared durable alert ingress, occurrence deduplication,
  suppression, transactional commit, delivery planning, outbox,
  claim/lease/fencing, retry, and dead-letter stack.
- Migrated Omnidots webhook, offline, and battery alerting.
- Migrated AirQ and Svantek rule, offline, battery, and site-average alerting.
- Retained MyAtm's product-specific durable outbox while sharing terminal
  decision, retry, and safe-error policy.
- Aligned outbox error truncation and extracted shared dispatch policy.
- Added provider adapters for MQTT, email, and SMS delivery.
- Deleted the legacy synchronous path:
  `IMessageService`, `MessageService`, legacy message enums/channels,
  `CommsException`, the duplicate contact DTO, inline dispatcher loops,
  compatibility delivery routes, and sync-over-async call sites.
- Added architecture tests that pin the synchronous caller allowlists at zero.
- Added a durable-alert architecture guide describing the current flow and
  product-specific dispatcher split.

Outcome: all active monitor alerts are durably committed before delivery and
are processed asynchronously with idempotency, retries, ownership fencing, and
dead-letter behavior.

### 11. Reporting consolidation

- Consolidated duplicate reporting implementations into
  `apps/monitors/reportingmonitor`.
- Decomposed reporting into Core, Messaging, PDF, Storage, and host projects.
- Preserved report generation, attachments, recipient handling, persisted
  outcomes, test-recipient mode, and AI narrative behavior behind explicit
  ports.
- Removed the former top-level `services/reporting` copy.
- Added report-content callback authentication and Portal release readiness
  documentation.

Outcome: the monorepo has one active reporting implementation rather than two
diverging copies.

### 12. Test and CI guardrails

- Added a pull-request `Tests` workflow with:
  - a TimescaleDB service;
  - locked .NET restore;
  - the complete aggregate .NET suite;
  - Portal TypeScript compilation and unit tests;
  - all five primary repository boundary guards;
  - every `tests/*.test.sh` contract test discovered by glob.
- Added an `Engineering standards` workflow for model, policy, configuration,
  shell, workflow-contract, and changed-range checks.
- Added a manual `SonarQube` workflow with:
  - JDK 17;
  - the pinned .NET SDK;
  - Node.js 24;
  - disposable PostgreSQL/Timescale databases;
  - release build;
  - Portal schema deployment;
  - .NET and client coverage;
  - quality-gate waiting.
- Added mutation/regression tests for each repository guard.
- Added AirQ and Portal architecture guards.
- Repaired monorepo-relative test and release paths.
- Removed obsolete tests that asserted retired behavior and restored MSTest
  discovery where coverage was valid.
- Replaced a load-dependent PID timing assertion with an outcome-based lock
  ownership check.

Outcome: pull requests now exercise source, client, integration, architecture,
documentation, and repository contracts rather than relying on manual local
verification.

### 13. Engineering standards and maintainability

- Added a repository-wide ratcheting standards model.
- Added Roslyn, formatting, ESLint, Prettier, shell, namespace, parameter,
  logging, and repeated-literal policies.
- Promoted recurring Sonar findings into guardrails instead of repeatedly
  fixing individual instances.
- Converted local variables to explicit types repo-wide.
- Applied file-scoped namespaces, collection expressions, primary
  constructors, formatting, unused-using cleanup, and targeted field naming.
- Added efficient logging diagnostics and sanitized-exception logging checks.
- Added portable shell conditional and repeated-literal scanners.
- Shrunk the standards baseline from the recorded initial
  1,994 entries / 7,709 tolerated diagnostics to the current
  209 entries / 534 diagnostics.

Current baseline composition:

| Tool | Entries | Tolerated diagnostics |
| --- | ---: | ---: |
| `dotnet-format-style` | 198 | 522 |
| ESLint | 1 | 2 |
| Prettier | 10 | 10 |
| **Total** | **209** | **534** |

The largest remaining rule is `IDE1006`: 140 entries and 396 diagnostics.

Outcome: touched code must meet current standards and the inherited debt can
only remain flat or decrease.

### 14. Security, Sonar, and reliability remediation

- Added SonarCloud analysis for the full monorepo, including .NET and client
  coverage.
- Added a self-hosted ARM64 runner stack and disposable integration database.
- Removed Sonar security and reliability findings across Portal, monitors,
  libraries, tests, shell, and configuration.
- Added fixed-time comparisons and safer API-key boundaries.
- Removed credential-like test/configuration patterns or documented approved
  non-secret exclusions.
- Added guardrails for repeated security, reliability, shell-literal,
  long-parameter, namespace, logging, and maintainability findings.
- Improved cancellation propagation and failure logging in Portal archive and
  monitor-summary paths.
- Removed personal/default recipient and serial allow-list values from
  Omnidots checked-in configuration.
- Retired the unreferenced `RVTUtilities` project and its dedicated tests.
- Repaired the common-source-boundary verifier after package validation was
  intentionally retired.

Outcome: the repeated classes of Sonar findings now have repository policy or
tests, while the remaining quality-gate failures are visible and measurable.

### 15. Dead-code and legacy cleanup

- Removed the unreferenced `RVTUtilities` project.
- Removed the duplicate top-level reporting service.
- Removed approximately 1,600 lines in the second dead-code sweep, including:
  - 13 unqueried EF view entities and their mappings/snapshot residue;
  - unused repository ports, adapters, and DI registrations;
  - dead service and repository members;
  - dead legacy enum members;
  - MyAtm's production-dead compatibility delivery route;
  - unused runtime-default resolver registrations.
- Removed stale package/release infrastructure no longer valid for an
  internal-source monorepo.
- Removed SQL Server assets and provider branches.
- Removed legacy synchronous communications and duplicate contact contracts.
- Removed obsolete frontend exports, components, state, and CSS.
- Archived historical plans and state rather than leaving them as current
  operating instructions.

Outcome: large imported compatibility surfaces were either deleted or moved
behind explicit, tested boundaries.

### 16. Toolchain and developer experience

- Pinned the repository SDK to .NET `10.0.302` in `global.json`.
- Standardized active .NET projects on `net10.0`.
- Standardized the Portal client and workflows on Node.js 24.
- Added locked restores and repeatable root build/test entry points.
- Repaired Visual Studio Portal startup through the `https` profile and
  Windows-local Vite launcher.
- Documented user secrets, Visual Studio environment variables, integration
  database variables, deployment secrets, and provider configuration.
- Added the Portal `RVT__Email_ENABLED` operational switch and local-safe
  launch default.

Outcome: local development, CI, Sonar, and Visual Studio use the same major
toolchain and documented configuration model.

## Day-by-day delivery timeline

### 2026-07-22 — import and immediate stabilization

- Designed and imported the monorepo.
- Added aggregate solution and layout/source guards.
- Centralized documentation.
- Restored reporting/vibration runtime paths.
- Hardened tenant authorization, public authentication, and email transitions.

### 2026-07-23 — Portal boundaries and release readiness

- Made authorization time deterministic.
- Defined Portal timestamp and UTC contracts.
- Required complete PostgreSQL schema deployment.
- Hardened the release platform.
- Began and largely completed the Sites application-boundary extraction.

### 2026-07-24 — communication decomposition

- Extracted communication abstractions and workflow.
- Added SendGrid, Microsoft Graph, and TransmitSMS adapters.
- Migrated monitor, Portal, and reporting consumers.
- Removed the old common communications infrastructure.

### 2026-07-25 — storage and database direction

- Added Local, Azure Blob, and S3 storage providers.
- Migrated Svantek and reporting storage consumers.
- Serialized sensitive archive/notification writes.
- Added host-filtering tests.
- Designed and began the PostgreSQL-only cutover.

### 2026-07-26 — PostgreSQL cutover and analysis infrastructure

- Removed Portal and monitor SQL Server dependencies.
- Canonicalized shared PostgreSQL persistence.
- Converted AirQ, Svantek, MyAtm, Omnidots, and ReportingMonitor.
- Defined the shared release train.
- Added the manual monorepo SonarQube workflow and runner hardening.

### 2026-07-27 — governance and test repair

- Repaired monorepo-relative test paths.
- Restored and validated MSTest coverage.
- Added engineering-standards ratcheting.
- Added standards reports and guardrails.
- Stabilized the Sonar runner and repository verification.

### 2026-07-28 — broad remediation

- Added Help Admin application and release-audit boundaries.
- Modernized Portal linting and cleared npm advisories.
- Repaired Visual Studio/Parallels SPA startup.
- Executed multiple Sonar security and reliability remediations.
- Retired `RVTUtilities`.
- Consolidated reporting.
- Completed the explicit-local-types pass.
- Performed full architecture, duplication, and legacy reviews.

### 2026-07-29 — CI, durability, cleanup, and convergence

- Added complete PR tests and repository guards.
- Consolidated monitor hosting, vendor HTTP, and test infrastructure.
- Executed the second dead-code sweep.
- Migrated Omnidots, AirQ, and Svantek to durable alerting.
- Deleted `IMessageService` and the legacy synchronous messaging family.
- Shared dispatch terminal/error policy.
- Removed Omnidots static-token and assembly-sniffing configuration seams.
- Reduced the standards baseline to 209 entries / 534 diagnostics.
- Fixed the load-dependent standards-lock test.
- Updated Portal email environment-variable documentation.

## Verification evidence

### Repository and test verification

The latest recorded merged-tree full-suite run in `project_state.md` reports:

- 2,379 / 2,379 .NET tests passed;
- AirQ: 140 / 140;
- Omnidots: 403 / 403;
- Portal: 560 / 560;
- zero skipped tests when both PostgreSQL variables were supplied;
- all five primary repository guards passed;
- every root shell contract test passed;
- the engineering-standards ratchet passed.

Current repository enforcement consists of:

- 42 projects in `Rvt.Mono.slnx`;
- 16 .NET test projects;
- 16 shell contract tests and 2 Node contract-test programs;
- three GitHub workflows;
- seven root verification scripts.

### SonarCloud quality gate

Latest recorded analysis:

| Field | Value |
| --- | --- |
| Project | `aileron-forward_rvt-mono` |
| Branch | `main` |
| Analysis revision | `7976b211b27bd28de8dff4546251304c833dcc96` |
| Analysis date | 2026-07-29 |
| Server processing | Successful |
| Quality gate | Failed |

Quality-gate conditions, with failures first:

| Condition | Status | Required | Actual |
| --- | --- | ---: | ---: |
| New reliability rating | Failed | A (`1`) | C (`3`) |
| New security rating | Failed | A (`1`) | B (`2`) |
| New coverage | Failed | at least 80% | 16.3% |
| New maintainability rating | Passed | A (`1`) | A (`1`) |
| New duplicated-line density | Passed | at most 3% | 0.9% |
| New security hotspots reviewed | Passed | 100% | 100% |

The analysis revision is older than this report's head. The current `main`
therefore requires a fresh analysis before these values can be attributed to
the complete current tree.

## Current outcome

The monorepo is no longer just a colocated import. It now has:

- one build and test graph;
- one database contract;
- explicit provider and application boundaries;
- one active reporting implementation;
- durable alert delivery;
- current CI and repository governance;
- measurable and shrinking inherited debt;
- a documented release and operational model.

The largest structural programmes started after import—communications,
storage, PostgreSQL-only persistence, monitor hosting, durable alerts, legacy
messaging retirement, reporting consolidation, and `RvtConfig` kind-detection
cleanup—have landed.

## Remaining work and recommended order

### 1. Re-establish the current Sonar baseline

Run the manual Sonar workflow against the current `main`, confirm the analyzed
revision equals the reporting head or newer, and retrieve the exact reliability
and security issues behind the B/C ratings.

### 2. Raise new-code coverage

The current measured 16.3% is far below the 80% gate. Prioritize tests around:

- Portal application and adapter branches added during extraction;
- durable alert commit, retry, ownership-loss, and dead-letter paths;
- vendor gateway timeout/cancellation/error classification;
- reporting storage and report-generation boundaries;
- client error, invalid-input, and destructive-action flows.

### 3. Resolve remaining correctness risks

Re-verify and close the still-relevant findings from the latest repository
reviews, especially:

- timezone and business-day calculations;
- malformed client date handling and destructive-action confirmation;
- missing-resource 404 behavior;
- swallowed or under-specified exception logging;
- stale generated OpenAPI client schema.

### 4. Continue Portal boundary extraction

- reduce `RVT.BusinessLogic` dependencies;
- continue vertical extraction out of the SPA host;
- extend architecture guards to adapter boundaries;
- unify Portal object-storage creation behind
  `IObjectStorageClientFactory`.

### 5. Finish standards debt

Decide and apply the private-field naming policy responsible for 396 of the
remaining 534 tolerated diagnostics, then regenerate the monotonic baseline.
Continue removing the smaller IDE2003, IDE0130, formatting, and React Fast
Refresh residues only through verified fixes.

### 6. Close release evidence

Produce and retain production Help Admin audit receipts, verify the deployed
OpenAPI/client boundary, and execute the cutover runbooks against the actual
target environment.

### 7. Strengthen main-branch enforcement

The tests and engineering-standard workflows currently run on pull requests.
Confirm branch protection prevents unverified direct pushes, or add a
push-to-`main` gate. Keep Sonar manual only if that remains an explicit product
decision.

## Evidence index

- Current state: `project_state.md`
- Historical state: `docs/history/project-state/2026-07-checkpoint-log.md`
- Aggregate solution: `Rvt.Mono.slnx`
- Engineering baseline: `eng/standards/baseline.json`
- Current full review: `docs/reviews/2026-07-29-full-repo-review.md`
- Duplication/legacy programme:
  `docs/reviews/2026-07-28-duplication-legacy-consistency-review.md`
- Engineering standards report:
  `docs/reviews/2026-07-27-engineering-standards-enforcement-report.md`
- Durable alerts:
  `docs/architecture/rvt-monitor-common/durable-alerts.md`
- Communication split:
  `docs/architecture/rvt-monitor-common/communications.md`
- Portal release runbook: `docs/release/portal/CUTOVER_RUNBOOK.md`
- Portal development secrets:
  `docs/operations/portal/dev-secrets-reference.md`
- GitHub tests: `.github/workflows/tests.yml`
- Engineering standards: `.github/workflows/engineering-standards.yml`
- SonarCloud: `.github/workflows/sonarqube.yml`
