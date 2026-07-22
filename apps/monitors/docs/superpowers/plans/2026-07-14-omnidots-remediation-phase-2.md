# Omnidots Remediation Phase 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add independent Peak/Veff/VDV cursors and ordered, atomic trace persistence for PostgreSQL and SQL Server.

**Architecture:** Add provider-aware migration assets and EF entities, then expose narrow cursor-query and atomic-import command ports implemented by the existing `DBClient` compatibility facade. Measurement rows and cursors commit together; each trace index and ordered sample batch commits together.

**Tech Stack:** .NET 10, EF Core 10, Npgsql, SQL Server provider, PostgreSQL integration fixtures, MSTest 4.

## Global Constraints

- Forward migrations run before the Phase 2 application.
- Provide idempotent forward and rollback scripts for PostgreSQL and SQL Server.
- Use series values exactly `Peak`, `Veff`, and `Vdv`.
- Never move an import cursor backward.
- Only Peak updates shared `LastDataTime1Min`.
- Preserve historical traces but document that their original order is unrecoverable.
- New trace samples use zero-based indexes and one transaction per trace.
- Keep `IDBClient` as a compatibility facade; new handlers use narrow ports.

---

### Task 1: Add dual-provider migration assets and contract tests

**Files:**
- Create: `omnidotsmonitor/OmnidotsMonitor/postgres/2026-07-14-add-import-cursors-and-trace-order.sql`
- Create: `omnidotsmonitor/OmnidotsMonitor/postgres/2026-07-14-rollback-import-cursors-and-trace-order.sql`
- Create: `omnidotsmonitor/OmnidotsMonitor/sqlserver/2026-07-14-add-import-cursors-and-trace-order.sql`
- Create: `omnidotsmonitor/OmnidotsMonitor/sqlserver/2026-07-14-rollback-import-cursors-and-trace-order.sql`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsMigrationContractTests.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj`

**Interfaces:**
- Produces: cursor table and ordered trace schema for both providers.
- Consumes: existing provider table names.

- [ ] **Step 1: Write red script-contract tests**

Load all four scripts from test output. Assert the forward scripts contain cursor table, composite cursor key/check, sample index, not-null transition, and trace composite primary key; assert rollback scripts remove the trace key/index column and cursor table. Assert PostgreSQL names are snake_case and SQL Server names are `dbo`/PascalCase.

```csharp
StringAssert.Contains(postgresForward, "CREATE TABLE IF NOT EXISTS omnidots_import_cursor");
StringAssert.Contains(postgresForward, "PRIMARY KEY (trace_id, sample_index)");
StringAssert.Contains(sqlServerForward, "CREATE TABLE dbo.OmnidotsImportCursor");
StringAssert.Contains(sqlServerForward, "PRIMARY KEY (TraceId, SampleIndex)");
```

- [ ] **Step 2: Run red tests**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter FullyQualifiedName~OmnidotsMigrationContractTests
```

Expected: script files are missing.

- [ ] **Step 3: Write PostgreSQL forward/rollback scripts**

The forward script must use `CREATE TABLE IF NOT EXISTS`, the approved series check, `ADD COLUMN IF NOT EXISTS sample_index integer`, a CTE using `row_number() over (partition by trace_id order by ctid) - 1` to populate null historical indexes, `SET NOT NULL`, and a guarded composite primary key. Drop the old trace-only index after the primary key exists if it is redundant.

The rollback script must drop the composite primary key, recreate the trace-only index if absent, drop `sample_index`, and drop `omnidots_import_cursor`. Every operation is guarded for re-execution.

- [ ] **Step 4: Write SQL Server forward/rollback scripts**

The forward script uses `OBJECT_ID`/`COL_LENGTH` guards, creates `dbo.OmnidotsImportCursor`, assigns historical indexes with `ROW_NUMBER() OVER (PARTITION BY TraceId ORDER BY (SELECT NULL)) - 1`, makes `SampleIndex` non-null, and creates `PK_OmnidotsTraces(TraceId, SampleIndex)` if absent.

The rollback script conditionally drops that key, drops `SampleIndex`, recreates `ix_traces` if needed, and drops `dbo.OmnidotsImportCursor`.

- [ ] **Step 5: Copy scripts, verify, and commit**

Add four `<None Update=... CopyToOutputDirectory="PreserveNewest" />` items to the test project.

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter FullyQualifiedName~OmnidotsMigrationContractTests
git add omnidotsmonitor/OmnidotsMonitor/postgres omnidotsmonitor/OmnidotsMonitor/sqlserver omnidotsmonitor/OmnidotsMonitorTests
git commit -m "feat: add Omnidots data integrity migrations"
```

### Task 2: Map import cursors and ordered traces in EF

**Files:**
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/db/EntityFramework/OmnidotsEntities.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/db/EntityFramework/OmnidotsMonitorContext.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/db/OmnidotsMonitorDbOptions.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsModelMappingTests.cs`

**Interfaces:**
- Produces: `OmnidotsImportCursorEntity` and keyed `OmnidotsTraceEntity`.
- Consumes: migration schema from Task 1.

- [ ] **Step 1: Write red provider-mapping tests**

Assert cursor table/columns for both providers, cursor composite key, trace `SampleIndex` column, and trace composite key. Replace the old `Assert.AreEqual(0, sqlServerTrace.GetKeys().Count())` with an assertion for `TraceId, SampleIndex`.

- [ ] **Step 2: Run red mapping tests**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter FullyQualifiedName~OmnidotsModelMappingTests
```

- [ ] **Step 3: Add entities and DbSets**

```csharp
public sealed class OmnidotsImportCursorEntity
{
    public string SerialId { get; set; } = string.Empty;
    public string Series { get; set; } = string.Empty;
    public DateTime LastSampleAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class OmnidotsTraceEntity
{
    public Guid TraceId { get; set; }
    public int SampleIndex { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Z { get; set; }
}
```

Add `DbSet<OmnidotsImportCursorEntity> ImportCursors` and map provider-specific tables/columns with composite keys. Add `OmnidotsImportCursor` to the identifier map.

- [ ] **Step 4: Verify and commit**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter FullyQualifiedName~OmnidotsModelMappingTests
git add omnidotsmonitor/OmnidotsMonitor/api/db omnidotsmonitor/OmnidotsMonitorTests/EntityFramework
git commit -m "feat: map Omnidots import cursors and trace order"
```

### Task 3: Add narrow cursor and atomic import ports

**Files:**
- Create: `omnidotsmonitor/OmnidotsMonitor/api/db/OmnidotsMeasurementSeries.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/api/db/Queries/IOmnidotsImportCursorQueries.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/api/db/Commands/IOmnidotsMeasurementImportCommands.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/db/DBClient.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsMonitorServices.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestDbClient.cs`

**Interfaces:**
- Produces: cursor reads/fallback reads and atomic series imports.
- Consumes: entities from Task 2.

- [ ] **Step 1: Write red PostgreSQL integration tests**

Test absent cursor, per-series isolation, latest stored measurement lookup, monotonic updates, and rollback when cursor update fails. For rollback, create a schema-local PostgreSQL trigger on `omnidots_import_cursor` whose function raises an exception, invoke an import, assert no measurement row committed, then drop the trigger and function in `finally`. Use the existing schema-scoped PostgreSQL fixture and category.

- [ ] **Step 2: Define exact narrow interfaces**

```csharp
public enum OmnidotsMeasurementSeries { Peak, Veff, Vdv }

public interface IOmnidotsImportCursorQueries
{
    DateTime? ReadImportCursor(string serialId, OmnidotsMeasurementSeries series);
    DateTime? ReadLatestMeasurementTime(string serialId, OmnidotsMeasurementSeries series);
}

public interface IOmnidotsMeasurementImportCommands
{
    void ImportPeakRecords(string serialId, DataTable records, DateTime newestSampleAt);
    void ImportVeffRecords(string serialId, IReadOnlyCollection<VeffRecordDto> records, DateTime newestSampleAt);
    void ImportVdvRecords(string serialId, IReadOnlyCollection<VdvRecordDto> records, DateTime newestSampleAt);
}
```

Register both interfaces to resolve the singleton `IDBClient` implementation.

- [ ] **Step 3: Implement cursor reads and monotonic upsert**

`ReadImportCursor` queries the composite key. `ReadLatestMeasurementTime` switches over the enum and calls `MaxAsync`/`Max` on the matching level table. A private `AdvanceCursor` adds a row or updates it only when `newestSampleAt > LastSampleAt`; set `UpdatedAt=DateTime.UtcNow` only on advancement.

- [ ] **Step 4: Implement transactional imports**

Each method opens one context and transaction, inserts unseen measurement keys, advances only its series cursor, updates `MonitorsList.LastDataTime1Min` only in `ImportPeakRecords`, calls `SaveChanges`, and commits. Roll back on every exception. Keep legacy `IOmnidotsMeasurementCommands` methods as compatibility wrappers until all handler callers move.

- [ ] **Step 5: Run integration tests and commit**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "TestCategory=PostgreSqlIntegration&FullyQualifiedName~TestDbClient"
git add omnidotsmonitor/OmnidotsMonitor/api/db omnidotsmonitor/OmnidotsMonitor/api/OmnidotsMonitorServices.cs omnidotsmonitor/OmnidotsMonitorTests/TestDbClient.cs
git commit -m "feat: add atomic Omnidots series imports"
```

### Task 4: Move Peak/Veff/VDV handlers onto independent cursors

**Files:**
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/StorePeakRecordsHandler.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/StoreVeffRecordsHandler.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/StoreVdvRecordsHandler.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsApi.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestOmnidotsApi.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestRules.cs`

**Interfaces:**
- Consumes: `IOmnidotsImportCursorQueries`, `IOmnidotsMeasurementImportCommands`, Phase 1 window calculator.
- Produces: cursor-based catch-up with no cross-series watermark writes.

- [ ] **Step 1: Write red isolation/restart tests**

Assert Veff/VDV never call `WriteLatestTimestamp`; their request begins at their own cursor minus five minutes; a missed run uses an old cursor rather than a fixed current-time window; Peak updates only Peak cursor and shared timestamp.

- [ ] **Step 2: Run red handler tests**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "FullyQualifiedName~TestStorePeakRecords|FullyQualifiedName~TestStoreVeffRecords|FullyQualifiedName~TestStoreVdvRecords"
```

- [ ] **Step 3: Implement cursor fallback rules**

For Peak use Peak cursor, then monitor `LastDataTime`, then deploy date. For Veff/VDV use their cursor, then `ReadLatestMeasurementTime`, then Phase 1 two-hour lookback. Subtract five minutes from any cursor. Order vendor records chronologically and derive `newestSampleAt` from the filtered batch.

- [ ] **Step 4: Call only atomic import commands**

Replace separate insert and timestamp calls with the matching `Import*Records` method. Publish MQTT only after the DB method returns. Veff/VDV may clear offline status after a committed non-empty batch but must not mutate `LastDataTime1Min`.

- [ ] **Step 5: Verify and commit**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "FullyQualifiedName~TestStorePeakRecords|FullyQualifiedName~TestStoreVeffRecords|FullyQualifiedName~TestStoreVdvRecords|FullyQualifiedName~TestRules"
git add omnidotsmonitor/OmnidotsMonitor/api omnidotsmonitor/OmnidotsMonitorTests
git commit -m "fix: isolate Omnidots measurement cursors"
```

### Task 5: Make trace writes ordered and atomic

**Files:**
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/db/Commands/IOmnidotsMeasurementCommands.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/db/DBClient.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestDbClient.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/testdata/create.postgres.sql`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/testdata/reset.postgres.sql`

**Interfaces:**
- Produces: transactional `WriteTraces` preserving zero-based sample order.
- Consumes: keyed trace entity from Task 2.

- [ ] **Step 1: Update fixture schema and write red ordering/rollback tests**

Add `sample_index integer NOT NULL` and `PRIMARY KEY(trace_id,sample_index)` plus the cursor table to `create.postgres.sql`; add cursor table to reset ordering. Query samples with `ORDER BY sample_index` and compare original lists without sorting. To test rollback, create a schema-local trigger that raises when `NEW.sample_index = 1`, invoke `WriteTraces`, assert neither trace index nor samples remain, then drop the trigger and function in `finally`.

- [ ] **Step 2: Run red integration tests**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "TestCategory=PostgreSqlIntegration&FullyQualifiedName~TestDbClient"
```

- [ ] **Step 3: Replace raw per-row SQL with EF batching**

For every vendor trace, begin a transaction, add one `OmnidotsTraceIndexEntity`, generate `OmnidotsTraceEntity` rows with `SampleIndex = index`, call `AddRange`, save once, and commit. Delete `InsertTraceRow`. Roll back on exception.

- [ ] **Step 4: Verify and commit**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "TestCategory=PostgreSqlIntegration&FullyQualifiedName~TestDbClient"
git add omnidotsmonitor/OmnidotsMonitor/api/db omnidotsmonitor/OmnidotsMonitorTests
git commit -m "fix: persist ordered Omnidots traces atomically"
```

### Task 6: Verify migrations and Phase 2 deployment documentation

**Files:**
- Modify: `omnidotsmonitor/README.md`
- Modify: `docs/container-builds.md`
- Modify: `project_state.md`

**Interfaces:**
- Consumes: Phase 2 schema/application contract.
- Produces: provider-specific forward/rollback runbook and verification evidence.

- [ ] **Step 1: Document deployment order and rollback**

Document backup, provider-specific forward script, schema verification, application deployment, one-shot Peak/Veff/VDV/trace smoke runs, and rollback ordering. State that historical trace order was not recoverable.

- [ ] **Step 2: Run full verification**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore --nologo
dotnet build omnidotsmonitor/omnidotsmonitor.sln --no-restore --nologo
git diff --check
```

Then run the PostgreSQL integration category using the configured schema-isolated connection. Expected: all tests pass, both provider mapping/contract tests pass, build is clean.

- [ ] **Step 3: Record exact results and commit**

```bash
git add omnidotsmonitor/README.md docs/container-builds.md project_state.md
git commit -m "docs: record Omnidots phase 2 data integrity"
```
