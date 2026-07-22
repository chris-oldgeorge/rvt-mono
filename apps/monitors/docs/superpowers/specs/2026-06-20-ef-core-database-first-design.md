# EF Core Database-First Monitor Data Access Design

## Goal

Transform all four monitor apps from hand-written ADO.NET database access to EF Core database-first-style data access while preserving both SQL Server and PostgreSQL/Timescale runtime support.

## Current Context

The repository has four monitor apps:

- `airqmonitor/AirQMonitor`
- `myatmmonitor/MyAtmMonitor`
- `omnidotsmonitor/OmnidotsMonitor`
- `svantekmonitor/SvantekMonitor`

All four apps currently expose monitor-specific `IDBClient` and `DBClient` types backed by large static `DBUtil` classes. The `DBUtil` methods use shared ADO.NET helpers from `rvt-monitor-common/Rvt.Monitor.Common/Data/MonitorDb.cs` to open SQL Server or PostgreSQL connections, rewrite SQL Server identifiers for PostgreSQL, and execute provider-specific SQL.

There is no EF Core reference in the repository today. Existing tests use SQL Server-oriented `testdata/create.sql` files and `Testcontainers.MsSql`.

A running Timescale/PostgreSQL database is available through Docker. During design review on 2026-06-20, `docker ps` showed:

- Container `rvt-timescaledb`, image `timescale/timescaledb:latest-pg17`, host port `5432`.
- Container `rvt-pgadmin`, image `dpage/pgadmin4:latest`, host port `5050`.

The implementation should use the running Timescale schema as an authoritative cross-check source alongside SQL Server fixtures and the existing PostgreSQL identifier maps. Do not dump Docker container environment variables into logs or documentation because they may contain database credentials.

## Design Decision

Use a shared EF Core entity model and provider-aware mapping as the foundation.

Common tables and concepts belong in `Rvt.Monitor.Common`: monitors, deployments, contracts, sites, users, alert rules, notifications, notification settings, notification audit rows, site averages, and error logs. Monitor-specific tables belong in the individual monitor apps: AirQ noise/status rows, MyAtm dust/accessory rows, Omnidots vibration/status/sensor/trace rows, and Svantek noise/status/file rows.

Each monitor app will own an EF-backed `DbContext` that composes:

- Shared entities and mappings from `Rvt.Monitor.Common`.
- Monitor-specific entities and mappings from the app.
- Provider-specific table and column names selected from the existing database provider setting.

The public `IDBClient` interfaces should remain during migration. Application and rule-processing code can keep calling `IDBClient` while the implementation behind each `DBClient` changes from ADO.NET to EF-backed repository methods.

## Provider Support

Both SQL Server and PostgreSQL/Timescale remain supported.

Runtime provider selection continues to use the existing environment-driven provider selection behavior:

- `RVT__MonitorDatabaseProvider`
- `RVT__DatabaseProvider`
- Default provider remains SQL Server.

The EF Core options factory will choose:

- `UseSqlServer` for SQL Server.
- `UseNpgsql` for PostgreSQL/Timescale.

Table and column mapping must be provider-aware. SQL Server mappings use the existing legacy table and column names. PostgreSQL mappings use the canonical names already represented by the current `MonitorDbOptions` identifier maps.

## Raw SQL Policy

Do not use `FromSqlRaw` unless it cannot be helped.

Normal reads and writes should use LINQ, tracked entities, `Add`, `AddRange`, updates, deletes, and `SaveChanges`. Aggregates should use LINQ `Average`, `Max`, `Count`, and filtered queries.

If raw SQL is unavoidable, the code must:

- Use `FromSqlInterpolated`, `ExecuteSqlInterpolated`, or provider parameters for values.
- Use hard-coded SQL text or explicit whitelisted identifiers for SQL syntax.
- Include a short comment explaining why EF Core cannot express the operation cleanly.
- Have a targeted test covering the behavior.

Likely raw-SQL or provider-specific candidates are limited to:

- High-volume trace or measurement bulk insert, if EF `AddRange` is not fast enough.
- Provider-specific upsert optimization, if load-update-insert is too slow.
- Timescale-specific database behavior that has no useful LINQ equivalent.

## Data Flow

Each monitor app will construct its `DbContext` through a common provider-aware factory. Repositories receive the context and implement the existing `IDBClient` behavior.

Upsert-like methods should be implemented as:

1. Query by natural key.
2. Update the existing entity if present.
3. Insert a new entity if absent.
4. Save changes at the same behavioral boundary as the old method.

Average and max lookups must not accept arbitrary SQL column names. Existing string field names from alert rules should be translated to typed selectors or enum-like field descriptors before querying. Unsupported fields should throw a controlled exception before touching the database.

## Entity Placement

Shared common entities should live under `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities`.

Shared mapping helpers should live under `rvt-monitor-common/Rvt.Monitor.Common/Data/EntityFramework`.

Monitor-specific entities and context code should live under each app's existing `api/db` area or a nearby `api/db/EntityFramework` folder.

This placement keeps common database semantics centralized while avoiding a bloated shared library full of monitor-specific measurement models.

## Migration Strategy

Migrate in slices.

1. Add EF Core provider packages and shared infrastructure.
2. Add shared entities and provider-aware mapping tests.
3. Add one monitor-specific EF context and repository.
4. Replace that monitor's `DBClient` implementation while keeping `IDBClient` stable.
5. Run that monitor's tests.
6. Repeat for the other three monitor apps.
7. Remove obsolete `DBUtil` methods and ADO.NET helper code once all callers are migrated.

The recommended first monitor is MyAtm because it has dust data, accessory data, average queries, alert rules, and notifications, but fewer monitor-specific table types than Omnidots.

## Testing Strategy

Use TDD for each migrated behavior:

- Write or reuse a focused test that exercises the current `IDBClient` behavior.
- Run it against the current implementation or with the EF implementation incomplete to confirm failure when appropriate.
- Implement the EF repository behavior.
- Run the monitor test project.

Add mapping-specific tests that inspect EF metadata for both providers. These tests should prove that key tables and columns map to the expected SQL Server and PostgreSQL names.

Keep existing SQL Server integration tests using `Testcontainers.MsSql`. Add PostgreSQL/Timescale integration coverage against the accessible Docker Timescale database or a testcontainer equivalent. The implementation plan should include a safe discovery step for the connection string, preferring existing app configuration or user-provided credentials over inspecting container environment variables.

## Subagent Use

The implementation plan should use subagents where useful. Good subagent slices are:

- Shared EF infrastructure and mapping tests.
- One subagent per monitor app migration.
- A final verification subagent that audits remaining raw SQL and dynamic SQL construction.

Each subagent should operate from a written task, return a diff summary, and leave verification commands/results for review before the next task starts.

## Non-Goals

This design does not introduce EF Core migrations as the source of truth. The target is database-first-style mapping to the existing database shape.

This design does not remove SQL Server or PostgreSQL support.

This design does not change public monitor API behavior, scheduler behavior, notification semantics, or database schema names.

This design does not require optimizing every insert path immediately. Correct EF behavior comes first; provider-specific performance optimization can follow only where measured or test-proven.

## Open Risks

The PostgreSQL/Timescale schema is represented in code through identifier maps and is also expected to be available from the running `rvt-timescaledb` Docker container on host port `5432`. The main risk is safe credential discovery: implementation should use existing configuration or user-provided credentials, not environment dumps that may expose secrets.

Some current SQL relies on provider-specific upsert and bulk insert behavior. The initial EF implementation can use load-update-insert and `AddRange`, but high-volume paths may require a later optimized provider-specific implementation.

Existing alert rule fields are stored as strings. EF removes value injection risks, but field-to-column selection still needs explicit typed mapping.
