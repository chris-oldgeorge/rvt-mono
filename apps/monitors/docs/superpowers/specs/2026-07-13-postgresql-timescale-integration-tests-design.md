# PostgreSQL/Timescale Integration Tests Design

## Purpose

Replace the monitor projects' SQL Server Testcontainers integration suites with
PostgreSQL integration suites that exercise the existing local Timescale
database through an explicitly supplied connection string. SQL Server remains
a supported runtime provider, but it no longer has container-backed persistence
coverage.

## Scope

This applies to AirQ, MyAtm, Omnidots, and Svantek test projects.

The replacement preserves database-persistence coverage for monitor catalog,
measurement, rule, notification, error, and audit behavior. Provider-mapping
unit tests continue to cover both SQL Server and PostgreSQL mappings. No SQL
Server container, SQL Server fixture SQL, `SqlConnection`, or
`Testcontainers.MsSql` package remains in monitor test projects.

## Connection Contract

Integration tests require `RVT__POSTGRES_INTEGRATION_CONNECTION`. The value is
never committed and is intentionally separate from normal application
configuration. Fixtures do not fall back to `ConnectionStrings__DefaultConnection`
or inspect Docker container environment variables for a connection string.

The account must be able to create and drop schemas in the selected local
PostgreSQL/Timescale database. It need not have access to application secrets.

## Isolation Model

Each fixture creates a schema named `rvt_integration_<random-guid>`. The
fixture uses a derived connection string with that schema as its sole
PostgreSQL `Search Path`; it does not include `public` as a fallback.

The monitor-specific PostgreSQL setup script creates all objects unqualified,
so they are owned by that generated schema. Between tests, the fixture may
truncate only tables in its generated schema. At class cleanup, it executes
`DROP SCHEMA <generated-schema> CASCADE` using a safely quoted identifier.

Consequently, tests cannot read, truncate, seed, or write application tables
in `public`. Failure to create, configure, or remove the generated schema must
fail the integration fixture with a clear diagnostic rather than silently
continuing against another schema.

## Fixture Architecture

A shared test-only PostgreSQL fixture helper will own:

- reading and validating `RVT__POSTGRES_INTEGRATION_CONNECTION`;
- generating and safely quoting the schema name;
- schema creation, setup-script execution, targeted reset, and teardown;
- producing the schema-scoped Npgsql connection string; and
- an explicit PostgreSQL provider configuration for the monitor under test.

Each monitor's `TestDBClient` suite will use that helper and `NpgsqlConnection`
for direct persistence assertions. The existing SQL Server setup scripts become
monitor-specific `create.postgres.sql` scripts with names and types matching
the current PostgreSQL EF mappings. The fixture DDL is ordinary PostgreSQL
table/index DDL; it must not create Timescale extensions, hypertables, or
objects outside the generated schema.

## Test Execution

PostgreSQL integration classes use the MSTest category
`PostgreSqlIntegration`. A dedicated repository script runs the four monitor
projects sequentially with a category filter, after verifying the required
connection variable is set:

1. AirQ
2. MyAtm
3. Omnidots
4. Svantek

The documented standard unit test command excludes this category. This keeps
ordinary local and CI unit runs independent of a reachable Timescale database,
while the explicit integration command verifies the production-default
provider against local development infrastructure.

## Error Handling

- Missing connection string: the integration script stops before tests begin
  with the required variable name.
- Schema create/setup failure: the relevant test class fails without running
  persistence tests; the error identifies the generated schema.
- Per-test reset failure: the test fails rather than risking test-state bleed.
- Cleanup failure: the fixture reports the schema name so it can be removed
  manually. It does not attempt broad cleanup.

## Verification

Verification will include:

- a clean build without SQL Server Testcontainers or direct SqlClient test
  dependencies;
- monitor unit suites with `PostgreSqlIntegration` excluded;
- the sequential PostgreSQL integration script using the supplied local
  Timescale connection string;
- a post-run metadata check that the generated schemas were removed; and
- source scans confirming no monitor test project references
  `Testcontainers.MsSql`, `Microsoft.Data.SqlClient`, or `SqlConnection`.

## Non-Goals

- Removing SQL Server runtime/provider support from monitor applications.
- Changing production database configuration.
- Running tests against the `public` schema or altering existing local monitor
  data.
- Introducing a PostgreSQL Docker container or Testcontainers dependency.
