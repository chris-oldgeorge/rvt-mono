# MyAtm CQRS-Lite Mapperly Implementation Plan

> **Status update, 2026-07-06:** This began as a MyAtm-only pilot plan. The preferred project direction is now the horizontal version of the same approach across all monitor apps: EF Core-backed DB clients, app-local Mapperly mappers for simple DTO/entity conversion, and CQRS-lite query/command interfaces over the `IDBClient` compatibility facade. The MyAtm-only limits below are historical plan constraints, not current architecture guidance.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Mapperly only to the MyAtm monitor as a pilot and use that pilot to split MyAtm database writes first, then layer CQRS-lite query/command boundaries without changing monitor behavior. The successful pilot has since been generalized to AirQ, Omnidots, and Svantek.

**Architecture:** Keep EF Core, the current schema, and the public `IDBClient` facade intact at first so the existing API and rule-processing tests keep their value. Extract mapping and write responsibilities from `DBClient` into small MyAtm-only collaborators, then move read responsibilities behind query interfaces once write behavior is covered. Mapperly is limited to simple DTO/entity conversion; vendor JSON parsing, rule logic, notification dispatch, and aggregate query field selection stay manual.

**Tech Stack:** .NET 10, EF Core 10, MSTest, Testcontainers for SQL Server-backed MyAtm DB tests, Mapperly `Riok.Mapperly`, existing `Rvt.Monitor.Common` shared infrastructure.

---

## Files

- Modify: `myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj`
  - Add `Riok.Mapperly` only here with `PrivateAssets="all"` and `OutputItemType="Analyzer"` so no other monitor takes a dependency.
- Create: `myatmmonitor/MyAtmMonitor/api/db/Mapping/MyAtmDbMapper.cs`
  - Mapperly mapper for `MonitorEntity`, `MyAtmDustLevelEntity`, `MyAtmAccessoryInfoEntity`, and MyAtm DTOs.
- Create: `myatmmonitor/MyAtmMonitor/api/db/Commands/IMyAtmMonitorCommands.cs`
  - Write-side monitor operations currently mixed into `IDBClient`.
- Create: `myatmmonitor/MyAtmMonitor/api/db/Commands/IMyAtmMeasurementCommands.cs`
  - Write-side dust/accessory measurement operations.
- Create: `myatmmonitor/MyAtmMonitor/api/db/Commands/IMyAtmOperationalCommands.cs`
  - Error, notification, and rule-state writes that are not monitor catalog writes.
- Create: `myatmmonitor/MyAtmMonitor/api/db/Queries/IMyAtmMonitorQueries.cs`
  - Monitor read operations currently mixed into `IDBClient`.
- Create: `myatmmonitor/MyAtmMonitor/api/db/Queries/IMyAtmRuleQueries.cs`
  - Rule, contact, open-notification, and average dust reads.
- Modify: `myatmmonitor/MyAtmMonitor/api/db/IDBClient.cs`
  - Make `IDBClient` inherit the smaller query/command interfaces during the transition.
- Modify: `myatmmonitor/MyAtmMonitor/api/db/DBClient.cs`
  - Use the mapper for simple conversions and keep it as a facade while extraction happens.
- Test: `myatmmonitor/MyAtmMonitorTests/Mapping/MyAtmDbMapperTests.cs`
  - Mapper equivalence tests that fail before mapper code exists.
- Test: `myatmmonitor/MyAtmMonitorTests/TestDbClient.cs`
  - Extend existing integration coverage for update semantics and dust duplicate behavior.
- Test: `myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmDependencyBoundaryTests.cs`
  - Verify Mapperly is scoped to MyAtm and does not leak into other monitor projects.

---

### Task 0: Checkpoint Current Observability Work

**Files:**
- Inspect: current working tree

- [ ] **Step 1: Confirm the current dirty tree**

Run:

```bash
git status --short
```

Expected: existing OpenTelemetry files may be modified or untracked. This plan file may also appear. Do not mix Mapperly/CQRS implementation commits into an unfinished observability checkpoint unless the user explicitly wants one combined commit.

- [ ] **Step 2: Commit or intentionally defer the observability checkpoint**

If the observability work is ready, commit it before starting Task 1:

```bash
git add observability/README.md rvt-monitor-common/Rvt.Monitor.Common rvt-monitor-common/Rvt.Monitor.CommonTests
git commit -m "feat: add monitor open telemetry instrumentation"
```

Expected: a focused checkpoint commit exists before the MyAtm refactor begins.

If the observability work is not ready, create a new branch or worktree before implementation so this plan can be executed without muddying the active changes.

---

### Task 1: Add Mapperly To MyAtm Only

**Files:**
- Modify: `myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj`
- Test: `myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmDependencyBoundaryTests.cs`

- [ ] **Step 1: Write the dependency-boundary test**

Create `myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmDependencyBoundaryTests.cs`:

```csharp
namespace MyAtmMonitorTests.Architecture;

[TestClass]
public sealed class MyAtmDependencyBoundaryTests
{
    [TestMethod]
    public void MapperlyPackageReference_IsOnlyUsedByMyAtmMonitorProject()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectFiles = Directory.GetFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories);

        var mapperlyProjects = projectFiles
            .Where(path => File.ReadAllText(path).Contains("Riok.Mapperly", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        CollectionAssert.AreEqual(
            new[] { "myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj" },
            mapperlyProjects);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        Assert.Fail("Could not find repository root from test output directory.");
        return string.Empty;
    }
}
```

- [ ] **Step 2: Run the new test to verify it fails**

Run:

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter FullyQualifiedName~MyAtmDependencyBoundaryTests
```

Expected: FAIL because no project references Mapperly yet.

- [ ] **Step 3: Add the Mapperly analyzer package only to MyAtm**

Modify `myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj` inside the existing package `ItemGroup`:

```xml
<PackageReference Include="Riok.Mapperly" Version="4.3.1" PrivateAssets="all" OutputItemType="Analyzer" />
```

- [ ] **Step 4: Run the dependency-boundary test**

Run:

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter FullyQualifiedName~MyAtmDependencyBoundaryTests
```

Expected: PASS. Only `myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj` contains `Riok.Mapperly`.

- [ ] **Step 5: Commit**

Run:

```bash
git add myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmDependencyBoundaryTests.cs
git commit -m "chore: add mapperly to myatm monitor"
```

---

### Task 2: Extract MyAtm Mapping Behind Mapperly

**Files:**
- Create: `myatmmonitor/MyAtmMonitor/api/db/Mapping/MyAtmDbMapper.cs`
- Test: `myatmmonitor/MyAtmMonitorTests/Mapping/MyAtmDbMapperTests.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/DBClient.cs`

- [ ] **Step 1: Write mapper equivalence tests**

Create `myatmmonitor/MyAtmMonitorTests/Mapping/MyAtmDbMapperTests.cs`:

```csharp
using MyAtm.Api.Db.EntityFramework;
using MyAtm.Api.Db.Mapping;
using MyAtm.Model.Dto;
using Rvt.Monitor.Common.Data.Entities;

namespace MyAtmMonitorTests.Mapping;

[TestClass]
public sealed class MyAtmDbMapperTests
{
    [TestMethod]
    public void ToDustMonitorDto_MapsMonitorEntityDefaults()
    {
        var id = Guid.NewGuid();
        var entity = new MonitorEntity
        {
            Id = id,
            CustomerId = 42,
            ListedAtTime = DateTime.Parse("2026-07-06T08:00:00Z").ToUniversalTime(),
            SerialId = "21972",
            Model = "Fidas Frog",
            LocationId = 7,
            Latitude = 51.5,
            Longitude = -0.1,
            LocationAddress = "Test Road",
            TimeZone = "Europe/London",
            CustomerDisplayName = "RVT Test",
            LastDataTime1Min = DateTime.Parse("2026-07-06T08:01:00Z").ToUniversalTime(),
            LastDataTime15Min = DateTime.Parse("2026-07-06T08:15:00Z").ToUniversalTime(),
            LastDataTime1Hour = DateTime.Parse("2026-07-06T09:00:00Z").ToUniversalTime(),
            LastDataTime24Hour = DateTime.Parse("2026-07-07T08:00:00Z").ToUniversalTime(),
            Manufacturer = "Palas GmbH",
            FirmwareVersion = "1.2.3",
            FleetNr = "R6025V",
            Offline = true,
            TypeOfMonitor = DustMonitorDto.MONITOR_TYPE_DUST
        };

        var dto = MyAtmDbMapper.ToDustMonitorDto(entity);

        Assert.AreEqual(id, dto.Id);
        Assert.AreEqual(42, dto.CustomerId);
        Assert.AreEqual("21972", dto.SerialId);
        Assert.AreEqual("Fidas Frog", dto.Model);
        Assert.AreEqual(7, dto.LocationId);
        Assert.AreEqual(51.5f, dto.Latitude);
        Assert.AreEqual(-0.1f, dto.Longitude);
        Assert.AreEqual("Test Road", dto.Address);
        Assert.AreEqual("Europe/London", dto.TimeZone);
        Assert.AreEqual("RVT Test", dto.CustomerDisplayName);
        Assert.AreEqual("Palas GmbH", dto.Manufacturer);
        Assert.AreEqual("1.2.3", dto.FirmwareVersion);
        Assert.AreEqual("R6025V", dto.FleetNr);
        Assert.IsTrue(dto.Offline);
    }

    [TestMethod]
    public void UpdateMonitorEntity_DoesNotOverwriteFleetOrLatestTimestamps()
    {
        var entity = new MonitorEntity
        {
            Id = Guid.NewGuid(),
            SerialId = "21972",
            FleetNr = "R6025V",
            LastDataTime1Min = DateTime.Parse("2026-07-06T08:01:00Z").ToUniversalTime()
        };

        var dto = new DustMonitorDto(
            id: entity.Id,
            customerId: 99,
            listedAtTime: DateTime.Parse("2026-07-06T07:00:00Z").ToUniversalTime(),
            serialId: "21972",
            model: "Updated model",
            locationId: 12,
            latitude: 10.5f,
            longitude: 20.5f,
            address: "New address",
            timeZone: "Europe/Athens",
            customerDisplayName: "Updated customer",
            lastDataTime1Min: DateTime.Parse("2030-01-01T00:00:00Z").ToUniversalTime(),
            lastDataTime15Min: null,
            lastDataTime1Hour: null,
            lastDataTime24Hour: null,
            manufacturer: "Palas GmbH",
            firmwareVersion: "9.9.9",
            fleetNr: "SHOULD-NOT-BE-COPIED",
            offline: false);

        MyAtmDbMapper.UpdateMonitorEntity(entity, dto);

        Assert.AreEqual("R6025V", entity.FleetNr);
        Assert.AreEqual(DateTime.Parse("2026-07-06T08:01:00Z").ToUniversalTime(), entity.LastDataTime1Min);
        Assert.AreEqual("Updated model", entity.Model);
        Assert.AreEqual(99, entity.CustomerId);
        Assert.AreEqual(DustMonitorDto.MONITOR_TYPE_DUST, entity.TypeOfMonitor);
    }

    [TestMethod]
    public void ToDustLevelEntity_MapsMeasurementDto()
    {
        var dto = new DustDto("21972", 60, DateTime.Parse("2026-07-06T08:00:00Z").ToUniversalTime(),
            pm1: 1.1, pm2_5: 2.2, pm10: 10.1, pmTotal: 13.4, weather_t: 20.5, weather_p: 1012.1, weather_rh: 80.2);

        var entity = MyAtmDbMapper.ToDustLevelEntity(dto);

        Assert.AreEqual("21972", entity.SerialId);
        Assert.AreEqual(60, entity.Avrg);
        Assert.AreEqual(dto.SampleTime, entity.SampleTime);
        Assert.AreEqual(1.1, entity.Pm1);
        Assert.AreEqual(2.2, entity.Pm2_5);
        Assert.AreEqual(10.1, entity.Pm10);
        Assert.AreEqual(13.4, entity.PmTotal);
        Assert.AreEqual(20.5, entity.Weather_t);
        Assert.AreEqual(1012.1, entity.Weather_p);
        Assert.AreEqual(80.2, entity.Weather_rh);
    }
}
```

- [ ] **Step 2: Run mapper tests to verify they fail**

Run:

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter FullyQualifiedName~MyAtmDbMapperTests
```

Expected: FAIL because `MyAtm.Api.Db.Mapping.MyAtmDbMapper` does not exist.

- [ ] **Step 3: Add the mapper**

Create `myatmmonitor/MyAtmMonitor/api/db/Mapping/MyAtmDbMapper.cs`:

```csharp
using MyAtm.Api.Db.EntityFramework;
using MyAtm.Model.Dto;
using Riok.Mapperly.Abstractions;
using Rvt.Monitor.Common.Data.Entities;

namespace MyAtm.Api.Db.Mapping;

[Mapper]
public static partial class MyAtmDbMapper
{
    [MapProperty(nameof(MonitorEntity.LocationAddress), nameof(DustMonitorDto.Address))]
    [MapProperty(nameof(MonitorEntity.FleetNr), nameof(DustMonitorDto.FleetNr))]
    public static partial DustMonitorDto ToDustMonitorDto(MonitorEntity entity);

    public static MonitorEntity ToMonitorEntity(DustMonitorDto dto)
    {
        var entity = new MonitorEntity { Id = dto.Id };
        UpdateMonitorEntity(entity, dto);
        return entity;
    }

    [MapperIgnoreTarget(nameof(MonitorEntity.FleetNr))]
    [MapperIgnoreTarget(nameof(MonitorEntity.LastDataTime1Min))]
    [MapperIgnoreTarget(nameof(MonitorEntity.LastDataTime15Min))]
    [MapperIgnoreTarget(nameof(MonitorEntity.LastDataTime1Hour))]
    [MapperIgnoreTarget(nameof(MonitorEntity.LastDataTime24Hour))]
    [MapperIgnoreSource(nameof(DustMonitorDto.FleetNr))]
    [MapperIgnoreSource(nameof(DustMonitorDto.LastDataTime1Min))]
    [MapperIgnoreSource(nameof(DustMonitorDto.LastDataTime15Min))]
    [MapperIgnoreSource(nameof(DustMonitorDto.LastDataTime1Hour))]
    [MapperIgnoreSource(nameof(DustMonitorDto.LastDataTime24Hour))]
    [MapProperty(nameof(DustMonitorDto.Address), nameof(MonitorEntity.LocationAddress))]
    public static partial void UpdateMonitorEntity(MonitorEntity entity, DustMonitorDto dto);

    public static partial MyAtmDustLevelEntity ToDustLevelEntity(DustDto dto);

    public static partial MyAtmAccessoryInfoEntity ToAccessoryInfoEntity(AccessoryInfoDto dto);
}
```

If Mapperly reports constructor or readonly-property generation issues for `DustMonitorDto`, replace the `ToDustMonitorDto` partial method with this explicit method and keep Mapperly for entity creation/update methods:

```csharp
public static DustMonitorDto ToDustMonitorDto(MonitorEntity row)
{
    return new DustMonitorDto(
        id: row.Id,
        customerId: row.CustomerId ?? 0,
        listedAtTime: row.ListedAtTime,
        serialId: row.SerialId,
        model: row.Model,
        locationId: row.LocationId ?? 0,
        latitude: (float)(row.Latitude ?? 0),
        longitude: (float)(row.Longitude ?? 0),
        address: row.LocationAddress,
        timeZone: row.TimeZone,
        customerDisplayName: row.CustomerDisplayName,
        lastDataTime1Min: row.LastDataTime1Min,
        lastDataTime15Min: row.LastDataTime15Min,
        lastDataTime1Hour: row.LastDataTime1Hour,
        lastDataTime24Hour: row.LastDataTime24Hour,
        manufacturer: row.Manufacturer,
        firmwareVersion: row.FirmwareVersion,
        fleetNr: row.FleetNr,
        offline: row.Offline ?? false);
}
```

- [ ] **Step 4: Replace DBClient private mapping helpers**

Modify `myatmmonitor/MyAtmMonitor/api/db/DBClient.cs`:

```csharp
using MyAtm.Api.Db.Mapping;
```

Then replace calls:

```csharp
.Select(row => MyAtmDbMapper.ToDustMonitorDto(row))
```

```csharp
return monitor == null ? null : MyAtmDbMapper.ToDustMonitorDto(monitor);
```

```csharp
context.Monitors.Add(MyAtmDbMapper.ToMonitorEntity(dto));
```

```csharp
MyAtmDbMapper.UpdateMonitorEntity(entity, dto);
```

```csharp
context.DustLevels.Add(MyAtmDbMapper.ToDustLevelEntity(dto));
```

```csharp
context.AccessoryInfo.Add(MyAtmDbMapper.ToAccessoryInfoEntity(dto));
```

Remove the old private `ToDustMonitorDto`, `ToMonitorEntity`, and `ApplyMonitorDto` methods from `DBClient`.

- [ ] **Step 5: Run mapper and MyAtm DB tests**

Run:

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter FullyQualifiedName~MyAtmDbMapperTests
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter FullyQualifiedName~TestDBClient
```

Expected: PASS.

- [ ] **Step 6: Commit**

Run:

```bash
git add myatmmonitor/MyAtmMonitor/api/db/Mapping/MyAtmDbMapper.cs myatmmonitor/MyAtmMonitor/api/db/DBClient.cs myatmmonitor/MyAtmMonitorTests/Mapping/MyAtmDbMapperTests.cs
git commit -m "refactor: map myatm database rows with mapperly"
```

---

### Task 3: Split Write Interfaces While Keeping IDBClient As Facade

**Files:**
- Create: `myatmmonitor/MyAtmMonitor/api/db/Commands/IMyAtmMonitorCommands.cs`
- Create: `myatmmonitor/MyAtmMonitor/api/db/Commands/IMyAtmMeasurementCommands.cs`
- Create: `myatmmonitor/MyAtmMonitor/api/db/Commands/IMyAtmOperationalCommands.cs`
- Create: `myatmmonitor/MyAtmMonitor/api/db/Queries/IMyAtmMonitorQueries.cs`
- Create: `myatmmonitor/MyAtmMonitor/api/db/Queries/IMyAtmRuleQueries.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/IDBClient.cs`

- [ ] **Step 1: Add write interfaces**

Create `myatmmonitor/MyAtmMonitor/api/db/Commands/IMyAtmMonitorCommands.cs`:

```csharp
using MyAtm.Model.Dto;

namespace MyAtm.Api.Db.Commands;

public interface IMyAtmMonitorCommands
{
    void WriteMonitorList(List<DustMonitorDto> devices);
    void WriteLatestTimestamp(string serialNumber, DateTime lastDataTime, Period period);
    void WriteFleetNr(string serialNumber, string fleetNr);
    void SetMonitorOffline(Guid monitorId, bool offline);
}
```

Create `myatmmonitor/MyAtmMonitor/api/db/Commands/IMyAtmMeasurementCommands.cs`:

```csharp
using MyAtm.Model.Dto;

namespace MyAtm.Api.Db.Commands;

public interface IMyAtmMeasurementCommands
{
    void InsertDustDtos(List<DustDto> dtos);
    void InsertAccessoryDto(AccessoryInfoDto dto);
}
```

Create `myatmmonitor/MyAtmMonitor/api/db/Commands/IMyAtmOperationalCommands.cs`:

```csharp
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;

namespace MyAtm.Api.Db.Commands;

public interface IMyAtmOperationalCommands
{
    void HandleException(string message, Exception exception);
    void WriteNotification(NotificationDto dto);
    void WriteNotificationAudit(Guid notificationId, string address, string message);
    void UpdateAlertRule(RvtAlertRuleDto dto);
    void ClearErrorMessages(DateTime before);
}
```

- [ ] **Step 2: Add read interfaces**

Create `myatmmonitor/MyAtmMonitor/api/db/Queries/IMyAtmMonitorQueries.cs`:

```csharp
using MyAtm.Model.Dto;

namespace MyAtm.Api.Db.Queries;

public interface IMyAtmMonitorQueries
{
    List<DustMonitorDto> ReadMonitorList(DateTime? lastDataTime);
    DustMonitorDto? ReadMonitor(string serialId);
}
```

Create `myatmmonitor/MyAtmMonitor/api/db/Queries/IMyAtmRuleQueries.cs`:

```csharp
using Rvt.Monitor.Common.Rules;

namespace MyAtm.Api.Db.Queries;

public interface IMyAtmRuleQueries
{
    List<RvtAlertRuleDto> ReadRules(string? serialId);
    List<RvtAlertRuleDto> ReadRules(string? serialId, Period period);
    List<RvtAlertRuleDto> ReadRules(Period period);
    List<RvtContactDto> ReadAlertContacts(Guid monitorId);
    bool HasOpenNotification(Guid monitorId, string alertField, AlertType alertType);
    double? GetAverageDustLevel(string serialNumber, string columnName, DateTime start, DateTime end);
}
```

- [ ] **Step 3: Make IDBClient inherit the smaller interfaces**

Replace the body of `myatmmonitor/MyAtmMonitor/api/db/IDBClient.cs` with:

```csharp
using MyAtm.Api.Db.Commands;
using MyAtm.Api.Db.Queries;

namespace MyAtm.Api.Db;

public interface IDBClient :
    IMyAtmMonitorQueries,
    IMyAtmRuleQueries,
    IMyAtmMonitorCommands,
    IMyAtmMeasurementCommands,
    IMyAtmOperationalCommands
{
}
```

- [ ] **Step 4: Build MyAtm**

Run:

```bash
dotnet build myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-build
```

Expected: PASS. No callers need to change yet because `DBClient` still implements all inherited members through `IDBClient`.

- [ ] **Step 5: Commit**

Run:

```bash
git add myatmmonitor/MyAtmMonitor/api/db/Commands myatmmonitor/MyAtmMonitor/api/db/Queries myatmmonitor/MyAtmMonitor/api/db/IDBClient.cs
git commit -m "refactor: split myatm database client interfaces"
```

---

### Task 4: Optimize Dust Writes Behind The Existing Method

**Files:**
- Modify: `myatmmonitor/MyAtmMonitor/api/db/DBClient.cs`
- Test: `myatmmonitor/MyAtmMonitorTests/TestDbClient.cs`

- [ ] **Step 1: Add an existing-row duplicate regression test**

Add to `TestDBClient` in `myatmmonitor/MyAtmMonitorTests/TestDbClient.cs` near `InsertDustDto_IgnoresDuplicateRowsInSingleBatch`:

```csharp
[TestMethod]
public void InsertDustDto_IgnoresRowsAlreadyPresentInDatabase()
{
    var serialId = "17239";
    var sampleTime = DateTime.Parse("2023-10-17T14:37:42");
    var dto = new DustDto(serialId: serialId, avrg: 60, sampleTime: sampleTime,
        pm1: 1.0, pm2_5: 2.5, pm10: 10, pmTotal: 13.5,
        weather_t: 3.1234, weather_p: 5.5678, weather_rh: 99.87654);

    testObj!.InsertDustDtos(new List<DustDto> { dto });
    testObj.InsertDustDtos(new List<DustDto> { dto });

    var connectionString = sqlContainer.GetConnectionString();
    using var connection = new SqlConnection(connectionString);
    connection.Open();
    var dtos = ReadDustDtos(connection);

    Assert.AreEqual(1, dtos.Count);
}
```

- [ ] **Step 2: Run the dust insertion tests**

Run:

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "Name~InsertDustDto"
```

Expected: PASS against current behavior. This locks behavior before the performance change.

- [ ] **Step 3: Replace per-row `Any` calls with one existing-key query**

Modify `InsertDustDtos` in `myatmmonitor/MyAtmMonitor/api/db/DBClient.cs`:

```csharp
public void InsertDustDtos(List<DustDto> dtos)
{
    if (dtos.Count == 0)
    {
        return;
    }

    using var context = CreateContext();
    var incomingKeys = dtos
        .Select(dto => (dto.SerialId, dto.SampleTime, dto.Avrg))
        .ToHashSet();

    var serialIds = incomingKeys.Select(key => key.SerialId).ToHashSet(StringComparer.Ordinal);
    var earliest = incomingKeys.Min(key => key.SampleTime);
    var latest = incomingKeys.Max(key => key.SampleTime);

    var existingKeys = context.DustLevels
        .AsNoTracking()
        .Where(row => serialIds.Contains(row.SerialId))
        .Where(row => row.SampleTime >= earliest && row.SampleTime <= latest)
        .Select(row => new { row.SerialId, row.SampleTime, row.Avrg })
        .AsEnumerable()
        .Select(row => (row.SerialId, row.SampleTime, row.Avrg))
        .ToHashSet();

    foreach (var dto in dtos)
    {
        var key = (dto.SerialId, dto.SampleTime, dto.Avrg);
        if (!incomingKeys.Remove(key) || existingKeys.Contains(key))
        {
            continue;
        }

        context.DustLevels.Add(MyAtmDbMapper.ToDustLevelEntity(dto));
    }

    context.SaveChanges();
}
```

- [ ] **Step 4: Run the MyAtm DB tests**

Run:

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter FullyQualifiedName~TestDBClient
```

Expected: PASS.

- [ ] **Step 5: Commit**

Run:

```bash
git add myatmmonitor/MyAtmMonitor/api/db/DBClient.cs myatmmonitor/MyAtmMonitorTests/TestDbClient.cs
git commit -m "perf: batch myatm dust duplicate checks"
```

---

### Task 5: Route MyAtm API Through CQRS-Lite Interfaces

**Files:**
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmApi.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmApiMonitors.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmApiDustLevels.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmApiAccessoryInfo.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmApiRuleProcessing.cs`
- Modify: MyAtm API tests that construct or mock `IDBClient`

- [ ] **Step 1: Keep constructor compatibility**

Do not change the public constructor shape until all partial MyAtm API files have been updated. Existing tests use `Mock<IDBClient>` heavily; keep that path working while adding narrower internal fields.

In `MyAtmApi.cs`, add fields like:

```csharp
private readonly IMyAtmMonitorQueries monitorQueries;
private readonly IMyAtmRuleQueries ruleQueries;
private readonly IMyAtmMonitorCommands monitorCommands;
private readonly IMyAtmMeasurementCommands measurementCommands;
private readonly IMyAtmOperationalCommands operationalCommands;
```

Then initialize them from the existing `IDBClient dbClient` constructor argument:

```csharp
monitorQueries = dbClient;
ruleQueries = dbClient;
monitorCommands = dbClient;
measurementCommands = dbClient;
operationalCommands = dbClient;
```

- [ ] **Step 2: Replace direct write calls in monitor catalog paths**

In `MyAtmApiMonitors.cs`, replace:

```csharp
dbClient.WriteMonitorList(...)
dbClient.SetMonitorOffline(...)
dbClient.HandleException(...)
```

with:

```csharp
monitorCommands.WriteMonitorList(...)
monitorCommands.SetMonitorOffline(...)
operationalCommands.HandleException(...)
```

Replace read calls with:

```csharp
monitorQueries.ReadMonitorList(...)
ruleQueries.ReadRules(...)
ruleQueries.ReadAlertContacts(...)
```

- [ ] **Step 3: Replace direct measurement writes**

In `MyAtmApiDustLevels.cs`, replace:

```csharp
dbClient.InsertDustDtos(dtos);
dbClient.WriteLatestTimestamp(monitor.SerialId, (DateTime)lastDataTime!, period);
```

with:

```csharp
measurementCommands.InsertDustDtos(dtos);
monitorCommands.WriteLatestTimestamp(monitor.SerialId, (DateTime)lastDataTime!, period);
```

Replace read/rule calls with `monitorQueries` and `ruleQueries`.

- [ ] **Step 4: Replace direct accessory writes**

In `MyAtmApiAccessoryInfo.cs`, replace:

```csharp
dbClient.InsertAccessoryDto(dto);
dbClient.HandleException("StoreAccessoryInfo", e);
```

with:

```csharp
measurementCommands.InsertAccessoryDto(dto);
operationalCommands.HandleException("StoreAccessoryInfo", e);
```

- [ ] **Step 5: Replace rule-processing reads and writes**

In `MyAtmApiRuleProcessing.cs`, replace:

```csharp
dbClient.ReadAlertContacts(...)
dbClient.HasOpenNotification(...)
dbClient.WriteNotification(...)
dbClient.WriteNotificationAudit(...)
dbClient.UpdateAlertRule(...)
```

with `ruleQueries` for reads and `operationalCommands` for writes.

- [ ] **Step 6: Run current MyAtm API tests**

Run:

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~TestMyAtmApi|FullyQualifiedName~TestRules|FullyQualifiedName~TestMyAtmApiMonitors|FullyQualifiedName~TestMyAtmApiExceptions"
```

Expected: PASS. This proves the CQRS-lite interface split did not change observable behavior.

- [ ] **Step 7: Commit**

Run:

```bash
git add myatmmonitor/MyAtmMonitor/api myatmmonitor/MyAtmMonitorTests
git commit -m "refactor: route myatm api through query and command interfaces"
```

---

### Task 6: Final Verification And Sonar Check

**Files:**
- Verify: full solution
- Verify: Sonar dependency and duplication behavior

- [ ] **Step 1: Run formatting and local verification**

Run:

```bash
git diff --check
dotnet build rvt-monitors.sln
dotnet test rvt-monitors.sln --no-build
```

Expected: all pass. Mapperly-generated code should be build-time only and should not appear as committed source.

- [ ] **Step 2: Verify Mapperly did not leak**

Run:

```bash
rg -n "Riok.Mapperly|Mapperly" --glob '*.csproj' --glob '*.cs'
```

Expected:

- `myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj`
- `myatmmonitor/MyAtmMonitor/api/db/Mapping/MyAtmDbMapper.cs`
- `myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmDependencyBoundaryTests.cs`
- `myatmmonitor/MyAtmMonitorTests/Mapping/MyAtmDbMapperTests.cs`

No AirQ, Omnidots, Svantek, or common project should reference Mapperly.

- [ ] **Step 3: Run Sonar analysis with tests if token is available**

Run only when the local environment has `SONAR_TOKEN` and the user wants a Sonar refresh:

```bash
SONAR_ORGANIZATION=aileron-forward SONAR_PROJECT_KEY=aileron-forward_rvt-monitors RUN_TESTS=true ./scripts/run-sonarqube-analysis.sh
```

Expected: analysis completes. Useful things to inspect afterward:

- MyAtm duplication around `DBClient` should drop slightly after mapper extraction.
- New-code coverage should include mapper and DB duplicate tests.
- No security or reliability regressions.

- [ ] **Step 4: Commit final cleanups**

If verification required follow-up fixes, commit them:

```bash
git add myatmmonitor/MyAtmMonitor myatmmonitor/MyAtmMonitorTests
git commit -m "test: cover myatm cqrs mapper refactor"
```

---

## Extended Useful Tests

The implementation is not complete until these tests exist and pass:

- `MyAtmDbMapperTests.ToDustMonitorDto_MapsMonitorEntityDefaults`
  - Protects nullable/default conversions from EF rows into `DustMonitorDto`.
- `MyAtmDbMapperTests.UpdateMonitorEntity_DoesNotOverwriteFleetOrLatestTimestamps`
  - Protects the current behavior where `WriteFleetNr` and `WriteLatestTimestamp` own those fields.
- `MyAtmDbMapperTests.ToDustLevelEntity_MapsMeasurementDto`
  - Protects dust measurement field mapping.
- `TestDBClient.InsertDustDto_IgnoresDuplicateRowsInSingleBatch`
  - Existing coverage, keep it green.
- `TestDBClient.InsertDustDto_IgnoresRowsAlreadyPresentInDatabase`
  - Adds coverage for a second insert call with the same key.
- `MyAtmDependencyBoundaryTests.MapperlyPackageReference_IsOnlyUsedByMonitorAppProjects`
  - Enforces the current project requirement that Mapperly stays inside monitor app projects and out of `rvt-monitor-common`.
- Existing `TestMyAtmApi*` and `TestRules*`
  - Proves the facade-preserving CQRS-lite split does not change monitor workflows.

## Deliberate Non-Goals

- Do not introduce full CQRS, MediatR, event sourcing, or a new database.
- Do not apply Mapperly to `rvt-monitor-common`; monitor app projects may use app-local Mapperly mappers for simple DTO/entity conversion.
- Do not move vendor JSON-to-DTO constructors into Mapperly.
- Do not rewrite notification/rule state machines as part of this pass.
- Do not remove `IDBClient` until all tests and production constructors have been migrated to the narrower interfaces.

## Self-Review

- Spec coverage: The plan covers adding Mapperly only in MyAtm, splitting writes first, layering CQRS-lite query/command interfaces, and extending tests.
- Placeholder scan: No `TBD`, `TODO`, or vague "add tests" steps remain; concrete file paths, snippets, commands, and expected outcomes are included.
- Type consistency: Interface names, method names, and file paths match current MyAtm code: `DBClient`, `IDBClient`, `DustMonitorDto`, `DustDto`, `AccessoryInfoDto`, `MonitorEntity`, `MyAtmDustLevelEntity`, and `MyAtmAccessoryInfoEntity`.
