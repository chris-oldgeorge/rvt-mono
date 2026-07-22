# Database Naming Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor the RVT database and data-access layer to use canonical lowercase, singular, snake_case database identifiers with `id` primary keys and `{table}_id` foreign keys.

**Architecture:** Build a canonical name registry first, then migrate the physical database names using metadata-only renames where possible. EF Core mappings, raw SQL, stored routines, views, Timescale objects, tests, and compatibility views are updated in controlled phases so the large Timescale/Postgres data set is not copied or rewritten unnecessarily.

**Tech Stack:** .NET 10, EF Core, SQL Server, PostgreSQL/TimescaleDB, Docker, Plane, PowerShell, psql, xUnit, Vitest.

---

## Current Findings

- The local Timescale/Postgres `rvt` database currently has 48 base tables and 733 columns.
- Current canonical violations are broad: mixed-case names are present across tables, views, and columns.
- Existing examples include `Sites`, `Contracts`, `Companies`, `MonitorsList`, `SiteOperatingHours`, `HelpArticles`, `CompanyId`, `SiteiD`, `SampleTime`, and `Timestamtp`.
- The current codebase supports SQL Server and PostgreSQL through provider-aware EF Core configuration, plus some raw SQL/stored-routine adapters. Both providers must remain covered during the refactor.
- Production data scale is high, so the migration strategy must prefer table/column renames and view compatibility over copy-and-reload operations.

## Naming Rules To Implement

- All database identifiers are lowercase.
- All multi-word identifiers use snake_case.
- Data-holding relations use singular names.
- Column names are descriptive nouns, not type names.
- Reserved and confusing words are avoided.
- Single-column primary keys are named `id`.
- Foreign keys use `{referenced_table}_{referenced_field}`, normally `{referenced_table}_id`.
- Index, constraint, function, routine, and view names use lowercase snake_case.
- API JSON and TypeScript-facing models remain camelCase. C# entity and DTO type names remain PascalCase under normal .NET conventions.
- ASP.NET Identity tables, columns, keys, and indexes are excluded from physical database refactoring and keep their framework-managed names.

## Target Approach

Use an expand-contract migration with a short compatibility window:

1. Inventory every current table, view, column, key, index, routine, and dependency.
2. Produce a reviewed old-to-new naming registry.
3. Add automated naming compliance checks before changing names.
4. Update EF Core mappings to canonical names while keeping C# code readable.
5. Generate provider-specific SQL Server and PostgreSQL/Timescale rename scripts.
6. Add temporary compatibility views in a dedicated `legacy` schema for external/reporting consumers.
7. Update raw SQL, stored routines, reports, and dashboards to canonical names.
8. Rehearse against cloned SQL Server and Timescale databases.
9. Cut over after backup/restore validation and remove compatibility objects after sign-off.

## Task 1: Schema Inventory And Canonical Name Registry

**Files:**
- Create: `docs/database/database-name-registry.csv`
- Create: `docs/database/database-refactor-inventory.md`
- Modify: `docs/database/database-naming-standard.md`

- [x] Export PostgreSQL schema objects from `information_schema` and `pg_catalog`.
- [x] Export SQL Server schema objects from `sys.tables`, `sys.columns`, `sys.views`, primary-key metadata, foreign-key metadata, and type metadata. Index/module detail remains for constraint/index script naming.
- [x] Record each object as `provider,current_schema,current_relation,current_column,current_type,current_constraint,new_schema,new_relation,new_column,new_constraint,notes`.
- [x] Flag reserved words, plural table names, non-lowercase names, non-snake-case names, data-type-like names, and inconsistent foreign keys.
- [ ] Review the registry with RVT and engineering before any migration script is generated.

## Task 2: Automated Naming Compliance Tests

**Files:**
- Create: `RvtPortal.Spa.Tests/DatabaseNamingConventionTests.cs`
- Create: `RVT.DataAccess/Configuration/DatabaseNamingRules.cs`

- [x] Add tests for the initial naming-rule helper, including lowercase snake_case validation, banned identifiers, FK naming, and legacy-to-snake-case conversion.
- [x] Add tests that validate generated registry targets reject mixed-case current names and produce canonical lower snake_case target names.
- [x] Add tests for primary key naming: single-column keys must be `id`.
- [x] Add tests for foreign key naming: `{referenced_table}_{referenced_field}`.
- [x] Add tests for reserved/banned words.
- [x] Add an approved exception list for temporary `legacy` schema objects, unavoidable vendor payload fields, and framework-managed ASP.NET Identity objects.

## Task 3: EF Core Canonical Mapping Layer

**Files:**
- Modify: `RVT.DataAccess/Context/RVTDbContext.cs`
- Modify: `RVT.DataAccess/Context/RVTSearchContext.cs`
- Modify: `RVT.DataAccess/Configuration/RvtDatabaseServiceCollectionExtensions.cs`
- Modify: entity files under `RVT.Entities/`

- [x] Add an opt-in central EF mapping convention so entity classes can continue to compile while physical database names become canonical snake_case. Live contexts remain on old names until the rename rehearsal/cutover gate.
- [x] Map table names from plural/PascalCase to singular snake_case in the opt-in convention.
- [x] Map PK columns to `id` in the opt-in convention.
- [x] Map FK columns to canonical names such as `company_id`, `site_id`, `monitor_id`, and `help_article_id` in the opt-in convention.
- [x] Keep API DTOs and TypeScript DTOs camelCase; the convention only affects EF relational metadata when explicitly applied.
- [x] Enable the canonical convention for live Npgsql/Postgres and SQL Server `RVTDbContext` after the development `rvt` databases were physically migrated.

## Task 4: Provider-Specific Rename Migrations

**Files:**
- Create: `RVT.DataAccess/Migrations/<timestamp>_CanonicalDatabaseNaming.cs`
- Create: `database/postgres/canonical_database_naming.sql`
- Create: `database/sqlserver/canonical_database_naming.sql`

- [x] Generate PostgreSQL rename SQL with `ALTER TABLE ... RENAME TO` and `ALTER TABLE ... RENAME COLUMN`; constraint/index renames remain for the reviewed registry pass.
- [x] Generate SQL Server rename SQL using `sp_rename`; explicit constraint/index rename statements remain for the reviewed registry pass.
- [x] Generate provider-specific constraint/index rename and rollback drafts from live Postgres and SQL Server metadata.
- [x] Add SQL Server default-constraint rename coverage missed by the initial constraint/index export.
- [x] Add a Timescale verification script that records hypertables, chunk counts, compression state, continuous aggregates, and Timescale jobs before/after rename.
- [x] Ensure Timescale hypertables are renamed without data copy on a cloned database and verify chunk, compression, continuous aggregate, and time_bucket dependencies.
- [x] Regenerate SQL Server relation/column scripts from `docs/database/sqlserver-name-registry.csv` so SQL Server-specific views are covered.
- [x] Keep migrations idempotent where possible and guarded by object-existence checks for the development Postgres migration path. SQL Server object renames are guarded; Postgres object renames use `IF EXISTS` guards and passed clone/restored/dev runs.
- [x] Include rollback scripts that restore previous names for rehearsals.
- [x] Fix and test rollback script ordering so column rollback runs before relation rollback.
- [x] Apply the PostgreSQL canonical naming sequence to the local development `rvt` database after backup.
- [x] Apply the SQL Server canonical naming sequence to the local development `rvt` database after backup and a forward -> rollback -> forward rehearsal clone.

## Task 5: Raw SQL, Stored Routine, View, And Report Refactor

**Files:**
- Modify: `RVT.DataAccess/Configuration/RvtStoredRoutineExecutor.cs`
- Modify: repository files under `RVT.DataAccess/`
- Modify: archive/reporting files under `RVT.BusinessLogic/`
- Modify: compatibility SQL under `database/postgres/` and `database/sqlserver/`

- [x] Replace quoted mixed-case PostgreSQL references with canonical lowercase names.
- [x] Add a SQL Server stored routine/function definition exporter and routine porting inventory.
- [x] Rename stored routines and set-returning functions to lowercase snake_case after exported definitions are reviewed.
- [x] Rehearse PostgreSQL routine execution against the live Timescale container after the canonical schema is loaded.
- [x] Rename views such as search/dashboard views to singular snake_case names.
- [x] Add SQL Server view/module rewrite scripts because `sp_rename` does not update stored view definitions; the SQL Server clone rehearsal failed until this exists.
- [x] Add SQL Server canonical routine rewrite scripts and keep routine result-column aliases compatible for existing callers.
- [x] Validate the SQL Server-to-Postgres migrator from canonical SQL Server source into a disposable Timescale target, including final equivalents, post-load views, post-load routines, Timescale hypertables, and row-count parity.
- [x] Update report/archive SQL to canonical names.
- [x] Add regression tests that scan source for old physical names outside the approved compatibility folder.

## Task 6: Compatibility Layer And Deprecation

**Files:**
- Create: `database/postgres/legacy_compatibility_views.sql`
- Create: `database/sqlserver/legacy_compatibility_views.sql`
- Create: `docs/database/legacy-compatibility-deprecation.md`

- [x] Create old-name compatibility views in a dedicated `legacy` schema only.
- [x] Keep compatibility views read-only unless a specific consumer requires writable INSTEAD OF triggers.
- [x] Document every compatibility object, RVT engineering ownership, removal workflow, and the remaining external-consumer inventory step.
- [x] Block new application code from referencing the `legacy` schema.
- [x] Do not deploy legacy compatibility views to the development `rvt` database.

## Task 7: TimescaleDB Rehearsal And Performance Validation

**Files:**
- Create: `docs/database/timescale-refactor-rehearsal.md`
- Create: `database/postgres/verify_timescale_after_rename.sql`

- [x] Clone the local `rvt-timescaledb` database or restore a production-like backup.
- [x] Apply the canonical naming migration to the clone.
- [x] Capture local pre-rename Timescale baseline for hypertables, chunks, compression policies, retention policies, continuous aggregates, and jobs.
- [x] Verify hypertables, chunks, compression policies, retention policies, continuous aggregates, and indexes after applying the rename scripts on a clone.
- [x] Run representative dashboard, monitor, archive, and time_bucket queries before and after.
- [x] Start SQL Server clone rehearsal and record forward/rollback timings.
- [x] Complete SQL Server forward verification after SQL Server-specific view/module rewrite scripts are generated.
- [x] Record local clone lock-timeout behavior and query plan changes for representative Timescale/dashboard/search queries.
- [x] Repeat lock-duration and query-plan capture on the seeded production-like restored dataset before final cutover.
- [x] Migrate the local development SQL Server `rvt` database after copy-only backup and passing clone rehearsal.
- [ ] Repeat the validation against a full production backup or 400M-row-equivalent data set before any later production-scale cutover.

## Task 8: Cutover Runbook And Rollback

**Files:**
- Create: `docs/database/database-naming-cutover-runbook.md`

- [x] Define pre-cutover backup and restore validation.
- [x] Define SQL Server and Postgres execution order.
- [x] Define application deployment order and feature freeze window.
- [x] Define smoke tests for login, dashboard, site search, monitor search, Help CMS, and archive/export flows.
- [x] Define rollback decision points and exact rollback commands.

## Task 9: Cleanup And Enforcement

**Files:**
- Modify: `AGENTS.md`
- Modify: `project_state.md`
- Modify: `docs/database/database-naming-standard.md`

- [x] Add workspace guidance that future database changes must use canonical lowercase snake_case naming.
- [ ] Remove compatibility views after consumer sign-off.
- [ ] Remove old-name exceptions from tests.
- [x] Update onboarding docs with the final database naming standard.

## Acceptance Criteria

- All canonical database objects pass lowercase/snake_case/reserved-word/PK/FK naming tests.
- Tables, views, hypertables, and materialized views use singular names.
- SQL Server and PostgreSQL/Timescale migrations are rehearsed and documented.
- EF Core maps cleanly to canonical names for both providers.
- Raw SQL, stored routines, reporting, and archive flows no longer reference old physical names except in approved compatibility scripts.
- API and SPA behavior remains unchanged from the user perspective.
- Backend build/tests and frontend lint/build/tests pass.
- Cutover and rollback runbooks are reviewed before implementation begins.
