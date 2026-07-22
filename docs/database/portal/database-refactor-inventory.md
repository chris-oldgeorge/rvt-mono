# Database Refactor Inventory

Generated: 2026-06-08

This inventory records the first implementation pass for DBR.1, based on the live local Timescale/Postgres `rvt` database and the reachable development SQL Server `rvt` database.

## Source Inspected

- Container: `rvt-timescaledb`
- Database: `rvt`
- Schema: `public`
- SQL Server endpoint: development SQL Server from `RvtPortal.Spa/appsettings.Development.json`
- SQL Server database: `rvt`
- SQL Server schema: `dbo`

## Timescale/Postgres Summary Counts

| Object group | Count |
| --- | ---: |
| Base tables | 48 |
| Views | 29 |
| Registry rows | 810 |
| Table rename candidates | 48 |
| Table column rename candidates | 405 |
| Table column currently compliant | 15 |
| View rename candidates | 29 |
| View column rename candidates | 308 |
| View column currently compliant | 5 |

## SQL Server Summary Counts

| Object group | Count |
| --- | ---: |
| Registry rows | 919 |
| Table rename candidates | 49 |
| Table column rename candidates | 415 |
| Table column currently compliant | 15 |
| View rename candidates | 37 |
| View column rename candidates | 399 |
| View column currently compliant | 4 |

## Primary Findings

- The current physical database uses mixed-case identifiers almost everywhere.
- Current relation names are mostly plural or legacy-shaped, such as `Sites`, `Contracts`, `Companies`, `MonitorsList`, `SiteOperatingHours`, and `HelpArticles`.
- Current foreign keys often follow the old `FooId` style and include inconsistent spellings such as `SiteiD`.
- Several measurement tables use domain acronyms. These need domain-owner review before final migration, especially `LAeq`, `LA10`, `LCeq`, `PM10`, and similar sensor payload fields.
- Some tables use natural single-column primary keys such as `SerialId`. The rule set maps single-column primary keys to `id`; these rows require explicit review because the target name no longer describes the original natural key.
- Legacy typo `Timestamtp` is mapped to `logged_at`.

## Generated Artifacts

- `docs/database/database-name-registry.csv`: complete registry with provider, object type, current name, proposed canonical name, data type, change type, and notes.
- `docs/database/database-name-equivalents-for-migrator.csv`: filtered old-name to new-name mapping intended for the SQL Server-to-Postgres migrator update.
- `docs/database/sqlserver-name-registry.csv`: SQL Server source-side registry exported from `sys.tables`, `sys.columns`, `sys.views`, foreign keys, primary keys, and type metadata.
- `docs/database/database-name-equivalents-for-migrator-sqlserver.csv`: SQL Server source-side old-name to canonical Postgres target-name mapping for the one-off migrator.
- `database/postgres/generate_database_name_registry.sql`: reusable generator used for the first registry pass.
- `database/sqlserver/export_database_name_registry.ps1`: reusable SQL Server exporter used for the source-side registry pass.
- `RVT.DataAccess/Configuration/RvtCanonicalModelBuilderExtensions.cs`: opt-in EF Core convention for canonical table/view/column mapping.
- `RVT.DataAccess/Configuration/DatabaseNamingRules.cs`: reusable canonical relation/column naming helper shared by tests and the opt-in EF convention.
- `docs/database/database-constraint-index-name-registry.csv`: old-to-new mapping for constraint and index names.
- `docs/database/postgres-constraint-index-source.csv`: Postgres source metadata used to generate constraint/index names.
- `docs/database/sqlserver-constraint-index-source.csv`: SQL Server source metadata used to generate constraint/index names.
- `database/postgres/export_constraint_index_name_registry.sql`: reusable Postgres constraint/index metadata exporter.
- `database/sqlserver/export_constraint_index_name_registry.ps1`: reusable SQL Server constraint/index metadata exporter.
- `database/postgres/canonical_constraint_index_naming.sql`: Postgres constraint/index rename draft.
- `database/postgres/canonical_constraint_index_naming_rollback.sql`: Postgres constraint/index rollback draft.
- `database/sqlserver/canonical_constraint_index_naming.sql`: SQL Server constraint/index rename draft.
- `database/sqlserver/canonical_constraint_index_naming_rollback.sql`: SQL Server constraint/index rollback draft.
- `database/postgres/verify_timescale_after_rename.sql`: Timescale pre/post rename verification script.
- `docs/database/portal/timescale-refactor-rehearsal.md`: rehearsal order and local Timescale baseline.
- `docs/database/portal/sqlserver-refactor-rehearsal.md`: SQL Server clone rehearsal result, rollback evidence, and view-rewrite follow-up.
- `docs/database/portal/database-naming-cutover-runbook.md`: Draft production cutover, validation, smoke-test, and rollback runbook for the canonical naming migration.
- `database/postgres/legacy_compatibility_views.sql`: PostgreSQL read-only `legacy` schema views that expose old relation and column names after canonical cutover.
- `database/sqlserver/legacy_compatibility_views.sql`: SQL Server read-only `legacy` schema views that expose old relation and column names after canonical cutover.
- `docs/database/portal/legacy-compatibility-deprecation.md`: Compatibility ownership, rules, removal workflow, and object inventory.

## Review Before Physical Rename

Before running any database rename migration, the team should review:

- Every relation-level row in `database-name-registry.csv`.
- Every relation-level row in `sqlserver-name-registry.csv`.
- Every `single_column_pk` note where the current column is not `Id`.
- Every measurement acronym mapping.
- Every inferred mapping that affects the legacy migrator app.
- All current external/reporting consumers that may still require temporary `legacy` schema compatibility views and their owner/removal dates.
- The point at which live `RVTDbContext` and `RVTSearchContext` should enable the opt-in EF convention. Do not enable it before the physical database rename rehearsal succeeds.
- The constraint/index registry and generated scripts, especially old SQL Server-generated names and measurement-table indexes.

## Timescale Rehearsal

The local `rvt-timescaledb` container was started and inspected on 2026-06-08. It reports TimescaleDB `2.27.1`, 10 current hypertables, no compressed hypertables, no continuous aggregates, and only Timescale system jobs. The verification script reports all 10 expected hypertables as `pre_rename`, which is expected before the canonical rename rehearsal.

The first local clone rehearsal was completed on `rvt_naming_rehearsal_20260608222014`. The Postgres relation/column and constraint/index rename scripts applied successfully, all 10 hypertables reported `renamed`, chunk counts were preserved, no legacy hypertable names remained, and representative dashboard, site search, monitor search, Help, and `time_bucket` queries ran against the canonical names.

The rehearsal also found and fixed a rollback-order defect in the generated Postgres and SQL Server database rollback scripts. Rollback scripts now restore columns before relations so table-qualified column renames still target canonical relation names. `DatabaseNamingConventionTests` includes a guardrail for this ordering.

On 2026-06-09, a backup/restore-style production-like rehearsal was completed on `rvt_prodlike_rehearsal_20260609083554`. The source `rvt` database was dumped with `pg_dump -Fc` to a `292.3M` artifact, restored into a fresh validation database with Timescale `pre_restore`/`post_restore`, then renamed forward, validated, rolled back, and moved forward again. The restored data set had 48 base tables, 10 hypertables, about 4.3M estimated user-table rows, and no compressed hypertables or continuous aggregates.

The restored validation run completed `canonical_database_naming.sql` in about `0.24s`, `canonical_constraint_index_naming.sql` in about `0.05s`, canonical view deployment in about `0.02s`, and routine deployment in under `0.01s`, all with the scripts' `5s` lock timeout and no lock-timeout failures. Post-rename validation found 0 noncanonical public relations, columns, indexes, or constraints outside ASP.NET Identity. Public view compile smoke passed in about `10.4s`, representative dashboard/site/monitor/time-bucket query plans stayed in the same shape, and rollback restored selected legacy objects before the validation database was returned to canonical state.

This is still not a substitute for a future full-volume production backup rehearsal. The local seeded data set is production-like in structure and has several million migrated rows, but the expected production history is closer to 400M rows.

The local development Postgres/Timescale `rvt` database was physically migrated on 2026-06-09 after the team confirmed that the application is still in development. A pre-migration `pg_dump -Fc` backup was saved to `/tmp/rvt_pre_canonical_migration_20260609.dump` (`292.3M`). The migration applied canonical relation/column naming, canonical constraint/index naming, canonical PostgreSQL views, and canonical PostgreSQL routines. The `legacy` compatibility view scripts were not run; the `legacy` schema was absent before migration and remained absent after migration.

Post-migration verification on development `rvt` found all 10 hypertables renamed, chunk counts preserved, 0 noncanonical public relations/columns/indexes/constraints outside ASP.NET Identity, public view compile smoke passing in about `11.46s`, and representative counts matching the rehearsal baseline. Backend tests passed `155/155`, frontend Vitest passed `34/34`, and Playwright e2e passed `4/4`.

`RVTDbContext` now applies `ApplyRvtCanonicalDatabaseNames()` automatically for both Npgsql/PostgreSQL and SQL Server providers, so the migrated development databases use canonical names at runtime. `DatabaseNamingConventionTests` covers both provider branches.

## SQL Server Rehearsal

The first SQL Server clone rehearsal was run on `rvt_sqlserver_naming_rehearsal_20260608224500`, restored from `rvt` through a copy-only backup. The clone restored `49` base tables. Forward relation/column renames completed in `647 ms` and constraint/index renames completed in `971 ms`.

Forward verification did not pass. The canonical scan still found `342` non-lowercase or non-canonical `dbo` identifiers, and public view validation failed because SQL Server view definitions still reference old physical names after `sp_rename`. Examples include `dbo.MonitorsList`, `dbo.Sites`, `dbo.Contracts`, `dbo.AirQNoiseLevels`, `dbo.SiteAverages`, `dbo.Notifications`, and `dbo.AspNetUsers`. The rehearsal also confirmed that SQL Server-specific views from `sqlserver-name-registry.csv`, such as `AirQNoiseLevel1dayAvg`, `MonitorReport`, `MyAtmDustLevel1DayAvg`, `OmnidotsReadStatus`, and `ReportSearch2`, need full script coverage.

Rollback on the clone succeeded. Constraint/index rollback completed in `749 ms`; relation/column rollback completed in `386 ms`; selected legacy columns and selected legacy primary-key names were restored. The live/source `rvt` database was not renamed.

### ASP.NET Identity Exclusion

ASP.NET Identity tables and their framework-managed columns, keys, foreign keys, and indexes are excluded from physical database naming refactoring. Registry rows now mark these objects with `identity_excluded` and generated provider rename scripts skip them. Application-owned views may continue to join Identity tables, but those joins must reference the original `AspNet*` table and column names.

### SQL Server Corrected Clone Gate

The SQL Server rehearsal clone now passes after excluding ASP.NET Identity physical objects from the refactor and adding SQL Server default-constraint coverage. Application-owned relations, views, columns, constraints, indexes, and default constraints reach canonical lowercase names on the clone, while `AspNet*` framework objects remain unchanged. The forward -> rollback -> forward cycle completed successfully and final smoke queries passed.

The local development SQL Server `rvt` database was physically migrated on 2026-06-09. A copy-only backup was saved to `/var/opt/mssql/data/rvt_pre_canonical_sqlserver_20260609092443.bak`, and the rehearsal clone `rvt_sqlserver_cutover_rehearsal_20260609092443` completed forward -> rollback -> forward before the source migration ran. Source verification found 0 noncanonical application-owned identifiers, 37 compiling views, passing routine smoke, and representative counts matching the rehearsal baseline. No legacy compatibility views were applied.

### Cutover Runbook Draft

`docs/database/portal/database-naming-cutover-runbook.md` now defines the pre-cutover backup and restore checks, SQL Server and PostgreSQL/TimescaleDB execution order, deployment freeze sequence for production-like environments, required smoke tests, rollback triggers, and rollback command order. The development Postgres and SQL Server `rvt` databases have now been physically renamed, and the runtime EF context maps both providers to canonical names. The runbook keeps the remaining open gates visible for any later production-scale cutover: registry review, full-volume rehearsal evidence, and final application deployment readiness.

### Legacy Compatibility Layer

The temporary compatibility layer has generated provider scripts for a canonical naming cutover, but the team decided not to keep old-name compatibility views for the development migration. PostgreSQL would create 70 read-only views in the `legacy` schema, and SQL Server would create 79 read-only views in the `legacy` schema, but these scripts were not applied to the migrated development `rvt` database. RVT Portal, SPA, API, migrator, and monitor code must use canonical names.

The PostgreSQL script was applied successfully to the canonical rehearsal clone `rvt_perf_rehearsal_202606090145`. Smoke checks returned `442` rows from `legacy."Sites"`, `786` rows from `legacy."MonitorsList"`, and `70` views in the `legacy` schema. This was rehearsal-only evidence; development `rvt` has no `legacy` schema after the physical migration.

`RvtPortal.Spa.Tests/CutoverReadinessTests.cs` includes a guardrail that checks both provider scripts stay read-only and scans application source so new code cannot reference the `legacy` schema outside approved database/docs/test artifacts.

### Raw SQL, Reports, Archives, And DBR-Era Migrations

DBR-era migration SQL now uses canonical physical names for application-owned objects. `20260608121300_AddMonitorArchiveFields` targets `monitor.archived`, `monitor.archived_at`, `monitor.archived_by`, and `monitor.archive_reason`. `20260608124500_AddSiteOperatingHoursAndHelpCms` targets canonical names such as `site_operating_hour`, `help_section`, `help_article`, `help_asset`, `site_id`, `help_section_id`, and lowercase snake_case indexes/constraints.

The archive export SQL in `RVT.BusinessLogic/Archive/SiteArchiveService.cs` remains provider-aware and uses canonical table/column names while preserving CSV output aliases. Cutover tests now guard DBR-era migrations and application code against reintroducing old application-owned physical names outside approved compatibility/history artifacts.

### EF Canonical Baseline Replacement

On 2026-06-09, the EF Core baseline was replaced in-place so future migration scaffolding starts from the canonical schema instead of the retired SQL Server legacy names. The migration id `20231024072158_dataaccess` was preserved because current development databases already have that history row stamped.

The baseline migration, its designer file, and `RVTDbContextModelSnapshot.cs` now describe canonical table and column names such as `company`, `contract`, `monitor`, `site`, `site_operating_hour`, `help_section`, `help_article`, and `help_asset`. `RVTDbContextDesignTimeFactory` was added so `dotnet ef` can instantiate the context without relying on runtime appsettings.

The later 202606 EF migrations remain in the repository as idempotent history/replay steps, but their rollback paths are non-destructive because their objects are now owned by the canonical baseline. A disposable EF scaffolding probe generated an empty future migration after the replacement, confirming that the active snapshot matches the current model.

Environments that predate the DBR cutover must be migrated through the approved DBR scripts and runbooks. They should not try to recover the retired legacy schema by replaying old EF metadata.

### SQL Server-To-Postgres Migrator Final Validation

The in-solution `RVT.DatabaseMigrator` was run on 2026-06-09 from the Windows VM against the canonical local SQL Server `rvt` source database into disposable Timescale target `rvt_migrator_validation`.

Results:

- Source and target table counts matched: `48`.
- Exact source and target row totals matched: `4,314,832`.
- The target had `0` canonical identifier violations outside ASP.NET Identity.
- ASP.NET Identity remained physically preserved as `7` `AspNet*` tables.
- No `legacy` schema was created.
- Post-load scripts created `37` public views and `10` Timescale hypertables.
- View compile smoke passed for all `37` public views.
- Routine smoke passed for the canonical PostgreSQL ports of the five SQL Server routines.
- Representative target counts included dashboard `4`, site search `443`, monitor search `2282`, `air_q_noise_level` `866939`, `my_atm_dust_level` `500001`, `omnidots_peak_level` `500003`, `omnidots_trace` `1100152`, `site_average` `42606`, and `user_action_history` `479727`.
