# Database Routine Porting Inventory

## Purpose

This document tracks the stored procedure and function gate for the database naming refactor. Views now have canonical PostgreSQL deployment coverage in `RVT.DatabaseMigrator/post-load/03_views_and_routines.sql`; stored routines are handled separately because their bodies need source-definition review and PostgreSQL-specific rewrites.

2026-07-09 update: `RVT.DatabaseMigrator/post-load/04_routines.sql` now drops leftover PascalCase PostgreSQL routine names before creating the canonical routines, and repository-read function result aliases use lowercase snake_case. Data-access readers still accept the old aliases as a short-term compatibility fallback for SQL Server or pre-cleanup databases.

## Current State

- Application source has three active stored routine read calls through `IRvtStoredRoutineExecutor`.
- `RVT.BusinessLogic/Archive/SiteArchiveService.cs` remains the only active `SqlQueryRaw` surface found in source and is already parameterized.
- PostgreSQL post-load scripts include Timescale setup, canonical views, and the exported routine ports in `RVT.DatabaseMigrator/post-load/04_routines.sql`.
- `RvtStoredRoutineExecutor` maps legacy SQL Server routine names to lowercase snake_case PostgreSQL routine names when the configured provider is PostgreSQL.
- ASP.NET Identity physical objects remain excluded from canonical renaming. Routine bodies that reference `AspNet*` objects must preserve those table and column names with PostgreSQL quoting.

## Exported Routine Status

| SQL Server routine | PostgreSQL routine | Current consumer | Port status |
| --- | --- | --- | --- |
| `ErrorInsert` | `public.error_insert` | No active SPA call found | Draft procedure added |
| `MonitorStatusForMonth` | `public.monitor_status_for_month` | `MonitorRepository.MonitorStatusForMonth` | Draft set-returning function added |
| `MonitorStatusTimeCheck` | `public.monitor_status_time_check` | `MonitorRepository.MonitorStatusTimeCheck` | Draft set-returning function added |
| `PeakRecordBreachAndAlerts` | `public.peak_record_breach_and_alerts` | `OmnidotsBreachesAndAlertsRepository.BreachesAndAlertsForDate` | Draft set-returning function added |
| `UserActionsHistoryInsert` | `public.user_actions_history_insert` | No active SPA call found | Draft procedure added |

## Export Source Definitions

Run this exporter from the alpha repository on the Windows VM or from an environment that can reach the SQL Server `rvt` database:

```powershell
.\database\sqlserver\export_routine_definitions.ps1
```

The exporter writes:

```text
docs/database/sqlserver-routine-definitions-source.csv
```

The CSV includes current routine name, canonical target name, routine type, review notes, and full SQL Server definition text.

## Porting Rules

- Rename application-owned routines to lowercase snake_case.
- Convert SQL Server functions/procedures to PostgreSQL `CREATE OR REPLACE FUNCTION` or `CREATE PROCEDURE` only after the source definition is exported and reviewed.
- Replace SQL Server-only syntax such as `GETDATE`, `GETUTCDATE`, `DATEADD`, `DATEDIFF`, `ISNULL`, `TOP`, bracketed identifiers, and `dbo.` schema references.
- Convert application-owned table, view, and column references to canonical names.
- Preserve ASP.NET Identity physical names such as `public."AspNetUsers"` and original Identity column names such as `"Id"`, `"UserName"`, and `"Email"`.
- Keep generated PostgreSQL routines in the migrator post-load phase once they are approved.

## Open Items

- Decide whether the two insert-style legacy procedures should remain available, move into application code, or be retired.
- Re-run the SQL Server exporter before production migration rehearsal to catch any new or changed routines.
- Keep regression tests checking that application-owned references are canonical and Identity references are preserved.

## Verification

The current guardrails verify that the routine exporter exists, this inventory is present, the migrator README documents the routine phase, exported rows target canonical names, and `04_routines.sql` contains the five canonical PostgreSQL routine definitions without active SQL Server dialect tokens.

## Rehearsal Result - 2026-06-09

Target database: `rvt_naming_rehearsal_20260608222014` inside the local `rvt-timescaledb` container.

Applied `RVT.DatabaseMigrator/post-load/04_routines.sql` with `ON_ERROR_STOP=1`, then executed smoke calls against the canonical schema:

| Routine | Result |
| --- | --- |
| `public.monitor_status_time_check` | Returned `1` sample row |
| `public.monitor_status_for_month` | Returned `1` sample row |
| `public.peak_record_breach_and_alerts` | Returned `341` sample rows |
| `public.error_insert` | Executed inside a rolled-back transaction |
| `public.user_actions_history_insert` | Executed inside a rolled-back transaction |

The first procedure smoke found that `user_action_history.id` has no default in the canonical Timescale clone. `user_actions_history_insert` now supplies `gen_random_uuid()` explicitly, and `CutoverReadinessTests.DatabaseMigratorPostLoadScripts_DeployCanonicalRoutines` guards that requirement.
