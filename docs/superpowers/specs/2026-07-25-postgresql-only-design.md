# PostgreSQL-Only Solution Design

**Date:** 2026-07-25  
**Status:** Approved  
**Branch:** `codex/postgresql-only`  
**Base:** `a07f6019fc492531a2f7d67294dd17ace47058db`

## Decision

PostgreSQL is the only supported relational database for the RVT
mono-repository. SQL Server runtime behavior, provider selection, packages,
schema assets, migration contracts, release instructions, and historical
artifacts will be removed.

The repository will retain this decision document, its implementation plan,
and the final `project_state.md` handoff as the audit record for the removal.
They are not compatibility promises and must not be interpreted as supported
SQL Server documentation.

## Context

The solution already treats PostgreSQL and TimescaleDB as canonical in
production:

- Portal EF migration snapshots are generated for Npgsql.
- `RVT.SchemaDeploy` applies the PostgreSQL schema and TimescaleDB objects.
- Reporting uses the PostgreSQL repository.
- Real provider integration tests target PostgreSQL/TimescaleDB.
- Recent concurrency and UTC contracts are defined against PostgreSQL.

Dual-provider code remains in the Portal and shared monitor persistence
layers. The initial inventory found:

- 184 active, non-history files containing SQL Server support language or
  provider-specific behavior;
- 63 C# source files containing provider branches or SQL Server APIs;
- 9 project/package files directly declaring `Microsoft.Data.SqlClient` or
  `Microsoft.EntityFrameworkCore.SqlServer`;
- 36 tracked paths whose names identify SQL Server-specific assets.

Keeping these paths implies a support level that the build and deployment
system does not certify. It also doubles query, mapping, migration, and
concurrency behavior without a live SQL Server verification environment.

## Goals

- Make every production database path PostgreSQL/Npgsql-only.
- Remove provider choice from application and shared-library APIs.
- Remove SQL Server packages and their transitive lockfile graph.
- Replace runtime T-SQL translation with canonical PostgreSQL SQL at source.
- Delete SQL Server schema, migration, rollback, registry, and rehearsal
  artifacts.
- Rewrite active documentation to describe a single PostgreSQL deployment.
- Remove archived SQL Server documents and update the documentation manifest.
- Fail fast when a stale deployment still supplies a SQL Server provider
  value.
- Add repository guards that prevent SQL Server support from returning.
- Preserve Portal, monitor, reporting, package-validation, and client behavior
  unrelated to database-provider selection.

## Non-Goals

- Migrating a live SQL Server database to PostgreSQL.
- Providing a compatibility adapter, dormant provider abstraction, or feature
  switch that can restore SQL Server.
- Preserving SQL Server rollback scripts in the active or historical tree.
- Reworking unrelated application architecture or domain behavior.
- Removing SQLite or EF InMemory from tests where they provide isolated,
  explicitly non-production coverage.

Any live data migration must be completed before this change is deployed. The
repository will not contain a SQL Server reader or conversion utility after the
removal.

## Chosen Approach

Use a hard PostgreSQL-only simplification.

Rejected alternatives:

1. **Compatibility shell:** retaining provider enums and rejecting SQL Server
   would leave dead abstractions, false configuration choices, and extra test
   paths.
2. **Long-lived phased support:** removing Portal and monitor support on
   separate release timelines would leave the mono-repository internally
   inconsistent.

Implementation may use small reviewed commits, but every merged state must
converge on the single PostgreSQL architecture described here.

## Target Architecture

### Portal data access

`RvtDatabaseOptions` remains the owner of connection, retry, timeout, schema
validation, and PostgreSQL routine-schema settings. It no longer exposes a
provider enum.

Portal registration will:

- configure all three EF contexts with `UseNpgsql`;
- create only `NpgsqlConnection`;
- preserve the separate domain, search, and Identity migration-history tables;
- retain `UtcTimestampGuardInterceptor`;
- retain the shared scoped connection used by `EfCoreUnitOfWork`;
- always render PostgreSQL routine calls and double-quoted identifiers.

The following concepts disappear:

- `RvtDatabaseProvider`;
- SQL Server aliases and default selection;
- `UseSqlServer` branches;
- `SqlConnection`;
- SQL Server stored-procedure command mode;
- provider-specific identifier delimiting.

The configuration key `Database:Provider` is no longer a selection mechanism.
A small startup compatibility guard may read legacy provider keys only to:

- accept an omitted value;
- accept explicit PostgreSQL aliases during deployment transition;
- reject `SqlServer`, `MSSQL`, and every non-PostgreSQL value with an
  actionable error stating that PostgreSQL is mandatory.

This validation is not a provider abstraction and must not select behavior.

### Portal queries and site writes

Archive queries use canonical PostgreSQL objects and expressions directly:

- `public.<table>`;
- `now()`;
- PostgreSQL identifier quoting.

Atomic site archive and notification-setting writes retain their PostgreSQL
`INSERT ... ON CONFLICT` implementation and unknown-commit reconciliation.
SQL Server locked-batch branches and `SiteSqlServerDmlTests` are deleted.

SQLite and EF InMemory paths remain only where current tests intentionally
exercise non-production compatibility. They must not be described as supported
deployment providers or concurrency evidence.

### Shared monitor persistence

`Rvt.Monitor.Common` becomes PostgreSQL-only:

- `MonitorDatabaseProvider` is deleted.
- `MonitorDbOptions` carries only PostgreSQL-relevant options and identifier
  mappings.
- Connections are always `NpgsqlConnection`.
- Parameters are always `NpgsqlParameter`.
- Bulk ingestion always uses PostgreSQL binary `COPY`.
- EF options always use `UseNpgsql`.
- Model-cache keys no longer vary by provider.
- Entity mappings use canonical PostgreSQL table, column, constraint, index,
  and store-type names directly.

The current runtime SQL rewriter exists to translate legacy T-SQL:

- bracketed identifiers;
- `dbo.` prefixes;
- PascalCase SQL identifiers;
- `DATEPART`;
- integer boolean comparisons.

That compatibility layer is removed. Every live query must be converted to
canonical PostgreSQL SQL at its source before the rewriter is deleted. No
method may continue accepting a SQL Server and PostgreSQL SQL pair.

### Monitor applications

AirQ, MyATM, Omnidots, Svantek, and the monitor reporting host will consume the
PostgreSQL-only common API.

Each application will:

- remove direct EF Core SQL Server package references;
- replace dual table/column mapping helpers with canonical PostgreSQL names;
- remove SQL Server timestamp normalization paths;
- remove SQL Server migration tests and fixtures;
- retain PostgreSQL/TimescaleDB integration and contract tests.

The monitor reporting host already rejects non-PostgreSQL providers. Its
registration will be simplified so PostgreSQL is construction-time truth, not
a checked enum value.

### Reporting service

The reporting service is already PostgreSQL-only. Its implementation remains
unchanged except for removing obsolete SQL Server migration commentary and
ensuring package locks and documentation contain no retired provider claim.

## Schema and migration assets

Delete:

- `apps/portal/database/sqlserver/`;
- monitor `.sqlserver.sql` migrations and rollback scripts;
- `apps/monitors/omnidotsmonitor/OmnidotsMonitor/sqlserver/`;
- shared monitor SQL Server migrations;
- SQL Server name registries, routine/view extracts, rehearsal documents, and
  migrator-equivalence files;
- SQL Server-specific migration contract tests.

Keep and verify:

- all PostgreSQL EF migration chains and snapshots;
- `apps/portal/database/postgres/`;
- monitor PostgreSQL schema/migration scripts;
- TimescaleDB schema and integration fixtures;
- PostgreSQL rollback scripts that remain part of supported deployment.

Deleting historical SQL Server artifacts is intentional. Git history remains
the recovery mechanism for obsolete files.

## Packages and lockfiles

Remove direct declarations of:

- `Microsoft.Data.SqlClient`;
- `Microsoft.EntityFrameworkCore.SqlServer`.

Regenerate all affected `packages.lock.json` files from the PostgreSQL-only
project graph. The completion guard must prove that neither package remains in
project files or lockfiles, including package-validation consumers.

Other packages must not be upgraded merely to make lockfile regeneration
easier. The five existing `System.Security.Cryptography.Xml` 10.0.7 NU1903
advisories remain separate work unless dependency resolution proves they are
removed as a direct consequence of deleting SQL Server packages.

## Configuration and failure behavior

Supported connection configuration remains:

- Portal: `ConnectionStrings:DefaultConnection` or
  `Database:ConnectionString`;
- monitor applications: their existing PostgreSQL connection-string variable;
- provider integration tests: `RVT_TEST_POSTGRES_CONNECTION` and
  `RVT__POSTGRES_INTEGRATION_CONNECTION` as currently scoped.

Provider-selection documentation and examples are deleted. During transition,
stale provider variables fail startup when they name SQL Server or an unknown
provider. Errors must not print connection strings or credentials.

No fallback connection string with embedded credentials is introduced.
Design-time EF tooling continues to require `RVT_EF_CONNECTION` and always
uses Npgsql; `RVT_EF_PROVIDER` is retired.

## Documentation

Active architecture, database, development, deployment, onboarding, module,
operations, and release documents will be rewritten around PostgreSQL-only
commands and terminology.

Historical documents and SQL Server-specific evidence files will be deleted,
not moved to a new archive. `docs/documentation-move-manifest.md` and its
verification tests must be updated so the documentation consolidation guard
continues to pass.

The new decision/specification, implementation plan, and final state handoff
remain because they explain why files were deleted and define the continuing
guard.

## Guardrails

Add an executable repository guard and test that reject:

- SQL Server package references or lockfile entries;
- `Microsoft.Data.SqlClient`, `UseSqlServer`, `SqlConnection`,
  `SqlBulkCopy`, or SQL Server EF namespaces in production/test source;
- provider enum members or branches representing SQL Server;
- active configuration values or help text offering SQL Server;
- SQL Server-named schema/migration directories and files;
- active operational documentation claiming SQL Server support.

The guard may allow the term only in this removal decision, its implementation
plan, final state handoff, and an explicitly maintained allowlist for unavoidable
third-party advisory metadata. It must not use a broad `docs/` exemption.

## Testing Strategy

All behavior changes follow strict red/green TDD.

1. Add the repository guard and verify it fails against the current tree.
2. Remove one provider boundary at a time and keep its focused tests green.
3. Convert SQL and mappings before deleting the runtime compatibility helper.
4. Regenerate locks and prove the guard detects a deliberately restored
   forbidden package entry.
5. Run the complete verification matrix:
   - `scripts/build-mono.sh`;
   - Portal Application and SPA suites;
   - all monitor and shared-library suites;
   - reporting suites;
   - client tests and production build when API contracts change;
   - mono-layout, mono-solution, shared-package, and documentation guards;
   - Portal EF `has-pending-model-changes` for every migration context;
   - PostgreSQL schema-deployment/idempotency tests;
   - `git diff --check`.

Live PostgreSQL/TimescaleDB tests run when their two connection variables are
available. If unavailable, the final report must name the exact skipped suites
and must not claim live-provider closure.

## Baseline Repair

The clean isolated worktree exposed a pre-existing test-host defect before the
provider removal began. `WebApplicationFactory` resolved
`HostFilteringOptions.AllowedHosts` to `*`, so
`ForgotPassword_WithDisallowedHost_IsRejectedBeforeDelivery` returned success
instead of proving host rejection.

Commit `1683eed` sets the test host's `AllowedHosts` setting to
`localhost;127.0.0.1`. The original focused test then passed, and the complete
Portal SPA suite passed 415 tests with the same nine live-PostgreSQL skips.
This repair is intentionally isolated from the provider-removal commits.

The aggregate baseline built successfully. Its monitor test phase cannot be
green without `RVT__POSTGRES_INTEGRATION_CONNECTION`; 33 AirQ and 64 Omnidots
integration cases failed at their explicit environment guard. This is recorded
as an environment requirement, not treated as a product regression.

## Rollout

Before deploying the PostgreSQL-only build:

1. Confirm every supported environment already uses PostgreSQL/TimescaleDB.
2. Complete any live data migration outside this repository.
3. Remove stale SQL Server provider variables and connection strings from
   deployment secret stores.
4. Apply the canonical PostgreSQL EF migrations and `RVT.SchemaDeploy`.
5. Run live provider verification against the deployment candidate.
6. Deploy application and monitor images only after schema verification.

Rollback means returning to the previous application release and its matching
repository revision. This change does not ship SQL Server rollback scripts or
promise that a PostgreSQL-mutated deployment can be moved back to SQL Server.

## Acceptance Criteria

- No production or test project references SQL Server client or EF packages.
- No package lock contains those packages.
- No runtime provider selector can choose SQL Server.
- All live SQL and EF mappings are canonical PostgreSQL.
- SQL Server schema/migration/history artifacts are absent.
- Stale SQL Server configuration fails safely and visibly.
- Repository guards prevent reintroduction.
- Supported non-provider behavior remains green across the mono-repository.
- Live-provider gaps and existing dependency advisories are reported exactly.
- `project_state.md` contains the final file structure, configuration variables,
  verification results, and rollout constraints.
