# PostgreSQL-Only Solution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove SQL Server support, provider choice, packages, schema assets,
tests, and documentation from the mono-repository so PostgreSQL/Npgsql is the
only production relational database contract.

**Architecture:** Portal data access and shared monitor persistence become
direct PostgreSQL adapters. Provider enums, runtime SQL translation, paired SQL
strings, conditional EF mappings, and compatibility packages disappear. A
small validation seam may inspect legacy provider settings only to reject
non-PostgreSQL values; it never selects behavior. SQLite and EF InMemory remain
test-only tools.

**Tech Stack:** .NET 10, C# 14, EF Core 10.0.7, Npgsql,
PostgreSQL/TimescaleDB, xUnit, MSTest, Bash, npm/Vite.

## Global Constraints

- Implement the approved design in
  `docs/superpowers/specs/2026-07-25-postgresql-only-design.md`.
- Preserve Portal HTTP routes, response envelopes, authorization behavior,
  three independent EF migration histories, `UtcTimestampGuardInterceptor`,
  and the shared scoped connection used by `EfCoreUnitOfWork`.
- Keep `RvtPortal.Application` BCL-only and preserve the source-reference
  `Rvt.Monitor.Common` package boundary.
- Keep SQLite and EF InMemory only where tests deliberately use them; neither
  may appear as a supported deployment provider.
- Do not add a SQL Server reader, conversion utility, dormant compatibility
  adapter, provider enum, or provider-specific feature switch.
- Accept an absent legacy provider value and explicit `postgres`, `postgresql`,
  `npgsql`, `timescale`, or `timescaledb` values. Reject every other non-empty
  value before opening a connection, with a credential-free error containing
  `PostgreSQL is the only supported database provider`.
- Portal design-time EF tooling requires `RVT_EF_CONNECTION` and always uses
  Npgsql. Remove `RVT_EF_PROVIDER`.
- Preserve `RVT_TEST_POSTGRES_CONNECTION` for Portal live tests and
  `RVT__POSTGRES_INTEGRATION_CONNECTION` for monitor/TimescaleDB tests. Never
  invent or print their values.
- Delete the exact retired paths named in this plan. The user explicitly
  authorized deletion of active and historical SQL Server artifacts; Git
  history is their recovery path.
- Remove only `Microsoft.Data.SqlClient` and
  `Microsoft.EntityFrameworkCore.SqlServer` as a consequence of this work. Do
  not opportunistically upgrade other dependencies.
- Treat the five existing `System.Security.Cryptography.Xml` 10.0.7 NU1903
  advisories as an independent baseline unless they vanish transitively.
- Clean-worktree auth baseline commit `1683eed` is already green: 415 Portal
  SPA tests passed and nine live-PostgreSQL tests skipped. Do not fold it into
  provider-removal commits.
- The aggregate baseline restores and builds. Without
  `RVT__POSTGRES_INTEGRATION_CONNECTION`, 33 AirQ and 64 Omnidots integration
  cases stop at their explicit environment guard. Record unavailable live
  suites; never claim live-provider closure without running them.
- Do not touch the main checkout's `.codegraph/` or
  `apps/.nuget-packages/`. Work only in
  `/Users/oldgeorge/Documents/rvt-mono/.worktrees/postgresql-only`.
- Use strict red/green/refactor TDD. A focused test must fail for the intended
  reason before production behavior changes.
- Commit after every task. Before every commit run the task's focused tests and
  `git diff --check`.

---

### Task 1: Add the PostgreSQL-only repository guard

**Files:**
- Create: `scripts/verify-postgresql-only.sh`
- Create: `tests/verify-postgresql-only.test.sh`
- Modify: `scripts/build-mono.sh`

**Interfaces:**
- Consumes: a repository root argument, defaulting to the current checkout.
- Produces: a non-zero exit and path-qualified findings for forbidden packages,
  APIs, provider selection, configuration/help text, and retired path names.

- [ ] **Step 1: Write a black-box guard test**

The test creates temporary positive and negative repository fixtures. The
positive fixture contains Npgsql registration and neutral PostgreSQL
documentation. Separate negative cases inject one forbidden category at a
time: project package, lockfile package, C# API, provider configuration, prose,
and a retired path name. Build forbidden tokens from adjacent shell fragments
inside the test so the repository guard does not have to exempt its own test.

Run:

```bash
bash tests/verify-postgresql-only.test.sh
```

Expected RED: exit 127 because `scripts/verify-postgresql-only.sh` does not
exist.

- [ ] **Step 2: Implement the guard with a narrow audit allowlist**

The guard must inspect both tracked file contents and tracked path names. Its
only content/path exemptions are:

```text
docs/superpowers/specs/2026-07-25-postgresql-only-design.md
docs/superpowers/plans/2026-07-25-postgresql-only.md
project_state.md
scripts/verify-postgresql-only.sh
```

Do not exempt a directory. Make findings deterministic and redact file
contents; print the path and matched rule only.

- [ ] **Step 3: Prove fixture behavior GREEN**

Run:

```bash
bash tests/verify-postgresql-only.test.sh
```

Expected: the positive fixture passes; every mutation fixture is rejected.

- [ ] **Step 4: Establish the repository-wide RED baseline**

Run:

```bash
bash scripts/verify-postgresql-only.sh .
```

Expected RED: current Portal, monitor, lockfile, schema, and documentation
paths are reported. Save the output in the task notes, not in the repository.

- [ ] **Step 5: Wire the guard into the aggregate build**

Add the invocation after `repo_root` is resolved and before package restore,
but gate it temporarily with:

```bash
if [[ "${RVT_ENFORCE_POSTGRESQL_ONLY:-0}" == "1" ]]; then
  bash scripts/verify-postgresql-only.sh .
fi
```

Task 13 removes this transition gate. This keeps intermediate commits
buildable while the guard remains demonstrably RED.

- [ ] **Step 6: Verify and commit**

Run:

```bash
bash tests/verify-postgresql-only.test.sh
git diff --check
git add scripts/verify-postgresql-only.sh tests/verify-postgresql-only.test.sh scripts/build-mono.sh
git commit -m "test: guard PostgreSQL-only repository boundary"
```

---

### Task 2: Make Portal configuration and connections PostgreSQL-only

**Files:**
- Delete: `apps/portal/RVT.DataAccess/Configuration/RvtDatabaseProvider.cs`
- Modify: `apps/portal/RVT.DataAccess/Configuration/RvtDatabaseOptions.cs`
- Modify: `apps/portal/RVT.DataAccess/Configuration/IRvtDatabaseConnectionFactory.cs`
- Modify: `apps/portal/RVT.DataAccess/Configuration/RvtDatabaseConnectionFactory.cs`
- Modify: `apps/portal/RVT.DataAccess/Configuration/RvtDatabaseServiceCollectionExtensions.cs`
- Modify: `apps/portal/RVT.DataAccess/Configuration/RvtStoredRoutineExecutor.cs`
- Modify: `apps/portal/RVT.DataAccess/Context/RVTDbContext.cs`
- Modify: `apps/portal/RVT.DataAccess/Context/RvtDesignTimeDatabaseOptions.cs`
- Modify: `apps/portal/RVT.DataAccess/Context/RVTDbContextDesignTimeFactory.cs`
- Modify: `apps/portal/RVT.DataAccess/Context/RVTSearchContextDesignTimeFactory.cs`
- Modify: `apps/portal/RvtPortal.Spa/Data/ApplicationDbContextDesignTimeFactory.cs`
- Modify: `apps/portal/RvtPortal.Spa/Program.cs`
- Modify: `apps/portal/RvtPortal.Spa/appsettings.json`
- Modify: `apps/portal/RvtPortal.Spa.Tests/DatabaseProviderConfigurationTests.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/SpaTestApplicationFactory.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/SpaHostSmokeTests.cs`

**Interfaces:**
- `RvtDatabaseOptions` produces connection/retry/timeout/schema-validation and
  routine-schema settings, with no provider property.
- `RvtDatabaseOptions.ValidateLegacyProvider(string?)` accepts absent or
  PostgreSQL aliases and throws `InvalidOperationException` for all other
  values.
- `IRvtDatabaseConnectionFactory` produces `NpgsqlConnection` through its
  existing `DbConnection` return type and delimits identifiers with double
  quotes.
- `UseRvtDatabaseProvider` keeps its public name for call-site stability but
  always calls `UseNpgsql`.

- [ ] **Step 1: Replace provider-selection tests with PostgreSQL-only RED tests**

Cover:

```text
absent provider -> accepted
postgres/postgresql/npgsql/timescale/timescaledb -> accepted
SqlServer/MSSQL/oracle -> credential-free InvalidOperationException
CreateDbConnection -> NpgsqlConnection
all three EF contexts -> Npgsql provider
domain/search/Identity history table names remain distinct
routine executor -> CommandType.Text PostgreSQL function SQL
design-time without RVT_EF_CONNECTION -> actionable failure
```

Keep the connection string value a sentinel secret and assert it is absent
from exception messages.

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --no-restore -m:1 \
  --filter 'FullyQualifiedName~DatabaseProviderConfigurationTests'
```

Expected RED: tests still observe `RvtDatabaseProvider`, SQL aliases, and
SqlClient connections.

- [ ] **Step 2: Remove provider choice from the options contract**

Remove `Provider` and `ParseProvider`. During `FromConfiguration`, read
`Database:Provider`, then `RvtDatabase:Provider`, only to call:

```csharp
public static void ValidateLegacyProvider(string? value)
```

Normalize the five approved aliases. Every other non-empty value throws the
required PostgreSQL-only message without including any connection string.
Remove `Database:Provider` from checked-in appsettings after the tests prove
omission works.

- [ ] **Step 3: Collapse Portal registration to Npgsql**

Keep both `UseRvtDatabaseProvider` overloads and all history-table/retry/timeout
behavior. Remove SqlClient imports and `ConfigureSqlServer`. Always:

```csharp
optionsBuilder
    .AddInterceptors(UtcTimestampGuardInterceptor.Instance)
    .UseNpgsql(connectionOrConnectionString, npgsql => ConfigureNpgsql(...));
```

`CreateDbConnection` returns `new NpgsqlConnection(options.ConnectionString)`.
Remove `Provider` from `IRvtDatabaseConnectionFactory` and its implementation.

- [ ] **Step 4: Collapse stored routines and design-time factories**

Always render quoted PostgreSQL function calls and use `CommandType.Text`.
`RvtDesignTimeDatabaseOptions.FromEnvironment()` reads only
`RVT_EF_CONNECTION`; when absent, throw before creating options. Preserve the
three factory history-table arguments.

- [ ] **Step 5: Update the host and test factory**

Remove the test factory's SQL Server provider value without changing its
InMemory context replacements or the host-filter repair from `1683eed`.
Remove provider-selection comments and configuration from `Program.cs`.

- [ ] **Step 6: Run focused and complete Portal data-access tests**

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --no-restore -m:1 \
  --filter 'FullyQualifiedName~DatabaseProviderConfigurationTests|FullyQualifiedName~SpaHostSmokeTests'
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --no-restore -m:1
git diff --check
```

Expected: 415 non-live tests pass; the same nine live PostgreSQL tests skip
when `RVT_TEST_POSTGRES_CONNECTION` is absent.

- [ ] **Step 7: Commit**

```bash
git add apps/portal/RVT.DataAccess apps/portal/RvtPortal.Spa apps/portal/RvtPortal.Spa.Tests
git commit -m "refactor: make portal data access PostgreSQL-only"
```

---

### Task 3: Remove Portal SQL dialect and SQL Server site-write paths

**Files:**
- Modify: `apps/portal/RvtPortal.Spa/Adapters/Archive/SiteArchiveQueryCatalog.cs`
- Modify: `apps/portal/RvtPortal.Spa/Adapters/Archive/SiteArchiveQueryExecutor.cs`
- Modify: `apps/portal/RvtPortal.Spa/Adapters/Sites/EfSiteWriteAdapter.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/SiteArchiveServiceSecurityTests.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/DatabaseNamingConventionTests.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/CanonicalNamingSnapshotTests.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/MonitorListReaderSqlTests.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/MonitorOwnershipWindowSqlTests.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/SchemaValidatorTests.cs`
- Delete: `apps/portal/RvtPortal.Spa.Tests/SiteSqlServerDmlTests.cs`

**Interfaces:**
- Archive queries produce canonical `public`-schema PostgreSQL SQL with
  `now()` and Npgsql parameters.
- Site writes retain PostgreSQL `INSERT ... ON CONFLICT`, unknown-commit
  reconciliation, and intentional SQLite/InMemory test behavior.

- [ ] **Step 1: Add PostgreSQL query and site-write RED assertions**

Replace dual-provider theories with single canonical assertions. Cover quoted
identifiers, `public` schema, `now()`, `NpgsqlParameter`, both upserts, and the
absence of locked-batch SQL. Preserve SQL-injection and parameterization
coverage.

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --no-restore -m:1 \
  --filter 'FullyQualifiedName~SiteArchiveServiceSecurityTests|FullyQualifiedName~DatabaseNamingConventionTests|FullyQualifiedName~SiteConcurrencyTests'
```

Expected RED: catalog/executor constructors still require a provider and tests
still observe dual SQL.

- [ ] **Step 2: Make the archive query catalog canonical**

Delete `SiteArchiveSqlDialect`, provider constructor arguments, `getdate()`,
`dbo`, bracket delimiters, and SqlClient parameters. Keep one PostgreSQL SQL
definition per operation and create `NpgsqlParameter` directly.

- [ ] **Step 3: Remove SQL Server site-write DML**

Delete the SQL Server locked-batch branch and its private SQL constants from
`EfSiteWriteAdapter`. Keep relational detection required to distinguish the
canonical Npgsql path from intentional SQLite/InMemory tests. Delete
`SiteSqlServerDmlTests.cs`.

- [ ] **Step 4: Remove SQL Server-only test setup elsewhere**

Convert naming and schema-validator tests to Npgsql metadata or SQLite where
the assertion is provider-neutral. Remove `UseSqlServer` from every listed
test. Do not weaken canonical-name assertions.

- [ ] **Step 5: Verify and commit**

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --no-restore -m:1
git diff --check
git add apps/portal/RvtPortal.Spa apps/portal/RvtPortal.Spa.Tests
git commit -m "refactor: remove portal SQL Server dialects"
```

---

### Task 4: Remove Portal packages and retired database assets

**Files:**
- Modify: `apps/portal/RVT.DataAccess/RVT.DataAccess.csproj`
- Modify: affected `apps/portal/**/packages.lock.json`
- Delete: `apps/portal/database/sqlserver/`
- Delete: `apps/portal/docs/database/sqlserver-constraint-index-source.csv`
- Delete: `apps/portal/docs/database/sqlserver-name-registry.csv`
- Delete: `apps/portal/docs/database/sqlserver-routine-definitions-source.csv`
- Modify: `apps/portal/docs/database/database-constraint-index-name-registry.csv`
- Modify: `apps/portal/docs/database/database-name-registry.csv`
- Modify: `apps/portal/RVT.DataAccess/Context/ReadMe.txt`
- Modify: `apps/portal/AGENTS.md`

**Interfaces:**
- Portal project and lock graphs contain Npgsql but neither retired SQL Server
  package.
- PostgreSQL EF migrations and `apps/portal/database/postgres/` remain intact.

- [ ] **Step 1: Add package and asset assertions to the guard test**

Add mutation cases for a project package, a transitive lock entry, a
`database/sqlserver` path, and a `.sqlserver.sql` path.

Run:

```bash
bash tests/verify-postgresql-only.test.sh
```

Expected RED: at least one new mutation is not yet classified by a
path-qualified rule.

- [ ] **Step 2: Remove direct Portal packages and exact retired assets**

Delete `Microsoft.Data.SqlClient` and
`Microsoft.EntityFrameworkCore.SqlServer` references from
`RVT.DataAccess.csproj`. Delete the SQL Server directory and the three
SQL-Server-source CSVs. Keep the canonical registries only after removing
retired-source columns or provenance claims.

- [ ] **Step 3: Regenerate Portal lockfiles without broad upgrades**

Run from the repository root:

```bash
dotnet restore Rvt.Mono.slnx --force-evaluate -p:RestoreLockedMode=false
```

Review every changed lockfile. Revert unrelated version churn by restoring with
the checked-in versions; do not hand-edit dependency hashes.

- [ ] **Step 4: Prove all three Portal models are current**

Set `RVT_EF_CONNECTION` only in the invoking environment and run:

```bash
dotnet ef migrations has-pending-model-changes \
  --project apps/portal/RVT.DataAccess/RVT.DataAccess.csproj \
  --startup-project apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj \
  --context RVTDbContext --no-build
dotnet ef migrations has-pending-model-changes \
  --project apps/portal/RVT.DataAccess/RVT.DataAccess.csproj \
  --startup-project apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj \
  --context RVTSearchContext --no-build
dotnet ef migrations has-pending-model-changes \
  --project apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj \
  --startup-project apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj \
  --context ApplicationDbContext --no-build
```

Expected: all three report no pending model changes. If
`RVT_EF_CONNECTION` is unavailable, record these exact commands as unrun; do
not substitute credentials.

- [ ] **Step 5: Verify and commit**

Run:

```bash
dotnet build apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj --no-restore -m:1
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore -m:1
bash tests/verify-postgresql-only.test.sh
git diff --check
git add apps/portal
git commit -m "chore: remove portal SQL Server packages and assets"
```

---

### Task 5: Collapse shared monitor connection primitives to Npgsql

**Files:**
- Delete: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Data/MonitorDatabaseProvider.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Data/MonitorDbOptions.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Data/MonitorDatabaseProviderGuard.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Data/MonitorDb.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Data/MonitorDbParameterExtensions.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Data/EntityFramework/MonitorDbContextOptionsFactory.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Data/EntityFramework/MonitorDbContextBase.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Data/EntityFramework/MonitorModelCacheKeyFactory.cs`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Data/MonitorDbTests.cs`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Data/EntityFramework/MonitorDbContextOptionsFactoryTests.cs`

**Interfaces:**
- `MonitorDbOptions` is
  `sealed record MonitorDbOptions(IReadOnlyDictionary<string,string> IdentifierMap)`.
- `MonitorDbOptions.FromEnvironment` validates
  `RVT__DATABASE_PROVIDER` then `DatabaseProvider`, but stores no provider.
- `MonitorDatabaseProviderGuard.EnsureSupported()` calls
  `MonitorDb.ValidateLegacyProvider(primary, fallback)`.
- `MonitorDb.OpenConnection(string)` opens `NpgsqlConnection`.
- `MonitorDb.CreateCommand(string, DbConnection)` uses canonical input SQL
  unchanged.
- `MonitorDb.BulkInsert(string, string, DataTable, MonitorDbOptions)` always
  uses binary `COPY`.
- `DbParameterCollection.AddWithValue(string, object?)` always creates
  `NpgsqlParameter`.

- [ ] **Step 1: Rewrite common connection tests as PostgreSQL-only RED tests**

Cover legacy-provider validation, Npgsql connection type, Npgsql parameters,
binary-COPY identifier validation, canonical command text, and the Npgsql EF
provider. Delete assertions for provider selection and paired SQL.

Run:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj \
  --no-restore -m:1 \
  --filter 'FullyQualifiedName~MonitorDbTests|FullyQualifiedName~MonitorDbContextOptionsFactoryTests'
```

Expected RED: production signatures still require
`MonitorDatabaseProvider`/`IsPostgreSql`.

- [ ] **Step 2: Simplify options and fail-fast validation**

Implement `ValidateLegacyProvider(string? primary, string? fallback)` with the
global alias/error contract. `FromEnvironment` returns options containing only
the identifier map. Keep environment reads at the composition boundary.

- [ ] **Step 3: Collapse connection, command, parameter, and bulk APIs**

Remove SqlClient imports, `ResolveProvider`, `SelectProviderSql`, the SQL Server
bulk path, and option arguments used only for provider choice. Keep
identifier-map arguments only where a monitor-specific canonical table or
column lookup still needs them.

- [ ] **Step 4: Collapse EF setup and cache identity**

`MonitorDbContextOptionsFactory.Configure` always calls `UseNpgsql`.
`MonitorDbContextBase` exposes no provider cache value, and
`MonitorModelCacheKeyFactory` no longer includes provider in its key. Preserve
context type and design-time dimensions required by EF.

- [ ] **Step 5: Verify and commit**

Run:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj \
  --no-restore -m:1 \
  --filter 'FullyQualifiedName~MonitorDbTests|FullyQualifiedName~MonitorDbContextOptionsFactoryTests'
git diff --check
git add libs/rvt-monitor-common/src/Rvt.Monitor.Common/Data \
  libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Data
git commit -m "refactor: make monitor database primitives PostgreSQL-only"
```

---

### Task 6: Canonicalize shared monitor SQL and EF mappings

**Files:**
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Data/MonitorDb.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Data/EntityFramework/MonitorModelBuilderExtensions.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Alerts/Persistence/AlertOutboxClaimSql.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Alerts/Persistence/AlertPersistenceExceptionClassifier.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Alerts/Persistence/EfAlertOutboxStore.cs`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Data/MonitorDbTests.cs`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Data/EntityFramework/MonitorModelMappingTests.cs`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Alerts/AlertOutboxClaimSqlTests.cs`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Alerts/AlertPersistenceExceptionClassifierTests.cs`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Alerts/EfAlertCommitStoreDuplicateRecoveryTests.cs`

**Interfaces:**
- Shared alert claims expose one PostgreSQL statement and
  `IsolationLevel.ReadCommitted`.
- Common mappings use canonical lowercase snake_case names, PostgreSQL store
  types, PostgreSQL constraints/indexes, and no schema.
- Runtime command creation does not translate bracketed identifiers,
  `dbo`, `DATEPART`, integer booleans, or PascalCase SQL.

- [ ] **Step 1: Make canonical SQL and mapping tests RED**

Refactor tests to instantiate `MonitorDbOptions` with only the identifier map.
Assert exact PostgreSQL table/column/store-type/index/constraint names. Assert
the claim SQL contains `FOR UPDATE SKIP LOCKED` and excludes alternate
provider syntax. Add a source-structure assertion that the runtime rewriter
entry points are absent.

Run:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj \
  --no-restore -m:1 \
  --filter 'FullyQualifiedName~MonitorModelMappingTests|FullyQualifiedName~AlertOutboxClaimSqlTests|FullyQualifiedName~AlertPersistenceExceptionClassifierTests'
```

Expected RED: dual mappings and provider-dependent APIs remain.

- [ ] **Step 2: Replace common mappings with canonical PostgreSQL mappings**

Remove every conditional schema, table, column, type, collation, constraint,
and index name. Reduce mapping helpers so each accepts one canonical name.
Preserve all max lengths, required/optional semantics, concurrency properties,
keys, and indexes.

- [ ] **Step 3: Replace alert persistence branches**

Expose a provider-free claim SQL member and fixed isolation level. Materialize
only canonical result-column names. Classify PostgreSQL unique violations by
`PostgresException.SqlState`/constraint name; remove SQL Server number checks.

- [ ] **Step 4: Delete runtime T-SQL translation**

After shared call sites pass canonical SQL, remove the bracket/schema/datepart/
boolean rewrite regexes, `RewriteSql`, and provider normalization. Retain only
safe identifier validation and explicit identifier-map lookup used for dynamic
but allowlisted monitor objects.

- [ ] **Step 5: Run the complete common suite and commit**

Run:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj \
  --no-restore -m:1
dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.Common.InfrastructureTests/Rvt.Monitor.Common.InfrastructureTests.csproj \
  --no-restore -m:1
git diff --check
git add libs/rvt-monitor-common/src libs/rvt-monitor-common/tests
git commit -m "refactor: canonicalize shared monitor PostgreSQL persistence"
```

---

### Task 7: Convert AirQ and Svantek to canonical PostgreSQL

**Files:**
- Modify: `apps/monitors/airqmonitor/AirQMonitor/api/db/DBClient.cs`
- Modify: `apps/monitors/airqmonitor/AirQMonitor/api/db/EntityFramework/AirQMonitorContext.cs`
- Modify: `apps/monitors/airqmonitor/AirQMonitor/AirQMonitor.csproj`
- Modify: `apps/monitors/airqmonitor/AirQMonitorTests/TestDbClient.cs`
- Modify: `apps/monitors/airqmonitor/AirQMonitorTests/EntityFramework/AirQModelMappingTests.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitor/api/db/DBClient.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitor/api/db/EntityFramework/SvantekMonitorContext.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitor/SvantekMonitor.csproj`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitorTests/TestDbClient.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitorTests/EntityFramework/SvantekModelMappingTests.cs`

**Interfaces:**
- Both monitor contexts construct provider-free `MonitorDbOptions`.
- All raw SQL is canonical PostgreSQL at the call site.
- AirQ and Svantek model metadata contains only canonical PostgreSQL names and
  store types.

- [ ] **Step 1: Rewrite model tests to one canonical contract**

Delete provider data rows. Assert canonical table names, no schema,
snake_case columns, `timestamp with time zone`, PostgreSQL boolean behavior,
and unchanged keys/indexes.

Run:

```bash
dotnet test apps/monitors/airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj \
  --no-restore -m:1 \
  --filter 'FullyQualifiedName~AirQModelMappingTests'
dotnet test apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --no-restore -m:1 \
  --filter 'FullyQualifiedName~SvantekModelMappingTests'
```

Expected RED: context constructors and conditional mapping helpers still
require a provider.

- [ ] **Step 2: Canonicalize both EF contexts**

Delete `Schema()`, provider conditionals, dual-name helper arguments, SQL
Server collations, and timestamp-normalization branches. Keep entity
relationships, uniqueness, value generation, and UTC semantics unchanged.

- [ ] **Step 3: Canonicalize raw SQL and shared API calls**

Convert every bracketed identifier, `dbo` prefix, `DATEPART`, integer boolean,
and PascalCase database identifier in both `DBClient.cs` files before adapting
to the provider-free common signatures. Remove startup provider guards only
after composition calls the new legacy-value validator.

- [ ] **Step 4: Remove direct SQL Server packages**

Remove the two direct EF SQL Server references from the app projects. Defer
lockfile regeneration to Task 11.

- [ ] **Step 5: Run available suites and commit**

Run:

```bash
dotnet test apps/monitors/airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj \
  --no-restore -m:1
dotnet test apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --no-restore -m:1
git diff --check
git add apps/monitors/airqmonitor apps/monitors/svantekmonitor
git commit -m "refactor: make AirQ and Svantek PostgreSQL-only"
```

When the AirQ suite reaches its live integration guard because
`RVT__POSTGRES_INTEGRATION_CONNECTION` is absent, separately run its non-live
test categories and record the live gap in Task 13.

---

### Task 8: Convert MyATM and its migration contract to PostgreSQL

**Files:**
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitor/api/db/DBClient.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitor/api/db/EntityFramework/MyAtmMonitorContext.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitorTests/TestDbClient.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitorTests/EntityFramework/MyAtmModelMappingTests.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmSharedOutboxMigrationContractTests.cs`
- Delete: `apps/monitors/myatmmonitor/database/migrations/2026-07-14-add-durable-outbox.sqlserver.sql`
- Delete: every tracked `apps/monitors/myatmmonitor/database/migrations/*.sqlserver.sql`
- Keep: every tracked `apps/monitors/myatmmonitor/database/migrations/*.postgres.sql`

**Interfaces:**
- MyATM uses the provider-free common persistence API and canonical PostgreSQL
  model.
- Migration contracts validate only the supported PostgreSQL scripts,
  rerunnability, and rollback pairing.

- [ ] **Step 1: Replace dual-provider tests with canonical RED tests**

Assert MyATM table/column/index/type metadata once. Change the outbox migration
contract to enumerate supported `.postgres.sql` scripts and explicitly reject
retired-provider filenames.

Run:

```bash
dotnet test apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --no-restore -m:1 \
  --filter 'FullyQualifiedName~MyAtmModelMappingTests|FullyQualifiedName~MyAtmSharedOutboxMigrationContractTests'
```

Expected RED: tests discover dual mappings and retired scripts.

- [ ] **Step 2: Canonicalize context and queries**

Remove provider guards, `Schema()`, dual-name helpers, alternate store types,
and paired SQL. Convert each query to PostgreSQL at source and use the new
common signatures.

- [ ] **Step 3: Delete SQL Server migration assets and package reference**

Delete all tracked `.sqlserver.sql` files under the exact migration directory.
Remove the direct EF SQL Server package from `MyAtmMonitor.csproj`. Preserve
PostgreSQL forward/rollback files and their ordering.

- [ ] **Step 4: Run the MyATM suite and commit**

```bash
dotnet test apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --no-restore -m:1
git diff --check
git add apps/monitors/myatmmonitor
git commit -m "refactor: make MyATM persistence PostgreSQL-only"
```

---

### Task 9: Convert Omnidots and its migration contract to PostgreSQL

**Files:**
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitor/api/db/DBClient.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitor/api/db/EntityFramework/OmnidotsMonitorContext.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj`
- Delete: `apps/monitors/omnidotsmonitor/OmnidotsMonitor/sqlserver/`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/TestDbClient.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/TestUtil.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/Architecture/OmnidotsAlertArchitectureTests.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsModelMappingTests.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsAlertMigrationContractTests.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsImportConflictTests.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsMigrationContractTests.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsWebhookEndToEndTests.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsAlertCommitStoreTests.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsAlertOutboxStoreTests.cs`

**Interfaces:**
- Omnidots uses one canonical PostgreSQL model and query set.
- Import cursor, trace ordering, alert, outbox, webhook, and unknown-commit
  contracts remain intact.

- [ ] **Step 1: Rewrite mapping and migration tests to RED**

Delete dual-provider rows and assert only canonical tables, columns, store
types, constraints, and indexes. Migration tests enumerate PostgreSQL scripts
and require the retired `sqlserver` directory to be absent.

Run:

```bash
dotnet test apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj \
  --no-restore -m:1 \
  --filter 'FullyQualifiedName~OmnidotsModelMappingTests|FullyQualifiedName~OmnidotsMigrationContractTests|FullyQualifiedName~OmnidotsAlertMigrationContractTests'
```

Expected RED: the dual mapping and exact retired directory still exist.

- [ ] **Step 2: Canonicalize context and all raw SQL**

Remove schema/provider conditionals and dual-name/type helpers. Convert raw
queries and parameters before deleting use of the common rewriter. Preserve
ordered trace imports, conflict handling, lease semantics, and webhook
idempotency.

- [ ] **Step 3: Adapt all test doubles and persistence tests**

Construct provider-free `MonitorDbOptions`. Keep PostgreSQL-specific exception,
duplicate, and unknown-commit assertions. Remove only alternate-provider
expectations.

- [ ] **Step 4: Delete retired assets and package reference**

Delete the exact `OmnidotsMonitor/sqlserver/` directory and remove the direct
SQL Server package from `OmnidotsMonitor.csproj`.

- [ ] **Step 5: Run available suites and commit**

```bash
dotnet test apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj \
  --no-restore -m:1
git diff --check
git add apps/monitors/omnidotsmonitor
git commit -m "refactor: make Omnidots persistence PostgreSQL-only"
```

If the 64 live cases stop at the missing integration environment guard, run
the non-live categories separately and carry the exact gap to Task 13.

---

### Task 10: Simplify reporting monitor and reporting-service remnants

**Files:**
- Modify: `apps/monitors/reportingmonitor/ReportingMonitor/api/ReportingMonitorServices.cs`
- Modify: `apps/monitors/reportingmonitor/ReportingMonitorTests/TestReportingDbClient.cs`
- Modify: `apps/monitors/reportingmonitor/ReportingMonitorTests/EntityFramework/ReportingModelMappingTests.cs`
- Modify: `services/reporting/src/Rvt.Reporting.Data/Postgres/PostgresReportingRepository.cs`
- Modify: `docs/modules/reporting/migration-notes.md`

**Interfaces:**
- Reporting monitor registration validates stale configuration once and then
  constructs provider-free PostgreSQL options.
- Reporting service remains PostgreSQL-only with no retired migration
  commentary.

- [ ] **Step 1: Add a reporting composition RED test**

Extend the reporting monitor tests to prove absent/PostgreSQL aliases register,
while a non-PostgreSQL legacy value fails before resolving the DB client. Assert
the failure message contains no connection string.

Run:

```bash
dotnet test apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj \
  --no-restore -m:1
```

Expected RED: registration still resolves and compares a provider enum.

- [ ] **Step 2: Simplify registration and tests**

Call the shared validator once, instantiate
`MonitorDbOptions(identifierMap)`, and remove enum comparisons from production
and test code.

- [ ] **Step 3: Remove reporting-service remnants**

Keep `PostgresReportingRepository` behavior unchanged. Rewrite only comments or
exception text that claim compatibility with the retired provider. Rewrite the
migration notes as a PostgreSQL deployment record.

- [ ] **Step 4: Verify and commit**

```bash
dotnet test apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj \
  --no-restore -m:1
dotnet test services/reporting/tests/Rvt.Reporting.Core.Tests/Rvt.Reporting.Core.Tests.csproj \
  --no-restore -m:1
dotnet test services/reporting/tests/Rvt.Reporting.Service.Tests/Rvt.Reporting.Service.Tests.csproj \
  --no-restore -m:1
git diff --check
git add apps/monitors/reportingmonitor services/reporting docs/modules/reporting/migration-notes.md
git commit -m "refactor: simplify PostgreSQL reporting composition"
```

---

### Task 11: Remove monitor packages, migrations, and lockfile graph

**Files:**
- Modify: `apps/monitors/Directory.Packages.props`
- Modify: `libs/rvt-monitor-common/Directory.Packages.props`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj`
- Delete: `libs/rvt-monitor-common/database/migrations/2026-07-15-add-monitor-delivery-outbox.sqlserver.sql`
- Modify: every tracked `packages.lock.json` under
  `apps/monitors/` and `libs/rvt-monitor-common/`

**Interfaces:**
- Central package catalogs and project files contain no direct declaration of
  either retired package.
- Runtime, test, integration-testing, infrastructure, and package-validation
  lock graphs resolve without them.
- The source-reference common package still packs and both package-validation
  consumers still restore/test.

- [ ] **Step 1: Strengthen the package mutation test**

Make `tests/verify-postgresql-only.test.sh` copy a clean lockfile fixture, add a
forbidden transitive entry, and prove the guard rejects it. Restore the clean
fixture and prove it passes.

Run:

```bash
bash tests/verify-postgresql-only.test.sh
```

Expected: mutation rejection is GREEN before real lockfiles are changed.

- [ ] **Step 2: Remove central/direct package declarations and shared migration**

Remove the two retired package versions from both
`Directory.Packages.props` files and their `PackageReference` entries from the
two common project files. Delete the exact shared `.sqlserver.sql` migration.

- [ ] **Step 3: Regenerate every affected lockfile**

Run:

```bash
dotnet restore Rvt.Mono.slnx --force-evaluate -p:RestoreLockedMode=false
dotnet restore libs/rvt-monitor-common/package-validation/RuntimeConsumer/RuntimeConsumer.csproj \
  --force-evaluate -p:RestoreLockedMode=false
dotnet restore libs/rvt-monitor-common/package-validation/TestConsumer/TestConsumer.csproj \
  --force-evaluate -p:RestoreLockedMode=false
dotnet restore libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj \
  --force-evaluate -p:RestoreLockedMode=false
dotnet restore libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj \
  --force-evaluate -p:RestoreLockedMode=false
```

Review the diff across:

```text
apps/monitors/airqmonitor/AirQMonitor/packages.lock.json
apps/monitors/airqmonitor/AirQMonitorTests/packages.lock.json
apps/monitors/myatmmonitor/MyAtmMonitor/packages.lock.json
apps/monitors/myatmmonitor/MyAtmMonitorTests/packages.lock.json
apps/monitors/omnidotsmonitor/OmnidotsMonitor/packages.lock.json
apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/packages.lock.json
apps/monitors/reportingmonitor/ReportingMonitor/packages.lock.json
apps/monitors/reportingmonitor/ReportingMonitorTests/packages.lock.json
apps/monitors/reportingmonitor/Rvt.Reporting.Core/packages.lock.json
apps/monitors/reportingmonitor/Rvt.Reporting.Messaging/packages.lock.json
apps/monitors/reportingmonitor/Rvt.Reporting.Pdf/packages.lock.json
apps/monitors/reportingmonitor/Rvt.Reporting.Storage/packages.lock.json
apps/monitors/svantekmonitor/SvantekMonitor/packages.lock.json
apps/monitors/svantekmonitor/SvantekMonitorTests/packages.lock.json
libs/rvt-monitor-common/package-validation/RuntimeConsumer/packages.lock.json
libs/rvt-monitor-common/package-validation/TestConsumer/packages.lock.json
libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/packages.lock.json
libs/rvt-monitor-common/src/Rvt.Monitor.Common/packages.lock.json
libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting.Tests/packages.lock.json
libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/packages.lock.json
libs/rvt-monitor-common/tests/Rvt.Monitor.Common.InfrastructureTests/packages.lock.json
libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/packages.lock.json
libs/rvt-monitor-common/tests/Rvt.Monitor.PackageValidationTests/packages.lock.json
```

Only graph changes caused by deleting the retired packages are accepted.

- [ ] **Step 4: Pack and validate the shared package**

Run:

```bash
dotnet pack libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj \
  --no-restore -m:1
dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj \
  --no-restore -m:1
bash scripts/verify-rvt-common-source-boundary.sh
bash tests/verify-rvt-common-source-boundary.test.sh
bash tests/verify-rvt-common-source-boundary-regression.test.sh
```

- [ ] **Step 5: Prove the real package graph and commit**

Run:

```bash
bash scripts/verify-postgresql-only.sh .
```

Expected RED is now limited to documentation/history/configuration remnants,
not packages, lockfiles, source APIs, or schema paths.

Then:

```bash
git diff --check
git add apps/monitors libs/rvt-monitor-common
git commit -m "chore: remove SQL Server monitor package graph"
```

---

### Task 12: Rewrite active docs and delete historical provider artifacts

**Files:**
- Modify: `apps/monitors/README.md`
- Modify: `docs/architecture/portal/ports-and-adapters-catalog.md`
- Modify: `docs/database/monitors/monitor-data-access-migration.md`
- Delete: `docs/database/portal/database-name-equivalents-for-migrator.md`
- Modify: `docs/database/portal/database-naming-cutover-runbook.md`
- Modify: `docs/database/portal/database-naming-standard.md`
- Modify: `docs/database/portal/database-performance-index-review-2026-06-09.md`
- Modify: `docs/database/portal/database-refactor-inventory.md`
- Modify: `docs/database/portal/database-routine-porting-inventory.md`
- Modify: `docs/database/portal/ef-data-access-remediation-plan.md`
- Modify: `docs/database/portal/ef-migrations.md`
- Delete: `docs/database/portal/legacy-compatibility-deprecation.md`
- Delete: `docs/database/portal/sqlserver-refactor-rehearsal.md`
- Modify: `docs/database/portal/timescale-refactor-rehearsal.md`
- Modify: `docs/database/rvt-monitor-common/migrations/README.md`
- Modify: `docs/development/portal/onboarding/DATABASE_NAMING_ONBOARDING.md`
- Modify: `docs/development/portal/onboarding/REACT_PORT_ONBOARDING.md`
- Modify: `docs/development/portal/sonar/SQL_SCRIPT_ANALYSIS_POLICY.md`
- Modify: `docs/development/portal/testing/testability-rc-grade-update.md`
- Modify: `docs/development/rvt-monitor-common/dependency-license-review.md`
- Modify: `docs/modules/monitors/myatmmonitor/README.md`
- Modify: `docs/modules/monitors/omnidotsmonitor/README.md`
- Modify: `docs/operations/monitors/container-builds.md`
- Modify: `docs/operations/portal/dev-secrets-reference.md`
- Modify: `docs/release/portal/2026-07-15-rc-review-response.md`
- Delete: every file under `docs/history/` reported by the PostgreSQL-only
  guard
- Modify or delete: every older file under `docs/superpowers/plans/` and
  `docs/superpowers/specs/` reported by the guard
- Modify: `docs/documentation-move-manifest.md`
- Modify: `tests/verify-documentation-layout.test.sh`
- Modify: `tests/verify-documentation-layout-regression.test.sh`

**Interfaces:**
- Active docs describe one PostgreSQL/TimescaleDB setup, migration, deployment,
  rollback, and test path.
- Historical provider-support artifacts are absent; Git history is their
  recovery mechanism.
- Documentation move-manifest checks remain exact and green.

- [ ] **Step 1: Capture the deterministic documentation inventory**

Run:

```bash
bash scripts/verify-postgresql-only.sh . 2>&1 | \
  rg 'documentation|retired path|provider configuration' | sort
```

Compare the paths to the approved design. Every matched `docs/history/` file
is an authorized deletion target. For active docs, classify each as rewrite or
delete according to the explicit file list above; do not retain a compatibility
archive.

- [ ] **Step 2: Make documentation guards RED for the new state**

Update the documentation layout expectations so the retired files are absent
and the design, implementation plan, PostgreSQL operations docs, and final
state handoff are the canonical destinations.

Run:

```bash
bash tests/verify-documentation-layout.test.sh
bash tests/verify-documentation-layout-regression.test.sh
```

Expected RED: the manifest and filesystem still advertise retired sources.

- [ ] **Step 3: Rewrite active docs around the single provider**

Remove provider-selection examples, `sqlcmd`, Windows-authentication defaults,
dual migration instructions, package claims, and rollback instructions for the
retired provider. Keep PostgreSQL EF history tables, SchemaDeploy,
TimescaleDB, UTC rules, connection variable names, and live-test prerequisites
precise.

- [ ] **Step 4: Delete historical and superseded provider documents**

Delete every guard-reported file under `docs/history/`. For older
`docs/superpowers` documents, delete superseded provider-specific reports and
rewrite a document only when it still defines an active non-provider contract.
The current design and plan are the only allowed audit documents containing
the retired provider name.

- [ ] **Step 5: Update the move manifest and regression fixtures**

Remove deleted source/destination rows from
`docs/documentation-move-manifest.md`; add the current PostgreSQL-only design
and plan only where the manifest format requires current canonical documents.
Keep the stale-source regression fixture able to prove a removed path is
rejected without storing a forbidden provider token in a tracked filename.

- [ ] **Step 6: Verify and commit**

Run:

```bash
bash tests/verify-documentation-layout.test.sh
bash tests/verify-documentation-layout-regression.test.sh
bash scripts/verify-documentation-layout.sh
bash scripts/verify-postgresql-only.sh .
git diff --check
git add apps/monitors docs tests/verify-documentation-layout.test.sh \
  tests/verify-documentation-layout-regression.test.sh
git commit -m "docs: make PostgreSQL the sole database contract"
```

Expected: the PostgreSQL-only guard is fully GREEN for the first time.

---

### Task 13: Enable the permanent guard, verify the mono-repository, and hand off

**Files:**
- Modify: `scripts/build-mono.sh`
- Modify: `README.md`
- Modify: `project_state.md`
- Modify only if verification exposes a defect: files already owned by Tasks
  1–12

**Interfaces:**
- Aggregate builds always execute the PostgreSQL-only guard.
- `project_state.md` records the current branch/head, file structure,
  PostgreSQL configuration variables, deleted-provider decision, verification
  totals, environment-gated gaps, and rollout sequence.

- [ ] **Step 1: Remove the guard transition gate**

Replace the `RVT_ENFORCE_POSTGRESQL_ONLY` conditional in
`scripts/build-mono.sh` with an unconditional:

```bash
bash scripts/verify-postgresql-only.sh .
```

Add the guard command to the root README's contributor checks.

- [ ] **Step 2: Run static repository guards**

Run:

```bash
bash scripts/verify-postgresql-only.sh .
bash tests/verify-postgresql-only.test.sh
bash scripts/verify-mono-layout.sh
bash tests/verify-mono-layout.test.sh
bash scripts/verify-mono-solution.sh
bash tests/verify-mono-solution.test.sh
bash scripts/verify-rvt-common-source-boundary.sh
bash tests/verify-rvt-common-source-boundary.test.sh
bash tests/verify-rvt-common-source-boundary-regression.test.sh
bash scripts/verify-documentation-layout.sh
bash tests/verify-documentation-layout.test.sh
bash tests/verify-documentation-layout-regression.test.sh
```

Expected: every command exits zero.

- [ ] **Step 3: Restore, build, and run all non-live .NET suites**

Run:

```bash
dotnet restore Rvt.Mono.slnx --locked-mode
dotnet build Rvt.Mono.slnx --no-restore -m:1
dotnet test apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj --no-restore -m:1
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore -m:1
dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --no-restore -m:1
dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.Common.InfrastructureTests/Rvt.Monitor.Common.InfrastructureTests.csproj --no-restore -m:1
dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj --no-restore -m:1
dotnet test apps/monitors/airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --no-restore -m:1
dotnet test apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-restore -m:1
dotnet test apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore -m:1
dotnet test apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --no-restore -m:1
dotnet test apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --no-restore -m:1
dotnet test services/reporting/tests/Rvt.Reporting.Core.Tests/Rvt.Reporting.Core.Tests.csproj --no-restore -m:1
dotnet test services/reporting/tests/Rvt.Reporting.Service.Tests/Rvt.Reporting.Service.Tests.csproj --no-restore -m:1
```

When `RVT__POSTGRES_INTEGRATION_CONNECTION` is absent, rerun the AirQ and
Omnidots suites with:

```bash
dotnet test apps/monitors/airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj \
  --no-restore -m:1 --filter 'TestCategory!=PostgreSqlIntegration'
dotnet test apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj \
  --no-restore -m:1 --filter 'TestCategory!=PostgreSqlIntegration'
```

Exclude only that explicit live category.
Record exact passed/failed/skipped totals for every command.

- [ ] **Step 4: Run client verification**

The provider removal changes host configuration but no DTO. Still run the
client regression and production build:

```bash
npm --prefix apps/portal/RvtPortal.Spa/ClientApp run test:run
npm --prefix apps/portal/RvtPortal.Spa/ClientApp run build
```

- [ ] **Step 5: Run schema and live PostgreSQL verification when configured**

When `RVT_EF_CONNECTION` is set, rerun all three
`has-pending-model-changes` commands from Task 4. When
`RVT_TEST_POSTGRES_CONNECTION` is set, run the complete Portal SPA suite
without filtering. When `RVT__POSTGRES_INTEGRATION_CONNECTION` is set, run the
complete AirQ and Omnidots suites plus:

```bash
dotnet test libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj \
  --no-restore -m:1
```

Run SchemaDeploy idempotency tests selected by:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --no-restore -m:1 \
  --filter 'FullyQualifiedName~SchemaDeployTests'
```

Do not claim a live test passed if its connection variable is absent.

- [ ] **Step 6: Run the aggregate build**

Run:

```bash
scripts/build-mono.sh
```

If it invokes live environment-gated cases, distinguish missing-variable
stops from product failures and retain the individually verified non-live
results above.

- [ ] **Step 7: Update the persistent handoff**

Update `project_state.md` with:

```text
branch and exact HEAD
PostgreSQL-only architecture and deleted directories
current Portal/monitor/reporting structure
supported connection/configuration variable names
retired RVT_EF_PROVIDER and provider-selection keys
all test/build totals
live suites run or exact environment-gated gaps
existing dependency advisories
deployment prerequisites and rollback boundary
next-session instruction: Read project_state.md to get up to speed
```

The state file is an allowed audit record, not deployment compatibility
documentation.

- [ ] **Step 8: Perform an independent diff review and repair findings**

Review:

```bash
git diff a07f6019fc492531a2f7d67294dd17ace47058db...HEAD --stat
git diff a07f6019fc492531a2f7d67294dd17ace47058db...HEAD
git status --short
git diff --check
```

Check authorization/config fail-fast behavior, UTC mappings, migration-history
separation, canonical SQL, lockfile completeness, deleted assets, docs,
secrets, and guard bypasses. Fix each finding with a reproducing test and rerun
the affected verification.

- [ ] **Step 9: Commit final state**

```bash
git add scripts/build-mono.sh README.md project_state.md
git add -u
git diff --cached --check
git commit -m "chore: enforce PostgreSQL-only solution"
```

- [ ] **Step 10: Re-run final evidence and prepare the remote handoff**

Run:

```bash
bash scripts/verify-postgresql-only.sh .
git status --short --branch
git log --oneline --decorate -15
```

Expected: guard green and tracked worktree clean. Report the exact commits,
verification totals, and environment gaps. Push only after the user authorizes
the final remote mutation or if the active execution instruction already
includes pushing this branch.
