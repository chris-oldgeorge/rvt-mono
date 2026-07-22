# SQL Server Refactor Rehearsal

Generated: 2026-06-08

This note records the SQL Server clone rehearsal for the canonical database naming migration. The source `rvt` database was not renamed.

## Rehearsal Database

- Source database: `rvt`
- Rehearsal database: `rvt_sqlserver_naming_rehearsal_20260608224500`
- Clone method: SQL Server copy-only backup and restore into a separate database
- Restored base tables: `49`
- Backup/restore duration: `3677 ms`

## Forward Rename Result

Scripts applied to the rehearsal clone:

1. `database/sqlserver/canonical_database_naming.sql`
2. `database/sqlserver/canonical_constraint_index_naming.sql`

Timing:

- Relation and column rename script: `647 ms`
- Constraint and index rename script: `971 ms`

Forward verification did not pass.

Observed issues:

- Canonical identifier scan still found `342` non-lowercase or non-canonical public `dbo` identifiers.
- Public view smoke validation failed because many SQL Server view definitions still reference old physical names after `sp_rename`.
- Examples of stale references in view definitions include `dbo.MonitorsList`, `dbo.Sites`, `dbo.Contracts`, `dbo.AirQNoiseLevels`, `dbo.SiteAverages`, `dbo.Notifications`, and `dbo.AspNetUsers`.
- SQL Server-specific views such as `AirQNoiseLevel1dayAvg`, `MonitorReport`, `MyAtmDustLevel1DayAvg`, `OmnidotsReadStatus`, and `ReportSearch2` are present in `docs/database/sqlserver-name-registry.csv` but are not yet fully represented in the current SQL Server forward script.

Root cause:

- SQL Server `sp_rename` changes object and column metadata but does not rewrite stored view definitions.
- The current SQL Server rename script must be regenerated from the SQL Server-specific registry and must add an explicit view/module rewrite phase.

## Rollback Result

Rollback scripts applied to the same rehearsal clone:

1. `database/sqlserver/canonical_constraint_index_naming_rollback.sql`
2. `database/sqlserver/canonical_database_naming_rollback.sql`

Timing:

- Constraint and index rollback script: `749 ms`
- Relation and column rollback script: `386 ms`

Rollback verification:

- Selected legacy columns returned: `10`
- Selected legacy primary-key names returned: `3`
- The source `rvt` database remained untouched.

## Required Follow-Up

Before a SQL Server forward rehearsal can pass:

- Regenerate `database/sqlserver/canonical_database_naming.sql` and rollback from `docs/database/sqlserver-name-registry.csv`, not only the Postgres/common registry.
- Add generated SQL Server view/module rewrite scripts that recreate or alter views with canonical relation and column references.
- Add rollback view/module rewrite scripts so legacy definitions can be restored during rollback rehearsal.
- Add tests that verify SQL Server scripts cover every SQL Server registry relation row.
- Rerun the SQL Server clone rehearsal using forward -> verify -> rollback -> verify -> forward.

### Identity Exclusion Correction

The SQL Server rehearsal uncovered that the generated scripts were planning to rename ASP.NET Identity tables. Those tables are now an explicit exclusion: relation/column scripts, constraint/index scripts, and view rewrite scripts preserve `AspNet*` objects and only rewrite application-owned objects around them.

### Corrected SQL Server Rehearsal Pass - 2026-06-08

The SQL Server clone rehearsal was rerun after two corrections:

- ASP.NET Identity tables, columns, keys, foreign keys, and indexes are now explicit `identity_excluded` / `no_change` objects and are skipped by provider rename scripts.
- SQL Server view rewrites now keep joins to `AspNet*` framework tables under original physical names while exposing canonical aliases from application-owned views.
- SQL Server default constraints missed by the metadata export were added to the constraint/index registry with canonical `df_*` names.

Final clone cycle evidence:

- Clone: `rvt_sqlserver_naming_rehearsal_20260608224500`, source `rvt`, `49` base tables, clone duration `3009 ms`.
- Forward: database `546 ms`, views `59 ms`, constraints/indexes/defaults `873 ms`.
- Canonical verify: invalid non-Identity identifier count `0`; dashboard `4`, site search `443`, monitor search `2282`, Help articles `0`, AirQ bucket `5`, site-average bucket `5`.
- Rollback: constraints/indexes/defaults `861 ms`, database `424 ms`, views `51 ms`.
- Legacy verify after rollback: expected selected legacy column hits `10`, selected legacy PK hits `3`.
- Final forward after rollback: database `395 ms`, views `51 ms`, constraints/indexes/defaults `133 ms`.
- Final canonical verify after forward-rollback-forward: invalid non-Identity identifier count `0`, same smoke query counts as above.

The disposable clone was left in canonical state for inspection. The source SQL Server `rvt` database was not renamed during this rehearsal.

### Local SQL Server Source Migration - 2026-06-09

The local development SQL Server `rvt` source database was physically migrated after the development PostgreSQL/TimescaleDB migration and after the team confirmed no freeze window or legacy compatibility views were required.

Migration runner evidence:

- Source database: `rvt`
- Copy-only backup: `/var/opt/mssql/data/rvt_pre_canonical_sqlserver_20260609092443.bak`
- Rehearsal clone: `rvt_sqlserver_cutover_rehearsal_20260609092443`
- Rehearsal flow: forward -> canonical verify -> rollback -> legacy verify -> final forward -> canonical verify.
- Source flow: forward -> canonical verify.
- Source forward timings: database rename `0.74s`, view/module rewrite `0.08s`, routine rewrite `0.02s`, constraint/index/default rename `1.00s`.

Source verification:

- Invalid non-Identity identifiers: `0`.
- View compile smoke: `37` SQL Server views passed `SELECT TOP (0)`.
- Routine smoke: `monitor_status_time_check`, `monitor_status_for_month`, `peak_record_breach_and_alerts`, `error_insert`, and `user_actions_history_insert` executed successfully.
- Representative counts: dashboard `4`, site search `443`, monitor search `2282`, Help articles `0`, `air_q_noise_level` `866939`, `site_average` `42606`.
- No legacy compatibility views were applied.

Follow-up source changes:

- `RVTDbContext` now applies canonical EF mapping for SQL Server as well as PostgreSQL.
- `RvtStoredRoutineExecutor` maps legacy C# routine names to canonical SQL Server procedure names.
- SQL Server canonical routine definitions preserve legacy result-column aliases where callers depend on the current result shape.
