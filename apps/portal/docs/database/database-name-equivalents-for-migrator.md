# Database Name Equivalents For Migrator

Generated: 2026-06-08

This document explains how to use the name-equivalence registry when updating the older SQL Server-to-Postgres migration application.

## Files To Use

- Full registry: `docs/database/database-name-registry.csv`
- Migrator-focused mapping: `docs/database/database-name-equivalents-for-migrator.csv`
- SQL Server source registry: `docs/database/sqlserver-name-registry.csv`
- SQL Server source migrator mapping: `docs/database/database-name-equivalents-for-migrator-sqlserver.csv`
- Generator script: `database/postgres/generate_database_name_registry.sql`
- SQL Server exporter: `database/sqlserver/export_database_name_registry.ps1`

The migrator-focused CSV files contain only rows where the proposed canonical name differs from the current physical name. Use `database-name-equivalents-for-migrator-sqlserver.csv` as the primary source-side mapping for the old SQL Server-to-Postgres migrator, because it is generated directly from SQL Server metadata.

## CSV Columns

| Column | Meaning |
| --- | --- |
| `current_relation` | Existing table/view name used by the old schema and current migrator input. |
| `current_column` | Existing column name. Empty rows represent relation-level renames. |
| `new_relation` | Canonical target relation name in Postgres. |
| `new_column` | Canonical target column name in Postgres. Empty rows represent relation-level renames. |
| `change_type` | `rename_relation` or `rename_column`. |
| `notes` | Review hints such as `single_column_pk`, `fk_to_site`, or `legacy_misspelling`. |

## Migrator Update Rules

- Source SQL Server reads may continue to use old names until the SQL Server source schema is renamed.
- Postgres writes must use the canonical `new_relation` and `new_column` names after the database naming migration is applied.
- Prefer the SQL Server-specific mapping when updating source readers, then cross-check with the Postgres registry before writing canonical target inserts.
- For relation-level rows, update insert targets from `current_relation` to `new_relation`.
- For column-level rows, update insert column lists and row mappers from `current_column` to `new_column`.
- For `single_column_pk` rows, confirm whether the migrator currently treats the old natural key as an identifier. The target database column will be `id` under the new standard.
- For FK rows such as `fk_to_site`, populate the canonical `{referenced_table}_id` target column.
- Do not update the migrator to write into the temporary `legacy` schema. Compatibility views are for old readers only, not new migration writes.

## High-Impact Examples

| Current | Canonical |
| --- | --- |
| `Sites.Id` | `site.id` |
| `Companies.Id` | `company.id` |
| `Contracts.CompanyId` | `contract.company_id` |
| `Contracts.SiteiD` | `contract.site_id` |
| `MonitorsList.Id` | `monitor.id` |
| `Deployments.ContractId` | `deployment.contract_id` |
| `Deployments.MonitorId` | `deployment.monitor_id` |
| `SiteOperatingHours.SiteId` | `site_operating_hour.site_id` |
| `HelpArticles.SectionId` | `help_article.help_section_id` |
| `ErrorLog.Timestamtp` | `error_log.logged_at` |

## Review Notes

- The first registry pass is generated from the live Timescale/Postgres schema. It should be reviewed before being treated as a final migration contract.
- The SQL Server-specific registry has been exported from the development SQL Server database and should be compared against any production SQL Server source before the one-off migration application runs.
- Domain acronym mappings for measurement tables should be reviewed by someone familiar with noise, dust, and vibration data.
- If the old migrator depends on quoted PostgreSQL identifiers, remove quoting as part of the update once canonical lowercase names are in place.
