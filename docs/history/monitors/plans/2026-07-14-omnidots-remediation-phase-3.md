# Omnidots Remediation Phase 3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the hard-coded trace serial with safe, fair, configuration-driven fleet trace collection.

**Architecture:** Bind validated trace options, query latest trace end-times through a narrow read port, and select eligible monitors with a pure fair selector before the existing sequential vendor workflow. Preserve current single-monitor behavior in checked-in configuration, then expand operationally without code changes.

**Tech Stack:** .NET 10, EF Core 10, MSTest 4, Moq, ASP.NET Core configuration, Quartz.

## Global Constraints

- Phase 2 migrations and ordered transactional trace writes must already be deployed.
- Do not parallelize vendor trace requests.
- Initial configuration enables only serial `23423` with one monitor per run.
- Empty `AllowedSerialIds` means all deployed Omnidots monitors.
- `MaxMonitorsPerRun` must be greater than zero.
- Throttling must not starve eligible monitors across repeated runs.
- Any attempted-monitor failure faults the job after remaining selected monitors are attempted.

---

### Task 1: Add and validate trace-collection options

**Files:**
- Create: `omnidotsmonitor/OmnidotsMonitor/model/config/OmnidotsTraceCollectionOptions.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsMonitorServices.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/appsettings.json`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/Config/OmnidotsTraceCollectionOptionsTests.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestMonitorJobScheduling.cs`

**Interfaces:**
- Produces: validated `OmnidotsTraceCollectionOptions` singleton.
- Consumes: `Omnidots:TraceCollection` configuration.

- [ ] **Step 1: Write red validation/configuration tests**

Assert invalid zero/negative limits fail, null lists normalize to empty, duplicate/blank serial IDs fail validation, and checked-in settings preserve serial `23423` with limit one.

- [ ] **Step 2: Run red tests**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "FullyQualifiedName~OmnidotsTraceCollectionOptionsTests|FullyQualifiedName~TestMonitorJobScheduling"
```

- [ ] **Step 3: Implement the options contract**

```csharp
public sealed class OmnidotsTraceCollectionOptions
{
    public const string SectionName = "Omnidots:TraceCollection";
    public bool Enabled { get; init; } = true;
    public string[] AllowedSerialIds { get; init; } = [];
    public int MaxMonitorsPerRun { get; init; } = 1;
}
```

Add a `Validate()` method that throws `OptionsValidationException` for non-positive limits, blank serials, or case-insensitive duplicates. Bind/validate in `AddOmnidotsMonitor` following existing monitor option patterns.

- [ ] **Step 4: Add safe rollout configuration**

```json
"Omnidots": {
  "TraceCollection": {
    "Enabled": true,
    "AllowedSerialIds": ["23423"],
    "MaxMonitorsPerRun": 1
  }
}
```

- [ ] **Step 5: Verify and commit**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "FullyQualifiedName~OmnidotsTraceCollectionOptionsTests|FullyQualifiedName~TestMonitorJobScheduling"
git add omnidotsmonitor/OmnidotsMonitor omnidotsmonitor/OmnidotsMonitorTests
git commit -m "feat: configure Omnidots trace collection"
```

### Task 2: Add latest-trace query and fair selector

**Files:**
- Create: `omnidotsmonitor/OmnidotsMonitor/api/db/Queries/IOmnidotsTraceQueries.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/db/DBClient.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsMonitorServices.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/OmnidotsTraceMonitorSelector.cs`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/UseCases/OmnidotsTraceMonitorSelectorTests.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestDbClient.cs`

**Interfaces:**
- Produces: `ReadLatestTraceEndTimes(IReadOnlyCollection<string>)` and `Select(..., long rotationSlot)`.
- Consumes: deployed monitor DTOs and trace-index rows.

- [ ] **Step 1: Write red selector and query tests**

Cover disabled collection, allow-list filtering, empty allow-list, maximum count, unseen-first ordering, oldest-latest-trace ordering, deterministic serial base order, and rotation among monitors tied at the same priority even when selected monitors return no traces.

```csharp
var selected = OmnidotsTraceMonitorSelector.Select(monitors, latestTraceEndTimes, options, rotationSlot: 1);
CollectionAssert.AreEqual(new[] { "unseen", "oldest" }, selected.Select(x => x.SerialId).ToArray());
```

PostgreSQL integration test inserts multiple trace-index rows per serial and asserts only the maximum end time is returned.

- [ ] **Step 2: Run red tests**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "FullyQualifiedName~OmnidotsTraceMonitorSelectorTests|FullyQualifiedName~TestDbClient"
```

- [ ] **Step 3: Define and implement the narrow query**

```csharp
public interface IOmnidotsTraceQueries
{
    IReadOnlyDictionary<string, DateTime> ReadLatestTraceEndTimes(IReadOnlyCollection<string> serialIds);
}
```

Use `AsNoTracking`, filter to requested non-empty serials, group by serial, and return maximum `EndTime`. Register the interface to the existing `IDBClient` singleton.

- [ ] **Step 4: Implement the pure selector**

Filter by `Enabled` and allow-list. Group monitors by priority: unseen monitors use `DateTime.MinValue`, and seen monitors use latest trace end-time. Within each equal-priority group, sort by `SerialId` ordinal-ignore-case and rotate by `(rotationSlot * MaxMonitorsPerRun) % group.Count`. Flatten priority groups oldest first and apply `Take(MaxMonitorsPerRun)` last. Never mutate input collections.

- [ ] **Step 5: Verify and commit**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "FullyQualifiedName~OmnidotsTraceMonitorSelectorTests|FullyQualifiedName~TestDbClient"
git add omnidotsmonitor/OmnidotsMonitor/api omnidotsmonitor/OmnidotsMonitorTests
git commit -m "feat: select Omnidots trace monitors fairly"
```

### Task 3: Replace hard-coded trace filtering and add completion telemetry

**Files:**
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/StoreTracesHandler.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsApi.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestOmnidotsApi.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestOmnidotsApiException.cs`

**Interfaces:**
- Consumes: `OmnidotsTraceCollectionOptions`, `IOmnidotsTraceQueries`, selector, Phase 1 aggregate failure behavior, Phase 2 transactional `WriteTraces`.
- Produces: sequential trace collection for selected fleet monitors and one structured summary log.

- [ ] **Step 1: Write red handler tests**

Assert serials other than `23423` run when eligible, disabled mode makes no vendor calls, allow-list/limit are enforced, selected monitors remain sequential, one failure does not block a later selected monitor, and the final job throws after logging/recording failure.

- [ ] **Step 2: Run red tests**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "FullyQualifiedName~TestStoreTraces|FullyQualifiedName~TestOmnidotsApiException"
```

Expected: non-23423 monitors are skipped by source code.

- [ ] **Step 3: Inject options/query and remove literal filter**

Read monitors, query latest trace times, compute `rotationSlot = timeProvider.GetUtcNow().ToUnixTimeSeconds() / 300`, call the selector, then iterate the returned list. Inject `TimeProvider.System` through composition and a fixed provider in tests. Delete the `"23423"` comparison and warning. Keep authentication/request order sequential.

- [ ] **Step 4: Track and log completion counters**

Track eligible, attempted, succeeded, failed, traces stored, samples stored, and elapsed duration. Have `ReadTraces` return a small result record containing trace/sample counts. Emit one structured `LogInformation` summary after attempts and before throwing any aggregate exception; do not log response bodies or tokens.

- [ ] **Step 5: Verify and commit**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "FullyQualifiedName~TestStoreTraces|FullyQualifiedName~TestOmnidotsApiException|FullyQualifiedName~OmnidotsTraceMonitorSelectorTests"
git add omnidotsmonitor/OmnidotsMonitor/api omnidotsmonitor/OmnidotsMonitorTests
git commit -m "fix: enable fleet Omnidots trace collection"
```

### Task 4: Document staged activation and verify Phase 3

**Files:**
- Modify: `omnidotsmonitor/README.md`
- Modify: `docs/quartz-monitor-scheduling.md`
- Modify: `project_state.md`

**Interfaces:**
- Consumes: trace configuration and telemetry.
- Produces: operational rollout/rollback instructions and final evidence.

- [ ] **Step 1: Document configuration semantics**

Document disabled mode, non-empty allow-list staging, empty allow-list fleet-wide behavior, fair `MaxMonitorsPerRun`, sequential execution, summary log fields, and configuration-only rollback (`Enabled=false`).

- [ ] **Step 2: Document rollout sequence**

Start with `AllowedSerialIds:["23423"]`/limit one; observe duration and failures; add a small group; increase the limit; finally clear the allow-list only when vendor and DB load are acceptable.

- [ ] **Step 3: Run full verification**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore --nologo
dotnet build omnidotsmonitor/omnidotsmonitor.sln --no-restore --nologo
git diff --check
```

Run PostgreSQL integration tests as well because selector fairness depends on latest trace end-times. Expected: all tests pass and build has zero warnings/errors.

- [ ] **Step 4: Record results and commit**

```bash
git add omnidotsmonitor/README.md docs/quartz-monitor-scheduling.md project_state.md
git commit -m "docs: record Omnidots fleet trace activation"
```
