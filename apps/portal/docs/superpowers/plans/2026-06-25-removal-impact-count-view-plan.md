# Removal Impact Count View Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace 14 sequential measurement `COUNT` round trips in monitor removal impact checks with a database-side aggregate view for SQL Server and PostgreSQL, with supporting PostgreSQL indexes.

**Architecture:** Add a canonical `monitor_measurement_removal_impact` view that exposes `serial_id`, `measurement_table_count`, and `measurement_row_count`. Keep provider-specific DDL in the database script folders and add an EF migration that creates/drops the view for both providers. Refactor `MonitorRemovalImpactReader` to query the view through a small keyless EF model, while preserving an InMemory-safe fallback for tests.

**Tech Stack:** ASP.NET Core, EF Core 10, SQL Server, PostgreSQL/TimescaleDB, xUnit.

---

### Task 1: Guardrail Tests

**Files:**
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`

- [x] **Step 1: Write failing architecture tests**

Add assertions that:
- `MonitorRemovalImpactReader` no longer contains the measurement `new[] { await ... CountAsync(...) }` pattern.
- `RVTSearchContext` exposes a `DbSet<MonitorMeasurementRemovalImpact>`.
- SQL Server and PostgreSQL view scripts define `monitor_measurement_removal_impact`.
- PostgreSQL physical-table index sources include `serial_id` indexes for the tables that feed the view; aggregate relations that are normal views are documented as not directly indexable.

- [x] **Step 2: Run red test**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~CqrsArchitectureTests" --logger "console;verbosity=minimal"
```

Expected: fail before implementation because the view, model, and guardrails do not exist.

Actual: failed because `MonitorRemovalImpactReader` still contained the sequential `new[]` measurement count pattern.

### Task 2: Database Scripts And Migration

**Files:**
- Create: `database/postgres/monitor_measurement_removal_impact_view.sql`
- Create: `database/postgres/monitor_measurement_removal_impact_view_rollback.sql`
- Create: `database/sqlserver/monitor_measurement_removal_impact_view.sql`
- Create: `database/sqlserver/monitor_measurement_removal_impact_view_rollback.sql`
- Checked: `database/postgres/performance_indexes_20260609.sql`
- Checked: `database/postgres/monitor_natural_key_changes_20260618.sql`
- Create: `RVT.Dataaccess/Migrations/20260625203000_AddMonitorMeasurementRemovalImpactView.cs`

- [x] **Step 1: Add PostgreSQL view script**

Create `public.monitor_measurement_removal_impact` with `UNION ALL` grouped counts from the 14 serial-id keyed measurement/status relations and outer aggregation by `serial_id`.

- [x] **Step 2: Add SQL Server view script**

Create `dbo.monitor_measurement_removal_impact` with the same shape and canonical lowercase object names.

- [x] **Step 3: Add rollback scripts**

Drop the provider-specific view in rollback scripts.

- [x] **Step 4: Check PostgreSQL indexes**

Checked local `rvt-timescaledb` and repo scripts. Physical relations already have serial-id indexes:
- `my_atm_dust_level`: `ix_my_atm_dust_level_serial_id_avrg_sample_time`
- `omnidots_peak_level`: `ix_omnidots_peak_level_serial_id_sample_time` and `ux_omnidots_peak_level_serial_id_sample_time`
- `omnidots_trace_index`: `ix_omnidots_trace_index_serial_id_start_time`
- `omnidots_monitor_status`: `ix_omnidots_monitor_status_serial_id`
- `svantek_monitor_status`: `ix_svantek_monitor_status_serial_id`
- noise average views depend on indexed base noise tables.

- [x] **Step 5: Add EF migration**

Use `migrationBuilder.ActiveProvider` to execute the provider-specific `CREATE VIEW` and `DROP VIEW` SQL in migration `20260625203000_AddMonitorMeasurementRemovalImpactView`.

### Task 3: EF Model And Reader Refactor

**Files:**
- Create: `RVT.Dataaccess/EntityModels/Models/MonitorMeasurementRemovalImpact.cs`
- Modify: `RVT.Dataaccess/Context/RVTSearchContext.cs`
- Modify: `RvtPortal.Spa/Application/Monitors/MonitorRemovalImpactReader.cs`

- [x] **Step 1: Add keyless view model**

Add a simple model with `SerialId`, `MeasurementTableCount`, and `MeasurementRowCount`.

- [x] **Step 2: Map view in `RVTSearchContext`**

Expose `DbSet<MonitorMeasurementRemovalImpact>` and map it to `monitor_measurement_removal_impact` with canonical columns.

- [x] **Step 3: Refactor reader**

Query the view once for the serial id. Keep the current sequential count path only as an InMemory fallback so existing tests can run without a physical database view.

### Task 4: Verification And Docs

**Files:**
- Modify: `docs/database/database-name-registry.csv`
- Checked: `docs/database/database-constraint-index-name-registry.csv`
- Modify: root `project_state.md`

- [x] **Step 1: Run focused DB guardrails**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~CqrsArchitectureTests|FullyQualifiedName~DatabaseNamingConventionTests|FullyQualifiedName~CutoverReadinessTests|FullyQualifiedName~SiteArchiveServiceSecurityTests|FullyQualifiedName~DatabaseProviderConfigurationTests" --logger "console;verbosity=minimal"
```

- [x] **Step 2: Run full backend suite**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --logger "console;verbosity=minimal"
```

- [x] **Step 3: Update docs and Plane**

Update root `project_state.md`; comment on Plane issue `#430` with files and verification.

- [x] **Step 4: Commit and push**

Commit on `review-improvements`; do not merge to `master`.

Committed on `review-improvements` with message `perf(api): consolidate removal impact counts`.

---

## Completion Evidence - 2026-06-25

Implementation:
- Added SQL Server and PostgreSQL `monitor_measurement_removal_impact` view scripts and rollback scripts.
- Added EF migration `20260625203000_AddMonitorMeasurementRemovalImpactView`.
- Added keyless EF model `MonitorMeasurementRemovalImpact` and `RVTSearchContext` mapping.
- Updated `MonitorRemovalImpactReader` so real database providers query the view once per serial id.
- Kept the old entity-set count path only for EF InMemory test hosts.
- Updated database name registries for the new canonical view and columns.

PostgreSQL index check:
- Live container: `rvt-timescaledb`, database `rvt`.
- Physical tables used by the view have serial-id indexes.
- Average relations such as `noise_level_1_hour_avg` and `omnidots_peak_level_1_min` are normal views and are not directly indexable; they rely on their indexed base tables.
- No duplicate index migration was added because no missing physical-table serial index was found.

Verification:
- Red guardrail failed before implementation on the sequential `new[]` measurement count pattern.
- Focused architecture guardrail passed: `3/3`.
- Focused DB/architecture test bundle passed: `99/99`.
- Full backend suite passed: `214/214`.
- Applied `database/postgres/monitor_measurement_removal_impact_view.sql` to local `rvt-timescaledb`; `SELECT * FROM public.monitor_measurement_removal_impact LIMIT 5` returned rows.
- `git diff --check` passed.
