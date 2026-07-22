# Reporting Monitor Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrate the reporting service as an Omnidots-style monitor application using the shared host and PostgreSQL-first EF Core data ports while preserving its report-generation behavior.

**Architecture:** `ReportingMonitor` is the executable monitor host and owns API routes, use cases, scheduling dispatch, EF Core mappings, and data-port implementations. The imported core, PDF, storage, and messaging projects remain vendor-neutral boundaries beneath `reportingmonitor/`; the core orchestrator consumes focused query/command/lock ports rather than a broad reporting repository.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, Quartz, EF Core 10, Npgsql EF Core provider, Riok.Mapperly analyzer, PostgreSQL/Timescale, QuestPDF, Azure Blob Storage, SendGrid, xUnit, Docker Compose.

## Global Constraints

- Use Omnidots as the code-style reference: short `Program.cs`, `api/UseCases`, `api/db`, composition extensions, file-scoped namespaces, summary comments, narrow focused types, and focused tests.
- Keep .NET 10 throughout; configure `Riok.Mapperly` only in `reportingmonitor/ReportingMonitor/ReportingMonitor.csproj` with `PrivateAssets="all"` and `OutputItemType="Analyzer"`.
- Use `MonitorHost`, `MonitorScheduler`, `MonitorOpenTelemetry`, `MonitorDbContextBase`, `MonitorDbContextOptionsFactory`, and the shared PostgreSQL provider guard; do not add reporting policy to `rvt-monitor-common`.
- Use `ConnectionStrings__DefaultConnection`, `RVT__DATABASE_PROVIDER=PostgreSql`, and flat `RVT__…` variables; never commit a secret or production connection string.
- New application/API/use-case code depends only on narrow reporting ports, never `ReportingMonitorContext`, EF entities, `NpgsqlConnection`, `NpgsqlDataSource`, or a vendor adapter concrete type.
- Keep report API paths and payload contracts: `/internal/reports/run-scheduled`, `/internal/reports/rules/{reportRuleId}/generate`, and `/internal/reports/one-time`.
- Every I/O path is `async`, accepts/propagates `CancellationToken`, and faults the invoking API/Quartz/one-shot operation on unexpected persistence, storage, or delivery exceptions.
- Keep external packages in the reporting project that owns their adapter. Do not copy `.git`, `bin`, `obj`, `.DS_Store`, or generated sample PDFs from the source tree.
- Preserve unrelated working-tree changes; stage only files listed in each task.

---

## File Structure

```text
reportingmonitor/
├── ReportingMonitor/
│   ├── Program.cs
│   ├── ReportingMonitor.csproj
│   ├── Dockerfile
│   ├── appsettings.json
│   ├── api/
│   │   ├── ReportingMonitorApi.cs
│   │   ├── ReportingMonitorJobDispatcher.cs
│   │   ├── ReportingMonitorJobRunner.cs
│   │   ├── ReportingMonitorOptions.cs
│   │   ├── ReportingMonitorServices.cs
│   │   ├── Security/InternalApiKeyFilter.cs
│   │   ├── UseCases/GenerateOneTimeReportHandler.cs
│   │   ├── UseCases/GenerateRuleReportHandler.cs
│   │   ├── UseCases/GenerateScheduledReportsHandler.cs
│   │   └── db/
│   │       ├── EntityFramework/ReportingDbMapper.cs
│   │       ├── EntityFramework/ReportingEntities.cs
│   │       ├── EntityFramework/ReportingMonitorContext.cs
│   │       ├── EntityFramework/ReportingReadModels.cs
│   │       └── ReportingDbClient.cs
├── ReportingMonitorTests/
│   ├── Architecture/ReportingDependencyBoundaryTests.cs
│   ├── EntityFramework/ReportingModelMappingTests.cs
│   ├── Mapping/ReportingDbMapperTests.cs
│   ├── TestReportingApiEndpoints.cs
│   ├── TestReportingDbClient.cs
│   ├── TestReportingDispatcher.cs
│   ├── TestReportingFixture.cs
│   └── testdata/{create,reset}.postgres.sql
├── Rvt.Reporting.Core/
│   └── Reports/Ports/{IReportingRuleQueries,IReportingDataQueries,IReportingHealthQueries,IReportingGenerationLocks,IReportingGenerationCommands}.cs
├── Rvt.Reporting.Pdf/
├── Rvt.Reporting.Storage/
├── Rvt.Reporting.Messaging/
├── database/postgres/reporting_service_prerequisites_20260625.sql
└── README.md
```

`Rvt.Reporting.Data` and `Rvt.Reporting.Service` are not copied as projects: their behavior moves into `ReportingMonitor/api/db` and `ReportingMonitor/api`, respectively. `Rvt.Reporting.Core`, `Rvt.Reporting.Pdf`, `Rvt.Reporting.Storage`, and `Rvt.Reporting.Messaging` retain their current namespaces and focused responsibilities.

---

### Task 1: Import the reporting domain and establish an integrated baseline

**Files:**

- Create: `reportingmonitor/Rvt.Reporting.Core/**` from source `src/Rvt.Reporting.Core/**`
- Create: `reportingmonitor/Rvt.Reporting.Pdf/**` from source `src/Rvt.Reporting.Pdf/**`
- Create: `reportingmonitor/Rvt.Reporting.Storage/**` from source `src/Rvt.Reporting.Storage/**`
- Create: `reportingmonitor/Rvt.Reporting.Messaging/**` from source `src/Rvt.Reporting.Messaging/**`
- Create: `reportingmonitor/ReportingMonitorTests/Core/**` from source `tests/Rvt.Reporting.Core.Tests/**`
- Create: `reportingmonitor/database/postgres/reporting_service_prerequisites_20260625.sql`
- Create: `reportingmonitor/README.md`
- Modify: `reportingmonitor/Rvt.Reporting.Pdf/Rvt.Reporting.Pdf.csproj`
- Modify: `reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj`
- Modify: `reportingmonitor/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj`

**Interfaces:**

- Consumes: The source project’s `Rvt.Reporting.Core` records and adapter contracts.
- Produces: The unchanged report-domain API used by the new host: `IReportGenerationService`, `IReportPdfRenderer`, `IReportStorage`, `IReportMessageSender`, `ICustomerLogoProvider`, `IReportNarrativeProvider`, `ReportPeriodCalculator`, and `OneTimeReportValidator`.

- [ ] **Step 1: Write failing import-contract tests**

Create `reportingmonitor/ReportingMonitorTests/Core/ImportedReportingContractTests.cs`:

```csharp
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;
using Rvt.Reporting.Core.Scheduling;

namespace ReportingMonitorTests.Core;

public sealed class ImportedReportingContractTests
{
    [Fact]
    public void ImportedDomain_ExposesScheduledOneTimeAndAdapterContracts()
    {
        Assert.NotNull(typeof(IReportGenerationService));
        Assert.NotNull(typeof(IReportPdfRenderer));
        Assert.NotNull(typeof(IReportStorage));
        Assert.NotNull(typeof(IReportMessageSender));
        Assert.NotNull(typeof(ICustomerLogoProvider));
        Assert.NotNull(typeof(IReportNarrativeProvider));
        Assert.Equal(FrequencyType.OneTime, (FrequencyType)5);
        Assert.NotNull(ReportPeriodCalculator.CreatePeriods(new ReportRule { Frequency = FrequencyType.Daily }, DateTimeOffset.UtcNow));
    }
}
```

- [ ] **Step 2: Run the import-contract test to verify it fails**

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter FullyQualifiedName~ImportedReportingContractTests --nologo`

Expected: FAIL because the reporting projects and test project do not yet exist.

- [ ] **Step 3: Copy only retained source files and create the test project**

Copy the four retained production projects and their source tests into the paths above. Create `ReportingMonitorTests.csproj` as a net10 test project with project references to Core, Pdf, Storage, Messaging, and `Rvt.Monitor.Common`; add its host-project reference in Task 3. Remove source-specific test-project references to the retired Data and Service projects. Ensure package versions remain as imported: QuestPDF `2026.6.0`, Azure Identity `1.21.0`, Azure Storage Blobs `12.29.1`, SendGrid `9.29.3`, xUnit `2.9.3`, and Microsoft.NET.Test.Sdk `17.14.1`.

Copy the prerequisite SQL into `reportingmonitor/database/postgres/`. Write `reportingmonitor/README.md` with the integrated build/test command and required non-secret configuration names. Do not copy generated PDFs or filesystem metadata.

- [ ] **Step 4: Run core/import tests to verify they pass**

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter "FullyQualifiedName~ImportedReportingContractTests|FullyQualifiedName~ReportPeriodCalculatorTests|FullyQualifiedName~OneTimeReportValidatorTests" --nologo`

Expected: PASS; the imported core contract, date windows, and one-time validator build inside this repository.

- [ ] **Step 5: Commit the baseline import**

```bash
git add reportingmonitor/Rvt.Reporting.Core reportingmonitor/Rvt.Reporting.Pdf reportingmonitor/Rvt.Reporting.Storage reportingmonitor/Rvt.Reporting.Messaging reportingmonitor/ReportingMonitorTests/Core reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj reportingmonitor/database reportingmonitor/README.md
git commit -m "feat: import reporting domain and adapters"
```

### Task 2: Define narrow reporting data ports and refactor core orchestration

**Files:**

- Create: `reportingmonitor/Rvt.Reporting.Core/Reports/Ports/IReportingRuleQueries.cs`
- Create: `reportingmonitor/Rvt.Reporting.Core/Reports/Ports/IReportingDataQueries.cs`
- Create: `reportingmonitor/Rvt.Reporting.Core/Reports/Ports/IReportingHealthQueries.cs`
- Create: `reportingmonitor/Rvt.Reporting.Core/Reports/Ports/IReportingGenerationLocks.cs`
- Create: `reportingmonitor/Rvt.Reporting.Core/Reports/Ports/IReportingGenerationCommands.cs`
- Modify: `reportingmonitor/Rvt.Reporting.Core/Reports/ReportGenerationContracts.cs`
- Modify: `reportingmonitor/Rvt.Reporting.Core/Reports/ReportGenerationService.cs`
- Modify: `reportingmonitor/ReportingMonitorTests/Core/ReportGenerationServiceTests.cs`

**Interfaces:**

- Consumes: Imported models `ReportRule`, `SiteReportData`, `ReportPeriod`, `GeneratedReportSaveRequest`, and `RuleGenerationLock`.
- Produces: Five focused interfaces and a core orchestrator that depends on those interfaces only.

- [ ] **Step 1: Write a failing orchestration-port test**

Replace the source fake repository with separate fakes in `ReportGenerationServiceTests.cs` and add this test:

```csharp
[Fact]
public async Task GenerateRuleAsync_UsesSeparateRuleDataLockAndCommandPorts()
{
    var rules = new FakeRuleQueries { Rule = DailyRule() };
    var data = new FakeDataQueries { Site = Site() };
    var locks = new FakeGenerationLocks();
    var commands = new FakeGenerationCommands();
    var service = CreateService(rules, data, locks, commands);

    await service.GenerateRuleAsync(rules.Rule!.Id, new DateTimeOffset(2026, 6, 30, 8, 0, 0, TimeSpan.Zero), CancellationToken.None);

    Assert.Equal(rules.Rule.Id, Assert.Single(rules.RequestedRuleIds));
    Assert.Single(data.Requests);
    Assert.Single(locks.Requests);
    Assert.Single(commands.SaveRequests);
}
```

- [ ] **Step 2: Run the orchestration-port test to verify it fails**

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter FullyQualifiedName~GenerateRuleAsync_UsesSeparateRuleDataLockAndCommandPorts --nologo`

Expected: FAIL because the split-port constructor and fake-port test helpers do not exist.

- [ ] **Step 3: Add the five explicit ports and change the core constructor**

Define the exact interfaces:

```csharp
public interface IReportingRuleQueries
{
    Task<IReadOnlyList<ReportRule>> GetDueReportRulesAsync(DateTimeOffset maxLastGeneratedUtc, CancellationToken cancellationToken);
    Task<ReportRule?> GetReportRuleAsync(Guid reportRuleId, CancellationToken cancellationToken);
}

public interface IReportingDataQueries
{
    Task<SiteReportData> LoadSiteReportDataAsync(Guid siteId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);
}

public interface IReportingGenerationLocks
{
    Task<RuleGenerationLock?> TryAcquireAsync(Guid reportRuleId, ReportPeriod period, CancellationToken cancellationToken);
}

public interface IReportingGenerationCommands
{
    Task<GeneratedReport> SaveGeneratedReportAsync(GeneratedReportSaveRequest request, CancellationToken cancellationToken);
}

public interface IReportingHealthQueries
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
```

Place the interfaces in `Rvt.Reporting.Core/Reports/Ports/` under the `Rvt.Reporting.Core.Reports` namespace so the Core orchestrator references no host project. Delete `IReportingRepository` after all source references are replaced. Inject `IReportingRuleQueries`, `IReportingDataQueries`, `IReportingGenerationLocks`, and `IReportingGenerationCommands` into `ReportGenerationService`; replace each former repository call with its owning narrow port.

- [ ] **Step 4: Run all core orchestration tests to verify they pass**

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter FullyQualifiedName~ReportGenerationServiceTests --nologo`

Expected: PASS; scheduled, rule-specific, and one-time report generation retain their observable save requests while using split ports.

- [ ] **Step 5: Commit the narrow-port refactor**

```bash
git add reportingmonitor/Rvt.Reporting.Core/Reports reportingmonitor/ReportingMonitorTests/Core/ReportGenerationServiceTests.cs
git commit -m "refactor: split reporting data ports"
```

### Task 3: Create the Omnidots-style monitor host, dispatcher, and configuration

**Files:**

- Create: `reportingmonitor/ReportingMonitor/ReportingMonitor.csproj`
- Create: `reportingmonitor/ReportingMonitor/Program.cs`
- Create: `reportingmonitor/ReportingMonitor/api/ReportingMonitorServices.cs`
- Create: `reportingmonitor/ReportingMonitor/api/ReportingMonitorJobRunner.cs`
- Create: `reportingmonitor/ReportingMonitor/api/ReportingMonitorJobDispatcher.cs`
- Create: `reportingmonitor/ReportingMonitor/api/ReportingMonitorOptions.cs`
- Create: `reportingmonitor/ReportingMonitor/appsettings.json`
- Create: `reportingmonitor/ReportingMonitor/Dockerfile`
- Create: `reportingmonitor/ReportingMonitorTests/TestReportingDispatcher.cs`
- Create: `reportingmonitor/ReportingMonitorTests/TestReportingOptions.cs`

**Interfaces:**

- Consumes: `MonitorHost.RunAsync<TDispatcher>`, `IMonitorJobDispatcher`, `IReportGenerationService`, and the focused data ports.
- Produces: Job name `GenerateScheduledReports`; `ReportingMonitorJobDispatcher.RunAsync(string, CancellationToken)`; validated `ReportingMonitorOptions`.

- [ ] **Step 1: Write failing dispatcher and options tests**

Create `TestReportingDispatcher.cs`:

```csharp
[Fact]
public async Task RunAsync_GenerateScheduledReports_UsesCurrentUtcTimeAndReturnsZero()
{
    var handler = new RecordingScheduledReportsHandler();
    var dispatcher = new ReportingMonitorJobDispatcher(handler);

    var result = await dispatcher.RunAsync("GenerateScheduledReports", CancellationToken.None);

    Assert.Equal(0, result);
    Assert.Equal(1, handler.CallCount);
}

[Fact]
public async Task RunAsync_UnknownJob_ReturnsTwo()
{
    var result = await new ReportingMonitorJobDispatcher(new RecordingScheduledReportsHandler())
        .RunAsync("GenerateAllReports", CancellationToken.None);

    Assert.Equal(2, result);
}
```

Create `TestReportingOptions.cs` with a test that binds `RVT__BLOB_REPORT_CONTAINER_NAME=pdfreports` and `RVT__AI_SUMMARY_TIMEOUT_SECONDS=8` and asserts the options contain those exact values.

- [ ] **Step 2: Run the host tests to verify they fail**

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter "FullyQualifiedName~TestReportingDispatcher|FullyQualifiedName~TestReportingOptions" --nologo`

Expected: FAIL because the host project, dispatcher, handler port, and options type do not exist.

- [ ] **Step 3: Implement host composition in Omnidots style**

Use this `Program.cs` shape:

```csharp
using ReportingMonitor.Api;
using Rvt.Monitor.Common.Hosting;

return await MonitorHost.RunAsync<ReportingMonitorJobDispatcher>(
    args,
    "ReportingMonitor",
    ReportingMonitorJobRunner.GetJobName,
    (jobName, services) => services.GetRequiredService<ReportingMonitorJobDispatcher>().RunAsync(jobName, CancellationToken.None),
    app => app.MapReportingMonitorApi(),
    configureServices: services => services.AddReportingMonitor());
```

Make `ReportingMonitorJobDispatcher` implement `IMonitorJobDispatcher`, expose `SupportedJobNames` containing only `GenerateScheduledReports`, and call `GenerateScheduledReportsHandler.HandleAsync(DateTimeOffset.UtcNow, cancellationToken)`. Make `ReportingMonitorJobRunner.GetJobName` accept `--job` and `RVT__MONITOR_JOB` exactly as the Omnidots runner does. In `ReportingMonitor.csproj`, reference Core/Pdf/Storage/Messaging/Common and add EF Core `10.0.4`, Npgsql EF provider `10.0.2`, and Mapperly `4.3.1` with analyzer-only metadata.

Bind reporting-only settings from the `RVT` section into `ReportingMonitorOptions`; configure `ConnectionStrings:DefaultConnection`, `MonitorApi`, and `MonitorScheduler` in `appsettings.json`. The default scheduler job is disabled and describes `GenerateScheduledReports`; its enabled cron is `0 0 1-6 ? * * *` in UTC. The Dockerfile follows the Omnidots root-context publish pattern and exposes port 8080.

- [ ] **Step 4: Run host tests and build to verify they pass**

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter "FullyQualifiedName~TestReportingDispatcher|FullyQualifiedName~TestReportingOptions" --nologo`

Expected: PASS.

Run: `dotnet build reportingmonitor/ReportingMonitor/ReportingMonitor.csproj --nologo`

Expected: successful build with zero warnings and zero errors.

- [ ] **Step 5: Commit the shared-host shell**

```bash
git add reportingmonitor/ReportingMonitor reportingmonitor/ReportingMonitorTests/TestReportingDispatcher.cs reportingmonitor/ReportingMonitorTests/TestReportingOptions.cs
git commit -m "feat: add reporting monitor host and dispatcher"
```

### Task 4: Map the reporting schema with PostgreSQL-first EF Core and Mapperly

**Files:**

- Create: `reportingmonitor/ReportingMonitor/api/db/EntityFramework/ReportingMonitorContext.cs`
- Create: `reportingmonitor/ReportingMonitor/api/db/EntityFramework/ReportingEntities.cs`
- Create: `reportingmonitor/ReportingMonitor/api/db/EntityFramework/ReportingReadModels.cs`
- Create: `reportingmonitor/ReportingMonitor/api/db/EntityFramework/ReportingDbMapper.cs`
- Create: `reportingmonitor/ReportingMonitorTests/EntityFramework/ReportingModelMappingTests.cs`
- Create: `reportingmonitor/ReportingMonitorTests/Mapping/ReportingDbMapperTests.cs`

**Interfaces:**

- Consumes: `MonitorDbContextBase`, `MonitorDbOptions`, `MonitorDbContextOptionsFactory`, and the Core report records.
- Produces: `ReportingMonitorContext`, entity sets for report writes, keyless read sets for report projections, and `ReportingDbMapper` mappings.

- [ ] **Step 1: Write failing EF mapping tests**

Create `ReportingModelMappingTests.cs`:

```csharp
[Fact]
public void Model_MapsReportWritesAndReadViewsToCanonicalPostgreSqlNames()
{
    using var context = ReportingContextFactory.CreatePostgreSqlContext();

    Assert.Equal("report_rule", context.Model.FindEntityType(typeof(ReportRuleEntity))!.GetTableName());
    Assert.Equal("report", context.Model.FindEntityType(typeof(ReportEntity))!.GetTableName());
    Assert.Equal("report_sent", context.Model.FindEntityType(typeof(ReportSentEntity))!.GetTableName());
    Assert.Null(context.Model.FindEntityType(typeof(SiteSearchRow))!.FindPrimaryKey());
    Assert.Equal("site_search", context.Model.FindEntityType(typeof(SiteSearchRow))!.GetViewName());
}
```

Create `ReportingDbMapperTests.cs`:

```csharp
[Fact]
public void ToReportRule_MapsScalarRuleValuesAndRecipients()
{
    var entity = new ReportRuleEntity { Id = Guid.NewGuid(), SiteId = Guid.NewGuid(), Frequency = 1, IsHiddenSystemRule = false };
    var result = ReportingDbMapper.ToReportRule(entity, [new ReportRecipient(entity.UserId, "report@example.com")]);

    Assert.Equal(entity.Id, result.Id);
    Assert.Equal(FrequencyType.Daily, result.Frequency);
    Assert.Equal("report@example.com", Assert.Single(result.Recipients).Email);
}
```

- [ ] **Step 2: Run EF mapping tests to verify they fail**

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter "FullyQualifiedName~ReportingModelMappingTests|FullyQualifiedName~ReportingDbMapperTests" --nologo`

Expected: FAIL because the context, entities, keyless rows, and mapper do not exist.

- [ ] **Step 3: Implement the EF context, entities, keyless read models, and mapper**

Derive `ReportingMonitorContext` from `MonitorDbContextBase`. Add `DbSet<ReportRuleEntity>`, `DbSet<ReportEntity>`, and `DbSet<ReportSentEntity>` plus keyless sets `SiteSearchRows`, `MonitorReportRows`, `MonitorWindowRows`, `ReportRecipientRows`, and read models for notifications and alert rules. In `OnMonitorModelCreating`, map PostgreSQL snake_case names, `report_rule.is_hidden_system_rule`, `report.report_date`, `report.report_from`, `report.report_to`, `report_sent.send_time`, `report_sent.error_message`, the `site_search` view, the `monitor_report` view, and the quoted ASP.NET Identity table/columns exactly.

Map only simple entity-to-domain scalar conversions through Mapperly:

```csharp
[Mapper]
internal static partial class ReportingDbMapper
{
    [MapProperty(nameof(ReportRuleEntity.Frequency), nameof(ReportRule.Frequency))]
    private static partial ReportRule ToReportRuleValues(ReportRuleEntity source);

    public static ReportRule ToReportRule(ReportRuleEntity source, IReadOnlyList<ReportRecipient> recipients) =>
        ToReportRuleValues(source) with { Recipients = recipients };
}
```

Keep monitor aggregation, ownership-window selection, notification matching, graph bucket selection, and hidden-one-time reuse manual in `ReportingDbClient`, because they are non-trivial reporting business logic.

- [ ] **Step 4: Run EF mapping tests to verify they pass**

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter "FullyQualifiedName~ReportingModelMappingTests|FullyQualifiedName~ReportingDbMapperTests" --nologo`

Expected: PASS; all write entities and keyless read models map to their expected PostgreSQL objects.

- [ ] **Step 5: Commit the EF model boundary**

```bash
git add reportingmonitor/ReportingMonitor/api/db/EntityFramework reportingmonitor/ReportingMonitorTests/EntityFramework reportingmonitor/ReportingMonitorTests/Mapping
git commit -m "feat: map reporting schema with ef core"
```

### Task 5: Implement focused reporting rule and report-data queries

**Files:**

- Modify: `reportingmonitor/Rvt.Reporting.Core/Reports/Ports/IReportingRuleQueries.cs`
- Modify: `reportingmonitor/Rvt.Reporting.Core/Reports/Ports/IReportingDataQueries.cs`
- Create: `reportingmonitor/ReportingMonitor/api/db/ReportingDbClient.cs`
- Create: `reportingmonitor/ReportingMonitorTests/TestReportingDbClient.cs`
- Create: `reportingmonitor/ReportingMonitorTests/testdata/create.postgres.sql`
- Create: `reportingmonitor/ReportingMonitorTests/testdata/reset.postgres.sql`

**Interfaces:**

- Consumes: `ReportingMonitorContext`, `ReportingDbMapper`, read models, and Core `IReportingRuleQueries`/`IReportingDataQueries`.
- Produces: Asynchronous, read-only due-rule/requested-rule/site-report-data queries that preserve imported report projections.

- [ ] **Step 1: Write failing query tests**

Add these tests to `TestReportingDbClient.cs` using the repository’s PostgreSQL fixture and seeded test schema:

```csharp
[Fact]
public async Task GetDueReportRulesAsync_ExcludesHiddenDeletedAndNotDueRules()
{
    await Fixture.SeedReportRulesAsync(due: true, hidden: true, deleted: true, notDue: true);

    var rules = await Fixture.Client.GetDueReportRulesAsync(new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

    Assert.Single(rules);
    Assert.False(rules[0].IsHiddenSystemRule);
}

[Fact]
public async Task LoadSiteReportDataAsync_ClampsMonitorDataToEffectiveOwnershipWindow()
{
    var siteId = await Fixture.SeedSiteWithTransferredMonitorAsync();

    var site = await Fixture.Client.LoadSiteReportDataAsync(siteId, Fixture.FromUtc, Fixture.ToUtc, CancellationToken.None);

    var monitor = Assert.Single(site.Monitors);
    Assert.All(monitor.NoiseDailyAverage, point => Assert.InRange(point.MeasuredAt, monitor.EffectiveFrom, monitor.EffectiveTo));
}
```

- [ ] **Step 2: Run query tests to verify they fail**

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter "FullyQualifiedName~GetDueReportRulesAsync|FullyQualifiedName~LoadSiteReportDataAsync_Clamps" --nologo`

Expected: FAIL because `ReportingDbClient` does not implement the query ports or test schema does not expose reporting fixtures.

- [ ] **Step 3: Implement read-only query ports**

Implement `ReportingDbClient : IReportingRuleQueries, IReportingDataQueries`. Use `AsNoTracking()` for all read projections. `GetDueReportRulesAsync` filters out `Deleted`, `IsHiddenSystemRule`, `Off`, and `OneTime`, applies the imported due-time condition, and loads recipient emails through the report-user/identity relation. `GetReportRuleAsync` returns null for a missing rule and loads recipients with the same projection.

`LoadSiteReportDataAsync` reads a single `site_search` row, joins monitor-report data to deployment/contract ownership windows, and clamps every measurement/notification/alert-rule aggregation to both requested UTC range and the monitor’s effective ownership interval. Build domain `SiteReportData` and `MonitorReportData` manually from the EF results. Preserve imported bucket selection: dust hourly/daily, noise hourly/daily/site, and vibration daily peak. Use `DateTimeOffset` consistently; do not use local time.

Seed `create.postgres.sql` with the smallest schema satisfying the mapped reporting tables/views and fixture data; `reset.postgres.sql` clears only those test rows. Keep schema setup inside the fixture’s generated schema.

- [ ] **Step 4: Run query tests to verify they pass**

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter "FullyQualifiedName~GetDueReportRulesAsync|FullyQualifiedName~LoadSiteReportDataAsync_Clamps" --nologo`

Expected: PASS; query behavior excludes hidden rules and never includes measurements outside ownership.

- [ ] **Step 5: Commit the read-query adapter**

```bash
git add reportingmonitor/ReportingMonitor/api/db/Queries reportingmonitor/ReportingMonitor/api/db/ReportingDbClient.cs reportingmonitor/ReportingMonitorTests/TestReportingDbClient.cs reportingmonitor/ReportingMonitorTests/testdata
git commit -m "feat: add reporting ef core read queries"
```

### Task 6: Implement advisory locking and atomic generated-report persistence

**Files:**

- Modify: `reportingmonitor/Rvt.Reporting.Core/Reports/Ports/IReportingGenerationLocks.cs`
- Modify: `reportingmonitor/Rvt.Reporting.Core/Reports/Ports/IReportingGenerationCommands.cs`
- Modify: `reportingmonitor/ReportingMonitor/api/db/ReportingDbClient.cs`
- Modify: `reportingmonitor/ReportingMonitorTests/TestReportingDbClient.cs`

**Interfaces:**

- Consumes: `ReportPeriod`, `GeneratedReportSaveRequest`, write entities, and `ReportingMonitorContext.Database`.
- Produces: `TryAcquireAsync` advisory lock and `SaveGeneratedReportAsync` transactionally consistent metadata persistence.

- [ ] **Step 1: Write failing lock and transaction tests**

Add these tests:

```csharp
[Fact]
public async Task TryAcquireAsync_ReturnsNullWhenAnotherClientOwnsTheSameRulePeriodLock()
{
    await using var first = await Fixture.Client.TryAcquireAsync(Fixture.RuleId, Fixture.DailyPeriod, CancellationToken.None);
    await using var second = await Fixture.SecondClient.TryAcquireAsync(Fixture.RuleId, Fixture.DailyPeriod, CancellationToken.None);

    Assert.NotNull(first);
    Assert.Null(second);
}

[Fact]
public async Task SaveGeneratedReportAsync_RollsBackRuleReportAndRecipientRowsWhenARecipientWriteFails()
{
    var request = Fixture.GeneratedReportRequest(withInvalidRecipient: true);

    await Assert.ThrowsAsync<DbUpdateException>(() => Fixture.Client.SaveGeneratedReportAsync(request, CancellationToken.None));

    Assert.Equal(0, await Fixture.CountAsync("report_rule"));
    Assert.Equal(0, await Fixture.CountAsync("report"));
    Assert.Equal(0, await Fixture.CountAsync("report_sent"));
}
```

- [ ] **Step 2: Run lock and transaction tests to verify they fail**

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter "FullyQualifiedName~TryAcquireAsync_ReturnsNull|FullyQualifiedName~SaveGeneratedReportAsync_RollsBack" --nologo`

Expected: FAIL because the lock and command ports have no implementations.

- [ ] **Step 3: Implement the vendor boundary and EF transaction**

Implement `IReportingGenerationLocks.TryAcquireAsync`. Form a deterministic lock key from report-rule ID, frequency, period start, and period end. Issue the parameterized PostgreSQL `pg_try_advisory_lock(hashtextextended(...))` command through the EF Core database connection, retain that same opened connection while the returned `RuleGenerationLock` exists, and release it with parameterized `pg_advisory_unlock` in `DisposeAsync`. This is the only allowed non-LINQ provider command in the reporting data adapter.

Implement `IReportingGenerationCommands.SaveGeneratedReportAsync` as one EF transaction:

1. For one-time generation, find or create the unique hidden rule for the site/frequency pair and update its report name.
2. Add the `ReportEntity` using the resolved rule ID.
3. Add one `ReportSentEntity` per delivery result.
4. Update `LastGenerated` only when `UpdateLastGenerated` is true.
5. Call `SaveChangesAsync`, then `CommitAsync`; roll back and rethrow all failures.

Use `ReportingDbMapper` only for simple values; retain the hidden-rule selection and transaction behavior as explicit data-boundary code.

- [ ] **Step 4: Run lock and transaction tests to verify they pass**

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter "FullyQualifiedName~TryAcquireAsync_ReturnsNull|FullyQualifiedName~SaveGeneratedReportAsync_RollsBack" --nologo`

Expected: PASS; the second client cannot acquire the active lock and a failed recipient write leaves no reporting metadata.

- [ ] **Step 5: Commit the write/lock adapter**

```bash
git add reportingmonitor/ReportingMonitor/api/db/Locks reportingmonitor/ReportingMonitor/api/db/Commands reportingmonitor/ReportingMonitor/api/db/ReportingDbClient.cs reportingmonitor/ReportingMonitorTests/TestReportingDbClient.cs
git commit -m "feat: add atomic reporting persistence"
```

### Task 7: Add focused report-generation use cases and protected API endpoints

**Files:**

- Create: `reportingmonitor/ReportingMonitor/api/UseCases/GenerateScheduledReportsHandler.cs`
- Create: `reportingmonitor/ReportingMonitor/api/UseCases/GenerateRuleReportHandler.cs`
- Create: `reportingmonitor/ReportingMonitor/api/UseCases/GenerateOneTimeReportHandler.cs`
- Create: `reportingmonitor/ReportingMonitor/api/ReportingMonitorApi.cs`
- Create: `reportingmonitor/ReportingMonitor/api/Security/InternalApiKeyFilter.cs`
- Create: `reportingmonitor/ReportingMonitorTests/TestReportingApiEndpoints.cs`

**Interfaces:**

- Consumes: `IReportGenerationService`, `ReportingMonitorOptions`, `IReportingHealthQueries`, and ASP.NET Core minimal API abstractions.
- Produces: Existing internal API routes, fixed-time protected endpoint filter, `/liveness`, and `/readiness`.

- [ ] **Step 1: Write failing API tests**

Create endpoint tests:

```csharp
[Fact]
public async Task RunScheduled_WithMissingConfiguredKey_ReturnsUnauthorized()
{
    using var client = Fixture.CreateApiClient(internalApiKey: "test-key");

    var response = await client.PostAsync("/internal/reports/run-scheduled", null);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
public async Task OneTime_WithInvalidDateWindow_ReturnsValidationProblem()
{
    using var client = Fixture.CreateApiClient(internalApiKey: null);
    var request = Fixture.OneTimeRequest(fromUtc: Fixture.ToUtc, toUtc: Fixture.ToUtc);

    var response = await client.PostAsJsonAsync("/internal/reports/one-time", request);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
}

[Fact]
public async Task Readiness_WhenDatabaseIsUnavailable_ReturnsServiceUnavailable()
{
    using var client = Fixture.CreateApiClient(healthReady: false);

    var response = await client.GetAsync("/readiness");

    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
}
```

- [ ] **Step 2: Run API tests to verify they fail**

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter "FullyQualifiedName~RunScheduled_WithMissingConfiguredKey|FullyQualifiedName~OneTime_WithInvalidDateWindow|FullyQualifiedName~Readiness_WhenDatabaseIsUnavailable" --nologo`

Expected: FAIL because routes, key filter, and readiness mapping do not exist.

- [ ] **Step 3: Implement the handlers, filter, and endpoints**

Handlers have one dependency each where possible:

```csharp
public sealed class GenerateScheduledReportsHandler(IReportGenerationService service)
{
    public Task<IReadOnlyList<GeneratedReport>> HandleAsync(DateTimeOffset triggerUtc, CancellationToken cancellationToken) =>
        service.GenerateScheduledReportsAsync(triggerUtc, cancellationToken);
}
```

`GenerateRuleReportHandler` delegates `GenerateRuleAsync`; `GenerateOneTimeReportHandler` delegates `GenerateOneTimeReportAsync`. Map the exact existing routes under `/internal/reports`. When `RVT__INTERNAL_API_KEY` is non-empty, `InternalApiKeyFilter` obtains `X-RVT-Internal-Key`, encodes the candidate and configured values with UTF-8, compares equal-length byte arrays with `CryptographicOperations.FixedTimeEquals`, and returns `Results.Unauthorized()` on absence/mismatch. It must not log either key. When no key is configured in Development, allow the request.

Map `/liveness` to 200 and `/readiness` through `IReportingHealthQueries.CanConnectAsync`, returning 200 or 503. Catch `OneTimeReportValidationException` only at the one-time endpoint and return `Results.ValidationProblem`; let unexpected failures propagate to shared host logging and non-success status.

- [ ] **Step 4: Run API tests to verify they pass**

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter "FullyQualifiedName~TestReportingApiEndpoints" --nologo`

Expected: PASS; authentication, validation, and readiness statuses match the contract.

- [ ] **Step 5: Commit the use-case/API boundary**

```bash
git add reportingmonitor/ReportingMonitor/api/UseCases reportingmonitor/ReportingMonitor/api/ReportingMonitorApi.cs reportingmonitor/ReportingMonitor/api/Security reportingmonitor/ReportingMonitorTests/TestReportingApiEndpoints.cs
git commit -m "feat: add reporting monitor api handlers"
```

### Task 8: Register adapters and protect the intended dependency boundaries

**Files:**

- Modify: `reportingmonitor/ReportingMonitor/api/ReportingMonitorServices.cs`
- Modify: `reportingmonitor/ReportingMonitor/api/ReportingMonitorJobDispatcher.cs`
- Create: `reportingmonitor/ReportingMonitorTests/Architecture/ReportingDependencyBoundaryTests.cs`
- Modify: `reportingmonitor/ReportingMonitorTests/TestReportingFixture.cs`
- Modify: `reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj`

**Interfaces:**

- Consumes: All narrow ports, `ReportingDbClient`, report adapters, `TimeProvider`, and the shared monitor configuration/diagnostics services.
- Produces: A host that resolves every report endpoint/job dependency without direct database/vender coupling in application code.

- [ ] **Step 1: Write failing architecture and DI tests**

Create `ReportingDependencyBoundaryTests.cs`:

```csharp
[Fact]
public void ApplicationFolders_DoNotReferenceEfCoreEntitiesOrNpgsql()
{
    var source = SourceFiles.Under("reportingmonitor/ReportingMonitor/api")
        .Where(path => !path.Contains("/api/db/", StringComparison.Ordinal));

    Assert.All(source, path =>
    {
        var text = File.ReadAllText(path);
        Assert.DoesNotContain("ReportingMonitorContext", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Npgsql", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportRuleEntity", text, StringComparison.Ordinal);
    });
}

[Fact]
public void Composition_ResolvesEveryNarrowReportingPort()
{
    using var provider = ReportingServiceProviderFactory.Create();

    Assert.IsAssignableFrom<IReportingRuleQueries>(provider.GetRequiredService<IReportingRuleQueries>());
    Assert.IsAssignableFrom<IReportingGenerationCommands>(provider.GetRequiredService<IReportingGenerationCommands>());
    Assert.IsAssignableFrom<IReportingHealthQueries>(provider.GetRequiredService<IReportingHealthQueries>());
}
```

- [ ] **Step 2: Run architecture and composition tests to verify they fail**

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter "FullyQualifiedName~ReportingDependencyBoundaryTests" --nologo`

Expected: FAIL because `AddReportingMonitor` does not yet register the complete dependency graph.

- [ ] **Step 3: Register focused services and adapters**

In `AddReportingMonitor`:

1. Bind/validate `ReportingMonitorOptions` and require PostgreSQL through `MonitorDatabaseProviderGuard`.
2. Register `ReportingMonitorContext` using `MonitorDbContextOptionsFactory.CreateOptions<ReportingMonitorContext>(RvtConfig.DB_CONNECTION_STRING, monitorOptions)` or the equivalent scoped `DbContextOptions` factory based on `ConnectionStrings:DefaultConnection`.
3. Register `ReportingDbClient` and resolve each narrow port from it; do not register a broad `IReportingRepository` or `IDBClient`.
4. Register Core orchestration, its use-case handlers, `TimeProvider.System`, PDF renderer, Azure Blob report storage, SendGrid message sender, SPA-logo typed `HttpClient`, and Ollama typed `HttpClient`.
5. Register the job dispatcher and API filter/health dependencies in the service container.

Use the shared sensitive-log redactor/configuration conventions for any options-validation errors. Keep all vendor implementation registrations in this single composition root.

- [ ] **Step 4: Run architecture and composition tests to verify they pass**

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter "FullyQualifiedName~ReportingDependencyBoundaryTests" --nologo`

Expected: PASS; application code is data-implementation agnostic and all narrow ports resolve.

- [ ] **Step 5: Commit service composition and boundary enforcement**

```bash
git add reportingmonitor/ReportingMonitor/api/ReportingMonitorServices.cs reportingmonitor/ReportingMonitor/api/ReportingMonitorJobDispatcher.cs reportingmonitor/ReportingMonitorTests/Architecture reportingmonitor/ReportingMonitorTests/TestReportingFixture.cs reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj
git commit -m "refactor: enforce reporting monitor boundaries"
```

### Task 9: Integrate solution, containers, documentation, and PostgreSQL verification

**Files:**

- Modify: `rvt-monitors.sln`
- Modify: `docker-compose.yml`
- Modify: `README.md`
- Modify: `docs/container-builds.md`
- Modify: `project_state.md`
- Modify: `reportingmonitor/README.md`
- Modify: `reportingmonitor/ReportingMonitorTests/TestReportingFixture.cs`
- Modify: `reportingmonitor/ReportingMonitorTests/TestReportingDbClient.cs`
- Modify: `reportingmonitor/ReportingMonitorTests/testdata/create.postgres.sql`
- Modify: `reportingmonitor/ReportingMonitorTests/testdata/reset.postgres.sql`

**Interfaces:**

- Consumes: Completed ReportingMonitor projects, root solution conventions, root compose conventions, and `RVT__POSTGRES_INTEGRATION_CONNECTION` fixture convention.
- Produces: A solution/build/container/documentation surface that treats reporting as a first-class monitor application.

- [ ] **Step 1: Write failing solution and deployment contract tests**

Add tests in `TestReportingFixture.cs`:

```csharp
[Fact]
public void PrerequisiteSql_IsIdempotentAndDocumentsHiddenOneTimeRuleIndex()
{
    var sql = File.ReadAllText(RepositoryPath("reportingmonitor/database/postgres/reporting_service_prerequisites_20260625.sql"));

    Assert.Contains("create extension if not exists pgcrypto", sql, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("add column if not exists is_hidden_system_rule", sql, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("ux_report_rule_hidden_one_time_per_site", sql, StringComparison.OrdinalIgnoreCase);
}
```

Add a root-level test or verification script assertion that `rvt-monitors.sln` contains `ReportingMonitor` and `ReportingMonitorTests` paths and `docker-compose.yml` contains `reportingmonitor-api` with `RVT__DATABASE_PROVIDER: PostgreSql`.

- [ ] **Step 2: Run solution/deployment contract tests to verify they fail**

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter FullyQualifiedName~PrerequisiteSql_IsIdempotent --nologo`

Expected: PASS for the imported SQL if already present; the root solution/compose assertions must fail until their integration edits are made. Treat the existing passing SQL test as preservation coverage, not evidence that root integration has happened.

- [ ] **Step 3: Add root integration and fixture configuration**

Add the host, Core, Pdf, Storage, Messaging, and tests projects beneath a `reportingmonitor` solution folder. Add root Compose service `reportingmonitor-api` with the existing monitor build pattern, port `8085:8080`, `ASPNETCORE_URLS=http://+:8080`, `Infrastructure=local`, `MonitorApi__Enabled=true`, `MonitorScheduler__Enabled=false`, and `RVT__DATABASE_PROVIDER=PostgreSql`; do not publish or declare a second local database.

Document build/run/configuration/health/API-key/schema prerequisite steps in root and reporting READMEs and `docs/container-builds.md`. Update `project_state.md` with the final project layout, reporting ports, EF context, configuration names, verification commands, and the source provenance. Configure `TestReportingFixture` to use the established `RVT__POSTGRES_INTEGRATION_CONNECTION` resolution and its generated unique schema. Its create/reset scripts must execute in that schema only.

- [ ] **Step 4: Run integration verification to verify it passes**

Run: `dotnet sln rvt-monitors.sln list | rg "ReportingMonitor|Rvt.Reporting"`

Expected: lists the six reporting projects.

Run: `docker compose config`

Expected: valid Compose configuration containing `reportingmonitor-api` and no additional Timescale service.

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --nologo`

Expected: all reporting unit/architecture/endpoint/mapping tests pass; PostgreSQL fixture tests pass when `RVT__POSTGRES_INTEGRATION_CONNECTION` is supplied and otherwise report their established explicit configuration prerequisite.

- [ ] **Step 5: Commit root integration and docs**

```bash
git add rvt-monitors.sln docker-compose.yml README.md docs/container-builds.md project_state.md reportingmonitor/README.md reportingmonitor/ReportingMonitorTests
git commit -m "feat: integrate reporting monitor solution"
```

### Task 10: Run final verification and publish the completed integration

**Files:**

- Verify: all files changed by Tasks 1–9

**Interfaces:**

- Consumes: the complete solution, reporting tests, Compose definition, and source-control state.
- Produces: evidence that the integrated reporting monitor builds and tests without modifying unrelated work.

- [ ] **Step 1: Verify no generated source artifacts or source repository metadata were imported**

Run: `find reportingmonitor -type d \( -name .git -o -name bin -o -name obj \) -print`

Expected: no output.

Run: `find reportingmonitor \( -name '.DS_Store' -o -path 'reportingmonitor/output/*' \) -print`

Expected: no output.

- [ ] **Step 2: Run focused and solution verification**

Run: `dotnet build rvt-monitors.sln --no-restore --nologo`

Expected: successful build with zero warnings and zero errors.

Run: `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --no-restore --nologo`

Expected: all non-database reporting tests pass; PostgreSQL tests pass when their explicit environment connection is available.

Run: `dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --no-restore --nologo`

Expected: shared-host/configuration regression tests pass.

Run: `docker compose config && git diff --check && git status --short`

Expected: valid Compose, no whitespace errors, and only intentionally changed/staged reporting files plus pre-existing unrelated user changes.

- [ ] **Step 3: Commit final verification-only adjustments if required**

If verification requires a reporting-only correction, first add a focused failing regression test, observe its failure, make the minimal correction, re-run the affected test and the commands in Step 2, then commit only the reporting files:

```bash
git add reportingmonitor rvt-monitors.sln docker-compose.yml README.md docs/container-builds.md project_state.md
git commit -m "test: verify reporting monitor integration"
```

- [ ] **Step 4: Push completed commits from the native macOS clone**

Run: `git push origin main`

Expected: all reporting-monitor integration commits are accepted by GitHub; unrelated AirQ work remains unstaged and uncommitted.

---

## Plan Self-Review

- **Spec coverage:** Tasks 1–3 cover source import, Omnidots style, shared host, one-shot dispatch, Quartz configuration, and configuration. Tasks 4–6 cover PostgreSQL-first EF Core mappings, Mapperly, narrow ports, advisory locking, and atomic persistence. Tasks 7–8 cover protected API, health, exception behavior, adapter composition, and architecture boundaries. Tasks 9–10 cover schema, integration tests, solution/Compose/docs/state updates, verification, commits, and GitHub synchronization.
- **No-placeholder scan:** The plan supplies paths, types, test names, commands, expected results, and commit scopes for every task; it contains no deferred implementation markers.
- **Type consistency:** `IReportingRuleQueries`, `IReportingDataQueries`, `IReportingGenerationLocks`, `IReportingGenerationCommands`, and `IReportingHealthQueries` are the same names and methods throughout. `GenerateScheduledReports` is the single dispatcher/scheduler job name. `ReportingMonitorContext`, `ReportingDbClient`, and `ReportingDbMapper` are used consistently.
