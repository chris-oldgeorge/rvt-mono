# Reliability Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the confirmed cancellation, silent-failure, UTC-watermark, and hard-coded Omnidots configuration defects without widening the monitor architecture refactor.

**Architecture:** Preserve the existing use-case and adapter boundaries. Cancellation remains an exceptional control-flow signal, optional Portal summaries retain their fallback behavior but emit structured logs, AirQ derives its empty watermark from an injected `TimeProvider`, and Omnidots deployment-specific values come only from configuration.

**Tech Stack:** .NET, MSTest, xUnit, Moq, `Microsoft.Extensions.Logging`, `TimeProvider`, ASP.NET Core configuration/options.

## Global Constraints

- Use explicit local variable types; do not introduce `var`.
- Write and run a failing behavior test before each production change.
- Do not change public HTTP contracts or database schemas.
- Do not log signed archive URLs, credentials, recipients, or vendor tokens.
- Preserve the intentionally untracked `.codex/`, `AGENTS.md`, and Sonar remediation plan.

---

### Task 1: Omnidots trace cancellation

**Files:**
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/TestOmnidotsCancellation.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitor/api/UseCases/StoreTracesHandler.cs`

**Interfaces:**
- Consumes: `StoreTracesHandler.RunAsync(DateTime, CancellationToken)`
- Produces: caller cancellation is rethrown and never recorded as `OmnidotsMonitorFailure`

- [x] Add a test whose gateway throws `OperationCanceledException` with the supplied cancelled token while reading one eligible monitor.
- [x] Run the focused Omnidots cancellation test and verify it fails with `OmnidotsImportException`.
- [x] Add an explicit token check at the monitor-loop boundary and a filtered `OperationCanceledException` catch before the general failure catch.
- [x] Run the focused test and the Omnidots test project; verify both pass.

### Task 2: Observable Portal fallback failures

**Files:**
- Create: `apps/portal/RvtPortal.Spa.Tests/MonitorDetailSummaryServiceTests.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/SiteArchiveServiceSecurityTests.cs`
- Modify: `apps/portal/RvtPortal.Spa/Api/MonitorDetailSummaryService.cs`
- Modify: `apps/portal/RvtPortal.Spa/Adapters/Sites/SiteArchiveAdapter.cs`
- Modify: `apps/portal/RvtPortal.Spa/Application/Monitors/MonitorDetailReader.cs`

**Interfaces:**
- Consumes: `IMonitorDetailSummaryService` and `ISiteArchivePort`
- Produces: summary cancellation propagates, genuine optional-summary failures retain fallback behavior with structured warning logs, and archive failures retain mapped results with structured error logs

- [x] Add a summary-service test that expects `OperationCanceledException` from the data source to propagate.
- [x] Add summary and archive tests that observe one structured log for genuine failures without asserting on implementation-only mock calls.
- [x] Run the focused Portal tests and verify the new cancellation/log assertions fail.
- [x] Inject typed loggers, rethrow cancellation before general catches, and log only operation and entity identifiers.
- [x] Pass the existing request token from `MonitorDetailReader` through summary-service methods so future cancellable data access can use it.
- [x] Run the focused tests and the Portal unit-test project; verify both pass.

### Task 3: AirQ UTC empty watermark and redundant catches

**Files:**
- Modify: `apps/monitors/airqmonitor/AirQMonitorTests/TestAirQCancellation.cs`
- Modify: `apps/monitors/airqmonitor/AirQMonitor/api/AirQApi.cs`
- Modify: `apps/monitors/airqmonitor/AirQMonitor/api/AirQMonitorServices.cs`
- Modify: `apps/monitors/airqmonitor/AirQMonitor/api/UseCases/StoreNoiseLevelsHandler.cs`
- Modify: `apps/monitors/airqmonitor/AirQMonitor/api/UseCases/StoreNoiseLevelsForDateHandler.cs`

**Interfaces:**
- Consumes: `TimeProvider.GetUtcNow()`
- Produces: a missing AirQ watermark starts exactly one year before injected UTC time and remains `DateTimeKind.Utc`

- [x] Add a handler test with a fixed `TimeProvider` that captures the watermark passed to `IAirQVendorGateway`.
- [x] Run the focused AirQ test and verify it fails because the handler uses server-local `DateTime.Now`.
- [x] Inject `TimeProvider`, derive the fallback watermark from `GetUtcNow().UtcDateTime`, and wire `TimeProvider.System` at compatibility/composition boundaries.
- [x] Remove the behavior-neutral `catch (AggregateException) { throw; }` blocks after the test is green.
- [x] Run the focused test and the AirQ test project; verify both pass.

### Task 4: Omnidots deployment-only defaults

**Files:**
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/TestMonitorJobScheduling.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/TestOmnidotsApi.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/TestUtil.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitor/appsettings.json`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitor/api/OmnidotsApi.cs`
- Modify: `docs/modules/monitors/omnidotsmonitor/README.md`
- Modify: `docs/operations/monitors/container-builds.md`

**Interfaces:**
- Consumes: `RVT__OMNIDOTS_MONITORING_ALERT_TO` and `Omnidots__TraceCollection__AllowedSerialIds__<index>`
- Produces: no personal recipient or serial number in checked-in runtime defaults; empty allow-list means the throttled filtered fleet is eligible

- [x] Change the appsettings contract test to require no checked-in recipient and an empty trace allow-list.
- [x] Add a compatibility-facade behavior test proving an arbitrary configured monitor is eligible without the former serial.
- [x] Run the focused tests and verify they fail against the committed defaults.
- [x] Remove the recipient and serial defaults from appsettings and compatibility/test helpers while retaining `MaxMonitorsPerRun`.
- [x] Update current operator documentation to describe deployment-supplied recipient and optional staged allow-list.
- [x] Run Omnidots tests and repository documentation/configuration guards.

### Task 5: Verification and state handoff

**Files:**
- Modify: `project_state.md`

**Interfaces:**
- Consumes: repository test and engineering-standards scripts
- Produces: a reproducible handoff recording completed fixes and any environment-blocked checks

- [x] Run focused projects, repository guards, and the engineering-standards ratchet against the branch base.
- [x] Inspect the final diff for accidental unrelated changes and explicit-local-type violations.
- [x] Replace stale `project_state.md` statements with this branch’s actual status, verification, and remaining deferred work.
