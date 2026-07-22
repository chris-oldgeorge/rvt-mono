# PostgreSQL/Timescale Integration Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the four SQL Server Testcontainers persistence suites with schema-isolated PostgreSQL/Timescale integration suites that use the explicitly supplied local connection string.

**Architecture:** Add a small test-only support library which provisions a random PostgreSQL schema, runs monitor-specific setup/reset SQL through a connection whose `Search Path` contains only that schema, and deletes that schema at fixture cleanup. Port each `TestDBClient` suite from SQL Server names and `SqlConnection` to canonical PostgreSQL names and `NpgsqlConnection`; use a script to run the four integration projects sequentially.

**Tech Stack:** .NET 10, MSTest 4, Npgsql 10.0.3, EF Core/Npgsql, PostgreSQL/Timescale, Bash.

## Global Constraints

- Require `RVT__POSTGRES_INTEGRATION_CONNECTION`; never fall back to `ConnectionStrings__DefaultConnection`.
- Create a unique `rvt_integration_<guid>` schema per test class and set it as the sole `Search Path`.
- Setup/reset scripts must use unqualified identifiers; do not include `public` as a fallback schema.
- Cleanup may execute only `DROP SCHEMA <generated-name> CASCADE`; never truncate or modify `public` tables.
- Keep SQL Server runtime support and provider-mapping unit tests; remove SQL Server Testcontainers and direct SqlClient integration dependencies.
- All new integration test classes use `[TestCategory("PostgreSqlIntegration")]`.
- Run AirQ, MyAtm, Omnidots, and Svantek integration projects sequentially.

---

## File Structure

- Create `rvt-monitor-common/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj`: test-only Npgsql helper library.
- Create `rvt-monitor-common/Rvt.Monitor.IntegrationTesting/PostgreSqlIntegrationDatabase.cs`: validates the environment variable and owns generated-schema lifecycle.
- Create `rvt-monitor-common/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj`: MSTest coverage for schema lifecycle and connection scoping.
- Create `rvt-monitor-common/Rvt.Monitor.IntegrationTesting.Tests/PostgreSqlIntegrationDatabaseTests.cs`: fixture safety regression tests, selected with `PostgreSqlIntegration`.
- Modify `rvt-monitors.sln`: include the helper and helper-test projects under `rvt-monitor-common`.
- Modify each monitor `*Tests.csproj`: replace `Testcontainers.MsSql` with `Npgsql` and reference the test-only helper.
- Modify each monitor `TestDbClient.cs`: replace the container fixture and SQL Server direct assertions with `PostgreSqlIntegrationDatabase` and `Npgsql` equivalents.
- Replace each `testdata/create.sql` with `testdata/create.postgres.sql` and add `testdata/reset.postgres.sql`: canonical PostgreSQL fixture DDL/reset data.
- Create `scripts/run-postgres-integration-tests.sh`: validates the environment and runs projects in a fixed sequence.
- Modify `README.md`, `docs/container-builds.md`, and `project_state.md`: document connection, permissions, category filters, and generated-schema isolation.

### Task 1: Add the schema-isolated test support library

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj`
- Create: `rvt-monitor-common/Rvt.Monitor.IntegrationTesting/PostgreSqlIntegrationDatabase.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj`
- Create: `rvt-monitor-common/Rvt.Monitor.IntegrationTesting.Tests/PostgreSqlIntegrationDatabaseTests.cs`
- Modify: `rvt-monitors.sln`

**Interfaces:**
- Produces `PostgreSqlIntegrationDatabase.CreateAsync(string setupSql, string resetSql, CancellationToken)`.
- Produces `ConnectionString`, `SchemaName`, `ResetAsync(CancellationToken)`, `OpenConnection()`, and `DisposeAsync()`.
- Consumes the `RVT__POSTGRES_INTEGRATION_CONNECTION` environment variable only.

- [ ] **Step 1: Write failing lifecycle tests**

```csharp
[TestClass]
[TestCategory("PostgreSqlIntegration")]
public sealed class PostgreSqlIntegrationDatabaseTests
{
    [TestMethod]
    public async Task CreateAsync_UsesGeneratedSchemaAsTheOnlySearchPath()
    {
        await using var database = await PostgreSqlIntegrationDatabase.CreateAsync(
            "CREATE TABLE probe (id integer PRIMARY KEY);", "TRUNCATE TABLE probe;");

        await using var connection = database.OpenConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SHOW search_path;", connection);

        Assert.AreEqual(database.SchemaName, (string?)await command.ExecuteScalarAsync());
    }

    [TestMethod]
    public async Task DisposeAsync_DropsOnlyTheGeneratedSchema()
    {
        string schemaName;
        await using (var database = await PostgreSqlIntegrationDatabase.CreateAsync(
            "CREATE TABLE probe (id integer PRIMARY KEY);", "TRUNCATE TABLE probe;"))
        {
            schemaName = database.SchemaName;
        }

        await using var connection = new NpgsqlConnection(
            Environment.GetEnvironmentVariable("RVT__POSTGRES_INTEGRATION_CONNECTION"));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = @schema);", connection);
        command.Parameters.AddWithValue("schema", schemaName);

        Assert.IsFalse((bool)(await command.ExecuteScalarAsync())!);
    }

    [TestMethod]
    public async Task NoGeneratedSchemasRemainAfterFixtureCleanup()
    {
        await using var connection = new NpgsqlConnection(
            Environment.GetEnvironmentVariable("RVT__POSTGRES_INTEGRATION_CONNECTION"));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM pg_namespace WHERE nspname LIKE 'rvt_integration_%';", connection);

        Assert.AreEqual(0L, (long)(await command.ExecuteScalarAsync())!);
    }
}
```

- [ ] **Step 2: Run the lifecycle tests and verify the helper is absent**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj --filter "TestCategory=PostgreSqlIntegration"
```

Expected: compile failure because `PostgreSqlIntegrationDatabase` and its project do not exist.

- [ ] **Step 3: Create the helper project and minimal schema lifecycle implementation**

```xml
<!-- rvt-monitor-common/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql" Version="10.0.3" />
  </ItemGroup>
</Project>
```

```xml
<!-- rvt-monitor-common/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.1" />
    <PackageReference Include="MSTest.TestAdapter" Version="4.0.2" />
    <PackageReference Include="MSTest.TestFramework" Version="4.0.2" />
    <PackageReference Include="Npgsql" Version="10.0.3" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\\Rvt.Monitor.IntegrationTesting\\Rvt.Monitor.IntegrationTesting.csproj" />
  </ItemGroup>
</Project>
```

```csharp
// PostgreSqlIntegrationDatabase.cs
using Npgsql;

namespace Rvt.Monitor.IntegrationTesting;

public sealed class PostgreSqlIntegrationDatabase : IAsyncDisposable
{
    public const string ConnectionStringEnvironmentVariable = "RVT__POSTGRES_INTEGRATION_CONNECTION";
    private readonly string adminConnectionString;
    private bool disposed;

    private PostgreSqlIntegrationDatabase(string adminConnectionString, string connectionString, string schemaName)
    {
        this.adminConnectionString = adminConnectionString;
        ConnectionString = connectionString;
        SchemaName = schemaName;
    }

    public string ConnectionString { get; }
    public string SchemaName { get; }

    public static async Task<PostgreSqlIntegrationDatabase> CreateAsync(
        string setupSql, string resetSql, CancellationToken cancellationToken = default)
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(adminConnectionString))
        {
            throw new InvalidOperationException(
                $"Set {ConnectionStringEnvironmentVariable} to run PostgreSQL integration tests.");
        }

        var schemaName = $"rvt_integration_{Guid.NewGuid():N}";
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString) { SearchPath = schemaName };
        var database = new PostgreSqlIntegrationDatabase(adminConnectionString, builder.ConnectionString, schemaName);

        try
        {
            await database.ExecuteAdminAsync($"CREATE SCHEMA {QuoteIdentifier(schemaName)};", cancellationToken);
            await database.ExecuteScopedAsync(setupSql, cancellationToken);
            await database.ExecuteScopedAsync(resetSql, cancellationToken);
            return database;
        }
        catch
        {
            await database.DropSchemaAsync(CancellationToken.None);
            throw;
        }
    }

    public NpgsqlConnection OpenConnection() => new(ConnectionString);

    public Task ResetAsync(string resetSql, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(resetSql, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            disposed = true;
            await DropSchemaAsync(CancellationToken.None);
        }
    }

    private async Task ExecuteScopedAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task ExecuteAdminAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private Task DropSchemaAsync(CancellationToken cancellationToken) =>
        ExecuteAdminAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(SchemaName)} CASCADE;", cancellationToken);

    private static string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";
}
```

Add an MSTest project referencing the helper, then add both projects to `rvt-monitors.sln` beneath the existing `rvt-monitor-common` solution folder.

- [ ] **Step 4: Run lifecycle tests and inspect the generated schema cleanup**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj --filter "TestCategory=PostgreSqlIntegration"
```

Expected: two tests pass and no `rvt_integration_*` schema remains after the run.

- [ ] **Step 5: Commit the support library**

```bash
git add rvt-monitor-common/Rvt.Monitor.IntegrationTesting rvt-monitor-common/Rvt.Monitor.IntegrationTesting.Tests rvt-monitors.sln
git commit -m "test: add isolated PostgreSQL integration fixture"
```

### Task 2: Convert common test-project dependencies and fixtures

**Files:**
- Modify: `airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj`
- Modify: `myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj`
- Modify: `svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj`
- Modify: `airqmonitor/AirQMonitorTests/TestDbClient.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/TestDbClient.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestDbClient.cs`
- Modify: `svantekmonitor/SvantekMonitorTests/TestDbClient.cs`

**Interfaces:**
- Consumes `PostgreSqlIntegrationDatabase.ConnectionString`, `CreateAsync`, `ResetAsync`, and `OpenConnection`.
- Each test class creates the helper once and labels itself `PostgreSqlIntegration`.

- [ ] **Step 1: Add a failing direct-PostgreSQL fixture assertion to each monitor class**

At the class declarations, add the category and change setup intent before replacing the implementation:

```csharp
[TestClass]
[TestCategory("PostgreSqlIntegration")]
public class TestDBClient
{
    private static PostgreSqlIntegrationDatabase? database;
    private static DBClient? testObj;
}
```

Add one test in each class that uses `database!.OpenConnection()` and asserts `current_schema()` equals `database.SchemaName`. This must fail until the fixture establishes the scoped connection.

- [ ] **Step 2: Run each new assertion and verify its expected failure**

Run:

```bash
dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --filter "TestCategory=PostgreSqlIntegration"
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "TestCategory=PostgreSqlIntegration"
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "TestCategory=PostgreSqlIntegration"
dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "TestCategory=PostgreSqlIntegration"
```

Expected: the tests fail before any monitor test points at PostgreSQL.

- [ ] **Step 3: Replace the fixture skeleton consistently**

Use this pattern in every `TestDBClient.cs`:

```csharp
using Npgsql;
using Rvt.Monitor.IntegrationTesting;

[ClassInitialize]
public static async Task TestFixtureSetup(TestContext context)
{
    Environment.SetEnvironmentVariable("RVT__DATABASE_PROVIDER", "PostgreSql");
    var setupSql = TestUtil.ReadTextFromFile("testdata/create.postgres.sql");
    var resetSql = TestUtil.ReadTextFromFile("testdata/reset.postgres.sql");
    database = await PostgreSqlIntegrationDatabase.CreateAsync(setupSql, resetSql);
    testObj = new DBClient(database.ConnectionString);
}

[ClassCleanup]
public static async Task TestFixtureCleanup()
{
    if (database is not null)
    {
        await database.DisposeAsync();
    }
}

[TestInitialize]
public async Task BeforeTest()
{
    await database!.ResetAsync(TestUtil.ReadTextFromFile("testdata/reset.postgres.sql"));
}
```

Replace `Microsoft.Data.SqlClient` and `Testcontainers.MsSql` imports with `Npgsql` and `Rvt.Monitor.IntegrationTesting`; replace every `SqlConnection` parameter/local with `NpgsqlConnection`. Replace the package reference with `Npgsql` version `10.0.3`, add a project reference to `Rvt.Monitor.IntegrationTesting`, copy both PostgreSQL SQL files to output, and remove the `create.sql` content item.

- [ ] **Step 4: Verify the four project files no longer contain SQL Server integration dependencies**

Run:

```bash
rg -n "Testcontainers\.MsSql|Microsoft\.Data\.SqlClient|SqlConnection|MsSqlBuilder|MsSqlContainer" airqmonitor/AirQMonitorTests myatmmonitor/MyAtmMonitorTests omnidotsmonitor/OmnidotsMonitorTests svantekmonitor/SvantekMonitorTests
```

Expected: no matches after the four fixture conversions are complete.

- [ ] **Step 5: Commit shared fixture conversion mechanics**

```bash
git add airqmonitor/AirQMonitorTests/*.csproj myatmmonitor/MyAtmMonitorTests/*.csproj omnidotsmonitor/OmnidotsMonitorTests/*.csproj svantekmonitor/SvantekMonitorTests/*.csproj
git commit -m "test: use PostgreSQL integration fixture"
```

### Task 3: Port AirQ persistence coverage to canonical PostgreSQL

**Files:**
- Create: `airqmonitor/AirQMonitorTests/testdata/create.postgres.sql`
- Create: `airqmonitor/AirQMonitorTests/testdata/reset.postgres.sql`
- Delete: `airqmonitor/AirQMonitorTests/testdata/create.sql`
- Modify: `airqmonitor/AirQMonitorTests/TestDbClient.cs`

**Interfaces:**
- AirQ canonical tables: `monitor`, `air_q_monitor_status`, `air_q_noise_level`, `air_q_noise_8_hour_average`, `air_q_error_message`, `rvt_alert_rule`, `deployment`, `contract`, `site_user`, `notification_setting`, `notification_sent`, `notification`, `site`, `site_average`, and `"AspNetUsers"`.
- `reset.postgres.sql` must establish the global offline rule needed by `TestReadGlobalRules`.

- [ ] **Step 1: Add failing canonical-table assertion**

Replace the existing AirQ direct noise-row assertion with an Npgsql read against the canonical table:

```csharp
await using var connection = database!.OpenConnection();
await connection.OpenAsync();
await using var command = new NpgsqlCommand(
    "SELECT serial_id, sample_time, laeq FROM air_q_noise_level ORDER BY sample_time;", connection);
await using var reader = await command.ExecuteReaderAsync();
Assert.IsTrue(await reader.ReadAsync());
```

- [ ] **Step 2: Run the AirQ category and verify failure before DDL exists**

Run:

```bash
dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --filter "TestCategory=PostgreSqlIntegration"
```

Expected: setup fails because `create.postgres.sql` and its canonical tables do not yet exist.

- [ ] **Step 3: Write schema and reset scripts, then port direct SQL**

Create unqualified PostgreSQL DDL using `uuid`, `text`, `boolean`, `double precision`, `timestamp with time zone`, and `time` types. Define each table with the canonical names above and PostgreSQL column names from `AirQMonitorDbOptions` plus `AirQMonitorContext`. Use `CREATE INDEX` for `(serial_id, sample_time)` measurement reads. Do not create SQL Server triggers or `AirQErrorMessages`; assert common errors from `error_log` because `MonitorDb.WriteException` writes PostgreSQL errors there.

`reset.postgres.sql` must begin with:

```sql
TRUNCATE TABLE notification_sent, notification, notification_setting, site_user,
  "AspNetUsers", site_average, air_q_noise_8_hour_average, air_q_noise_level,
  air_q_monitor_status, monitor, deployment, contract, site, rvt_alert_rule,
  error_log RESTART IDENTITY CASCADE;

INSERT INTO rvt_alert_rule
  (id, monitor_id, serial_id, alert_field, limit_on, limit_off, alert_type,
   is_active, averaging_period, weekdays, saturdays, sundays, is_deleted, created)
VALUES
  ('00000000-0000-0000-0000-000000000001'::uuid, NULL, NULL, 'offline-rule', 0, 0, 2,
   true, 86400, true, true, true, false, CURRENT_TIMESTAMP);
```

Do not use UUID defaults which depend on `pgcrypto`, `uuid-ossp`, or any other extension. The test data must supply UUID values explicitly. Port every helper query from `dbo.<PascalCase>` to the table/column names listed above and replace SQL Server `@name` parameters with Npgsql parameters of the same names.

- [ ] **Step 4: Run AirQ integration and unit coverage**

Run:

```bash
dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --filter "TestCategory=PostgreSqlIntegration"
dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --filter "TestCategory!=PostgreSqlIntegration"
```

Expected: both commands exit 0; the integration fixture leaves no generated schema.

- [ ] **Step 5: Commit AirQ conversion**

```bash
git add airqmonitor/AirQMonitorTests
git commit -m "test: port AirQ persistence tests to PostgreSQL"
```

### Task 4: Port MyAtm persistence coverage to canonical PostgreSQL

**Files:**
- Create: `myatmmonitor/MyAtmMonitorTests/testdata/create.postgres.sql`
- Create: `myatmmonitor/MyAtmMonitorTests/testdata/reset.postgres.sql`
- Delete: `myatmmonitor/MyAtmMonitorTests/testdata/create.sql`
- Modify: `myatmmonitor/MyAtmMonitorTests/TestDbClient.cs`

**Interfaces:**
- MyAtm canonical tables: `monitor`, `my_atm_dust_level`, `my_atm_dust_level_8_hour_avg`, `my_atm_accessory_info`, `my_atm_error_message`, `rvt_alert_rule`, `deployment`, `contract`, `site_user`, `notification_setting`, `notification_sent`, `notification`, `site`, and `"AspNetUsers"`.

- [ ] **Step 1: Add a failing dust persistence assertion**

```csharp
await using var connection = database!.OpenConnection();
await connection.OpenAsync();
await using var command = new NpgsqlCommand(
    "SELECT serial_id, sample_time, pm_2_5 FROM my_atm_dust_level ORDER BY sample_time;", connection);
await using var reader = await command.ExecuteReaderAsync();
Assert.IsTrue(await reader.ReadAsync());
```

- [ ] **Step 2: Verify the assertion fails before PostgreSQL fixture DDL is present**

Run:

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "TestCategory=PostgreSqlIntegration"
```

Expected: fixture setup fails before test execution because the PostgreSQL fixture files are absent.

- [ ] **Step 3: Create MyAtm DDL/reset and convert helper SQL**

Create all MyAtm canonical tables unqualified. Use `uuid` for identifiers, `timestamp with time zone` for dates, `double precision` for measurements, and `boolean` for flags. Reset all tables in the generated schema and seed exactly one global `offline-rule` row in `rvt_alert_rule`. Convert `ReadDustDtos`, notification/contact helpers, and rule inserts to `NpgsqlConnection` with canonical snake_case identifiers. Replace the SQL Server custom-error trigger assertion with an `error_log` assertion.

- [ ] **Step 4: Run MyAtm integration and unit coverage**

Run:

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "TestCategory=PostgreSqlIntegration"
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "TestCategory!=PostgreSqlIntegration"
```

Expected: both commands exit 0.

- [ ] **Step 5: Commit MyAtm conversion**

```bash
git add myatmmonitor/MyAtmMonitorTests
git commit -m "test: port MyAtm persistence tests to PostgreSQL"
```

### Task 5: Port Omnidots persistence coverage to canonical PostgreSQL

**Files:**
- Create: `omnidotsmonitor/OmnidotsMonitorTests/testdata/create.postgres.sql`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/testdata/reset.postgres.sql`
- Delete: `omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestDbClient.cs`
- Test: `omnidotsmonitor/OmnidotsMonitorTests/TestDbClient.cs`

**Interfaces:**
- Omnidots canonical tables: `monitor`, `omnidots_monitor_status`, `omnidots_sensor`, `omnidots_peak_level`, `omnidots_veff_level`, `omnidots_vdv_level`, `omnidots_trace_index`, `omnidots_trace`, `omnidots_error_message`, `rvt_alert_rule`, `deployment`, `contract`, `site_user`, `notification_setting`, `notification_sent`, `notification`, `site`, and `"AspNetUsers"`.
- Production `InsertTraceRow` already uses canonical `omnidots_trace` for PostgreSQL; the fixture must assert that branch.

- [ ] **Step 1: Add a failing trace assertion for the PostgreSQL branch**

```csharp
await using var connection = database!.OpenConnection();
await connection.OpenAsync();
await using var command = new NpgsqlCommand(
    "SELECT trace_id, x, y, z FROM omnidots_trace ORDER BY trace_id;", connection);
await using var reader = await command.ExecuteReaderAsync();
Assert.IsTrue(await reader.ReadAsync());
```

- [ ] **Step 2: Verify the new test fails before the schema exists**

Run:

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "TestCategory=PostgreSqlIntegration"
```

Expected: fixture setup fails because PostgreSQL fixture DDL has not been added.

- [ ] **Step 3: Add Omnidots PostgreSQL DDL/reset and port direct SQL**

Create the canonical vibration tables, all notification/rule tables, and the `monitor`/deployment support tables in the generated schema. Use ordinary PostgreSQL tables, not hypertables. Reset all tables and seed the global offline rule. Convert count/read helpers to `omnidots_*` canonical names and replace all `dbo.` references. Keep the trace test focused on `omnidots_trace`; do not add an equivalent SQL Server container assertion.

- [ ] **Step 4: Run Omnidots integration and unit coverage**

Run:

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "TestCategory=PostgreSqlIntegration"
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "TestCategory!=PostgreSqlIntegration"
```

Expected: both commands exit 0.

- [ ] **Step 5: Commit Omnidots conversion**

```bash
git add omnidotsmonitor/OmnidotsMonitorTests
git commit -m "test: port Omnidots persistence tests to PostgreSQL"
```

### Task 6: Port Svantek persistence coverage to canonical PostgreSQL

**Files:**
- Create: `svantekmonitor/SvantekMonitorTests/testdata/create.postgres.sql`
- Create: `svantekmonitor/SvantekMonitorTests/testdata/reset.postgres.sql`
- Delete: `svantekmonitor/SvantekMonitorTests/testdata/create.sql`
- Modify: `svantekmonitor/SvantekMonitorTests/TestDbClient.cs`

**Interfaces:**
- Svantek canonical tables: `monitor`, `svantek_monitor_status`, `svantek_noise_level`, `svantek_noise_8_hour_average`, `svantek_error_message`, `rvt_alert_rule`, `deployment`, `contract`, `site_user`, `notification_setting`, `notification_sent`, `notification`, `site`, `site_average`, and `"AspNetUsers"`.

- [ ] **Step 1: Add a failing noise persistence assertion**

```csharp
await using var connection = database!.OpenConnection();
await connection.OpenAsync();
await using var command = new NpgsqlCommand(
    "SELECT serial_id, sample_time, laeq FROM svantek_noise_level ORDER BY sample_time;", connection);
await using var reader = await command.ExecuteReaderAsync();
Assert.IsTrue(await reader.ReadAsync());
```

- [ ] **Step 2: Verify expected failure before fixture DDL exists**

Run:

```bash
dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "TestCategory=PostgreSqlIntegration"
```

Expected: setup fails because `create.postgres.sql` has not been added.

- [ ] **Step 3: Add Svantek PostgreSQL DDL/reset and port direct SQL**

Create canonical unqualified tables using PostgreSQL types and the columns defined by `SvantekMonitorContext`, including the `what_3_words` deployment field. Reset the generated-schema tables and seed the global offline rule. Convert all direct helper reads/writes from `dbo.<PascalCase>` to canonical table/column names, including noise levels, eight-hour averages, contacts, notifications, site averages, and errors. Assert common exception logging against `error_log`; do not reproduce the SQL Server `ErrorMessages` trigger.

- [ ] **Step 4: Run Svantek integration and unit coverage**

Run:

```bash
dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "TestCategory=PostgreSqlIntegration"
dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "TestCategory!=PostgreSqlIntegration"
```

Expected: both commands exit 0.

- [ ] **Step 5: Commit Svantek conversion**

```bash
git add svantekmonitor/SvantekMonitorTests
git commit -m "test: port Svantek persistence tests to PostgreSQL"
```

### Task 7: Add the sequential runner and repository documentation

**Files:**
- Create: `scripts/run-postgres-integration-tests.sh`
- Modify: `README.md`
- Modify: `docs/container-builds.md`
- Modify: `project_state.md`

**Interfaces:**
- Consumes `RVT__POSTGRES_INTEGRATION_CONNECTION`.
- Runs only `TestCategory=PostgreSqlIntegration` in a fixed monitor order.

- [ ] **Step 1: Write a failing script precondition test manually**

Run:

```bash
env -u RVT__POSTGRES_INTEGRATION_CONNECTION scripts/run-postgres-integration-tests.sh
```

Expected: command is absent before implementation; after creation it must exit 2 and print the required environment variable name without printing any connection value.

- [ ] **Step 2: Create the sequential runner**

```bash
#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${RVT__POSTGRES_INTEGRATION_CONNECTION:-}" ]]; then
  printf '%s\n' 'RVT__POSTGRES_INTEGRATION_CONNECTION is required.' >&2
  exit 2
fi

projects=(
  airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj
  myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj
  omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj
  svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj
)

for project in "${projects[@]}"; do
  dotnet test "$project" --filter 'TestCategory=PostgreSqlIntegration' --no-restore
done
```

Make the script executable. Document this exact command:

```bash
RVT__POSTGRES_INTEGRATION_CONNECTION='Host=localhost;Port=5432;Database=rvt;Username=...;Password=...' \
  scripts/run-postgres-integration-tests.sh
```

Documentation must state that the database must permit schema creation/drop, tests create a random schema, and integration tests never use `public`. Update the normal full-suite command to:

```bash
dotnet test rvt-monitors.sln --filter "TestCategory!=PostgreSqlIntegration"
```

- [ ] **Step 3: Verify runner behavior and source removal**

Run:

```bash
env -u RVT__POSTGRES_INTEGRATION_CONNECTION scripts/run-postgres-integration-tests.sh
scripts/run-postgres-integration-tests.sh
rg -n "Testcontainers\.MsSql|Microsoft\.Data\.SqlClient|SqlConnection|MsSqlBuilder|MsSqlContainer" airqmonitor/AirQMonitorTests myatmmonitor/MyAtmMonitorTests omnidotsmonitor/OmnidotsMonitorTests svantekmonitor/SvantekMonitorTests
git diff --check
```

Expected: missing-variable invocation exits 2 without a secret; sequential integration runner exits 0 with the supplied local connection; source scan returns no matches; diff check exits 0.

- [ ] **Step 4: Commit runner and documentation**

```bash
git add scripts/run-postgres-integration-tests.sh README.md docs/container-builds.md project_state.md
git commit -m "docs: document PostgreSQL integration test workflow"
```

### Task 8: Run final verification and remove generated schemas

**Files:**
- Verify: `rvt-monitors.sln`
- Verify: all four monitor test projects

- [ ] **Step 1: Run the non-integration solution suite**

Run:

```bash
dotnet test rvt-monitors.sln --filter "TestCategory!=PostgreSqlIntegration" --no-restore
```

Expected: exit 0 with no SQL Server container startup attempt.

- [ ] **Step 2: Run the sequential PostgreSQL integration suite**

Run:

```bash
scripts/run-postgres-integration-tests.sh
```

Expected: AirQ, MyAtm, Omnidots, and Svantek each exit 0 in that order.

- [ ] **Step 3: Check for leaked test schemas without exposing credentials**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj --filter "TestCategory=PostgreSqlIntegration"
```

Expected: `NoGeneratedSchemasRemainAfterFixtureCleanup` passes, proving the metadata query found zero `rvt_integration_%` schemas without echoing the connection string.

- [ ] **Step 4: Commit final verification adjustments only when needed**

```bash
git status --short
git diff --check
git commit -am "test: finalize PostgreSQL integration coverage"
```

Do not create an empty commit. Record the exact test counts and any non-code environment failure in `project_state.md`.

## Plan Self-Review

- Spec coverage: Tasks 1-2 implement the explicit connection and generated-schema lifecycle; Tasks 3-6 replace all four SQL Server fixture suites with canonical PostgreSQL tests; Task 7 defines sequential execution and documents the local workflow; Task 8 proves both excluded unit and explicit integration paths.
- Safety coverage: all DDL is scoped through a sole-schema search path; no task introduces `public` fallback, shared-table truncation, Docker/Testcontainers, or committed credentials.
- Type consistency: every monitor consumes the same `PostgreSqlIntegrationDatabase` API defined in Task 1; all direct database assertions use `NpgsqlConnection`.
- Placeholder scan: no `TODO`, `TBD`, or deferred implementation steps remain.
