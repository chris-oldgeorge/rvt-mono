# EF migrations

## What was broken

`dotnet ef database update` and `dotnet ef migrations add` did not work against this repository, and never had.

The migrations under `RVT.DataAccess/Migrations` were hand-written, and all but the 2023 baseline were missing
their `.Designer.cs` files. That file is not decoration: it carries `BuildTargetModel`, the snapshot of the model
*as of that migration*. Without it EF cannot compute the migration chain, so:

- `dotnet ef migrations add` failed with a `NullReferenceException`;
- `dotnet ef database update` failed with `PendingModelChangesWarning`, because the checked-in
  `RVTDbContextModelSnapshot` no longer matched the model.

This is also why no database had an `__EFMigrationsHistory` table: the schema was built by
`RVT.DatabaseMigrator` (since retired) plus the SQL under `database/`, and the EF migrations were effectively
documentation that nobody could run.

## What changed

The migration chain was squashed into a single **EF-generated** baseline, `CanonicalBaseline`, which carries a
proper `.Designer.cs` and a model snapshot that matches the current model. `dotnet ef migrations add` now
produces an empty migration (proving model and snapshot agree), and `dotnet ef database update` reports
"already up to date" instead of throwing.

The DDL of the superseded migrations is not lost - it is in git history, and the parts that still matter are
deployable SQL under `database/{provider}/`.

## Three contexts, three chains, one database

`RVTDbContext`, `RVTSearchContext` and `ApplicationDbContext` map disjoint halves of a *single* database, and
each has its own migration chain and its own history table:

| Context | Migrations | History table | Covers |
| --- | --- | --- | --- |
| `RVTDbContext` | `RVT.DataAccess/Migrations` | `__EFMigrationsHistory` | domain tables |
| `RVTSearchContext` | `RVT.DataAccess/Migrations/Search` | `__EFMigrationsHistorySearch` | time-series tables |
| `ApplicationDbContext` | `RvtPortal.Spa/Data/Migrations` | `__EFMigrationsHistoryIdentity` | ASP.NET Identity tables |

The separate history tables are not cosmetic. If they shared the default `__EFMigrationsHistory`, each context
would read the other contexts' migrations as its own: `database update --context RVTSearchContext` would find
`CanonicalBaseline` already recorded, conclude nothing was pending, and silently never create the time-series
tables. The names are pinned in `RvtDatabaseServiceCollectionExtensions`.

## Building a database from scratch

The EF migrations do not build the whole database, and cannot: 23 ingestion/telemetry tables and 13 columns on
otherwise-mapped tables are not in any EF model (see "Columns no EF model maps" below), and the views,
hypertables and continuous aggregates are Timescale objects that belong in SQL. Those live in
`database/postgres/create_unmapped_schema.sql` and `database/postgres/post-load/`.

`RVT.SchemaDeploy` applies them. Run it after the EF migrations:

```
export RVT_EF_CONNECTION='Host=...;Database=...;Username=...;Password=...'

dotnet ef database update --context RVTDbContext         --project RVT.DataAccess/RVT.DataAccess.csproj --startup-project RVT.DataAccess/RVT.DataAccess.csproj
dotnet ef database update --context RVTSearchContext     --project RVT.DataAccess/RVT.DataAccess.csproj --startup-project RVT.DataAccess/RVT.DataAccess.csproj
dotnet ef database update --context ApplicationDbContext --project RvtPortal.Spa/RvtPortal.Spa.csproj   --startup-project RvtPortal.Spa/RvtPortal.Spa.csproj

# once per database, as a user that may create extensions
psql -c 'CREATE EXTENSION IF NOT EXISTS timescaledb;'

dotnet run --project RVT.SchemaDeploy/RVT.SchemaDeploy.csproj -- --connection "$RVT_EF_CONNECTION"
```

`RVT.SchemaDeploy` is deliberately the harmless half of what `RVT.DatabaseMigrator` used to do: it creates and
replaces, and never drops a table or any data. It is safe to re-run, `--dry-run` lists what it would apply in
order, and it refuses to start if the `timescaledb` extension is missing rather than half-applying the schema.
The SQL ships next to its executable, so a published build works on a host with no repository checked out.

This was verified on 2026-07-14 against a `rvt` database built by `RVT.DatabaseMigrator`: the from-scratch build
produced the same 56 tables and 38 views, with no missing relation and no missing column.

## Retired bulk migrator

The one-way cutover utility was retired on 2026-07-14 after row-count, canonical-name, view, routine, and hypertable verification completed. It is not a deployment tool and must not be restored as an operational dependency. EF migrations plus `RVT.SchemaDeploy` are the only supported schema path; Git history retains the removed utility for audit purposes.

## Columns no EF model maps

Thirteen columns exist physically but are in no EF model, so EF neither reads them nor supplies them on insert:
`monitor.offline`, `monitor.battery_status`, `notification_setting.site_id`, `notification_setting.user_id`,
`report_rule.is_hidden_system_rule`, `rvt_alert_rule.created`, `rvt_alert_rule.accessed`, and six on
`svantek_monitor_status`. `create_unmapped_schema.sql` adds them.

Two of them are **NOT NULL**, so EF — which never names them in an INSERT — can only insert if the database
fills them in. An earlier PostgreSQL schema port omitted both defaults, which created a live defect:

- `rvt_alert_rule.created` — `AlertLevelCommands` does `domainContext.RvtAlertRules.Add(level)` (and monitor
  creation seeds default alert levels the same way), and EF's INSERT omits `created`, so on PostgreSQL the
  insert failed with `23502: null value in column "created" violates not-null constraint`. Confirmed against the
  dev database on 2026-07-14.
- `monitor.battery_status` — same shape. No EF code path inserts a `monitor` today (monitors arrive through
  ingestion), so this one never fired, but it was the same landmine.

**The fix enforces the PostgreSQL contract**: `create_unmapped_schema.sql` now gives both columns a database
default — `DEFAULT (now() AT TIME ZONE 'utc')` for `created`, `DEFAULT 0` for `battery_status`. They stay
unmapped by EF; the database supplies the value. Databases that already have the columns (where the idempotent
`ADD COLUMN IF NOT EXISTS` is a no-op and cannot add a default) are repaired by
`database/postgres/restore_unmapped_column_defaults.sql`.

`RvtPortal.Spa.Tests/UnmappedColumnDefaultTests` guards this: a static test reads `create_unmapped_schema.sql`
and fails the build if any unmapped `ADD COLUMN` is NOT NULL without a default, and an opt-in test (set
`RVT__POSTGRES_INTEGRATION_CONNECTION`) inserts a monitor and an alert rule against a real PostgreSQL schema to prove
EF's INSERT survives it. The rest of the suite runs on InMemory/SQLite, which build their schema from the model
and so cannot see an unmapped column at all.

## Adopting an existing database

A database created by the old `RVT.DatabaseMigrator` already has the schema but no history tables. EF will therefore
consider each baseline pending and try to `CREATE TABLE` over live tables. **Do not run `database update`
against such a database until it has been baselined.**

To adopt one:

1. Bring the schema up to date with the current release. For a database that predates the column rename:
   ```
   database/postgres/rename_mangled_columns.sql
   database/postgres/post-load/03_views_and_routines.sql
   database/postgres/post-load/04_routines.sql
   ```
   The rename is **breaking**: the previous release reads `fleet_row_count`, so schema and application must be
   deployed together.

2. Record all three baselines as already applied, so EF does not try to re-create the schema:
   ```sql
   CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
       "MigrationId"    character varying(150) NOT NULL,
       "ProductVersion" character varying(32)  NOT NULL,
       CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
   );
   CREATE TABLE IF NOT EXISTS "__EFMigrationsHistorySearch" (
       "MigrationId"    character varying(150) NOT NULL,
       "ProductVersion" character varying(32)  NOT NULL,
       CONSTRAINT "PK___EFMigrationsHistorySearch" PRIMARY KEY ("MigrationId")
   );
   CREATE TABLE IF NOT EXISTS "__EFMigrationsHistoryIdentity" (
       "MigrationId"    character varying(150) NOT NULL,
       "ProductVersion" character varying(32)  NOT NULL,
       CONSTRAINT "PK___EFMigrationsHistoryIdentity" PRIMARY KEY ("MigrationId")
   );

   INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
   VALUES ('20260714132042_CanonicalBaseline', '10.0.7') ON CONFLICT ("MigrationId") DO NOTHING;
   INSERT INTO "__EFMigrationsHistorySearch" ("MigrationId", "ProductVersion")
   VALUES ('20260714134534_SearchBaseline', '10.0.7') ON CONFLICT ("MigrationId") DO NOTHING;
   INSERT INTO "__EFMigrationsHistoryIdentity" ("MigrationId", "ProductVersion")
   VALUES ('20260714140112_IdentityBaseline', '10.0.7') ON CONFLICT ("MigrationId") DO NOTHING;
   ```

3. Confirm: for each context, `dotnet ef migrations list` should show the baseline with no `(Pending)` marker,
   and `dotnet ef database update` should report that the database is already up to date.

Startup schema validation (`Database:ValidateSchemaOnStartup`, on by default) checks the same thing from the
application side: it compares every relation and column the models map against the live schema and fails the
boot if anything is missing. Note that it only checks *model → database*: a column the database has and the
model does not, like the thirteen above, is invisible to it.

## Running EF tooling

The design-time factory reads the connection from the environment; there is no connection string in the
repository.

```
export RVT_EF_CONNECTION='Host=...;Database=...;Username=...;Password=...'

dotnet ef migrations add <Name> \
  --project RVT.DataAccess/RVT.DataAccess.csproj \
  --startup-project RVT.DataAccess/RVT.DataAccess.csproj \
  --context RVTDbContext
```

`RVT.DataAccess` is its own startup project for its two contexts; `ApplicationDbContext` lives in
`RvtPortal.Spa`, which now carries a design-time-only reference to `Microsoft.EntityFrameworkCore.Design` so the
tooling can reach it. `--context` is always required, because the solution has three `DbContext` types.

`RVTSearchContext` maps both tables and views. EF creates the tables; the views are `ToView`-mapped and are built
by the post-load scripts, which is why `SearchBaseline` contains no view DDL.

## The rule that keeps this working

**Generate migrations with the EF tooling. Do not hand-write them.** A hand-written migration without its
`.Designer.cs` silently breaks the chain for everyone who comes after, and nothing fails until someone tries to
use it.
