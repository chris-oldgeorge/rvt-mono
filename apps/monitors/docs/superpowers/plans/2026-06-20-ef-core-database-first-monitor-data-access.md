# EF Core Database-First Monitor Data Access Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the four monitor apps' hand-written ADO.NET data access with EF Core database-first-style data access while preserving SQL Server and PostgreSQL/Timescale support.

**Architecture:** Shared tables and provider-aware EF infrastructure live in `Rvt.Monitor.Common`; monitor-specific entities and contexts live in each monitor app. Existing `IDBClient` interfaces remain stable while `DBClient` implementations move from `DBUtil` calls to EF-backed repository code.

**Tech Stack:** .NET 10, EF Core 10, `Microsoft.EntityFrameworkCore.SqlServer`, `Npgsql.EntityFrameworkCore.PostgreSQL`, MSTest, Testcontainers.MsSql, running Docker Timescale container `rvt-timescaledb` on host port `5432`.

---

## Execution Notes

- Do not inspect Docker container environment variables for credentials. Use existing app configuration or ask the user for a connection string if needed.
- The working tree currently contains uncommitted SQL-hardening edits in `DBUtil` and `MonitorDb`. Do not revert them. If later tasks delete or stop using those files, let the EF migration naturally supersede the edits.
- Avoid `FromSqlRaw`. If raw SQL is unavoidable, use interpolated/parameterized EF APIs, whitelist identifiers, add a code comment explaining why EF cannot express the operation, and add a targeted test.
- Run tasks in order. Use a fresh subagent per task when using subagent-driven execution.

## File Structure

Create common EF infrastructure:

- `rvt-monitor-common/Rvt.Monitor.Common/Data/EntityFramework/MonitorDbContextBase.cs`
- `rvt-monitor-common/Rvt.Monitor.Common/Data/EntityFramework/MonitorDbContextOptionsFactory.cs`
- `rvt-monitor-common/Rvt.Monitor.Common/Data/EntityFramework/MonitorModelBuilderExtensions.cs`
- `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/*.cs`
- `rvt-monitor-common/Rvt.Monitor.Common/Data/Queries/MonitorAggregateField.cs`
- `rvt-monitor-common/Rvt.Monitor.CommonTests/Data/EntityFramework/*Tests.cs`

Create monitor-specific EF code:

- `myatmmonitor/MyAtmMonitor/api/db/EntityFramework/MyAtmMonitorContext.cs`
- `myatmmonitor/MyAtmMonitor/api/db/EntityFramework/MyAtmEntities.cs`
- `myatmmonitor/MyAtmMonitor/api/db/EntityFramework/MyAtmAggregateFields.cs`
- `airqmonitor/AirQMonitor/api/db/EntityFramework/AirQMonitorContext.cs`
- `airqmonitor/AirQMonitor/api/db/EntityFramework/AirQEntities.cs`
- `airqmonitor/AirQMonitor/api/db/EntityFramework/AirQAggregateFields.cs`
- `omnidotsmonitor/OmnidotsMonitor/api/db/EntityFramework/OmnidotsMonitorContext.cs`
- `omnidotsmonitor/OmnidotsMonitor/api/db/EntityFramework/OmnidotsEntities.cs`
- `omnidotsmonitor/OmnidotsMonitor/api/db/EntityFramework/OmnidotsAggregateFields.cs`
- `svantekmonitor/SvantekMonitor/api/db/EntityFramework/SvantekMonitorContext.cs`
- `svantekmonitor/SvantekMonitor/api/db/EntityFramework/SvantekEntities.cs`
- `svantekmonitor/SvantekMonitor/api/db/EntityFramework/SvantekAggregateFields.cs`

Modify existing clients:

- `myatmmonitor/MyAtmMonitor/api/db/DBClient.cs`
- `airqmonitor/AirQMonitor/api/db/DBClient.cs`
- `omnidotsmonitor/OmnidotsMonitor/api/db/DBClient.cs`
- `svantekmonitor/SvantekMonitor/api/db/DBClient.cs`
- Project files for package references.

Delete only after all tests pass:

- Monitor `DBUtil.cs` files.
- Unused portions of `rvt-monitor-common/Rvt.Monitor.Common/Data/MonitorDb.cs`.

---

### Task 1: Capture Schema Sources And Confirm Baseline

**Files:**
- Read: `docs/superpowers/specs/2026-06-20-ef-core-database-first-design.md`
- Read: `airqmonitor/AirQMonitorTests/testdata/create.sql`
- Read: `myatmmonitor/MyAtmMonitorTests/testdata/create.sql`
- Read: `omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql`
- Read: `svantekmonitor/SvantekMonitorTests/testdata/create.sql`
- Create: `docs/superpowers/schema/2026-06-20-sqlserver-table-inventory.md`
- Create: `docs/superpowers/schema/2026-06-20-timescale-table-inventory.md`

- [ ] **Step 1: Record SQL Server schema inventory**

Run:

```bash
mkdir -p docs/superpowers/schema
rg -n "CREATE TABLE|CREATE INDEX|ALTER TABLE|CREATE TRIGGER" */*Tests/testdata/create.sql > /tmp/sqlserver-schema-inventory.txt
```

Create `docs/superpowers/schema/2026-06-20-sqlserver-table-inventory.md` with this structure:

```markdown
# SQL Server Test Schema Inventory

Source files:

- `airqmonitor/AirQMonitorTests/testdata/create.sql`
- `myatmmonitor/MyAtmMonitorTests/testdata/create.sql`
- `omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql`
- `svantekmonitor/SvantekMonitorTests/testdata/create.sql`

## Shared Tables

- MonitorsList
- Deployments
- Contracts
- Sites
- SiteUsers
- NotificationSettings
- Notifications
- NotificationsSent
- RvtAlertRules
- AspNetUsers
- ErrorMessages

## Monitor-Specific Tables

- AirQ: AirQMonitorStatus, AirQNoiseLevels, AirQNoise8HourAverage, AirQErrorMessages, SiteAverages
- MyAtm: MyAtmDustLevels, MyAtmAccessoryInfo, MyAtmDustLevel8hourAvg, MyAtmErrorMessages
- Omnidots: OmnidotsMonitorStatus, OmnidotsSensors, OmnidotsPeakLevels, OmnidotsVeffLevels, OmnidotsVdvLevels, OmnidotsTracesIndex, OmnidotsTraces, OmnidotsErrorMessages
- Svantek: SvantekMonitorStatus, SvantekNoiseLevels, SvantekNoise8HourAverage, SvantekErrorMessages, SiteAverages

## Raw Inventory

Include the captured lines from `/tmp/sqlserver-schema-inventory.txt`, preserving file paths and line numbers.
```

- [ ] **Step 2: Discover Timescale schema safely**

Run:

```bash
docker ps --format '{{.Names}} {{.Image}} {{.Ports}}'
```

Expected: output includes `rvt-timescaledb timescale/timescaledb:latest-pg17` and `0.0.0.0:5432->5432/tcp`.

If a PostgreSQL connection string is already present in app config or environment, use it. If not, ask the user for a temporary schema-read connection string and store it only in the current shell as `RVT_TIMESCALE_SCHEMA_CONNECTION`.

Run once a connection string is available:

```bash
psql "$RVT_TIMESCALE_SCHEMA_CONNECTION" -c "\dt public.*"
psql "$RVT_TIMESCALE_SCHEMA_CONNECTION" -c "select table_name, column_name, data_type from information_schema.columns where table_schema = 'public' order by table_name, ordinal_position;"
```

Expected: command output lists canonical tables such as `monitor`, `rvt_alert_rule`, `notification`, and monitor-specific tables such as `my_atm_dust_level`, `air_q_noise_level`, `omnidots_peak_level`, and `svantek_noise_level`.

- [ ] **Step 3: Record Timescale inventory**

Create `docs/superpowers/schema/2026-06-20-timescale-table-inventory.md` with this structure:

```markdown
# Timescale Schema Inventory

Source:

- Docker container: `rvt-timescaledb`
- Image: `timescale/timescaledb:latest-pg17`
- Host port: `5432`

Credentials were not written to this file.

## Tables

Summarize the table names returned by `\dt public.*`. Do not include usernames, passwords, hostnames beyond `localhost`, or full connection strings.

## Columns

Summarize the safe `table_name`, `column_name`, and `data_type` rows from `information_schema.columns`. Do not include any credential-bearing command text.
```

- [ ] **Step 4: Verify current baseline**

Run:

```bash
dotnet build rvt-monitor-common/rvt-monitor-common.sln
dotnet build myatmmonitor/myatmmonitor.sln
dotnet build airqmonitor/airqmonitor.sln
dotnet build omnidotsmonitor/omnidotsmonitor.sln
dotnet build svantekmonitor/svantekmonitor.sln
```

Expected: builds complete with zero errors. Existing warnings are acceptable if they match the current baseline.

- [ ] **Step 5: Commit schema notes**

Run:

```bash
git add docs/superpowers/schema/2026-06-20-sqlserver-table-inventory.md docs/superpowers/schema/2026-06-20-timescale-table-inventory.md
git commit -m "docs: capture monitor database schema sources"
```

---

### Task 2: Add EF Core Packages And Provider Factory

**Files:**
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj`
- Modify: `rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/EntityFramework/MonitorDbContextOptionsFactory.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Data/EntityFramework/MonitorDbContextOptionsFactoryTests.cs`

- [ ] **Step 1: Add failing provider factory tests**

Create `rvt-monitor-common/Rvt.Monitor.CommonTests/Data/EntityFramework/MonitorDbContextOptionsFactoryTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace Rvt.Monitor.CommonTests.Data.EntityFramework;

[TestClass]
public sealed class MonitorDbContextOptionsFactoryTests
{
    [TestMethod]
    public void CreateOptions_UsesSqlServerProvider()
    {
        var options = MonitorDbContextOptionsFactory.CreateOptions<DbContext>(
            "Server=(local);Database=Rvt;Trusted_Connection=True;TrustServerCertificate=True",
            new MonitorDbOptions(MonitorDatabaseProvider.SqlServer, new Dictionary<string, string>()));

        Assert.IsTrue(options.Extensions.Any(extension => extension.GetType().FullName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true));
    }

    [TestMethod]
    public void CreateOptions_UsesNpgsqlProvider()
    {
        var options = MonitorDbContextOptionsFactory.CreateOptions<DbContext>(
            "Host=localhost;Port=5432;Database=rvt;Username=rvt;Password=rvt",
            new MonitorDbOptions(MonitorDatabaseProvider.PostgreSql, new Dictionary<string, string>()));

        Assert.IsTrue(options.Extensions.Any(extension => extension.GetType().FullName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true));
    }
}
```

- [ ] **Step 2: Run tests and confirm failure**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter MonitorDbContextOptionsFactoryTests
```

Expected: compile fails because EF Core packages and `MonitorDbContextOptionsFactory` do not exist.

- [ ] **Step 3: Add EF package references**

Modify `rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj` and add:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.2" />
```

Modify `rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj` and add:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.2" />
```

- [ ] **Step 4: Implement provider factory**

Create `rvt-monitor-common/Rvt.Monitor.Common/Data/EntityFramework/MonitorDbContextOptionsFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace Rvt.Monitor.Common.Data.EntityFramework;

public static class MonitorDbContextOptionsFactory
{
    public static DbContextOptions<TContext> CreateOptions<TContext>(
        string connectionString,
        MonitorDbOptions options)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        if (options.IsPostgreSql)
        {
            builder.UseNpgsql(connectionString);
        }
        else
        {
            builder.UseSqlServer(connectionString);
        }

        return builder.Options;
    }
}
```

- [ ] **Step 5: Run tests and commit**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter MonitorDbContextOptionsFactoryTests
dotnet build rvt-monitor-common/rvt-monitor-common.sln
```

Expected: tests and build pass.

Commit:

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj rvt-monitor-common/Rvt.Monitor.Common/Data/EntityFramework/MonitorDbContextOptionsFactory.cs rvt-monitor-common/Rvt.Monitor.CommonTests/Data/EntityFramework/MonitorDbContextOptionsFactoryTests.cs
git commit -m "feat: add provider-aware ef core options"
```

---

### Task 3: Add Shared EF Entities And Mapping Tests

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/MonitorEntity.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/DeploymentEntity.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/ContractEntity.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/SiteEntity.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/RvtAlertRuleEntity.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/NotificationEntity.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/NotificationSentEntity.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/NotificationSettingEntity.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/AspNetUserEntity.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/SiteUserEntity.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/SiteAverageEntity.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/ErrorMessageEntity.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/EntityFramework/MonitorDbContextBase.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/EntityFramework/MonitorModelBuilderExtensions.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Data/EntityFramework/MonitorModelMappingTests.cs`

- [ ] **Step 1: Write failing metadata mapping test**

Create `rvt-monitor-common/Rvt.Monitor.CommonTests/Data/EntityFramework/MonitorModelMappingTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace Rvt.Monitor.CommonTests.Data.EntityFramework;

[TestClass]
public sealed class MonitorModelMappingTests
{
    private sealed class TestMonitorContext(DbContextOptions<TestMonitorContext> options, MonitorDbOptions monitorOptions)
        : MonitorDbContextBase(options, monitorOptions)
    {
    }

    [TestMethod]
    public void SharedModel_MapsSqlServerMonitorTable()
    {
        using var context = CreateContext(MonitorDatabaseProvider.SqlServer);
        var entityType = context.Model.FindEntityType(typeof(MonitorEntity));

        Assert.IsNotNull(entityType);
        Assert.AreEqual("MonitorsList", entityType.GetTableName());
        Assert.AreEqual("dbo", entityType.GetSchema());
        Assert.AreEqual("SerialId", entityType.FindProperty(nameof(MonitorEntity.SerialId))!.GetColumnName());
    }

    [TestMethod]
    public void SharedModel_MapsPostgreSqlMonitorTable()
    {
        using var context = CreateContext(MonitorDatabaseProvider.PostgreSql);
        var entityType = context.Model.FindEntityType(typeof(MonitorEntity));

        Assert.IsNotNull(entityType);
        Assert.AreEqual("monitor", entityType.GetTableName());
        Assert.IsNull(entityType.GetSchema());
        Assert.AreEqual("serial_id", entityType.FindProperty(nameof(MonitorEntity.SerialId))!.GetColumnName());
    }

    private static TestMonitorContext CreateContext(MonitorDatabaseProvider provider)
    {
        var monitorOptions = new MonitorDbOptions(provider, new Dictionary<string, string>(StringComparer.Ordinal));
        var dbOptions = new DbContextOptionsBuilder<TestMonitorContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestMonitorContext(dbOptions, monitorOptions);
    }
}
```

- [ ] **Step 2: Run test and confirm failure**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter MonitorModelMappingTests
```

Expected: compile fails because shared entities and `MonitorDbContextBase` do not exist.

- [ ] **Step 3: Add common entity examples**

Create `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/MonitorEntity.cs`:

```csharp
namespace Rvt.Monitor.Common.Data.Entities;

public sealed class MonitorEntity
{
    public Guid Id { get; set; }
    public string? FleetNr { get; set; }
    public string SerialId { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public DateTime ListedAtTime { get; set; }
    public string? Model { get; set; }
    public int? LocationId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationAddress { get; set; }
    public string? TimeZone { get; set; }
    public string? CustomerDisplayName { get; set; }
    public string? Manufacturer { get; set; }
    public string? FirmwareVersion { get; set; }
    public int TypeOfMonitor { get; set; }
    public bool Offline { get; set; }
    public DateTime? LastDataTime1Min { get; set; }
    public DateTime? LastDataTime15Min { get; set; }
    public DateTime? LastDataTime1Hour { get; set; }
    public DateTime? LastDataTime24Hour { get; set; }
    public byte? BatteryStatus { get; set; }
}
```

Create the other common entity files with properties matching the SQL Server fixtures and current DTO projections. Use nullable properties where the fixture allows null and non-nullable properties where the fixture has `NOT NULL`.

- [ ] **Step 4: Add context base and shared mappings**

Create `rvt-monitor-common/Rvt.Monitor.Common/Data/EntityFramework/MonitorDbContextBase.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Rvt.Monitor.Common.Data.Entities;

namespace Rvt.Monitor.Common.Data.EntityFramework;

public abstract class MonitorDbContextBase : DbContext
{
    protected MonitorDbContextBase(DbContextOptions options, MonitorDbOptions monitorOptions)
        : base(options)
    {
        MonitorOptions = monitorOptions;
    }

    protected MonitorDbOptions MonitorOptions { get; }

    public DbSet<MonitorEntity> Monitors => Set<MonitorEntity>();
    public DbSet<DeploymentEntity> Deployments => Set<DeploymentEntity>();
    public DbSet<ContractEntity> Contracts => Set<ContractEntity>();
    public DbSet<SiteEntity> Sites => Set<SiteEntity>();
    public DbSet<RvtAlertRuleEntity> AlertRules => Set<RvtAlertRuleEntity>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<NotificationSentEntity> NotificationAudits => Set<NotificationSentEntity>();
    public DbSet<NotificationSettingEntity> NotificationSettings => Set<NotificationSettingEntity>();
    public DbSet<AspNetUserEntity> Users => Set<AspNetUserEntity>();
    public DbSet<SiteUserEntity> SiteUsers => Set<SiteUserEntity>();
    public DbSet<SiteAverageEntity> SiteAverages => Set<SiteAverageEntity>();
    public DbSet<ErrorMessageEntity> ErrorMessages => Set<ErrorMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplySharedMonitorMappings(MonitorOptions);
        OnMonitorModelCreating(modelBuilder);
    }

    protected virtual void OnMonitorModelCreating(ModelBuilder modelBuilder)
    {
    }
}
```

Create `rvt-monitor-common/Rvt.Monitor.Common/Data/EntityFramework/MonitorModelBuilderExtensions.cs` with provider-aware mappings. Start with `MonitorEntity`, then add the remaining common entities:

```csharp
using Microsoft.EntityFrameworkCore;
using Rvt.Monitor.Common.Data.Entities;

namespace Rvt.Monitor.Common.Data.EntityFramework;

public static class MonitorModelBuilderExtensions
{
    public static ModelBuilder ApplySharedMonitorMappings(this ModelBuilder modelBuilder, MonitorDbOptions options)
    {
        modelBuilder.Entity<MonitorEntity>(entity =>
        {
            entity.ToTable(options.IsPostgreSql ? "monitor" : "MonitorsList", options.IsPostgreSql ? null : "dbo");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName(options.IsPostgreSql ? "id" : "Id");
            entity.Property(row => row.FleetNr).HasColumnName(options.IsPostgreSql ? "fleet_row_count" : "FleetNr");
            entity.Property(row => row.SerialId).HasColumnName(options.IsPostgreSql ? "serial_id" : "SerialId");
            entity.Property(row => row.CustomerId).HasColumnName(options.IsPostgreSql ? "customer_id" : "CustomerId");
            entity.Property(row => row.ListedAtTime).HasColumnName(options.IsPostgreSql ? "listed_at_time" : "ListedAtTime");
            entity.Property(row => row.Model).HasColumnName(options.IsPostgreSql ? "model" : "Model");
            entity.Property(row => row.LocationId).HasColumnName(options.IsPostgreSql ? "location_id" : "LocationId");
            entity.Property(row => row.Latitude).HasColumnName(options.IsPostgreSql ? "latitude" : "Latitude");
            entity.Property(row => row.Longitude).HasColumnName(options.IsPostgreSql ? "longitude" : "Longitude");
            entity.Property(row => row.LocationAddress).HasColumnName(options.IsPostgreSql ? "location_address" : "LocationAddress");
            entity.Property(row => row.TimeZone).HasColumnName(options.IsPostgreSql ? "time_zone" : "TimeZone");
            entity.Property(row => row.CustomerDisplayName).HasColumnName(options.IsPostgreSql ? "customer_display_name" : "CustomerDisplayName");
            entity.Property(row => row.Manufacturer).HasColumnName(options.IsPostgreSql ? "manufacturer" : "Manufacturer");
            entity.Property(row => row.FirmwareVersion).HasColumnName(options.IsPostgreSql ? "firmware_version" : "FirmwareVersion");
            entity.Property(row => row.TypeOfMonitor).HasColumnName(options.IsPostgreSql ? "type_of_monitor" : "TypeOfMonitor");
            entity.Property(row => row.Offline).HasColumnName(options.IsPostgreSql ? "offline" : "Offline");
            entity.Property(row => row.LastDataTime1Min).HasColumnName(options.IsPostgreSql ? "last_data_time_1_min" : "LastDataTime1Min");
            entity.Property(row => row.LastDataTime15Min).HasColumnName(options.IsPostgreSql ? "last_data_time_15_min" : "LastDataTime15Min");
            entity.Property(row => row.LastDataTime1Hour).HasColumnName(options.IsPostgreSql ? "last_data_time_1_hour" : "LastDataTime1Hour");
            entity.Property(row => row.LastDataTime24Hour).HasColumnName(options.IsPostgreSql ? "last_data_time_24_hour" : "LastDataTime24Hour");
            entity.Property(row => row.BatteryStatus).HasColumnName(options.IsPostgreSql ? "battery_status" : "BatteryStatus");
            entity.HasIndex(row => new { row.SerialId, row.TypeOfMonitor });
        });

        return modelBuilder;
    }
}
```

- [ ] **Step 5: Expand mapping tests**

Add tests for these common entities:

```csharp
[DataRow(typeof(RvtAlertRuleEntity), "RvtAlertRules", "rvt_alert_rule")]
[DataRow(typeof(NotificationEntity), "Notifications", "notification")]
[DataRow(typeof(NotificationSentEntity), "NotificationsSent", "notification_sent")]
[DataRow(typeof(DeploymentEntity), "Deployments", "deployment")]
[DataRow(typeof(ContractEntity), "Contracts", "contract")]
[DataRow(typeof(SiteEntity), "Sites", "site")]
```

Use EF metadata to assert SQL Server and PostgreSQL table names for each type.

- [ ] **Step 6: Verify and commit**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter MonitorModelMappingTests
dotnet build rvt-monitor-common/rvt-monitor-common.sln
```

Expected: tests and build pass.

Commit:

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Data/Entities rvt-monitor-common/Rvt.Monitor.Common/Data/EntityFramework rvt-monitor-common/Rvt.Monitor.CommonTests/Data/EntityFramework
git commit -m "feat: add shared monitor ef model"
```

---

### Task 4: Add Typed Aggregate Field Selectors

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/Queries/MonitorAggregateField.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Data/Queries/MonitorAggregateFieldTests.cs`
- Create monitor-specific aggregate field files listed in File Structure.

- [ ] **Step 1: Write failing field selector tests**

Create `rvt-monitor-common/Rvt.Monitor.CommonTests/Data/Queries/MonitorAggregateFieldTests.cs`:

```csharp
using Rvt.Monitor.Common.Data.Queries;

namespace Rvt.Monitor.CommonTests.Data.Queries;

[TestClass]
public sealed class MonitorAggregateFieldTests
{
    [TestMethod]
    public void CreateAverageField_RejectsUnsupportedField()
    {
        Assert.ThrowsExactly<NotSupportedException>(() =>
            MonitorAggregateField<object>.Average("bad field", row => 0));
    }

    [TestMethod]
    public void CreateAverageField_AcceptsKnownSafeName()
    {
        var field = MonitorAggregateField<object>.Average("Pm10", row => 10);

        Assert.AreEqual("Pm10", field.Name);
    }
}
```

- [ ] **Step 2: Run test and confirm failure**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter MonitorAggregateFieldTests
```

Expected: compile fails because `MonitorAggregateField` does not exist.

- [ ] **Step 3: Implement aggregate field descriptor**

Create `rvt-monitor-common/Rvt.Monitor.Common/Data/Queries/MonitorAggregateField.cs`:

```csharp
using System.Linq.Expressions;

namespace Rvt.Monitor.Common.Data.Queries;

public sealed class MonitorAggregateField<TEntity>
{
    private MonitorAggregateField(string name, Expression<Func<TEntity, double?>> selector, bool useMaximum)
    {
        Name = name;
        Selector = selector;
        UseMaximum = useMaximum;
    }

    public string Name { get; }
    public Expression<Func<TEntity, double?>> Selector { get; }
    public bool UseMaximum { get; }

    public static MonitorAggregateField<TEntity> Average(string name, Expression<Func<TEntity, double?>> selector)
    {
        return Create(name, selector, useMaximum: false);
    }

    public static MonitorAggregateField<TEntity> Maximum(string name, Expression<Func<TEntity, double?>> selector)
    {
        return Create(name, selector, useMaximum: true);
    }

    private static MonitorAggregateField<TEntity> Create(string name, Expression<Func<TEntity, double?>> selector, bool useMaximum)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
        {
            throw new NotSupportedException($"Unsupported aggregate field '{name}'.");
        }

        return new MonitorAggregateField<TEntity>(name, selector, useMaximum);
    }
}
```

- [ ] **Step 4: Add monitor-specific dictionaries**

Each monitor aggregate field file should expose a `Resolve(string fieldName)` method returning a `MonitorAggregateField<TEntity>`.

Example for MyAtm:

```csharp
using MyAtm.Api.Db.EntityFramework;
using Rvt.Monitor.Common.Data.Queries;

namespace MyAtm.Api.Db.EntityFramework;

internal static class MyAtmAggregateFields
{
    private static readonly IReadOnlyDictionary<string, MonitorAggregateField<MyAtmDustLevelEntity>> Fields =
        new Dictionary<string, MonitorAggregateField<MyAtmDustLevelEntity>>(StringComparer.Ordinal)
        {
            ["Pm1"] = MonitorAggregateField<MyAtmDustLevelEntity>.Average("Pm1", row => row.Pm1),
            ["Pm2_5"] = MonitorAggregateField<MyAtmDustLevelEntity>.Average("Pm2_5", row => row.Pm2_5),
            ["Pm10"] = MonitorAggregateField<MyAtmDustLevelEntity>.Average("Pm10", row => row.Pm10),
            ["PmTotal"] = MonitorAggregateField<MyAtmDustLevelEntity>.Average("PmTotal", row => row.PmTotal)
        };

    public static MonitorAggregateField<MyAtmDustLevelEntity> Resolve(string fieldName)
    {
        return Fields.TryGetValue(fieldName, out var field)
            ? field
            : throw new NotSupportedException($"Unsupported MyAtm aggregate field '{fieldName}'.");
    }
}
```

- [ ] **Step 5: Verify and commit**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter MonitorAggregateFieldTests
```

Expected: tests pass.

Commit:

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Data/Queries rvt-monitor-common/Rvt.Monitor.CommonTests/Data/Queries
git commit -m "feat: add typed aggregate field descriptors"
```

---

### Task 5: Migrate MyAtm Data Access First

**Files:**
- Modify: `myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj`
- Create: `myatmmonitor/MyAtmMonitor/api/db/EntityFramework/MyAtmMonitorContext.cs`
- Create: `myatmmonitor/MyAtmMonitor/api/db/EntityFramework/MyAtmEntities.cs`
- Create: `myatmmonitor/MyAtmMonitor/api/db/EntityFramework/MyAtmAggregateFields.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/DBClient.cs`
- Test: `myatmmonitor/MyAtmMonitorTests/TestDbClient.cs`

- [ ] **Step 1: Add monitor project EF package references**

Modify `myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj`:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.2" />
```

- [ ] **Step 2: Add failing MyAtm mapping test**

Create `myatmmonitor/MyAtmMonitorTests/EntityFramework/MyAtmModelMappingTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MyAtm.Api.Db;
using MyAtm.Api.Db.EntityFramework;
using Rvt.Monitor.Common.Data;

namespace MyAtmMonitorTests.EntityFramework;

[TestClass]
public sealed class MyAtmModelMappingTests
{
    [TestMethod]
    public void MyAtmContext_MapsDustLevelForSqlServer()
    {
        using var context = CreateContext(MonitorDatabaseProvider.SqlServer);
        var entityType = context.Model.FindEntityType(typeof(MyAtmDustLevelEntity));

        Assert.IsNotNull(entityType);
        Assert.AreEqual("MyAtmDustLevels", entityType.GetTableName());
        Assert.AreEqual("dbo", entityType.GetSchema());
        Assert.AreEqual("Pm10", entityType.FindProperty(nameof(MyAtmDustLevelEntity.Pm10))!.GetColumnName());
    }

    [TestMethod]
    public void MyAtmContext_MapsDustLevelForPostgreSql()
    {
        using var context = CreateContext(MonitorDatabaseProvider.PostgreSql);
        var entityType = context.Model.FindEntityType(typeof(MyAtmDustLevelEntity));

        Assert.IsNotNull(entityType);
        Assert.AreEqual("my_atm_dust_level", entityType.GetTableName());
        Assert.IsNull(entityType.GetSchema());
        Assert.AreEqual("pm_10", entityType.FindProperty(nameof(MyAtmDustLevelEntity.Pm10))!.GetColumnName());
    }

    private static MyAtmMonitorContext CreateContext(MonitorDatabaseProvider provider)
    {
        var options = new MonitorDbOptions(provider, new Dictionary<string, string>());
        var dbOptions = new DbContextOptionsBuilder<MyAtmMonitorContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MyAtmMonitorContext(dbOptions, options);
    }
}
```

- [ ] **Step 3: Run mapping test and confirm failure**

Run:

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter MyAtmModelMappingTests
```

Expected: compile fails because context and entities do not exist.

- [ ] **Step 4: Implement MyAtm entities and context**

Create `myatmmonitor/MyAtmMonitor/api/db/EntityFramework/MyAtmEntities.cs`:

```csharp
namespace MyAtm.Api.Db.EntityFramework;

internal sealed class MyAtmDustLevelEntity
{
    public string SerialId { get; set; } = string.Empty;
    public int Avrg { get; set; }
    public DateTime SampleTime { get; set; }
    public double? Pm1 { get; set; }
    public double? Pm2_5 { get; set; }
    public double? Pm10 { get; set; }
    public double? PmTotal { get; set; }
    public double? Weather_t { get; set; }
    public double? Weather_p { get; set; }
    public double? Weather_rh { get; set; }
}

internal sealed class MyAtmAccessoryInfoEntity
{
    public string SerialId { get; set; } = string.Empty;
    public DateTime SampleTime { get; set; }
    public double? OperatingSpanPointDeviation { get; set; }
    public double? OperatingTLed { get; set; }
    public double? OperatingTHeating { get; set; }
    public double? OperatingVolumeFlow { get; set; }
    public double? OperatingVolumeFlowSignalLength { get; set; }
    public DateTime OperatingVolumeFlowTimestamp { get; set; }
    public double? OperatingPeakPosition15s { get; set; }
    public double? OperatingVelocity { get; set; }
}
```

Create `myatmmonitor/MyAtmMonitor/api/db/EntityFramework/MyAtmMonitorContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace MyAtm.Api.Db.EntityFramework;

internal sealed class MyAtmMonitorContext : MonitorDbContextBase
{
    public MyAtmMonitorContext(DbContextOptions<MyAtmMonitorContext> options, MonitorDbOptions monitorOptions)
        : base(options, monitorOptions)
    {
    }

    public DbSet<MyAtmDustLevelEntity> DustLevels => Set<MyAtmDustLevelEntity>();
    public DbSet<MyAtmAccessoryInfoEntity> AccessoryInfo => Set<MyAtmAccessoryInfoEntity>();

    protected override void OnMonitorModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MyAtmDustLevelEntity>(entity =>
        {
            entity.ToTable(MonitorOptions.IsPostgreSql ? "my_atm_dust_level" : "MyAtmDustLevels", MonitorOptions.IsPostgreSql ? null : "dbo");
            entity.HasKey(row => new { row.SerialId, row.SampleTime, row.Avrg });
            entity.Property(row => row.SerialId).HasColumnName(MonitorOptions.IsPostgreSql ? "serial_id" : "SerialId");
            entity.Property(row => row.Avrg).HasColumnName(MonitorOptions.IsPostgreSql ? "avrg" : "Avrg");
            entity.Property(row => row.SampleTime).HasColumnName(MonitorOptions.IsPostgreSql ? "sample_time" : "SampleTime");
            entity.Property(row => row.Pm1).HasColumnName(MonitorOptions.IsPostgreSql ? "pm_1" : "Pm1");
            entity.Property(row => row.Pm2_5).HasColumnName(MonitorOptions.IsPostgreSql ? "pm_2_5" : "Pm2_5");
            entity.Property(row => row.Pm10).HasColumnName(MonitorOptions.IsPostgreSql ? "pm_10" : "Pm10");
            entity.Property(row => row.PmTotal).HasColumnName(MonitorOptions.IsPostgreSql ? "pm_total" : "PmTotal");
            entity.Property(row => row.Weather_t).HasColumnName(MonitorOptions.IsPostgreSql ? "weather_t" : "Weather_t");
            entity.Property(row => row.Weather_p).HasColumnName(MonitorOptions.IsPostgreSql ? "weather_p" : "Weather_p");
            entity.Property(row => row.Weather_rh).HasColumnName(MonitorOptions.IsPostgreSql ? "weather_rh" : "Weather_rh");
        });

        modelBuilder.Entity<MyAtmAccessoryInfoEntity>(entity =>
        {
            entity.ToTable(MonitorOptions.IsPostgreSql ? "my_atm_accessory_info" : "MyAtmAccessoryInfo", MonitorOptions.IsPostgreSql ? null : "dbo");
            entity.HasKey(row => new { row.SerialId, row.SampleTime });
        });
    }
}
```

- [ ] **Step 5: Implement MyAtm EF DBClient**

Modify `myatmmonitor/MyAtmMonitor/api/db/DBClient.cs` to create `MyAtmMonitorContext` per method. Start with methods covered by existing `TestDbClient` tests:

```csharp
private MyAtmMonitorContext CreateContext()
{
    var monitorOptions = MyAtmMonitorDbOptions.Current;
    var options = MonitorDbContextOptionsFactory.CreateOptions<MyAtmMonitorContext>(ConnectionString, monitorOptions);
    return new MyAtmMonitorContext(options, monitorOptions);
}
```

Replace `GetAverageDustLevel` first:

```csharp
public double? GetAverageDustLevel(string serialId, string columnName, DateTime start, DateTime end)
{
    using var context = CreateContext();
    var field = MyAtmAggregateFields.Resolve(columnName);
    var query = context.DustLevels
        .Where(row => row.Avrg == 60)
        .Where(row => row.SerialId == serialId)
        .Where(row => row.SampleTime > start && row.SampleTime <= end);

    return query.Average(field.Selector);
}
```

Then migrate these `IDBClient` methods, preserving DTO construction exactly as existing tests expect:

- `WriteMonitorList`
- `ReadMonitorList`
- `ReadMonitor`
- `WriteLatestTimestamp`
- `WriteFleetNr`
- `WriteNotification`
- `HasOpenNotification`
- `UpdateAlertRule`
- `InsertDustDtos`
- `InsertAccessoryDto`
- `HandleException`
- `ReadRules` overloads
- `ReadAlertContacts`
- `WriteNotificationAudit`
- `SetMonitorOffline`
- `ClearErrorMessages`

- [ ] **Step 6: Run MyAtm tests**

Run:

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj
dotnet build myatmmonitor/myatmmonitor.sln
```

Expected: tests pass or only pre-existing skipped/excluded suites remain unchanged; build has zero errors.

- [ ] **Step 7: Commit MyAtm migration**

Run:

```bash
git add myatmmonitor/MyAtmMonitor myatmmonitor/MyAtmMonitorTests
git commit -m "feat: migrate myatm data access to ef core"
```

---

### Task 6: Migrate AirQ Data Access

**Files:**
- Modify: `airqmonitor/AirQMonitor/AirQMonitor.csproj`
- Create: `airqmonitor/AirQMonitor/api/db/EntityFramework/AirQMonitorContext.cs`
- Create: `airqmonitor/AirQMonitor/api/db/EntityFramework/AirQEntities.cs`
- Create: `airqmonitor/AirQMonitor/api/db/EntityFramework/AirQAggregateFields.cs`
- Modify: `airqmonitor/AirQMonitor/api/db/DBClient.cs`
- Test: `airqmonitor/AirQMonitorTests/TestDbClient.cs`

- [ ] **Step 1: Add package references**

Add the same EF package references used by MyAtm to `airqmonitor/AirQMonitor/AirQMonitor.csproj`.

- [ ] **Step 2: Add AirQ mapping tests**

Create `airqmonitor/AirQMonitorTests/EntityFramework/AirQModelMappingTests.cs` asserting:

```csharp
Assert.AreEqual("AirQNoiseLevels", sqlServerNoiseType.GetTableName());
Assert.AreEqual("air_q_noise_level", postgreSqlNoiseType.GetTableName());
Assert.AreEqual("LAeq", sqlServerNoiseType.FindProperty(nameof(AirQNoiseLevelEntity.LAeq))!.GetColumnName());
Assert.AreEqual("laeq", postgreSqlNoiseType.FindProperty(nameof(AirQNoiseLevelEntity.LAeq))!.GetColumnName());
```

- [ ] **Step 3: Run mapping tests and confirm failure**

Run:

```bash
dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --filter AirQModelMappingTests
```

Expected: compile fails until AirQ context/entities exist.

- [ ] **Step 4: Implement AirQ EF entities and mappings**

Create an `AirQNoiseLevelEntity` with:

```csharp
public string SerialId { get; set; } = string.Empty;
public DateTime SampleTime { get; set; }
public double LAeq { get; set; }
public double LAmax { get; set; }
public double LA90 { get; set; }
public double LA10 { get; set; }
public double LCeq { get; set; }
public double LCmax { get; set; }
public double LC90 { get; set; }
public double LC10 { get; set; }
```

Create `AirQMonitorStatusEntity` and `AirQNoise8HourAverageEntity` using SQL Server fixture columns and PostgreSQL canonical names from `AirQMonitorDbOptions`.

- [ ] **Step 5: Implement AirQ EF DBClient**

Replace `DBUtil` calls in `airqmonitor/AirQMonitor/api/db/DBClient.cs` with EF context operations. Preserve all `IDBClient` method signatures. Use `AirQAggregateFields.Resolve` for `GetAverageNoiseLevel`.

- [ ] **Step 6: Verify and commit**

Run:

```bash
dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj
dotnet build airqmonitor/airqmonitor.sln
```

Expected: tests pass and build has zero errors.

Commit:

```bash
git add airqmonitor/AirQMonitor airqmonitor/AirQMonitorTests
git commit -m "feat: migrate airq data access to ef core"
```

---

### Task 7: Migrate Omnidots Data Access

**Files:**
- Modify: `omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj`
- Create: `omnidotsmonitor/OmnidotsMonitor/api/db/EntityFramework/OmnidotsMonitorContext.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/api/db/EntityFramework/OmnidotsEntities.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/api/db/EntityFramework/OmnidotsAggregateFields.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/db/DBClient.cs`
- Test: `omnidotsmonitor/OmnidotsMonitorTests/TestDbClient.cs`
- Test: `omnidotsmonitor/OmnidotsMonitorTests/TestRules.cs`

- [ ] **Step 1: Add package references**

Add the same EF package references used by MyAtm to `omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj`.

- [ ] **Step 2: Add Omnidots mapping tests**

Create `omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsModelMappingTests.cs` asserting mappings for:

- `OmnidotsMonitorStatusEntity`: `OmnidotsMonitorStatus` / `omnidots_monitor_status`
- `OmnidotsSensorEntity`: `OmnidotsSensors` / `omnidots_sensor`
- `OmnidotsPeakLevelEntity`: `OmnidotsPeakLevels` / `omnidots_peak_level`
- `OmnidotsVeffLevelEntity`: `OmnidotsVeffLevels` / `omnidots_veff_level`
- `OmnidotsVdvLevelEntity`: `OmnidotsVdvLevels` / `omnidots_vdv_level`
- `OmnidotsTraceIndexEntity`: `OmnidotsTracesIndex` / `omnidots_trace_index`
- `OmnidotsTraceEntity`: `OmnidotsTraces` / `omnidots_trace`

- [ ] **Step 3: Run mapping tests and confirm failure**

Run:

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter OmnidotsModelMappingTests
```

Expected: compile fails until Omnidots context/entities exist.

- [ ] **Step 4: Implement Omnidots EF entities and mappings**

Include entities for monitor status, sensors, peak levels, veff levels, vdv levels, traces index, and traces. For trace rows, map the key as `{ TraceId, row ordinal }` only if the schema has an ordinal column. If the schema has no stable key, configure the trace row as keyless with:

```csharp
entity.HasNoKey();
```

Use `AddRange` for trace rows first. Only add provider-specific bulk insert later if trace tests or profiling show unacceptable performance.

- [ ] **Step 5: Implement Omnidots EF DBClient**

Replace `DBUtil` calls in `omnidotsmonitor/OmnidotsMonitor/api/db/DBClient.cs` with EF operations. Use `OmnidotsAggregateFields.Resolve` for `GetAveragePeakLevels`. Preserve `InsertPeakRecordsTable(DataTable table)` by converting rows to `OmnidotsPeakLevelEntity` instances before saving.

- [ ] **Step 6: Verify and commit**

Run:

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj
dotnet build omnidotsmonitor/omnidotsmonitor.sln
```

Expected: tests pass and build has zero errors.

Commit:

```bash
git add omnidotsmonitor/OmnidotsMonitor omnidotsmonitor/OmnidotsMonitorTests
git commit -m "feat: migrate omnidots data access to ef core"
```

---

### Task 8: Migrate Svantek Data Access

**Files:**
- Modify: `svantekmonitor/SvantekMonitor/SvantekMonitor.csproj`
- Create: `svantekmonitor/SvantekMonitor/api/db/EntityFramework/SvantekMonitorContext.cs`
- Create: `svantekmonitor/SvantekMonitor/api/db/EntityFramework/SvantekEntities.cs`
- Create: `svantekmonitor/SvantekMonitor/api/db/EntityFramework/SvantekAggregateFields.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/db/DBClient.cs`
- Test: `svantekmonitor/SvantekMonitorTests/TestDbClient.cs`

- [ ] **Step 1: Add package references**

Add the same EF package references used by MyAtm to `svantekmonitor/SvantekMonitor/SvantekMonitor.csproj`.

- [ ] **Step 2: Add Svantek mapping tests**

Create `svantekmonitor/SvantekMonitorTests/EntityFramework/SvantekModelMappingTests.cs` asserting mappings for:

- `SvantekMonitorStatusEntity`: `SvantekMonitorStatus` / `svantek_monitor_status`
- `SvantekNoiseLevelEntity`: `SvantekNoiseLevels` / `svantek_noise_level`
- `SvantekNoise8HourAverageEntity`: `SvantekNoise8HourAverage` / `svantek_noise_8_hour_average`

- [ ] **Step 3: Run mapping tests and confirm failure**

Run:

```bash
dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter SvantekModelMappingTests
```

Expected: compile fails until Svantek context/entities exist.

- [ ] **Step 4: Implement Svantek EF entities and mappings**

Use `SvantekNoiseLevelEntity` with the same noise level fields as AirQ plus any Svantek-specific columns present in the SQL Server fixture and Timescale schema. Map status text columns that currently store booleans as strings in PostgreSQL with explicit property conversions only if the live Timescale schema confirms those columns are text.

- [ ] **Step 5: Implement Svantek EF DBClient**

Replace `DBUtil` calls in `svantekmonitor/SvantekMonitor/api/db/DBClient.cs` with EF operations. Use `SvantekAggregateFields.Resolve` for `GetAverageNoiseLevel`, with `LAmax` and `LCmax` using `Max` rather than `Average` to preserve current behavior.

- [ ] **Step 6: Verify and commit**

Run:

```bash
dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj
dotnet build svantekmonitor/svantekmonitor.sln
```

Expected: tests pass and build has zero errors.

Commit:

```bash
git add svantekmonitor/SvantekMonitor svantekmonitor/SvantekMonitorTests
git commit -m "feat: migrate svantek data access to ef core"
```

---

### Task 9: PostgreSQL/Timescale Integration Verification

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Data/EntityFramework/TimescaleSchemaSmokeTests.cs`
- Modify: monitor test project files if package references are needed for PostgreSQL testing.

- [ ] **Step 1: Add safe connection-string test gate**

Create `rvt-monitor-common/Rvt.Monitor.CommonTests/Data/EntityFramework/TimescaleSchemaSmokeTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace Rvt.Monitor.CommonTests.Data.EntityFramework;

[TestClass]
public sealed class TimescaleSchemaSmokeTests
{
    [TestMethod]
    public async Task TimescaleSchema_HasCanonicalMonitorTable()
    {
        var connectionString = Environment.GetEnvironmentVariable("RVT_TIMESCALE_SCHEMA_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Inconclusive("Set RVT_TIMESCALE_SCHEMA_CONNECTION to run Timescale schema smoke tests.");
        }

        var monitorOptions = new MonitorDbOptions(MonitorDatabaseProvider.PostgreSql, new Dictionary<string, string>());
        var dbOptions = MonitorDbContextOptionsFactory.CreateOptions<TestMonitorContext>(connectionString, monitorOptions);
        await using var context = new TestMonitorContext(dbOptions, monitorOptions);

        FormattableString query =
            $"select 1 from information_schema.tables where table_schema = {'public'} and table_name = {'monitor'}";

        var tableExists = await context.Database
            .SqlQuery<int>(query)
            .AnyAsync();

        Assert.IsTrue(tableExists);
    }

    private sealed class TestMonitorContext(DbContextOptions<TestMonitorContext> options, MonitorDbOptions monitorOptions)
        : MonitorDbContextBase(options, monitorOptions)
    {
    }
}
```

This is an allowed SQL exception because it queries PostgreSQL metadata, not application data. It uses EF interpolation for values and has no user-provided SQL syntax.

- [ ] **Step 2: Run Timescale smoke test without connection string**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter TimescaleSchemaSmokeTests
```

Expected: test is inconclusive when `RVT_TIMESCALE_SCHEMA_CONNECTION` is not set.

- [ ] **Step 3: Run Timescale smoke test with connection string**

Run after setting the variable safely:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter TimescaleSchemaSmokeTests
```

Expected: test passes against the running `rvt-timescaledb` database.

- [ ] **Step 4: Commit Timescale verification**

Run:

```bash
git add rvt-monitor-common/Rvt.Monitor.CommonTests/Data/EntityFramework/TimescaleSchemaSmokeTests.cs
git commit -m "test: verify timescale schema access"
```

---

### Task 10: Remove Obsolete ADO.NET Data Access

**Files:**
- Delete: `airqmonitor/AirQMonitor/api/db/DBUtil.cs`
- Delete: `myatmmonitor/MyAtmMonitor/api/db/DBUtil.cs`
- Delete: `omnidotsmonitor/OmnidotsMonitor/api/db/DBUtil.cs`
- Delete: `svantekmonitor/SvantekMonitor/api/db/DBUtil.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Data/MonitorDb.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj`

- [ ] **Step 1: Confirm no production callers remain**

Run:

```bash
rg -n "DBUtil|MonitorDb\\.OpenConnection|MonitorDb\\.CreateCommand|MonitorDb\\.BulkInsert|SqlCommand|NpgsqlCommand|ExecuteReader|ExecuteNonQuery|ExecuteScalar" -g '*.cs' -g '!**/*Tests/**'
```

Expected: no production data-access callers remain except any explicitly documented raw SQL metadata tests or provider factory code.

- [ ] **Step 2: Delete monitor DBUtil files**

Run:

```bash
git rm airqmonitor/AirQMonitor/api/db/DBUtil.cs
git rm myatmmonitor/MyAtmMonitor/api/db/DBUtil.cs
git rm omnidotsmonitor/OmnidotsMonitor/api/db/DBUtil.cs
git rm svantekmonitor/SvantekMonitor/api/db/DBUtil.cs
```

- [ ] **Step 3: Reduce MonitorDb**

Keep `MonitorDb.ResolveProvider` only if EF provider selection still uses it. Remove ADO.NET-only helpers from `rvt-monitor-common/Rvt.Monitor.Common/Data/MonitorDb.cs`:

```csharp
OpenConnection
CreateCommand
BulkInsert
WriteException
RewriteSql
RewriteTableName
RewriteIdentifier
```

Move any still-needed provider option logic into smaller EF-specific classes.

- [ ] **Step 4: Remove unused packages**

If no production code references `Microsoft.Data.SqlClient` or `Npgsql` directly after EF migration, remove direct package references from `rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj`. Keep EF provider packages.

- [ ] **Step 5: Verify and commit**

Run:

```bash
dotnet build rvt-monitor-common/rvt-monitor-common.sln
dotnet build myatmmonitor/myatmmonitor.sln
dotnet build airqmonitor/airqmonitor.sln
dotnet build omnidotsmonitor/omnidotsmonitor.sln
dotnet build svantekmonitor/svantekmonitor.sln
```

Expected: all builds pass with zero errors.

Commit:

```bash
git add airqmonitor/AirQMonitor/api/db/DBUtil.cs myatmmonitor/MyAtmMonitor/api/db/DBUtil.cs omnidotsmonitor/OmnidotsMonitor/api/db/DBUtil.cs svantekmonitor/SvantekMonitor/api/db/DBUtil.cs rvt-monitor-common/Rvt.Monitor.Common/Data/MonitorDb.cs rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj
git commit -m "refactor: remove ado net monitor data access"
```

---

### Task 11: Final Security Audit And Full Test Run

**Files:**
- Modify only if audit finds missed data-access paths.

- [ ] **Step 1: Audit raw SQL and dynamic SQL**

Run:

```bash
rg -n "FromSqlRaw|ExecuteSqlRaw|SqlQueryRaw|string\\.Format|CommandText|SqlCommand|NpgsqlCommand|ExecuteReader|ExecuteNonQuery|ExecuteScalar|\\+ .*SELECT|\\+ .*UPDATE|\\+ .*INSERT|\\+ .*DELETE" -g '*.cs'
```

Expected:

- No `FromSqlRaw` or `ExecuteSqlRaw` in production.
- Any SQL metadata smoke test uses EF-interpolated APIs, such as `SqlQuery`, rather than `SqlQueryRaw`.
- No dynamic SQL string concatenation in production data access.

- [ ] **Step 2: Run full tests**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj
dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj
dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj
```

Expected: all active tests pass. Existing intentionally excluded Svantek suites remain excluded by project file.

- [ ] **Step 3: Run solution builds**

Run:

```bash
dotnet build rvt-monitor-common/rvt-monitor-common.sln
dotnet build myatmmonitor/myatmmonitor.sln
dotnet build airqmonitor/airqmonitor.sln
dotnet build omnidotsmonitor/omnidotsmonitor.sln
dotnet build svantekmonitor/svantekmonitor.sln
```

Expected: all builds pass with zero errors.

- [ ] **Step 4: Run git whitespace check**

Run:

```bash
git diff --check
```

Expected: no whitespace errors.

- [ ] **Step 5: Commit final fixes**

If Step 1 found any missed paths and fixes were made, commit them:

```bash
git add rvt-monitor-common airqmonitor myatmmonitor omnidotsmonitor svantekmonitor
git commit -m "fix: close remaining raw sql data access paths"
```

If no fixes were needed, do not create an empty commit.
