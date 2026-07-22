# MyATM Omnidots-Alignment Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Apply `superpowers:test-driven-development` to every behavior change and `superpowers:verification-before-completion` before completion claims.

**Goal:** Make MyATM UTC-safe, site-hours-aware, bounded at every vendor boundary, truthful about partial fleet failures, decoupled from delivery latency, and structurally aligned with the current Omnidots monitor.

**Architecture:** Keep `IDBClient` and `MyAtmApi` as compatibility facades. Scheduled production work resolves focused handlers and narrow query/command ports directly. Normalize all instants through one tick-preserving UTC operation, calculate offline duration through a pure app-local site-hours calculator, persist existing atomic alert/outbox commits unchanged, and deliver only through the dedicated shared outbox job.

**Tech Stack:** .NET 10, ASP.NET Core shared monitor host, C# 14, EF Core 10.0.4, PostgreSQL/Npgsql, SQL Server compatibility, Quartz, Mapperly analyzer-only, MSTest, Moq.

**Approved design:** `docs/superpowers/specs/2026-07-16-myatm-omnidots-alignment-remediation-design.md`

**Constraints:**

- Keep `StoreDustLevels` at 30 minutes and `DispatchOutbox` at one minute.
- Do not add or replace alert/outbox tables.
- Treat SQL Server `DateTimeKind.Unspecified` persistence values as UTC without changing ticks.
- Never persist or log API keys, connection strings, recipient data, or vendor response bodies.
- Use the runtime-only `RVT__POSTGRES_INTEGRATION_CONNECTION` setting for integration tests.
- Preserve existing occurrence, hysteresis, suppression, escalation, atomic commit, retry, and dead-letter behavior.

---

### Task 1: Fence UTC normalization at shared and MyATM boundaries

**Files:**
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Utilities/DateTimeUtil.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Utilities/DateTimeUtilTests.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/http/MyAtmHttpGateway.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmRuleEvaluator.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmRuleProcessor.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/DBClient.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/UseCases/StoreAccessoryInfoHandler.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/MyAtmHttpGatewayTests.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/TestDbClient.cs`

**Produces:** `DateTimeUtil.AsUtc(DateTime)` and nullable overload with the exact approved UTC/Local/Unspecified semantics.

- [ ] **Step 1: Write failing normalization tests**

Assert UTC returns unchanged, Local represents the same instant in UTC, Unspecified retains identical ticks while becoming UTC, and nullable values preserve null.

- [ ] **Step 2: Run the focused common test**

Run: `dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter "FullyQualifiedName~DateTimeUtilTests" --no-restore --nologo`

Expected: FAIL because `AsUtc` does not exist.

- [ ] **Step 3: Implement the pure helper**

Use a `DateTimeKind` switch. Do not call `ToUniversalTime()` for `Unspecified`.

- [ ] **Step 4: Add failing MyATM boundary regressions**

Pass Unspecified cursors/vendor timestamps and assert generated OData values, page timestamps, deduplication keys, alert occurrence timestamps, watermarks, notification times, and outbox times keep the original ticks and carry `Utc` kind. Retain Local-input coverage for genuine local values.

- [ ] **Step 5: Replace unsafe MyATM conversions**

Use `DateTimeUtil.AsUtc` at every MyATM vendor/database read and write boundary and in comparison/key construction. Leave deliberate display-local conversions untouched. UTC-only evaluators may validate and reject non-UTC after boundary normalization.

- [ ] **Step 6: Run focused UTC coverage**

Run: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~MyAtmHttpGatewayTests|FullyQualifiedName~TestDbClient|FullyQualifiedName~MyAtmRuleEvaluatorTests|FullyQualifiedName~OfflineAlertCommitTests" --no-restore --nologo`

Expected: PASS with no tick drift.

- [ ] **Step 7: Commit UTC boundary work**

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Utilities rvt-monitor-common/Rvt.Monitor.CommonTests/Utilities myatmmonitor/MyAtmMonitor myatmmonitor/MyAtmMonitorTests
git commit -m "fix(myatm): enforce utc boundary semantics"
```

---

### Task 2: Bind and validate vendor/monitor options and bound HTTP behavior

**Files:**
- Create: `myatmmonitor/MyAtmMonitor/model/config/MyAtmVendorOptions.cs`
- Create: `myatmmonitor/MyAtmMonitor/model/config/MyAtmOptionsValidators.cs`
- Modify: `myatmmonitor/MyAtmMonitor/model/config/MyAtmMonitorOptions.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/http/MyAtmRequestPolicy.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/http/HttpWebClient.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmMonitorServices.cs`
- Modify: `myatmmonitor/MyAtmMonitor/appsettings.json`
- Modify: `myatmmonitor/MyAtmMonitorTests/MyAtmMonitorOptionsTests.cs`
- Create: `myatmmonitor/MyAtmMonitorTests/MyAtmVendorOptionsTests.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/HttpWebClientTests.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/MyAtmMonitorServiceRegistrationTests.cs`

**Produces:** startup-validated MyATM options and a single configurable paced/retry policy used by every endpoint.

- [ ] **Step 1: Write failing options-pipeline tests**

Cover invalid customer/page limits, missing token/base URL, invalid absolute URL, non-positive response limit/pacing/attempts, fallback delay greater than retry cap, and successful binding/resolution. Add `MaxDevicePagesPerRun` default 100.

- [ ] **Step 2: Write failing HTTP-boundary tests**

Assert `Retry-After` delta/date are capped, large declared and streamed success bodies fail safely, error bodies are neither returned nor included in exceptions, 4xx responses do not retry, 408/429/5xx do retry, request pacing is shared, and caller cancellation propagates.

- [ ] **Step 3: Run focused tests and confirm failures**

Run: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~MyAtmMonitorOptionsTests|FullyQualifiedName~MyAtmVendorOptionsTests|FullyQualifiedName~HttpWebClientTests|FullyQualifiedName~MyAtmMonitorServiceRegistrationTests" --no-restore --nologo`

- [ ] **Step 4: Implement typed options**

Bind `MyAtmMonitor` and `MyAtmVendor` via `AddOptions<T>().BindConfiguration(...).ValidateOnStart()`. Keep compatibility fallback setup from existing runtime `RvtConfig` values without copying secrets into tracked files. Validators return safe field-name failures only.

Use defaults: response limit 4 MiB, maximum attempts 5, minimum request interval 500 ms, fallback retry cap 30 seconds, and maximum honored `Retry-After` 30 seconds.

- [ ] **Step 5: Implement bounded HTTP reads/retries**

Use `HttpRequestMessage` plus `SendAsync(..., ResponseHeadersRead, token)`. Reject oversized `Content-Length`, stream success content with a hard byte limit, do not read non-success content, include only safe path/status context, cap all retry delays, and dispose every request/response.

- [ ] **Step 6: Run focused configuration/HTTP tests**

Expected: PASS and no error assertion contains the sentinel vendor body or API key.

- [ ] **Step 7: Commit bounded vendor configuration**

```bash
git add myatmmonitor/MyAtmMonitor/model/config myatmmonitor/MyAtmMonitor/api/http myatmmonitor/MyAtmMonitor/api/MyAtmMonitorServices.cs myatmmonitor/MyAtmMonitor/appsettings.json myatmmonitor/MyAtmMonitorTests
git commit -m "fix(myatm): bound vendor configuration and responses"
```

---

### Task 3: Add reusable best-effort fleet failure aggregation

**Files:**
- Create: `myatmmonitor/MyAtmMonitor/api/MyAtmJobAggregateException.cs`
- Create: `myatmmonitor/MyAtmMonitor/api/MyAtmFailureCollector.cs`
- Create: `myatmmonitor/MyAtmMonitorTests/MyAtmFailureCollectorTests.cs`

**Produces:** immutable `MyAtmJobFailure`, `MyAtmJobAggregateException`, and collector preserving both primary and optional recording exceptions.

- [ ] **Step 1: Write failing collector tests**

Assert successful operational recording, failed operational recording, safe identifier aggregation, immutable failure exposure, no failures/no throw, and immediate cancellation rethrow.

- [ ] **Step 2: Run focused tests and confirm missing types**

Run: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~MyAtmFailureCollectorTests" --no-restore --nologo`

- [ ] **Step 3: Implement the collector**

Model it on Omnidots' `OmnidotsMonitorFailure.Record`, but make cancellation token-aware: only caller-requested `OperationCanceledException` bypasses capture. Never let `HandleException` replace the primary exception.

- [ ] **Step 4: Run and commit**

```bash
git add myatmmonitor/MyAtmMonitor/api/MyAtmJobAggregateException.cs myatmmonitor/MyAtmMonitor/api/MyAtmFailureCollector.cs myatmmonitor/MyAtmMonitorTests/MyAtmFailureCollectorTests.cs
git commit -m "refactor(myatm): standardize fleet failure reporting"
```

---

### Task 4: Bound and isolate catalogue synchronization

**Files:**
- Modify: `myatmmonitor/MyAtmMonitor/api/UseCases/StoreMonitorsHandler.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmApi.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmMonitorServices.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/TestMyAtmApiMonitors.cs`
- Create: `myatmmonitor/MyAtmMonitorTests/StoreMonitorsHandlerTests.cs`

**Consumes:** `MyAtmFailureCollector`, `MyAtmMonitorOptions.MaxDevicePagesPerRun`.

- [ ] **Step 1: Write failing catalogue tests**

Cover a partial final page, multiple full pages then partial page, repeated full-page identifiers, page cap reached while still full, checked offset progress, one failed device detail followed by successful devices/pages, successful DTO persistence despite detail failures, list-page failure stopping continuation, operational recorder failure preserving the list/detail failure, and cancellation.

- [ ] **Step 2: Run focused tests and observe current abort/unbounded behavior**

Run: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~StoreMonitorsHandlerTests|FullyQualifiedName~TestMyAtmApiMonitors" --no-restore --nologo`

- [ ] **Step 3: Implement bounded pagination**

Loop `pageNumber < MaxDevicePagesPerRun`, calculate a deterministic in-memory fingerprint from ordered serial identifiers, reject repeated full pages, use `checked` offset advancement, and aggregate an explicit incomplete-catalogue failure when the final allowed page is full.

- [ ] **Step 4: Isolate detail retrieval**

Catch non-cancellation exceptions per device, record through the collector, continue, and persist successful transformed DTOs once per page. On list failure, capture and stop because the next page cannot be trusted. Throw the aggregate at the end.

- [ ] **Step 5: Run focused tests and commit**

```bash
git add myatmmonitor/MyAtmMonitor/api/UseCases/StoreMonitorsHandler.cs myatmmonitor/MyAtmMonitor/api/MyAtmApi.cs myatmmonitor/MyAtmMonitor/api/MyAtmMonitorServices.cs myatmmonitor/MyAtmMonitorTests
git commit -m "fix(myatm): bound catalogue synchronization"
```

---

### Task 5: Match Omnidots site-hours and DST offline behavior

**Files:**
- Create: `myatmmonitor/MyAtmMonitor/model/config/MyAtmSiteSchedule.cs`
- Create: `myatmmonitor/MyAtmMonitor/api/db/Queries/IMyAtmSiteScheduleQueries.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/IDBClient.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/DBClient.cs`
- Create: `myatmmonitor/MyAtmMonitor/api/UseCases/MyAtmSiteActiveDurationCalculator.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/UseCases/CheckForOfflineMonitorsHandler.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmApi.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmMonitorServices.cs`
- Create: `myatmmonitor/MyAtmMonitorTests/MyAtmSiteActiveDurationCalculatorTests.cs`
- Create: `myatmmonitor/MyAtmMonitorTests/CheckForOfflineMonitorsHandlerTests.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/TestDbClient.cs`

**Produces:** narrow active-site schedule query and pure DST-safe active-duration calculator.

- [ ] **Step 1: Port the approved Omnidots calculator tests**

Cover within/before/after hours, multiple days, closed weekends, overnight schedules, explicit midnight-to-24-hours, spring-forward elapsed time, fall-back repeated time, invalid gap boundary, ambiguous overlap boundary, non-UTC rejection, and non-positive intervals.

- [ ] **Step 2: Write failing handler tests**

Use a fake `TimeProvider`. Cover offline after active duration exceeds the rule, no offline change during closed time, online recovery for recent data, online recovery when active duration is insufficient, missing/invalid timezone, missing/invalid site schedule, per-monitor continuation, recorder failure preservation, aggregate job failure, stable UTC occurrence inputs, and cancellation.

- [ ] **Step 3: Write failing PostgreSQL schedule-query test**

Seed monitor -> active deployment -> contract -> site and assert all six schedule fields. Assert missing active deployment/site produces a safe configuration error. Verify query provider translation and no tracking.

- [ ] **Step 4: Implement the narrow schedule query and calculator**

Follow Omnidots date iteration and boundary validation exactly, including looking one local day back for overnight intervals. Keep the types app-local; do not move schedule policy into common.

- [ ] **Step 5: Refactor offline handling**

Capture one UTC `TimeProvider` instant, normalize persisted last-data values, resolve each monitor timezone, read its site schedule, calculate active elapsed time, and preserve the existing atomic offline/recovery commit path. Apply `MyAtmFailureCollector` per monitor and throw after continuation.

- [ ] **Step 6: Run focused and integration tests**

Run: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~MyAtmSiteActiveDurationCalculatorTests|FullyQualifiedName~CheckForOfflineMonitorsHandlerTests|FullyQualifiedName~TestDbClient" --no-restore --nologo`

- [ ] **Step 7: Commit site-hours behavior**

```bash
git add myatmmonitor/MyAtmMonitor myatmmonitor/MyAtmMonitorTests
git commit -m "fix(myatm): evaluate offline time within site hours"
```

---

### Task 6: Decouple dust delivery and align remaining fleet handlers

**Files:**
- Modify: `myatmmonitor/MyAtmMonitor/api/UseCases/StoreDustLevelsHandler.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/UseCases/StoreAccessoryInfoHandler.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/UseCases/ProcessDustLevelsHandler.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmApi.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/TestMyAtmApi.cs`
- Create: `myatmmonitor/MyAtmMonitorTests/MyAtmFleetFailureSemanticsTests.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/MyAtmOutboxContractTests.cs`

- [ ] **Step 1: Write failing delivery-decoupling test**

Commit a dust page that creates delivery rows and assert `StoreDustLevelsHandler` returns without invoking `MonitorDeliveryDispatcher`. Separately assert `DispatchOutbox` still invokes the dispatcher once with the same cancellation token.

- [ ] **Step 2: Write failing fleet-failure tests**

For dust, accessory, and aggregate processing, make early and late monitors fail, assert successful middle work completes, original exceptions survive operational recorder failures, one immutable aggregate is thrown, and cancellation stops immediately.

- [ ] **Step 3: Remove immediate dust dispatch**

Delete the dispatcher dependency from `StoreDustLevelsHandler`; leave transactionally enqueued rows unchanged. Use injected `TimeProvider` for page evaluation and commit creation instants.

- [ ] **Step 4: Apply the common failure collector**

Replace ad hoc lists/recording with `MyAtmFailureCollector` at each independent monitor boundary. Never capture requested cancellation.

- [ ] **Step 5: Run handler/outbox tests**

Run: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~MyAtmFleetFailureSemanticsTests|FullyQualifiedName~MyAtmOutboxContractTests|FullyQualifiedName~TestMyAtmApi" --no-restore --nologo`

- [ ] **Step 6: Commit decoupled fleet processing**

```bash
git add myatmmonitor/MyAtmMonitor/api myatmmonitor/MyAtmMonitorTests
git commit -m "refactor(myatm): decouple imports from delivery"
```

---

### Task 7: Compose scheduled work through focused handlers

**Files:**
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmService.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmMonitorServices.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmApi.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/MyAtmMonitorServiceRegistrationTests.cs`
- Create: `myatmmonitor/MyAtmMonitorTests/MyAtmServiceCompositionTests.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmScheduledAlertCommitBoundaryTests.cs`

- [ ] **Step 1: Write failing composition tests**

Assert every `IMyAtmMonitorJobs` method calls the correct focused handler/period with the original cancellation token. Assert the service constructor has no `MyAtmApi` or `IDBClient` parameter and the DI graph resolves all handlers, `TimeProvider`, options, gateway, shared dispatcher, compatibility facade, and scheduled job interface.

- [ ] **Step 2: Register narrow services and refactor `MyAtmService`**

Register handlers/readers/evaluator/processor explicitly. Inject focused handlers, options, and shared dispatcher into `MyAtmService`; keep `MyAtmApi` registered separately for compatibility. Scheduled production code must not call it.

- [ ] **Step 3: Run service, dispatcher, endpoint, and architecture tests**

Run: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~MyAtmServiceCompositionTests|FullyQualifiedName~MyAtmMonitorServiceRegistrationTests|FullyQualifiedName~MonitorJob|FullyQualifiedName~Architecture|FullyQualifiedName~TestMonitorApiEndpoints" --no-restore --nologo`

- [ ] **Step 4: Commit focused scheduled composition**

```bash
git add myatmmonitor/MyAtmMonitor/api myatmmonitor/MyAtmMonitorTests
git commit -m "refactor(myatm): compose scheduled jobs from handlers"
```

---

### Task 8: Replace the brittle Mapperly allow-list and document operations

**Files:**
- Modify: `myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmDependencyBoundaryTests.cs`
- Modify: `myatmmonitor/README.md`
- Modify: `project_state.md`

- [ ] **Step 1: Rewrite the Mapperly architecture test rule-first**

Discover every primary repository `.csproj` containing `Riok.Mapperly`. Parse XML and assert each is a non-test monitor app project, has `PrivateAssets=all`, and has `OutputItemType=Analyzer`. Independently assert no `rvt-monitor-common` project references Mapperly. Do not assert an exact list or project count.

- [ ] **Step 2: Add focused architecture fixtures if needed**

Extract the validation into test-local helpers accepting project XML/path and cover a valid monitor app, shared-common reference, test project, missing `PrivateAssets`, and missing/wrong `OutputItemType`.

- [ ] **Step 3: Update documentation**

Document UTC-only instants, SQL Server Unspecified handling, validated vendor settings, response/retry/page bounds, active-site offline semantics, 30-minute import cadence, one-minute independent dispatch, and failure aggregation. Update `project_state.md` with completed structure/variables and the folder-wide cross-monitor consistency requirement; include no live values.

- [ ] **Step 4: Run architecture/document checks and commit**

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~Architecture" --no-restore --nologo
git diff --check
git add myatmmonitor/MyAtmMonitorTests/Architecture myatmmonitor/README.md project_state.md
git commit -m "test(myatm): harden architecture consistency guards"
```

---

### Task 9: Full verification and final review

- [ ] **Step 1: Run focused common and MyATM tests**

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --no-restore --nologo
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-restore --nologo
```

Supply the approved PostgreSQL integration connection only as the environment of the MyATM test process. Never write it to a file or command output.

- [ ] **Step 2: Run builds**

```bash
dotnet build myatmmonitor/myatmmonitor.sln --no-restore --nologo
dotnet build rvt-monitors.sln --no-restore --nologo -m:1
```

- [ ] **Step 3: Run formatter and repository checks**

```bash
dotnet format rvt-monitors.sln --verify-no-changes --no-restore --verbosity minimal
git diff --check
git status --short
```

- [ ] **Step 4: Inspect the complete branch diff**

Verify no credential, connection, response body, destination, generated output, or unrelated change is tracked. Confirm schedules remain exactly 30 minutes and one minute.

- [ ] **Step 5: Request strict code review and remediate findings**

Use `superpowers:requesting-code-review`, rerun affected tests after every correction, then repeat the full gate.

- [ ] **Step 6: Record verification and final commit**

Update `project_state.md` with exact test/build counts and commit the verification note. Use `superpowers:finishing-a-development-branch` for the handoff; do not merge or push unless the user explicitly requests it.
