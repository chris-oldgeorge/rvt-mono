# RVT Database Naming Standard

Generated: 2026-06-08

This standard defines the canonical naming style for the RVT database refactor. It applies to production tables, views, materialized views, hypertables, columns, constraints, indexes, functions, stored routines, and relation-like objects.

## Canonical Rules

- Use lowercase identifiers only.
- Use snake_case for every multi-word identifier.
- Use singular names for tables, views, hypertables, materialized views, and other relations that hold or expose rows.
- Use noun-based column names. Do not name a field after its data type, such as `text`, `timestamp`, or `uuid`.
- Avoid database reserved words and confusing generic words, including `user`, `lock`, and `table`.
- Use `id` for every single-column primary key.
- Use `{referenced_table}_{referenced_field}` for foreign keys. Most foreign keys therefore end with `_id`, such as `site_id`, `company_id`, and `monitor_id`.
- Use lowercase snake_case for constraints and indexes: `pk_site`, `fk_deployment_site_id`, `ix_site_company_id`.

## Application Naming

- Database identifiers are physical lowercase snake_case names.
- API JSON and TypeScript-facing models should remain camelCase.
- C# type names remain PascalCase under .NET conventions, while EF Core maps them to the canonical database names.
- The EF Core canonical mapping convention is currently opt-in and should be enabled for live contexts only during the reviewed rename rehearsal/cutover. This avoids pointing the application at canonical table names before the physical database has been renamed.

## Examples

| Current name | Canonical name | Notes |
| --- | --- | --- |
| `Sites` | `site` | Singular table name. |
| `Companies` | `company` | Singular table name. |
| `Contracts` | `contract` | Singular table name. |
| `MonitorsList` | `monitor` | Removes plural/list wording. |
| `SiteOperatingHours` | `site_operating_hour` | Singular relation with snake_case. |
| `HelpArticles` | `help_article` | Singular relation with snake_case. |
| `Id` | `id` | Single-column primary key. |
| `CompanyId` | `company_id` | Foreign key to `company.id`. |
| `SiteiD` | `site_id` | Normalizes inconsistent capitalization. |
| `SampleTime` | `sample_time` | Noun-based timestamp field. |
| `OperatingVolumeFlowTimestamp` | `operating_volume_flow_time` | Removes type-name wording while preserving measurement meaning. |
| `Timestamtp` | `logged_at` | Correct spelling and avoid type-name wording. |

## Temporary Compatibility

Temporary old-name compatibility objects may be created only in a dedicated compatibility schema such as `legacy`. These objects must be documented with an owner and removal date. They are not canonical database objects and must not be used by new code.

The current generated compatibility scripts are:

- `database/postgres/legacy_compatibility_views.sql`
- `database/sqlserver/legacy_compatibility_views.sql`
- `docs/database/legacy-compatibility-deprecation.md`

Compatibility views are read-only by default. PostgreSQL revokes insert/update/delete privileges on the `legacy` schema, and SQL Server denies insert/update/delete on `SCHEMA::[legacy]`. If an external consumer needs write compatibility, that exception must be approved explicitly and implemented as a separate tracked change.

## Compliance Checks

The refactor must add automated checks that fail when canonical objects violate these rules:

- Identifier is not lowercase.
- Identifier is not snake_case.
- Relation name is plural, except approved terms such as `status`.
- Single-column primary key is not named `id`.
- Foreign key column name does not match `{referenced_table}_{referenced_field}`.
- Identifier is a reserved word or an approved banned word.
- Canonical objects require quoted identifiers in PostgreSQL.

## EF Mapping Convention

The canonical EF convention lives in `RVT.DataAccess/Configuration/RvtCanonicalModelBuilderExtensions.cs`. It maps:

- Entity table/view names through `DatabaseNamingRules.ToCanonicalRelationName`.
- Single-column primary keys to `id`.
- Foreign keys to `{referenced_table}_id`.
- Other scalar properties through `DatabaseNamingRules.ToCanonicalColumnName`.

This convention is a migration aid, not a runtime default yet. Enable it only in controlled rehearsals or after the canonical rename migration is applied.

## Developer Onboarding

New developers should read `docs/onboarding/DATABASE_NAMING_ONBOARDING.md` before changing migrations, raw SQL, reports, archive SQL, compatibility views, or the SQL Server-to-Postgres migrator. Repo-local `AGENTS.md` also records the required database naming rules for future coding agents.

## Constraint And Index Naming

Generated constraint and index names should use:

- `pk_{relation}` for primary keys.
- `fk_{relation}_{column}` for foreign keys.
- `uq_{relation}_{column}` for unique constraints.
- `ck_{relation}_{purpose}` for check constraints.
- `ix_{relation}_{column}` for non-constraint indexes.

The generated mapping lives in `docs/database/database-constraint-index-name-registry.csv`, and the provider-specific rename scripts live under `database/postgres` and `database/sqlserver`.
