# RVT Mono-Repository

This repository contains three RVT modules:

- `apps/monitors`
- `apps/portal`
- `libs/rvt-monitor-common`

## Client release changelist — 2026-07-30

This reviewable build advances the client release from monorepo source commit
`a9b1bd2` (the source recorded by the previous `RVT-monitors`
`release-candidate`) through `eb5aa3dd`. The range contains 134 commits and
changes 467 files.

### Monitor correctness, resilience, and security

- Bounded vendor HTTP response downloads to prevent untrusted endpoints from
  causing unbounded memory consumption, with regression coverage for AirQ and
  Svantek clients.
- Corrected monitor job cancellation and fleet-failure handling so cancellation
  is propagated while independent monitor failures remain isolated.
- Corrected battery alert latch ordering, measurement parsing, unknown-serial
  handling, and monitor calibration-date normalization to UTC.
- Moved MyAtm delivery/outbox behavior, persistence, migrations, and tests out
  of the shared kernel and into the owning monitor.
- Removed the dead synchronous alerting and async-migration compatibility
  surfaces from all monitors and the common library.
- Widened persisted outbox error details to match the safe-error contract and
  hardened persistence exception classification and unknown-monitor handling.
- Consolidated rule activity evaluation on UTC wall-clock semantics and
  excluded deleted global rules from global rule reads.
- Removed obsolete runtime-default, query-fallback, notification-compatibility,
  and duplicate DTO surfaces from the monitor common library.
- Added technology-confinement and dependency-boundary tests that prevent
  monitor-specific or persistence-specific behavior from leaking back into the
  shared kernel.
- Aligned the Omnidots monitor trace schema with the Portal-owned canonical
  column and added cross-component schema-fixture coverage.
- Prevented future-dated samples and pre-backfill rule windows from advancing
  monitor watermarks, and represented empty averaging windows as missing data
  instead of a false zero-decibel reading.

### Portal backend and PostgreSQL convergence

- Dissolved the legacy `RVT.BusinessLogic` project into the application and SPA
  layers, eliminating an unnecessary project boundary and legacy repository
  chain.
- Removed unused repositories, entities, paged site APIs, and other
  production-dead Portal endpoints and compatibility code.
- Replatformed the SPA test host onto isolated PostgreSQL schemas and removed
  the remaining production `InMemory`/`IsRelational` fallback branches.
- Added PostgreSQL-backed coverage for Help reads, report-rule queries, search
  timestamps, site concurrency, and other database-sensitive behavior.
- Propagated cancellation through deployment-data reads and API middleware.
- Batched unattached-monitor removal-impact reads per page to avoid repetitive
  database access.
- Converged Portal business-day, dashboard calendar, monitor-list, and seeded
  test time calculations on injected clocks and UTC semantics.
- Standardized invalid-sort failures on the shared Problem Details response.
- Hardened storage, upload, archive, notification, report-generation, and
  vendor adapter boundaries identified by the full code review.
- Preserved the contract's final day in site archives, resolved archive tables
  through the active PostgreSQL search path, and exercised archive exports
  against real PostgreSQL.

### Portal client behavior and maintainability

- Split the large application shell and administration, contract/site,
  monitor, notification/alert-level, and map/calendar panels into focused
  modules.
- Added focused company, user, site, monitor detail, assignment, removal,
  contract, alert-level, notification, authentication, privacy, and shared
  routing components.
- Prevented calendar-summary refetches on every deployment or selected-site
  change.
- Added cancellation and visible failure handling to installer status checks.
- Guarded date and URL-date parsing, read calendar detail days as served UTC
  dates, unified confirmation behavior, and stopped shell data from refetching
  on navigation-only changes.
- Removed dead frontend code and regenerated the OpenAPI client schema to match
  the current backend contract.

### Build, test, and architecture guardrails

- Added a shared monorepo setup action and deduplicated the engineering,
  testing, and Sonar workflows.
- Added change detection so documentation-only changes avoid unnecessary code
  work while main-branch code changes remain covered.
- Kept product, architecture, and engineering model/configuration checks active
  in curated client releases while gating the internal documentation and
  repository-contract fixtures they exclude and the source-development
  changed-range ratchet that does not apply to a publication-history diff.
- Made changed-range engineering checks use the pull request's actual base
  commit so review branches work when the client default branch is not `main`.
- Removed an internal-state dependency from storage architecture-test root
  discovery so the shipped tests run in the sanitized client checkout.
- Strengthened direct-project-reference, source-boundary, technology-
  confinement, PostgreSQL-only, and workflow-ordering guards.
- Converted architecture conventions discovered during review into executable
  tests, including negative mutation fixtures.
- Hoisted duplicated monitor and Portal test helpers into the integration test
  kit.
- Expanded regression coverage across monitor delivery, persistence, response
  limits, Portal API contracts, PostgreSQL behavior, and frontend request
  ownership.
- Recorded the product rulings, hexagonal-convergence and post-remediation
  reviews, remediation decisions, and completed P1/P2/P3 close-out evidence in
  the repository documentation.

The reporting workload lives in `apps/monitors/reportingmonitor`. The former
standalone `services/reporting` copy was a duplicate of that code and was
removed; see the
[reporting consolidation record](docs/modules/reporting/migration-notes.md).

Start with the [documentation index](docs/index.md) for architecture,
development, operations, release, database, module, and historical guidance.
All new and modified logical units follow the
[RVT Engineering Standards](docs/development/engineering-standards.md) under
[ratcheted enforcement](docs/development/engineering-standards-enforcement.md).
The current implementation and verification evidence are recorded in the
[engineering standards enforcement report](docs/reviews/2026-07-27-engineering-standards-enforcement-report.md).

Every pull request runs two required workflows: `Engineering standards` grades
the changed surface, and `Tests` runs the whole `Rvt.Mono.slnx` suite against an
ephemeral TimescaleDB service container, the Portal client type check and unit
tests, and every repository guard and contract test.

Run the repository guards from the root:

```bash
bash scripts/verify-postgresql-only.sh .
tests/verify-mono-solution.test.sh
tests/verify-mono-layout.test.sh
```

PostgreSQL is the repository's only supported relational database. Portal uses
three Npgsql EF migration histories plus `RVT.SchemaDeploy` for canonical
PostgreSQL and TimescaleDB objects; monitors and reporting use the same
PostgreSQL/TimescaleDB contract. Provider selection is retired, and the
PostgreSQL-only guard runs automatically at the start of every aggregate build.

Restore the aggregate solution, build the complete project-reference graph,
and run its tests with:

```bash
scripts/build-mono.sh
```

All internal RVT dependencies use direct `ProjectReference` entries. The
aggregate build does not pack, publish, or restore internal RVT NuGet packages
and does not require package-feed credentials. NuGet remains in use only for
third-party dependencies.

Monitor container definitions use the monorepo root as their build context so
their direct references to `libs/rvt-monitor-common` resolve inside the image
build. See
[`docs/operations/monitors/container-builds.md`](docs/operations/monitors/container-builds.md).

The nearest imported `AGENTS.md` governs work within a module. Before working
in `apps/portal`, read
[`docs/development/portal/development-guidelines.md`](docs/development/portal/development-guidelines.md)
first.
