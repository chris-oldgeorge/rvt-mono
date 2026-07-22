# MyATM Reliability Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make MyATM imports, rule processing, scheduling, health endpoints, and documentation reliable while preserving the Omnidots-style handler/port architecture.

**Architecture:** MyATM remains a facade over focused use-case handlers, with vendor traffic isolated in `MyAtmHttpGateway` and EF Core persistence isolated behind narrow ports. The change adds validated monitor options, asynchronous import paths, transactional batch persistence, and durable rule-evaluation results.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, Quartz, EF Core 10, Npgsql, Mapperly, MSTest, Moq.

## Global Constraints

- Work in `/Users/oldgeorge/Documents/rvt-monitors/rvt-monitors`; do not use the retired Windows share.
- Preserve EF Core PostgreSQL-first data access and current SQL Server-compatible mappings.
- Keep `IDBClient` as a compatibility facade; new behavior uses narrow MyATM ports.
- Keep vendor parsing and rule-state decisions manual and test-covered.
- Do not add credentials to tracked files or change vendor JSON contracts.
- Use Omnidots as an architecture/style reference only; do not copy its vendor protocol implementation.
- Keep Mapperly app-local, analyzer-only, and out of `rvt-monitor-common`.

---

## File Structure

- Create `model/config/MyAtmMonitorOptions.cs` for validated `CustomerId`, `DevicePageSize`, and `PortalBaseUrl` settings.
- Create `api/db/Commands/IMyAtmImportCommands.cs` for atomic dust and batched accessory persistence.
- Create `api/db/Queries/IMyAtmMeasurementQueries.cs` for the latest persisted accessory timestamp.
- Create `api/IMyAtmMonitorJobs.cs` for testable asynchronous service job dispatch.
- Create `api/db/Queries/IMyAtmHealthQueries.cs` for database readiness.
- Create `api/UseCases/RuleEvaluation.cs` for a rule state change plus optional notification.
- Modify MyATM's composition root, facade, service, dispatcher, HTTP gateway, handlers, DB client, endpoint map, configuration, docs, and tests.

### Task 1: Configure MyATM operational settings and complete job registration

**Files:**
- Create: `myatmmonitor/MyAtmMonitor/model/config/MyAtmMonitorOptions.cs`
- Create: `myatmmonitor/MyAtmMonitor/api/IMyAtmMonitorJobs.cs`
- Modify: `myatmmonitor/MyAtmMonitor/Program.cs`, `api/MyAtmMonitorServices.cs`, `api/MyAtmApi.cs`, `api/MyAtmService.cs`, `api/MonitorJobRunner.cs`, `api/MonitorJobDispatcher.cs`, `appsettings.json`
- Test: Create `myatmmonitor/MyAtmMonitorTests/MyAtmMonitorOptionsTests.cs`, `MonitorJobRunnerTests.cs`; modify `TestUtil.cs`
- Modify: `AGENTS.md`

**Interfaces:**

~~~csharp
public sealed class MyAtmMonitorOptions
{
    public const string SectionName = "MyAtmMonitor";
    public int CustomerId { get; init; } = 9;
    public int DevicePageSize { get; init; } = 100;
    public string PortalBaseUrl { get; init; } = string.Empty;
    public static void Validate(MyAtmMonitorOptions options);
}

public interface IMyAtmMonitorJobs
{
    Task StoreMonitorsAsync(CancellationToken cancellationToken);
    Task CheckForOfflineMonitorsAsync(CancellationToken cancellationToken);
    Task StoreDustLevelsAsync(CancellationToken cancellationToken);
    Task Store15MinAverageDustLevelsAsync(CancellationToken cancellationToken);
    Task Store1HourAverageDustLevelsAsync(CancellationToken cancellationToken);
    Task Store24HourAverageDustLevelsAsync(CancellationToken cancellationToken);
    Task Process8HourAverageDustLevelsAsync(CancellationToken cancellationToken);
    Task ClearOlderErrorMessagesAsync(CancellationToken cancellationToken);
    Task StoreAccessoryInfoAsync(CancellationToken cancellationToken);
}
~~~

- [ ] **Step 1: Write failing tests for page-size validation and `StoreAccessoryInfo` runner dispatch.**

~~~csharp
[TestMethod]
public async Task RunAsync_StoreAccessoryInfo_InvokesService()
{
    var service = new Mock<IMyAtmMonitorJobs>(MockBehavior.Strict);
    service.Setup(x => x.StoreAccessoryInfoAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    Assert.AreEqual(0, await MonitorJobRunner.RunAsync("StoreAccessoryInfo", service.Object, CancellationToken.None));
}
~~~

- [ ] **Step 2: Run `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-restore --filter "FullyQualifiedName~MyAtmMonitorOptionsTests|FullyQualifiedName~MonitorJobRunnerTests"`. Confirm failure because the options type and dispatch operation do not exist.**
- [ ] **Step 3: Bind and validate options once in `AddMyAtmMonitor`; inject them into the MyATM facade/service/gateway path. Make `MyAtmService` implement `IMyAtmMonitorJobs`, register it under that interface, and resolve that interface in `Program`, runner, and dispatcher. Add `StoreAccessoryInfo` to supported names, one-shot runner, and a daily UTC Quartz job. Add an `AGENTS.md` rule requiring consistent monitor style, handler/port boundaries, async conventions, tests, and docs unless a vendor-specific difference is documented.**
- [ ] **Step 4: Re-run the focused tests; expect PASS. Commit with `feat: configure MyATM jobs and operational settings`.**

### Task 2: Make vendor imports asynchronous, paged, ordered, and failure-visible

**Files:**
- Modify: `myatmmonitor/MyAtmMonitor/api/http/IHttpClient.cs`, `HttpWebClient.cs`, `MyAtmHttpGateway.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/UseCases/StoreMonitorsHandler.cs`, `StoreDustLevelsHandler.cs`, `StoreAccessoryInfoHandler.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmApi.cs`, `MyAtmService.cs`, `MonitorJobRunner.cs`, `MonitorJobDispatcher.cs`
- Test: Modify `TestMyAtmApi.cs`, `TestMyAtmApiMonitors.cs`, `TestMyAtmApiExceptions.cs`

**Interfaces:**

~~~csharp
Task<string> GetAsync(string path, CancellationToken cancellationToken);
Task<IReadOnlyList<T>> GetDeviceMeasurementsAsync<T>(
    int customerId, string serialId, DateTime watermark, Period period, CancellationToken cancellationToken)
    where T : BaseDeviceMeasurement;
~~~

- [ ] **Step 1: Write failing tests for two-page catalogue fetches (`$skip=0&$top=2`, `$skip=2&$top=2`), unordered timestamps returning ascending readings/max watermark, and a vendor failure producing job exit code 1 after `HandleException`.**
- [ ] **Step 2: Run `dotnet test ... --filter "FullyQualifiedName~Pagination|FullyQualifiedName~Unordered|FullyQualifiedName~VendorReadFails"`. Confirm failures reflect the current single page, element-zero watermark, and success-after-error behavior.**
- [ ] **Step 3: Convert the HTTP facade/gateway/handlers/service/runner to `Task` APIs with cancellation propagation. Replace `.Result`; request explicit `$top`; advance `$skip` by page size; UTC-normalize, filter strictly after watermark, sort ascending, and use `Max(Timestamp)`. Record errors, finish independent monitor attempts, then throw an aggregate failure so Quartz and one-shot telemetry fail.**
- [ ] **Step 4: Run `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-restore --filter "FullyQualifiedName!~TestDBClient"`; expect PASS. Commit with `refactor: make MyATM imports asynchronous and ordered`.**

### Task 3: Persist dust and accessory imports through narrow transactional commands

**Files:**
- Create: `myatmmonitor/MyAtmMonitor/api/db/Commands/IMyAtmImportCommands.cs`
- Create: `myatmmonitor/MyAtmMonitor/api/db/Queries/IMyAtmMeasurementQueries.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/IDBClient.cs`, `DBClient.cs`, `StoreDustLevelsHandler.cs`, `StoreAccessoryInfoHandler.cs`
- Test: Create `myatmmonitor/MyAtmMonitorTests/MyAtmImportCommandTests.cs`; modify `TestDbClient.cs`

**Interfaces:**

~~~csharp
public interface IMyAtmImportCommands
{
    void StoreDustBatch(string serialId, Period period, IReadOnlyList<DustDto> readings, DateTime watermark);
    void InsertAccessoryBatch(IReadOnlyList<AccessoryInfoDto> readings);
}

public interface IMyAtmMeasurementQueries
{
    DateTime? ReadLatestAccessoryTimestamp(string serialId);
}
~~~

- [ ] **Step 1: Write failing PostgreSQL fixture tests showing a failed dust batch leaves neither samples nor watermark committed, and a mixed existing/new accessory batch inserts only the new timestamp.**
- [ ] **Step 2: Run `dotnet test ... --filter "FullyQualifiedName~MyAtmImportCommandTests"`; confirm compilation failure for the new port.**
- [ ] **Step 3: Implement `StoreDustBatch` with one EF Core context and transaction: deduplicate/inserts readings, update the matching period watermark, save, commit. Implement `ReadLatestAccessoryTimestamp` via `MAX(sample_time)` and a single-context batch insert of only missing accessory keys. Use the accessory timestamp, never the one-minute dust watermark.**

~~~csharp
using var context = CreateContext();
using var transaction = context.Database.BeginTransaction();
InsertMissingDustLevels(context, readings);
SetLatestTimestamp(context, serialId, period, watermark);
context.SaveChanges();
transaction.Commit();
~~~

- [ ] **Step 4: Run the integration category when `RVT__POSTGRES_INTEGRATION_CONNECTION` is configured, otherwise record the prerequisite and run the full non-database suite. Commit with `feat: persist MyATM imports atomically in batches`.**

### Task 4: Persist rule decisions atomically and skip empty aggregate blocks

**Files:**
- Create: `myatmmonitor/MyAtmMonitor/api/UseCases/RuleEvaluation.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmRuleProcessor.cs`, `StoreDustLevelsHandler.cs`, `ProcessDustLevelsHandler.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/Commands/IMyAtmOperationalCommands.cs`, `DBClient.cs`
- Test: Create `myatmmonitor/MyAtmMonitorTests/RuleEvaluationTests.cs`; modify `TestRules.cs`, `TestRules2.cs`

**Interfaces:**

~~~csharp
public sealed record RuleEvaluation(RvtAlertRuleDto Rule, NotificationDto? Notification, bool StateChanged);
void CommitRuleEvaluation(RuleEvaluation evaluation);
~~~

- [ ] **Step 1: Write failing tests proving two successive imports create one notification and persist `IsActive=true`, and proving an eligible eight-hour block with no aggregate advances `Accessed` to its end.**
- [ ] **Step 2: Run `dotnet test ... --filter "FullyQualifiedName~RuleEvaluationTests|FullyQualifiedName~NoAverage"`; confirm normal ingestion currently has no durable update and the null-average path stalls.**
- [ ] **Step 3: Replace mutation-only rule processing with an evaluator that returns `RuleEvaluation`. Commit the state and optional notification in one EF Core transaction; only then send contact messages/audits. Preserve deleted-rule deactivation. For no aggregate, log serial/rule/block and update `Accessed=end` to guarantee progress.**

~~~csharp
if (level >= rule.LimitOn && !rule.IsActive)
{
    rule.IsActive = true;
    rule.Accessed = sampleTime;
    return new RuleEvaluation(rule, new NotificationDto(rule, level, sampleTime, monitor.Id), true);
}
~~~

- [ ] **Step 4: Run `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-restore --filter "FullyQualifiedName~TestRules|FullyQualifiedName~RuleEvaluationTests"`; expect PASS. Commit with `fix: persist MyATM rule decisions with notifications`.**

### Task 5: Add database readiness and refresh operational documentation

**Files:**
- Create: `myatmmonitor/MyAtmMonitor/api/db/Queries/IMyAtmHealthQueries.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/IDBClient.cs`, `DBClient.cs`, `api/MonitorApiEndpoints.cs`
- Test: Modify `TestMonitorApiEndpoints.cs`, `Architecture/MyAtmDependencyBoundaryTests.cs`
- Modify: `myatmmonitor/README.md`, `docs/container-builds.md`, `project_state.md`

**Interfaces:**

~~~csharp
public interface IMyAtmHealthQueries { bool CanConnect(); }
endpoints.MapGet("/readiness", (IMyAtmHealthQueries health) =>
    health.CanConnect() ? Results.Ok(new { status = "ready" })
                        : Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable));
~~~

- [ ] **Step 1: Write failing endpoint tests for both `/liveness` and `/readiness`, including `503` for unavailable database, and an architecture test that handlers use narrow ports rather than `DBClient`.**
- [ ] **Step 2: Run `dotnet test ... --filter "FullyQualifiedName~TestMonitorApiEndpoints|FullyQualifiedName~MyAtmDependencyBoundaryTests"`; confirm `/readiness` is absent.**
- [ ] **Step 3: Implement `CanConnect` using an EF Core context, register it through `IDBClient`, and map the readiness endpoint. Do not probe the vendor in readiness. Replace Azure Functions instructions in the MyATM README with shared-host modes, jobs, options, and `liveness`/`readiness`; add the readiness URL and daily accessory job to container docs.**
- [ ] **Step 4: Re-run the endpoint/architecture tests; expect PASS. Commit with `feat: add MyATM readiness and current operations docs`.**

### Task 6: Final verification and handoff

**Files:**
- Modify: `project_state.md`
- Modify: `docs/superpowers/plans/2026-07-14-myatm-reliability-refactor.md` to mark completed tasks

- [ ] **Step 1: Run `git diff --check`; expect exit 0 with no output.**
- [ ] **Step 2: Run `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-restore --nologo`; expect all focused MyATM tests to pass.**
- [ ] **Step 3: Run `dotnet build myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj --no-restore --nologo`; expect exit 0.**
- [ ] **Step 4: Update `project_state.md` with exact verification results and prerequisites, stage the plan/state, commit `docs: record MyATM reliability verification`, and push the completed branch.**
