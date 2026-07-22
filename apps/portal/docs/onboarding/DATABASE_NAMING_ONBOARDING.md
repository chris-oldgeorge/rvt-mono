# RVT Database Naming Onboarding

This guide explains the database naming rules developers must follow while the RVT Portal moves from SQL Server naming conventions to PostgreSQL/TimescaleDB-ready canonical names.

## Core Rules

- Use lowercase for all physical database identifiers.
- Use singular relation names: `site`, `monitor`, `contract`, not plural names.
- Use snake_case for multi-word names: `site_operating_hour`, `help_article`, `sample_time`.
- Use `id` for single-column primary keys.
- Use `{referenced_table}_id` for normal foreign keys, such as `site_id`, `monitor_id`, and `help_article_id`.
- Avoid reserved words and data-type-like column names such as `user`, `lock`, `table`, `text`, `timestamp`, and `uuid`.
- Keep C# types PascalCase and API/TypeScript-facing names camelCase; EF Core maps those names to physical database names.

The full standard is in `docs/database/database-naming-standard.md`.

## What Not To Rename

ASP.NET Identity objects are excluded from the physical database refactor. Tables and columns such as `AspNetUsers`, `AspNetRoles`, and Identity-managed keys keep their framework names. Application-owned views may join Identity objects, but those joins must preserve the Identity physical names while using canonical RVT names everywhere else.

## Required Registry Updates

When a database name changes, update the relevant registry before updating scripts or code:

- `docs/database/database-name-registry.csv`
- `docs/database/sqlserver-name-registry.csv`
- `docs/database/database-name-equivalents-for-migrator-sqlserver.csv`
- `docs/database/database-constraint-index-name-registry.csv`

The migrator depends on `database-name-equivalents-for-migrator-sqlserver.csv` to map SQL Server source names into canonical PostgreSQL target names.

## Migration And Script Expectations

- New application-owned migrations should create canonical names directly.
- SQL Server scripts may use `[dbo]`, but object names inside them should still be lowercase snake_case for application-owned objects.
- PostgreSQL scripts should avoid quoted mixed-case identifiers for RVT-owned objects.
- Rollback scripts must be kept alongside forward scripts.
- Rehearsal evidence belongs in `docs/database/database-refactor-inventory.md` and the relevant rehearsal document.

## EF Migration Baseline

The EF Core baseline migration id `20231024072158_dataaccess` has been replaced in-place with a canonical schema baseline. This is intentional: current development databases already have that migration id stamped, and future `dotnet ef migrations add` commands must diff against canonical table and column names.

Use `RVTDbContextDesignTimeFactory` for design-time scaffolding. Do not reintroduce the retired legacy baseline names into `RVTDbContextModelSnapshot.cs`; the cutover readiness tests check for this.

## Legacy Compatibility

The `legacy` schema is temporary and exists only to protect old external/reporting consumers during cutover. It is not a development target.

- Compatibility SQL lives under `database/postgres/legacy_compatibility_views.sql` and `database/sqlserver/legacy_compatibility_views.sql`.
- Ownership and removal status lives in `docs/database/legacy-compatibility-deprecation.md`.
- New application, migrator, and monitor code must not query the `legacy` schema.

## Developer Checklist

- Check existing mappings before inventing a name.
- Use canonical names in new migrations, raw SQL, reports, archive SQL, and post-load scripts.
- Keep EF/Core C# models readable; do not rename C# types solely to match database identifiers.
- Run the focused DBR tests after changing database-facing code:

```powershell
dotnet test .\RvtPortal.Spa.Tests\RvtPortal.Spa.Tests.csproj --configuration Release --filter "FullyQualifiedName~DatabaseNamingConventionTests|FullyQualifiedName~CutoverReadinessTests|FullyQualifiedName~SiteArchiveServiceSecurityTests|FullyQualifiedName~DatabaseProviderConfigurationTests"
```
