# Database Naming Cutover Runbook

Generated: 2026-06-08

## Purpose

This runbook defines the controlled cutover path for moving RVT application-owned database objects to canonical lowercase, singular, snake_case names. It covers SQL Server and PostgreSQL/TimescaleDB rehearsed script order, deployment sequencing, smoke checks, rollback decision points, and post-cutover cleanup.

ASP.NET Identity physical tables, columns, keys, foreign keys, and indexes are excluded from this refactor. They must keep their framework-managed `AspNet*` names in both providers.

## Current Gate Status

- PostgreSQL/TimescaleDB clone rehearsal passed on `rvt_naming_rehearsal_20260608222014`.
- PostgreSQL/TimescaleDB seeded backup/restore rehearsal passed on `rvt_prodlike_rehearsal_20260609083554`.
- Development PostgreSQL/TimescaleDB `rvt` was physically migrated on 2026-06-09 after saving `/tmp/rvt_pre_canonical_migration_20260609.dump`.
- SQL Server clone rehearsal passed on `rvt_sqlserver_naming_rehearsal_20260608224500`.
- SQL Server forward -> rollback -> forward passed with final non-Identity invalid identifier count `0`.
- Source SQL Server has not been physically renamed. Development PostgreSQL/TimescaleDB `rvt` has been physically renamed.
- `RVTDbContext` uses canonical EF table/column mappings for Npgsql/Postgres and keeps legacy names for SQL Server.
- No `legacy` compatibility views were deployed to development `rvt`.
- Remaining scale gate: repeat the restored rehearsal against a final production backup or a 400M-row-equivalent Timescale data set before any later production-scale cutover.

## Cutover Preconditions

- RVT and engineering have reviewed and approved:
  - `docs/database/database-name-registry.csv`
  - `docs/database/sqlserver-name-registry.csv`
  - `docs/database/database-constraint-index-name-registry.csv`
  - `docs/database/database-name-equivalents-for-migrator.csv`
  - `docs/database/database-name-equivalents-for-migrator-sqlserver.csv`
- Decision for development: do not deploy temporary `legacy` compatibility views. Revisit only if a later external/reporting consumer explicitly requires them.
- Open decision: confirm production storage tiering and migration-app assumptions for large historical Timescale data.
- Confirm no new schema migrations are pending outside the naming refactor.
- Confirm a feature freeze window and owner for each execution step for production-like environments. The development migration did not require a freeze window.
- Confirm application deployment package that enables canonical EF mappings and updated raw SQL is ready and tested. For the current development cutover, Npgsql/Postgres canonical EF mapping is enabled in `RVTDbContext`.
- Confirm monitoring and support coverage during the cutover window.

## Backup And Restore Validation

### SQL Server

1. Take a copy-only full backup of the production source database.
2. Restore the backup to a validation database on the same SQL Server version.
3. Run the SQL Server forward sequence against the restored validation database.
4. Run canonical verification:
   - non-Identity invalid identifier count is `0`.
   - all `dbo` views compile.
   - smoke queries return plausible rows.
5. Run rollback sequence against the validation database.
6. Verify selected legacy columns and selected legacy primary keys are restored.
7. Record backup path, restore target, timings, operator, and evidence in the cutover log.

### PostgreSQL/TimescaleDB

1. Take a logical or physical backup appropriate for the production Timescale deployment.
2. Restore or clone to a validation database.
3. Run the PostgreSQL forward sequence against the validation database.
4. Run `database/postgres/verify_timescale_after_rename.sql`.
5. Verify:
   - all expected hypertables report canonical names.
   - chunk counts match the pre-cutover baseline.
   - compression, retention policies, continuous aggregates, and Timescale jobs are unchanged or explicitly accounted for.
   - representative `time_bucket` queries return expected buckets.
6. Run rollback sequence against the validation database.
7. Record backup path, restore target, timings, operator, and evidence in the cutover log.

## Freeze And Deployment Sequence

1. Announce feature freeze start.
2. Disable scheduled monitor jobs and background data-writing jobs where safe.
3. Stop write-capable application instances or put the portal into maintenance mode.
4. Confirm no active database sessions are holding long-running transactions against target objects.
5. Take final production backups and verify backup completion.
6. Apply database rename scripts.
7. Deploy the application build that uses canonical mappings and canonical raw SQL.
8. Re-enable application instances.
9. Run smoke tests.
10. Re-enable scheduled monitor jobs and background workers after smoke tests pass.
11. Announce feature freeze end only after monitoring remains stable.

## SQL Server Execution Order

Run against the production SQL Server database only after backup validation:

1. `database/sqlserver/canonical_database_naming.sql`
2. `database/sqlserver/canonical_view_module_rewrite.sql`
3. `database/sqlserver/canonical_routine_rewrite.sql`
4. `database/sqlserver/canonical_constraint_index_naming.sql`

Verification:

- Run the same canonical identifier scan used in the SQL Server rehearsal, excluding `AspNet*` framework objects.
- Run `select top (0) *` against every `dbo` view.
- Run smoke queries for dashboard, site search, monitor search, Help CMS, AirQ bucket, and site-average bucket.

## PostgreSQL/TimescaleDB Execution Order

Run against a PostgreSQL/TimescaleDB database only after backup validation:

1. `database/postgres/canonical_database_naming.sql`
2. `database/postgres/canonical_constraint_index_naming.sql`
3. `database/postgres/verify_timescale_after_rename.sql`

Verification:

- Confirm expected canonical hypertables exist.
- Confirm no legacy hypertable names remain outside any approved compatibility schema.
- Confirm chunk counts match baseline.
- Compile public views with zero-row selects.
- Run dashboard, site search, monitor search, Help CMS, archive/export, and `time_bucket` smoke queries.
- Confirm the `legacy` schema is absent unless compatibility views were explicitly approved.

## Application Deployment Order

1. Deploy backend changes that enable canonical EF mappings only after physical database rename scripts complete. Npgsql/Postgres and SQL Server now use canonical mapping after their local development migrations.
2. Deploy SPA assets generated from the same backend contract version.
3. Keep ASP.NET Identity mapping unchanged.
4. Run API health checks before opening traffic.
5. Run SPA login and navigation checks after traffic resumes.

## Required Smoke Tests

- Login using an RVT admin account.
- Dashboard loads and returns dashboard summary counts.
- Site search loads and opens a site detail view.
- Monitor search loads and opens monitor detail/current status.
- Help CMS admin can list sections/articles.
- Published Help content is visible to a normal authorized user.
- Archive/export path can query archived site data without SQL errors.
- Report/search views load without stale legacy object references.
- Timescale `time_bucket` queries return expected buckets for AirQ and site-average data.
- Monitor jobs can run after re-enable without duplicate or overlapping executions.

## Rollback Decision Points

Rollback immediately if any of these occur before traffic is reopened:

- Backup validation failed or backup location is unavailable.
- Any database script fails and cannot be corrected within the approved window.
- SQL Server view validation fails.
- Timescale hypertable or chunk validation fails.
- Application deployment cannot start against canonical names.

Rollback after reopening traffic if any of these occur and cannot be fixed inside the approved incident window:

- Login fails for valid users.
- Dashboard, site search, monitor search, Help CMS, archive/export, or report/search endpoints fail because of database naming errors.
- Monitor ingestion or scheduled monitor jobs fail because of renamed objects.
- Query latency materially exceeds the accepted baseline for critical operational screens.

## SQL Server Rollback Order

1. Stop application traffic or return to maintenance mode.
2. Stop write-capable background jobs.
3. Run `database/sqlserver/canonical_constraint_index_naming_rollback.sql`.
4. Run `database/sqlserver/canonical_database_naming_rollback.sql`.
5. Run `database/sqlserver/canonical_view_module_rewrite_rollback.sql`.
6. Run `database/sqlserver/canonical_routine_rewrite_rollback.sql`.
7. Redeploy the previous application build that uses legacy physical names.
8. Verify selected legacy table, column, view, primary-key, routine, and default-constraint names.
9. Run legacy smoke tests.

## PostgreSQL/TimescaleDB Rollback Order

1. Stop application traffic or return to maintenance mode.
2. Stop write-capable background jobs.
3. Run `database/postgres/canonical_constraint_index_naming_rollback.sql`.
4. Run `database/postgres/canonical_database_naming_rollback.sql`.
5. Redeploy the previous application build that uses legacy physical names.
6. Run `database/postgres/verify_timescale_after_rename.sql` and confirm expected pre-rename/legacy objects are restored.
7. Run legacy smoke tests.

## Cutover Log Template

Record these items during each rehearsal and production cutover:

- Environment:
- Database provider:
- Source database:
- Backup path or backup id:
- Restore validation target:
- Operator:
- Start time:
- End time:
- Forward timings:
- Verification evidence:
- Rollback timings, if run:
- Smoke test result:
- Known issues:
- Go/no-go decision:

## Post-Cutover Follow-Up

- Keep compatibility views only if explicitly approved, documented, and owner-assigned. The development migration uses no `legacy` compatibility schema.
- Update onboarding documentation with the final naming standard.
- Keep the SQL Server-to-Postgres one-off migrator aligned with the final equivalents files and canonical post-load views/routines. The local final validation from canonical SQL Server source into disposable Timescale target `rvt_migrator_validation` passed with `48` tables and `4,314,832` rows copied exactly.
- Update operational runbooks for database backups, restores, monitor jobs, and archive/report support.
- Close DBR cycle items only after the full-volume restored lock duration and query-plan evidence is captured.
