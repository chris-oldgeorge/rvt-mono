# AirQ Reliability Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make AirQ one-shot failures observable, local test runs single-monitor, and catalogue metadata handling resilient.

**Architecture:** Keep the existing host and handler layout. Add an AirQ-local test filter at the reader/catalogue boundary, aggregate recorded import failures at each job boundary, and preserve fallback metadata for empty vendor responses.

**Tech Stack:** .NET 10, MSTest, Moq, EF Core, shared `MonitorHost`.

## Global Constraints

- Keep `IDBClient` as a compatibility facade and inject narrow interfaces into handlers.
- Do not store vendor credentials or a live AirQ test serial in tracked files.
- Preserve PostgreSQL-first runtime behaviour and the existing secured import endpoint.

---

### Task 1: Add controlled AirQ testlocal selection

**Files:**
- Create: `airqmonitor/AirQMonitor/api/AirQTestLocalMonitorFilter.cs`
- Modify: `airqmonitor/AirQMonitor/api/AirQApi.cs`
- Modify: `airqmonitor/AirQMonitor/api/AirQMonitorReader.cs`
- Modify: `airqmonitor/AirQMonitor/api/UseCases/StoreMonitorsHandler.cs`
- Test: `airqmonitor/AirQMonitorTests/TestLocalMonitorFilterTests.cs`

- [x] Write tests proving disabled filtering retains all monitors, enabled filtering retains only the configured serial, and enabled filtering without a serial throws.
- [x] Run the focused filter test and observe the missing type failure.
- [x] Implement the filter and pass it from `AirQApi` to reader and catalogue handler.
- [x] Run the focused test and verify it passes.

### Task 2: Propagate recorded import failures

**Files:**
- Modify: `airqmonitor/AirQMonitor/api/UseCases/StoreNoiseLevelsHandler.cs`
- Modify: `airqmonitor/AirQMonitor/api/UseCases/StoreNoiseLevelsForDateHandler.cs`
- Modify: `airqmonitor/AirQMonitor/api/UseCases/StoreMonitorsHandler.cs`
- Test: `airqmonitor/AirQMonitorTests/TestAirQApiException.cs`

- [x] Update existing failure tests to require the recorded monitor failures to surface as `AggregateException`.
- [x] Run the focused exception tests and observe their current no-throw failure.
- [x] Collect per-monitor exceptions after the existing operational writes and throw one aggregate after the loop; rethrow top-level failures after recording them.
- [x] Run the focused exception tests and verify they pass.

### Task 3: Preserve catalogue progress on empty metadata

**Files:**
- Modify: `airqmonitor/AirQMonitor/api/UseCases/StoreMonitorsHandler.cs`
- Test: `airqmonitor/AirQMonitorTests/TestAirQApi.cs`

- [x] Add a test where one vendor metadata response is `[]` and all monitors are still written.
- [x] Run the test and observe the current index failure.
- [x] Select the first metadata response when present, otherwise use an empty metadata DTO.
- [x] Run the focused catalogue tests and verify they pass.

### Task 4: Document and run the local suite

**Files:**
- Modify: `scripts/run-testlocal-suite.sh`
- Modify: `README.md`
- Modify: `docs/container-builds.md`
- Modify: `project_state.md`

- [x] Require `AIRQ_TESTLOCAL_SERIAL_ID` in the local suite and pass it as `AirQ__TestLocal__SerialId` only to AirQ jobs.
- [x] Document AirQ's explicit target requirement and add AirQ to the suite.
- [x] Run `dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --no-restore --nologo`, `dotnet build airqmonitor/airqmonitor.sln --no-restore --nologo`, `scripts/run-testlocal-suite.sh --dry-run`, and `git diff --check`.
