# Database Refactor Inventory

Generated: 2026-06-08; PostgreSQL-only contract updated 2026-07-26.

## Authoritative database

- Engine: PostgreSQL with TimescaleDB.
- Database: `rvt`.
- Application schema: `public`.
- Runtime naming: lowercase, singular, snake_case.
- ASP.NET Identity physical objects remain framework-managed `AspNet*`
  exceptions.
- Compatibility views and the `legacy` schema are retired.

## Retained artifacts

- `docs/database/database-name-registry.csv`
- `docs/database/database-constraint-index-name-registry.csv`
- `docs/database/postgres-constraint-index-source.csv`
- `database/postgres/generate_database_name_registry.sql`
- `database/postgres/export_constraint_index_name_registry.sql`
- `database/postgres/canonical_database_naming.sql`
- `database/postgres/canonical_database_naming_rollback.sql`
- `database/postgres/canonical_constraint_index_naming.sql`
- `database/postgres/canonical_constraint_index_naming_rollback.sql`
- `database/postgres/verify_timescale_after_rename.sql`
- `docs/database/portal/timescale-refactor-rehearsal.md`
- `docs/database/portal/database-naming-cutover-runbook.md`

## Review requirements

Before a physical rename, review every relation row, every single-column primary
key that is not named `id`, every measurement acronym mapping, every
constraint/index mapping, and every external consumer. New consumers must use
canonical `public` objects; there is no compatibility-schema exception.

## Rehearsal evidence

The first clone rehearsal on `rvt_naming_rehearsal_20260608222014` preserved
all 10 hypertables and their chunk counts. The restored production-like
rehearsal on `rvt_prodlike_rehearsal_20260609083554` used a 292.3 MiB
`pg_dump -Fc` artifact, 48 base tables, 10 hypertables, and about 4.3 million
rows. Forward, rollback, and forward execution completed without lock-timeout
failures; public-view and representative query smoke tests passed.

The development `rvt` database was migrated on 2026-06-09. Post-migration
verification found zero noncanonical public identifiers outside Identity,
preserved hypertable/chunk metadata, and no `legacy` schema. Backend, frontend,
and end-to-end checks passed in that rehearsal.

## Runtime contract

`RVTDbContext` and `RVTSearchContext` use canonical Npgsql mappings.
`ApplicationDbContext` retains Identity mappings. The three contexts keep
separate EF history tables. EF builds modeled objects; `RVT.SchemaDeploy`
applies ingestion tables, unmapped-column defaults, views, routines,
hypertables, and other PostgreSQL assets.

Git history is the recovery mechanism for removed provider-era inventories and
rehearsal artifacts.
