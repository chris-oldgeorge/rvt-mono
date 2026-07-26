# Database Naming Cutover Runbook

Generated: 2026-06-08; PostgreSQL-only contract updated 2026-07-26.

## Purpose

This runbook controls the PostgreSQL/TimescaleDB cutover to lowercase, singular,
snake_case application-owned objects. ASP.NET Identity physical objects remain
framework-managed and keep their `AspNet*` names.

## Current gate status

- The first clone rehearsal passed on `rvt_naming_rehearsal_20260608222014`.
- The seeded backup/restore rehearsal passed on
  `rvt_prodlike_rehearsal_20260609083554`.
- Development `rvt` was migrated on 2026-06-09 after a `pg_dump -Fc`
  backup was saved to `/tmp/rvt_pre_canonical_migration_20260609.dump`.
- All 10 hypertables retained their chunk counts and canonical names.
- No `legacy` compatibility schema is deployed or supported.
- Before any production-scale historical cutover, repeat the restored rehearsal
  against the final production backup or an equivalent full-volume data set.

## Preconditions

- Review and approve `docs/database/database-name-registry.csv` and
  `docs/database/database-constraint-index-name-registry.csv`.
- Confirm that all three EF migration chains are current and that SchemaDeploy
  assets match the application release.
- Assign an operator and rollback owner, announce a feature-freeze window, and
  verify monitoring/support coverage.
- Verify the application artifact against PostgreSQL with the same connection
  variable names used in the target environment.
- Confirm no application, monitor, report, or external integration queries a
  `legacy` schema.

## Backup and restore rehearsal

1. Take a logical or physical backup appropriate for the TimescaleDB deployment.
2. Restore or clone it to an isolated validation database.
3. Record the backup path, restore target, operator, start time, and checksums.
4. Run the forward sequence below.
5. Run `database/postgres/verify_timescale_after_rename.sql`.
6. Verify canonical identifiers, hypertable names, chunk counts, compression,
   retention policies, continuous aggregates, and Timescale jobs.
7. Run representative `time_bucket`, dashboard, site, monitor, Help CMS, and
   archive queries.
8. Rehearse rollback and restore the validation database to the intended final
   state.

## Freeze and forward sequence

1. Announce the feature freeze.
2. Pause monitor jobs, background writers, and write-capable portal instances.
3. Confirm no long-running transaction holds locks on target objects.
4. Take and verify the final backup.
5. Run, with stop-on-error enabled:
   1. `database/postgres/canonical_database_naming.sql`
   2. `database/postgres/canonical_constraint_index_naming.sql`
   3. `database/postgres/verify_timescale_after_rename.sql`
6. Apply the release's three EF migration chains using their separate history
   tables.
7. Run `RVT.SchemaDeploy` so `database/postgres/` and
   `database/postgres/post-load/` assets are applied in release order.
8. Deploy the matching portal build, then the matching SPA assets.
9. Run smoke tests before resuming writers.
10. Resume monitor jobs in controlled waves and end the freeze only after
    monitoring remains stable.

## Required verification

- Canonical public relation, column, index, and constraint violations are zero,
  excluding framework-managed Identity objects.
- All expected hypertables exist and no retired names remain.
- Chunk counts match the pre-cutover baseline.
- Public views compile with zero-row selects.
- The five canonical routines pass smoke calls.
- Dashboard, site search, monitor search, Help CMS, archive, AirQ bucket,
  site-average bucket, and authentication smoke tests pass.
- Query latency remains inside the approved baseline.

## Rollback triggers

Rollback when backup validation fails, a script cannot be corrected inside the
window, hypertable/chunk validation fails, the application cannot start against
canonical names, smoke tests fail materially, or critical query latency exceeds
the accepted baseline.

## Rollback boundary

1. Return the portal to maintenance mode and stop all database writers.
2. If the naming scripts completed but application deployment did not, run:
   1. `database/postgres/canonical_constraint_index_naming_rollback.sql`
   2. `database/postgres/canonical_database_naming_rollback.sql`
3. If data integrity or Timescale metadata is uncertain, restore the verified
   pre-cutover backup instead of attempting an in-place repair.
4. Deploy the previous application artifact only after its expected schema is
   restored.
5. Verify selected relation, column, constraint, index, hypertable, chunk, view,
   routine, and Identity objects.
6. Run the rollback smoke suite, resume writers gradually, and retain all
   evidence for the incident review.

Rollback never creates a compatibility archive or `legacy` schema. Git history
is the recovery source for retired implementation artifacts.
