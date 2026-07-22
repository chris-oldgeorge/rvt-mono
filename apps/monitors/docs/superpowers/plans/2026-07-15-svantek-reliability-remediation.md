# Svantek Reliability Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring Svantek to the Omnidots reference architecture with bounded async ingestion, truthful job failure, atomic state changes, shared durable delivery, site-hours-aware offline behavior, readiness, async sound retrieval, and fully compiled tests.

**Architecture:** Keep `IDBClient` as a compatibility facade while new behavior enters through narrow query/command ports. Pure calculators/evaluators prepare request windows, rule transitions, site-active durations, notifications, and delivery requests; app-local EF commands apply them transactionally. External email/SMS/MQTT delivery occurs only after commit through the shared fenced outbox.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, C# 14, Entity Framework Core 10.0.4, PostgreSQL/Npgsql, SQL Server, Mapperly 4.3.1 analyzer-only, Quartz through `Rvt.Monitor.Common`, MSTest, Moq, shared durable-delivery foundation.

## Global Constraints

- Prerequisite: `docs/superpowers/plans/2026-07-15-shared-durable-delivery-foundation.md` is merged and verified.
- Use producer `Svantek`, payload version `1`, and `MonitorDeliveryFailureMode.AnyDeliveryFailure`.
- Default `MaximumInitialBackfill` is 7 days, `MaximumRequestWindow` is 12 hours, and watermark overlap is 5 minutes.
- Never request a future endpoint or a window whose end is not after its start.
- Every scheduled/vendor path is async and accepts/flows `CancellationToken`; no `.Result`, `.Wait()`, or `GetAwaiter().GetResult()` remains there.
- Continue independent monitor/project work after individual failures, record operational failures, then fault the job with identifiers and inner exceptions.
- Atomically commit samples, eight-hour aggregates, monotonic watermark, offline recovery, rule state, notifications, and delivery rows.
- Keep Mapperly app-local and use it only for simple mappings; rule/vendor/state-machine logic remains manual.
- Do not create a Svantek alert-occurrence table.
- Use the common outbox table and existing notification/audit tables.
- Preserve PostgreSQL-first and SQL Server runtime/migration parity.
- Restore all four currently excluded Svantek test files and finish with zero skipped tests.
- Use only the isolated `RVT__POSTGRES_INTEGRATION_CONNECTION` fixture convention for database integration tests; never target a developer or production database.

---

### Task 1: Add cancellable async vendor and HTTP boundaries

**Files:**
- Modify: `svantekmonitor/SvantekMonitor/api/http/IHttpClient.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/http/HttpWebClient.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/http/SvantekHttpGateway.cs`
- Create: `svantekmonitor/SvantekMonitorTests/SvantekHttpGatewayAsyncTests.cs`
- Create: `svantekmonitor/SvantekMonitorTests/HttpWebClientCancellationTests.cs`
- Modify: `svantekmonitor/SvantekMonitorTests/TestCheckForSoundRecordings.cs`
- Modify: `svantekmonitor/SvantekMonitorTests/TestCheckForSoundRecordingStorage.cs`

**Interfaces:**
- Produces: `GetProjectsAsync`, `GetProjectFilesAsync`, `GetStationsAsync`, `GetDataMultiAsync`, and `GetSoundFileAsync`, all accepting optional cancellation.
- Consumes: cancellation-aware `HttpClient.SendAsync` and response-content APIs.

- [ ] **Step 1: Write failing async/cancellation tests**

Assert the gateway awaits responses, passes the exact token, wraps non-cancellation adapter failures, and lets caller `OperationCanceledException` escape unchanged:

```csharp
var token = new CancellationTokenSource().Token;
http.Setup(x => x.PostAsync("stations-get-list.php", It.IsAny<HttpContent>(), token))
    .ReturnsAsync(StationsJson);
var stations = await gateway.GetStationsAsync(token);
Assert.HasCount(1, stations);
```

- [ ] **Step 2: Run tests and verify signature failure**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~SvantekHttpGatewayAsyncTests|FullyQualifiedName~HttpWebClientCancellationTests"`

Expected: FAIL because the async gateway/token signatures do not exist.

- [ ] **Step 3: Extend `IHttpClient` and implementation**

Use these exact signatures:

```csharp
Task<string> GetAsync(string path, CancellationToken cancellationToken = default);
Task<string> PostAsync(string path, HttpContent content, CancellationToken cancellationToken = default);
Task<byte[]> GetByteArrayAsync(string path, MultipartFormDataContent content, CancellationToken cancellationToken = default);
```

Pass the token to `SendAsync`, `ReadAsStringAsync`, and `ReadAsByteArrayAsync`; dispose responses and requests.

Update the two currently compiled sound-recording test files so every Moq setup/verify supplies `It.IsAny<CancellationToken>()`; excluded legacy suites are rewritten after re-enabling in Task 11.

- [ ] **Step 4: Add awaited gateway methods without breaking current handlers**

Name every new public operation `*Async`, pass cancellation through private calls, use `ConfigureAwait(false)`, and add `catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }` before adapter wrapping. Keep the existing synchronous methods temporarily, mark them `[Obsolete("Scheduled callers must use the cancellable async method.")]`, and do not add new sync call sites. Task 3 converts every handler and deletes these shims, making this commit independently buildable.

- [ ] **Step 5: Run gateway tests**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~SvantekHttpGatewayAsyncTests|FullyQualifiedName~HttpWebClientCancellationTests"`

Expected: PASS with token identity assertions.

- [ ] **Step 6: Commit the vendor boundary**

```bash
git add svantekmonitor/SvantekMonitor/api/http svantekmonitor/SvantekMonitorTests/SvantekHttpGatewayAsyncTests.cs svantekmonitor/SvantekMonitorTests/HttpWebClientCancellationTests.cs svantekmonitor/SvantekMonitorTests/TestCheckForSoundRecordings.cs svantekmonitor/SvantekMonitorTests/TestCheckForSoundRecordingStorage.cs
git commit -m "refactor(svantek): make vendor gateway cancellable"
```

---

### Task 2: Add validated import options and a pure request-window calculator

**Files:**
- Create: `svantekmonitor/SvantekMonitor/model/config/SvantekImportOptions.cs`
- Create: `svantekmonitor/SvantekMonitor/api/NoiseRequestWindowCalculator.cs`
- Create: `svantekmonitor/SvantekMonitorTests/SvantekImportOptionsTests.cs`
- Create: `svantekmonitor/SvantekMonitorTests/NoiseRequestWindowCalculatorTests.cs`
- Modify: `svantekmonitor/SvantekMonitor/appsettings.json`
- Modify: `svantekmonitor/SvantekMonitor/api/SvantekMonitorServices.cs`

**Interfaces:**
- Produces: `SvantekImportOptions`, `NoiseRequestWindow`, and `NoiseRequestWindowCalculator.Calculate(...)`.
- Consumes: deployment start, optional watermark/status timestamp, and injected `utcNow`.

- [ ] **Step 1: Write failing option/window tests**

Cover first import capped at seven days, watermark minus five minutes but not before deployment, status plus one hour, end clamped to now, empty future/degenerated intervals, exact 12-hour slicing, and a short final slice.

```csharp
var windows = calculator.Calculate(
    deploymentStart: now.AddDays(-30), watermark: null,
    lastStatusTimestamp: null, utcNow: now);
Assert.AreEqual(now.AddDays(-7), windows[0].Start);
Assert.AreEqual(now, windows[^1].End);
Assert.IsTrue(windows.All(x => x.End - x.Start <= TimeSpan.FromHours(12)));
```

- [ ] **Step 2: Run tests and verify missing types**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~SvantekImportOptionsTests|FullyQualifiedName~NoiseRequestWindowCalculatorTests"`

Expected: FAIL because options/calculator do not exist.

- [ ] **Step 3: Implement and validate exact defaults**

Bind section `SvantekImport` with `MaximumInitialBackfill = 7.00:00:00`, `MaximumRequestWindow = 12:00:00`, and `WatermarkOverlap = 00:05:00`. Reject non-positive values and `MaximumRequestWindow > MaximumInitialBackfill`.

- [ ] **Step 4: Implement the pure slicing algorithm**

Implement private `LaterOf(DateTime, DateTime)` and `EarlierOf(DateTime, DateTime)` helpers. Use `start = watermark.HasValue ? LaterOf(deployment, watermark - overlap) : LaterOf(deployment, now - backfill)`. Use `candidateEnd = status.HasValue ? status + 1 hour : now`, clamp with `EarlierOf(candidateEnd, now)`, return empty when `end <= start`, then emit contiguous `[cursor, EarlierOf(cursor + maxWindow, end)]` windows.

- [ ] **Step 5: Register options/calculator and run tests**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~SvantekImportOptionsTests|FullyQualifiedName~NoiseRequestWindowCalculatorTests"`

Expected: PASS for every boundary and validation row.

- [ ] **Step 6: Commit bounded-window policy**

```bash
git add svantekmonitor/SvantekMonitor/model/config svantekmonitor/SvantekMonitor/api/NoiseRequestWindowCalculator.cs svantekmonitor/SvantekMonitor/appsettings.json svantekmonitor/SvantekMonitor/api/SvantekMonitorServices.cs svantekmonitor/SvantekMonitorTests/NoiseRequestWindowCalculatorTests.cs svantekmonitor/SvantekMonitorTests/SvantekImportOptionsTests.cs
git commit -m "feat(svantek): bound noise import windows"
```

---

### Task 3: Make the service/job pipeline async and truthful about partial failures

**Files:**
- Create: `svantekmonitor/SvantekMonitor/api/SvantekJobAggregateException.cs`
- Create: `svantekmonitor/SvantekMonitor/api/SvantekFailureCollector.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/UseCases/StoreMonitorsHandler.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/UseCases/StoreNoiseLevelsHandler.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/UseCases/NotifySiteAveragesHandler.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/UseCases/CheckForOfflineMonitorsHandler.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/UseCases/NotifyBatteryLevelsHandler.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/UseCases/CheckForSoundRecordingsHandler.cs`
- Create: `svantekmonitor/SvantekMonitor/api/ISvantekMonitorJobs.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/SvantekApi.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/SvantekService.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/SvantekMonitorServices.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/MonitorJobRunner.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/MonitorJobDispatcher.cs`
- Create: `svantekmonitor/SvantekMonitorTests/SvantekJobFailureSemanticsTests.cs`
- Create: `svantekmonitor/SvantekMonitorTests/SvantekJobCancellationTests.cs`

**Interfaces:**
- Consumes: Tasks 1–2 async gateway/calculator and `ISvantekOperationalCommands`.
- Produces: `RunAsync` through every scheduled layer and aggregate failure after bounded continuation.

- [ ] **Step 1: Write failing continuation/failure tests**

Arrange three monitors/projects where the first and third fail. Assert the second runs, two operational errors carry identifiers, and the final exception exposes two failures. Assert authentication/catalogue failure before iteration faults immediately.

- [ ] **Step 2: Write failing cancellation propagation tests**

Capture the token at service, handler, gateway, and DB mocks. Assert `SvantekMonitorJobDispatcher.RunAsync` passes its token to `MonitorJobRunner` (the current missing-token bug must fail this test).

- [ ] **Step 3: Run focused tests and verify failure**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~SvantekJobFailureSemanticsTests|FullyQualifiedName~SvantekJobCancellationTests"`

Expected: FAIL because scheduled methods are synchronous and exceptions are swallowed.

- [ ] **Step 4: Implement the failure collector**

`Capture(identifier, exception)` immediately calls `HandleException`, stores `new InvalidOperationException(identifier, exception)`, and continues. `ThrowIfAny(jobName)` throws `SvantekJobAggregateException` with an immutable failure list. Cancellation is never captured.

- [ ] **Step 5: Convert all scheduled entry points to `Task`**

Define `ISvantekMonitorJobs` with one cancellable method per supported job and make `SvantekService` implement it. Rename/use `RunAsync(CancellationToken)` in handlers, expose async methods in `SvantekApi` and `SvantekService`, await them in `MonitorJobRunner`, and pass the dispatcher token. Convert sound listing/download calls to the Task 1 async gateway methods while preserving its existing behavior until Task 9. Delete all obsolete gateway and scheduled sync wrappers. Use `DateTime.UtcNow.Date.AddDays(-1)` for daily site-average input rather than local `DateTime.Today`.

Register `ISvantekMonitorJobs` as the singleton `SvantekService`, and make the Quartz dispatcher depend on the interface so scheduler tests can use a strict mock.

- [ ] **Step 6: Apply aggregate semantics at each independent loop**

Catalogue/noise group by project, while offline/battery/site averages group by monitor. Continue after per-unit errors, call `ThrowIfAny` at the end, and let setup failures escape before collection starts.

- [ ] **Step 7: Run failure/cancellation tests**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~SvantekJobFailureSemanticsTests|FullyQualifiedName~SvantekJobCancellationTests"`

Expected: PASS and one-shot job methods fault rather than returning 0 after any configured failure.

- [ ] **Step 8: Commit async job semantics**

```bash
git add svantekmonitor/SvantekMonitor/api svantekmonitor/SvantekMonitorTests/SvantekJobFailureSemanticsTests.cs svantekmonitor/SvantekMonitorTests/SvantekJobCancellationTests.cs
git commit -m "refactor(svantek): propagate async job failures"
```

---

### Task 4: Preserve runtime-owned state during catalogue refresh

**Files:**
- Modify: `svantekmonitor/SvantekMonitor/api/db/Mapping/SvantekDbMapper.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/db/DBClient.cs`
- Modify: `svantekmonitor/SvantekMonitorTests/Mapping/SvantekDbMapperTests.cs`
- Modify: `svantekmonitor/SvantekMonitorTests/TestDbClient.cs`

**Interfaces:**
- Consumes: catalogue `NoiseMonitorDto` and existing monitor/status entities.
- Produces: catalogue upsert that cannot reset offline, watermarks, battery state, ownership, or deployment state.

- [ ] **Step 1: Add failing mapper regression**

Seed an entity with `Offline=true`, all four watermarks, battery state, customer/location IDs, and deployment-related values. Apply `UpdateMonitorEntity` from a DTO carrying false/default values and assert every runtime-owned value remains unchanged while model/firmware/status metadata updates.

- [ ] **Step 2: Run mapper/DB tests and observe Offline reset**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~SvantekDbMapperTests|FullyQualifiedName~TestDbClient"`

Expected: FAIL on the `Offline` preservation assertion.

- [ ] **Step 3: Complete Mapperly ignore policy**

Add `[MapperIgnoreTarget(nameof(MonitorEntity.Offline))]` and retain ignores for IDs, customer/location ownership, all latest-data timestamps, and battery status. Keep status telemetry mapped through `SvantekMonitorStatusEntity`.

- [ ] **Step 4: Run mapper and PostgreSQL integration tests**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~SvantekDbMapperTests|FullyQualifiedName~TestDbClient"`

Expected: PASS; repeated catalogue writes preserve runtime state.

- [ ] **Step 5: Commit catalogue preservation**

```bash
git add svantekmonitor/SvantekMonitor/api/db svantekmonitor/SvantekMonitorTests/Mapping/SvantekDbMapperTests.cs svantekmonitor/SvantekMonitorTests/TestDbClient.cs
git commit -m "fix(svantek): preserve runtime monitor state"
```

---

### Task 5: Add the atomic noise-import command and pure rule evaluation

**Files:**
- Create: `svantekmonitor/SvantekMonitor/api/db/Commands/ISvantekNoiseImportCommands.cs`
- Create: `svantekmonitor/SvantekMonitor/model/dto/SvantekNoiseImportCommit.cs`
- Create: `svantekmonitor/SvantekMonitor/api/SvantekRuleEvaluator.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/UseCases/StoreNoiseLevelsHandler.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/db/DBClient.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/db/IDBClient.cs`
- Modify: `svantekmonitor/SvantekMonitorTests/testdata/create.postgres.sql`
- Modify: `svantekmonitor/SvantekMonitorTests/testdata/reset.postgres.sql`
- Create: `svantekmonitor/SvantekMonitorTests/SvantekRuleEvaluatorTests.cs`
- Create: `svantekmonitor/SvantekMonitorTests/SvantekNoiseImportCommitTests.cs`

**Interfaces:**
- Consumes: request windows, common alert planner/requests/entity, existing sample/average PKs, and rule/contact queries.
- Produces: `CommitNoiseImportAsync(SvantekNoiseImportCommit, CancellationToken)` as the only noise write boundary.

- [ ] **Step 1: Write failing pure-evaluator tests**

Cover Alert/Caution activation, Alert suppressing Caution, LimitOff deactivation, deleted-rule deactivation, inactive activity window, deterministic correlation key, contacts, and no messaging/MQTT/DB dependency.

- [ ] **Step 2: Write failing transactional tests**

Test sorted/deduplicated samples, newly closed 8-hour blocks, monotonic `LastDataTime15Min`, no-op replay, concurrent replay, online recovery only when newest sample is within 24 hours, data MQTT only with a new row, notifications/deliveries, and rollback after injected failures before samples, aggregates, watermark, rules, notifications, and outbox save.

```csharp
var result = await commands.CommitNoiseImportAsync(commit, cancellationToken);
Assert.AreEqual(newestSample, result.Watermark);
Assert.AreEqual(1, CountRows("monitor_delivery_outbox", "kind = 'MqttDataInserted'"));
Assert.AreEqual(0, CountDuplicates("svantek_noise_level", "serial_id, sample_time"));
```

Define the private `CountRows` and `CountDuplicates` helpers in `SvantekNoiseImportCommitTests.cs` using the isolated fixture's `NpgsqlConnection`; identifiers are fixed test constants, never user input.

- [ ] **Step 3: Run focused tests and verify missing command**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~SvantekRuleEvaluatorTests|FullyQualifiedName~SvantekNoiseImportCommitTests"`

Expected: FAIL because evaluator/commit port do not exist.

- [ ] **Step 4: Define commit records and test seam**

Include monitor snapshot, normalized samples, closed aggregate endpoints, expected/current rule mutations, alert plans, and `UtcNow`. Return `Applied`, inserted sample count, monotonic watermark, and new delivery requests. Add protected `BeforeNoiseCommitStageAsync(SvantekNoiseCommitStage, token)` solely for deterministic rollback tests.

- [ ] **Step 5: Implement one EF transaction**

Insert only missing composite-PK samples; compute aggregates from committed rows; upsert aggregate composite keys; set watermark only when greater; recover Offline only for a qualifying newest sample; conditionally update rule state; insert deterministic notifications and outbox rows. If a conditional transition loses a race, omit its notification/deliveries rather than failing the whole measurement commit.

- [ ] **Step 6: Replace piecemeal handler writes**

Parse with invariant culture, normalize UTC, sort by timestamp, deduplicate, evaluate rules without side effects, and call only `CommitNoiseImportAsync`. Remove `InsertNoiseRecordsTable`, `Create8hourAverage`, `WriteLatestTimestamp`, `SetMonitorOffline`, and direct rule processor calls from the handler. Task 6 injects the registered common dispatcher and adds the immediate post-commit pass.

- [ ] **Step 7: Run atomicity and replay tests**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~SvantekRuleEvaluatorTests|FullyQualifiedName~SvantekNoiseImportCommitTests"`

Expected: PASS, including every forced rollback stage and concurrent replay.

- [ ] **Step 8: Commit atomic ingestion**

```bash
git add svantekmonitor/SvantekMonitor/api svantekmonitor/SvantekMonitor/model/dto svantekmonitor/SvantekMonitorTests
git commit -m "feat(svantek): commit noise imports atomically"
```

---

### Task 6: Implement Svantek's shared outbox adapter and scheduled dispatcher

**Files:**
- Create: `svantekmonitor/SvantekMonitor/api/SvantekDeliveryFailureSink.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/db/DBClient.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/db/IDBClient.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/SvantekMonitorServices.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/SvantekApi.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/SvantekService.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/MonitorJobRunner.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/MonitorJobDispatcher.cs`
- Modify: `svantekmonitor/SvantekMonitor/appsettings.json`
- Create: `svantekmonitor/SvantekMonitorTests/SvantekOutboxPersistenceTests.cs`
- Create: `svantekmonitor/SvantekMonitorTests/SvantekOutboxSchedulingTests.cs`

**Interfaces:**
- Consumes: common outbox ports/entity/dispatcher.
- Produces: producer-isolated fenced persistence, one-minute `DispatchOutbox`, and immediate post-import dispatch.

- [ ] **Step 1: Write failing persistence and scheduler tests**

Port MyATM claim/fencing cases for producer Svantek. Assert each failed retry/dead-letter is written through `ISvantekOperationalCommands`, `AnyDeliveryFailure` faults after a transient retry, `DispatchOutbox` exists in all three scheduler surfaces, and cancellation reaches the dispatcher.

- [ ] **Step 2: Run tests and verify missing adapter/job**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~SvantekOutbox"`

Expected: FAIL because Svantek does not implement common ports or schedule dispatch.

- [ ] **Step 3: Implement common ports in `DBClient`**

Use the same approved one-row predicate/order/fencing as MyATM, but require the caller producer and let each dispatcher claim only Svantek. Complete/dead-letter contact audits in the same transaction.

- [ ] **Step 4: Register common options/dispatcher/failure sink**

Use producer Svantek, failure mode AnyDeliveryFailure, 50/30s/120s/30s/30m/8 defaults, configured portal URL, and common alert/insert topics. The failure sink records every failed attempt through `ISvantekOperationalCommands` after the fenced outcome succeeds.

- [ ] **Step 5: Add the scheduled job**

Add `DispatchOutbox` with cron `0 0/1 * * * ?`, include it in `ISvantekMonitorJobs`, runner, and supported names, expose `DispatchOutboxAsync`, and pass cancellation end-to-end. Inject the same dispatcher into `StoreNoiseLevelsHandler` and invoke one bounded pass immediately after each successful atomic commit; a dispatch failure faults the job without rolling back the committed import.

- [ ] **Step 6: Run outbox tests**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~SvantekOutbox"`

Expected: PASS for producer isolation, fencing, retries, audits, scheduling, and aggregate failure.

- [ ] **Step 7: Commit durable dispatch**

```bash
git add svantekmonitor/SvantekMonitor svantekmonitor/SvantekMonitorTests/SvantekOutboxPersistenceTests.cs svantekmonitor/SvantekMonitorTests/SvantekOutboxSchedulingTests.cs
git commit -m "feat(svantek): dispatch shared durable deliveries"
```

---

### Task 7: Implement timezone-safe active-site duration

**Files:**
- Create: `svantekmonitor/SvantekMonitor/api/SiteActiveDurationCalculator.cs`
- Create: `svantekmonitor/SvantekMonitorTests/SiteActiveDurationCalculatorTests.cs`
- Create: `svantekmonitor/SvantekMonitorTests/SvantekMonitorReaderSiteHoursTests.cs`
- Modify: `svantekmonitor/SvantekMonitor/model/dto/NoiseMonitorReadDto.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/SvantekMonitorReader.cs`

**Interfaces:**
- Produces: `SiteOperatingHours` and `SiteActiveDurationCalculator.Calculate(SiteOperatingHours, TimeZoneInfo, DateTime fromUtc, DateTime toUtc)`.
- Consumes: weekday/Saturday/Sunday start/end pairs and monitor timezone.

- [ ] **Step 1: Write failing schedule/DST tests**

Cover same-day, closed day, one-null invalid pair, multi-day, overnight, equal-boundary 24-hour day, partial first/last day, spring invalid time advanced to valid, fall ambiguous start earlier/end later, missing timezone, and deployment start when no sample exists.

- [ ] **Step 2: Run calculator tests and verify failure**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~SiteActiveDurationCalculatorTests"`

Expected: FAIL because the calculator/site-hour read model does not exist.

- [ ] **Step 3: Implement exact local-boundary rules**

Define `SiteOperatingHours` with `WeekdayStart`, `WeekdayEnd`, `SaturdayStart`, `SaturdayEnd`, `SundayStart`, and `SundayEnd` nullable `TimeSpan` properties. Generate each local day's interval, normalize invalid/ambiguous endpoints as specified, convert to UTC, intersect with `[fromUtc,toUtc]`, and sum positive durations. Throw `InvalidOperationException` for invalid timezone or a half-configured pair.

- [ ] **Step 4: Populate site hours/timezone on read DTOs**

Extend the narrow monitor query/read mapping; do not make handlers query broad `DBClient` fields directly.

- [ ] **Step 5: Run calculator and reader tests**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~SiteActiveDurationCalculatorTests|FullyQualifiedName~SvantekMonitorReader"`

Expected: PASS for Europe/London DST fixtures and closed-day behavior.

- [ ] **Step 6: Commit site-hours calculation**

```bash
git add svantekmonitor/SvantekMonitor/api/SiteActiveDurationCalculator.cs svantekmonitor/SvantekMonitor/api/SvantekMonitorReader.cs svantekmonitor/SvantekMonitor/model/dto/NoiseMonitorReadDto.cs svantekmonitor/SvantekMonitorTests/SiteActiveDurationCalculatorTests.cs svantekmonitor/SvantekMonitorTests/SvantekMonitorReaderSiteHoursTests.cs
git commit -m "feat(svantek): calculate active site duration"
```

---

### Task 8: Make offline, battery, and site-average transitions atomic

**Files:**
- Create: `svantekmonitor/SvantekMonitor/api/db/Commands/ISvantekAlertCommitCommands.cs`
- Create: `svantekmonitor/SvantekMonitor/model/dto/SvantekAlertCommit.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/db/DBClient.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/db/IDBClient.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/UseCases/CheckForOfflineMonitorsHandler.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/UseCases/NotifyBatteryLevelsHandler.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/UseCases/NotifySiteAveragesHandler.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/SvantekRuleProcessor.cs`
- Create: `svantekmonitor/SvantekMonitorTests/SvantekScheduledAlertCommitTests.cs`

**Interfaces:**
- Consumes: common alert planner, site-duration calculator, current state snapshots, and shared outbox entity.
- Produces: `CommitAlertAsync` for conditional state + notification + delivery transactions.

- [ ] **Step 1: Write failing transition tests**

Cover offline edge only after active seconds are strictly greater than rule period, online recovery, battery Alert/Caution/Off edges, site-average rule activation/deactivation, concurrent stale expected state, replay, contact windows, and rollback between state/notification/outbox stages.

- [ ] **Step 2: Run tests and verify piecemeal behavior**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~SvantekScheduledAlertCommitTests"`

Expected: FAIL because handlers update state and notify separately.

- [ ] **Step 3: Define conditional commit records**

Include expected/current offline or battery/rule state, optional site-average write, one notification plan, correlation key, and `UtcNow`. Return `Applied` and created common delivery requests.

- [ ] **Step 4: Implement `CommitAlertAsync` transaction**

Conditionally update exactly one owning state. When the expected state no longer matches, return `Applied=false` with no notification/outbox. Otherwise write site average when present, notification, and all delivery rows in one transaction.

- [ ] **Step 5: Refactor handlers to prepare then commit**

Handlers may query and calculate, but may not inject messaging/MQTT or call direct notification/state writers. Use deterministic prefixes `svantek:offline`, `svantek:battery`, and `svantek:site-average`. Continue independent monitors and aggregate failures through Task 3.

- [ ] **Step 6: Run scheduled transition tests**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~SvantekScheduledAlertCommitTests|FullyQualifiedName~SiteActiveDurationCalculatorTests"`

Expected: PASS with no duplicates after replay/concurrency.

- [ ] **Step 7: Commit scheduled atomic alerts**

```bash
git add svantekmonitor/SvantekMonitor/api svantekmonitor/SvantekMonitor/model/dto/SvantekAlertCommit.cs svantekmonitor/SvantekMonitorTests/SvantekScheduledAlertCommitTests.cs
git commit -m "feat(svantek): commit scheduled alerts atomically"
```

---

### Task 9: Make sound-recording follow-up fully async, cancellable, and retry-safe

**Files:**
- Create: `svantekmonitor/SvantekMonitor/api/db/Commands/ISvantekSoundRecordingCommands.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/db/DBClient.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/UseCases/CheckForSoundRecordingsHandler.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/SvantekApi.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/SvantekService.cs`
- Modify: `svantekmonitor/SvantekMonitorTests/TestCheckForSoundRecordings.cs`
- Modify: `svantekmonitor/SvantekMonitorTests/TestCheckForSoundRecordingStorage.cs`

**Interfaces:**
- Consumes: Task 1 async file list/download, `IBlobStorageService`, and narrow notification link command.
- Produces: retry-safe `{notificationId}.wav` upload/link update with aggregate failures.

- [ ] **Step 1: Add failing cancellation/retry/continuation tests**

Assert one file-list request per project/point/day, nearest trigger selection, deterministic object key, token flow through list/download/blob/DB, retry after blob-success/link-failure overwrites the same key, later alerts continue after a failure, and final aggregate exception identifies failed notifications.

- [ ] **Step 2: Run sound tests and verify sync gateway failure**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~TestCheckForSoundRecording"`

Expected: FAIL because listing/download/link update are not fully async.

- [ ] **Step 3: Add narrow async link command**

Expose `Task<bool> UpdateRecordingLinkAsync(Guid notificationId, string objectName, CancellationToken)` and implement it with EF `ExecuteUpdateAsync`.

- [ ] **Step 4: Refactor handler**

Remove the sync `Run()` wrapper. Await `GetProjectFilesAsync`, `GetSoundFileAsync`, blob write, and link update. Keep the per-run cache and day-code rule. Capture per-notification failures, continue, then throw the job aggregate.

- [ ] **Step 5: Run sound tests**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~TestCheckForSoundRecording"`

Expected: PASS for cancellation, idempotent retry, cache, and aggregation.

- [ ] **Step 6: Commit async sound follow-up**

```bash
git add svantekmonitor/SvantekMonitor/api svantekmonitor/SvantekMonitorTests/TestCheckForSoundRecordings.cs svantekmonitor/SvantekMonitorTests/TestCheckForSoundRecordingStorage.cs
git commit -m "refactor(svantek): make sound follow-up retry-safe"
```

---

### Task 10: Add database readiness without weakening liveness

**Files:**
- Create: `svantekmonitor/SvantekMonitor/api/db/Queries/ISvantekHealthQueries.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/db/DBClient.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/db/IDBClient.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/SvantekMonitorServices.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/MonitorApiEndpoints.cs`
- Modify: `svantekmonitor/SvantekMonitorTests/TestMonitorApiEndpoints.cs`

**Interfaces:**
- Produces: `CanConnectAsync(CancellationToken)` and `/readiness` HTTP 200/503.
- Consumes: EF `Database.CanConnectAsync`.

- [ ] **Step 1: Write failing endpoint tests**

Assert route set equals `/liveness` and `/readiness`; liveness never calls DB; readiness returns 200 when true, 503 when false or DB throws, and passes `HttpContext.RequestAborted`.

- [ ] **Step 2: Run endpoint tests and verify missing readiness**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~TestMonitorApiEndpoints"`

Expected: FAIL because only `/liveness` exists.

- [ ] **Step 3: Implement narrow health query and endpoint**

Register `ISvantekHealthQueries` from `IDBClient`. Map:

```csharp
endpoints.MapGet("/readiness", async (ISvantekHealthQueries health, CancellationToken ct) =>
    await health.CanConnectAsync(ct) ? Results.Ok() : Results.StatusCode(503));
```

Catch DB exceptions inside the endpoint/query boundary and return 503; do not change liveness text.

- [ ] **Step 4: Run endpoint tests**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~TestMonitorApiEndpoints"`

Expected: PASS for both routes and all readiness outcomes.

- [ ] **Step 5: Commit readiness**

```bash
git add svantekmonitor/SvantekMonitor/api svantekmonitor/SvantekMonitorTests/TestMonitorApiEndpoints.cs
git commit -m "feat(svantek): add database readiness"
```

---

### Task 11: Restore excluded suites and strengthen architecture/scheduler coverage

**Files:**
- Modify: `svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj`
- Rewrite: `svantekmonitor/SvantekMonitorTests/TestRules.cs`
- Rewrite: `svantekmonitor/SvantekMonitorTests/TestSvantekApi.cs`
- Rewrite: `svantekmonitor/SvantekMonitorTests/TestSvantekApiException.cs`
- Rewrite: `svantekmonitor/SvantekMonitorTests/TestSvantekApiNoiseLevels.cs`
- Modify: `svantekmonitor/SvantekMonitorTests/Architecture/SvantekDependencyBoundaryTests.cs`
- Create: `svantekmonitor/SvantekMonitorTests/SvantekSchedulerParityTests.cs`

**Interfaces:**
- Consumes: all production boundaries from Tasks 1–10.
- Produces: complete compiled regression and architecture gates.

- [ ] **Step 1: Remove all four `<Compile Remove>` entries**

Run immediately: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --no-restore`

Expected: FAIL with obsolete constructor/method errors, proving the suites are compiled again.

- [ ] **Step 2: Rewrite `TestRules.cs` against pure evaluator/commit contracts**

Cover Alert/Caution activation/deactivation, deleted rules, activity windows, deterministic plans, and replay. Do not restore old direct messaging delegates.

- [ ] **Step 3: Rewrite API and exception suites against async entry points**

`TestSvantekApi.cs` covers every async facade method and cancellation. `TestSvantekApiException.cs` covers pre-loop immediate failure, per-unit continuation, operational recording, aggregate throw, and cancellation passthrough.

- [ ] **Step 4: Rewrite noise suite against windows and atomic commits**

`TestSvantekApiNoiseLevels.cs` verifies multi-project 12-hour requests, five-minute overlap, no future end, no-data status, malformed point mapping, normalized samples, one commit per response window, and post-commit dispatch failure without data rollback.

- [ ] **Step 5: Strengthen architecture tests**

Recursively scan all production C# under `svantekmonitor/SvantekMonitor/api`. Fail on concrete `DBClient` fields outside composition/persistence, direct messaging/MQTT in `UseCases`, missing atomic commit ports, `.Result`, `.Wait()`, or `GetAwaiter().GetResult()` in scheduled/vendor paths, and Mapperly outside the app project.

- [ ] **Step 6: Add exact scheduler parity test**

Parse enabled appsettings job names and compare them with `SupportedJobNames` and a table-driven `MonitorJobRunner` invocation list, including `DispatchOutbox`. Verify the same cancellation token reaches each mocked service method.

- [ ] **Step 7: Run the full Svantek suite**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --no-restore`

Expected: PASS with all four restored files compiled, zero failed, and zero skipped tests.

- [ ] **Step 8: Commit restored coverage**

```bash
git add svantekmonitor/SvantekMonitorTests
git commit -m "test(svantek): restore reliability coverage"
```

---

### Task 12: Add deployment guards, documentation, and run the release gate

**Files:**
- Create: `svantekmonitor/database/migrations/2026-07-15-enable-shared-delivery.postgres.sql`
- Create: `svantekmonitor/database/migrations/2026-07-15-enable-shared-delivery.sqlserver.sql`
- Create: `svantekmonitor/database/migrations/2026-07-15-rollback-shared-delivery.postgres.sql`
- Create: `svantekmonitor/database/migrations/2026-07-15-rollback-shared-delivery.sqlserver.sql`
- Create: `svantekmonitor/SvantekMonitorTests/EntityFramework/SvantekDeliveryMigrationContractTests.cs`
- Modify: `svantekmonitor/README.md`
- Modify: `project_state.md` with actual Svantek/root verification totals and rollout status.

**Interfaces:**
- Consumes: common schema prerequisite and all Tasks 1–11.
- Produces: deployable/rollback-safe Svantek release documentation and final proof.

- [ ] **Step 1: Write failing migration guard tests**

Assert forward scripts abort when the shared table or existing sample/aggregate composite keys are absent, never create a monitor-specific outbox, and never drop shared rows. Assert rollback scripts pause/preserve delivery work and contain no DELETE/DROP for the common table.

- [ ] **Step 2: Implement provider-equivalent guard scripts**

The scripts are deployment assertions because Svantek needs no new app-local table: verify the common table, notification FK, sample composite key, and aggregate composite key. Rollback is intentionally preservation-only and documents that pending Svantek rows remain for a future forward deploy.

- [ ] **Step 3: Run migration contract tests**

Run: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~SvantekDeliveryMigrationContractTests"`

Expected: PASS for PostgreSQL and SQL Server files.

- [ ] **Step 4: Document deploy/smoke/rollback sequence**

Update README with: deploy shared schema first; deploy Svantek guards/app; run one-shot StoreMonitors, StoreNoiseLevels, CheckForOfflineMonitors, NotifyBatteryLevels, NotifySiteAverages, CheckForSoundRecordings, and DispatchOutbox; verify readiness and outbox status counts; then enable Quartz. Rollback pauses jobs, deploys the prior app, and preserves shared rows.

- [ ] **Step 5: Run the complete Svantek suite**

Run: `dotnet test svantekmonitor/svantekmonitor.sln --no-restore`

Expected: PASS with zero failed and zero skipped tests.

- [ ] **Step 6: Build the root solution**

Run: `dotnet build rvt-monitors.sln --no-restore`

Expected: exit 0 with zero errors and no new warnings.

- [ ] **Step 7: Run architectural text guards**

Run: `rg -n "\.Result|\.Wait\(|GetAwaiter\(\)\.GetResult\(\)|IMessageService|IMonitorEventPublisher" svantekmonitor/SvantekMonitor/api/UseCases svantekmonitor/SvantekMonitor/api/http`

Expected: no results.

Run: `rg -n "Compile Remove=\"Test(Rules|SvantekApi|SvantekApiException|SvantekApiNoiseLevels)\.cs\"" svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj`

Expected: no results.

- [ ] **Step 8: Verify formatting and commit release assets**

Run: `git diff --check`

Expected: no output.

Record the actual restored-suite test total, root-build result, migration-contract result, and the statement that production rollout still requires one-shot smoke jobs before enabling Quartz in `project_state.md`.

```bash
git add svantekmonitor/database svantekmonitor/README.md svantekmonitor/SvantekMonitorTests/EntityFramework/SvantekDeliveryMigrationContractTests.cs project_state.md
git commit -m "docs(svantek): add durable delivery rollout"
```

Svantek is complete only when the full suite, root build, migration contracts, restored-suite check, and architecture text guards all pass.
