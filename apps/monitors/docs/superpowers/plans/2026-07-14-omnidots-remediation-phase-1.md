# Omnidots Remediation Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correct Omnidots runtime, security, time-calculation, and job-failure defects without changing the database schema.

**Architecture:** Keep the shared host and focused handlers. Introduce pure time calculators, typed API results, constant-time HMAC validation, validated monitoring options, and an aggregate import-failure type while preserving the existing facade and compatibility database client.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs and TestHost, MSTest 4, Moq, Quartz, `TimeProvider`, HMAC-SHA256.

## Global Constraints

- Do not change database tables or EF mappings in this phase.
- Preserve PostgreSQL and SQL Server runtime support.
- Keep Veff at `0 0 0/2 * * ?` and VDV at `0 15 0/2 * * ?`.
- Use a positive two-hour lookback plus five-minute overlap.
- Never return or log webhook/configuration secrets.
- Attempt later monitors after a per-monitor failure, then fault the job.
- Use `Europe/London` for watchdog hours and monitor-configured timezones for site schedules.

---

### Task 1: Correct Veff/VDV fetch windows

**Files:**
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/SampleFetchWindow.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/StoreVeffRecordsHandler.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/StoreVdvRecordsHandler.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsApi.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsService.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/MonitorJobRunner.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestMonitorJobScheduling.cs`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/UseCases/SampleFetchWindowTests.cs`

**Interfaces:**
- Produces: `SampleFetchWindow.Start(DateTime utcNow, TimeSpan lookback, TimeSpan overlap)` and duration-based Veff/VDV service methods.
- Consumes: existing gateway methods and schedules.

- [ ] **Step 1: Write failing calculator and request-window tests**

```csharp
[TestMethod]
public void Start_SubtractsPositiveLookbackAndOverlap()
{
    var now = new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);
    Assert.AreEqual(new DateTime(2026, 7, 14, 9, 55, 0, DateTimeKind.Utc),
        SampleFetchWindow.Start(now, TimeSpan.FromHours(2), TimeSpan.FromMinutes(5)));
}
```

Extend `TestMonitorJobScheduling` with one real monitor per Veff/VDV run. Capture the URL and assert `start_time` falls between timestamps taken immediately before and after `UtcNow - 2h05m`; assert `end_time <= UtcNow`. Add a test-local query parser instead of checking only `StartsWith`.

- [ ] **Step 2: Run red tests**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "FullyQualifiedName~SampleFetchWindowTests|FullyQualifiedName~TestMonitorJobScheduling"
```

Expected: calculator API missing and scheduled URL outside the past-time bounds.

- [ ] **Step 3: Implement the calculator and duration contract**

```csharp
internal static DateTime Start(DateTime utcNow, TimeSpan lookback, TimeSpan overlap)
{
    if (utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("utcNow must be UTC.", nameof(utcNow));
    if (lookback <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lookback));
    if (overlap < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(overlap));
    return utcNow - lookback - overlap;
}
```

Change Veff/VDV handler, facade, and service signatures to `Run(TimeSpan lookback)`. Capture `utcNow` once, call `Start(utcNow, lookback, TimeSpan.FromMinutes(5))`, and pass `utcNow` as the vendor end time. Runner cases call `TimeSpan.FromHours(2)`.

- [ ] **Step 4: Update existing Veff/VDV tests and verify**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "FullyQualifiedName~SampleFetchWindowTests|FullyQualifiedName~TestMonitorJobScheduling|FullyQualifiedName~TestStoreVeffRecords|FullyQualifiedName~TestStoreVdvRecords"
```

Expected: all selected tests pass and both URL bounds are asserted.

- [ ] **Step 5: Commit**

```bash
git add omnidotsmonitor/OmnidotsMonitor omnidotsmonitor/OmnidotsMonitorTests
git commit -m "fix: correct Omnidots sample fetch windows"
```

### Task 2: Propagate aggregate import failures

**Files:**
- Create: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/OmnidotsImportException.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/StorePeakRecordsHandler.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/StoreVeffRecordsHandler.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/StoreVdvRecordsHandler.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/StoreTracesHandler.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestOmnidotsApiException.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestMonitorJobScheduling.cs`

**Interfaces:**
- Produces: `OmnidotsImportException` with `Operation` and `Failures`.
- Consumes: `IOmnidotsOperationalCommands.HandleException`.

- [ ] **Step 1: Write red partial-failure tests**

For each importer, arrange two monitors: the first vendor call fails and the second succeeds. Assert the second is attempted, the error is recorded once, and the handler throws:

```csharp
var exception = Assert.ThrowsExactly<OmnidotsImportException>(
    () => api.StoreVeffRecords(TimeSpan.FromHours(2)));
CollectionAssert.AreEqual(new[] { "1" }, exception.Failures.Select(x => x.SerialId).ToArray());
```

Add a runner test asserting its task faults when the only monitor fails.

- [ ] **Step 2: Run red tests**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "FullyQualifiedName~TestOmnidotsApiException|FullyQualifiedName~TestMonitorJobScheduling"
```

Expected: current handlers return successfully.

- [ ] **Step 3: Add the failure types**

```csharp
public sealed record OmnidotsMonitorFailure(string SerialId, Exception Exception);

public sealed class OmnidotsImportException : Exception
{
    public string Operation { get; }
    public IReadOnlyList<OmnidotsMonitorFailure> Failures { get; }

    public OmnidotsImportException(string operation, IReadOnlyList<OmnidotsMonitorFailure> failures)
        : base($"{operation} failed for {failures.Count} monitor(s): {string.Join(", ", failures.Select(x => x.SerialId))}",
            new AggregateException(failures.Select(x => x.Exception)))
    {
        Operation = operation;
        Failures = failures;
    }
}
```

- [ ] **Step 4: Refactor every fleet loop**

Collect failures, retain `HandleException`, continue, then throw one `OmnidotsImportException` after the loop. Remove Peak's private catch/`return -1`; its owning loop records the failure once. Leave authentication and fleet queries before the loop so they fault immediately.

- [ ] **Step 5: Verify and commit**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "FullyQualifiedName~TestOmnidotsApiException|FullyQualifiedName~TestMonitorJobScheduling"
git add omnidotsmonitor/OmnidotsMonitor/api/UseCases omnidotsmonitor/OmnidotsMonitorTests
git commit -m "fix: fault failed Omnidots import jobs"
```

### Task 3: Secure configuration and webhook endpoints

**Files:**
- Create: `omnidotsmonitor/OmnidotsMonitor/model/dto/ConfigureMeasuringPointResult.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/OmnidotsWebhookSignatureValidator.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/OmnidotsWebhookAuthenticationException.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/ConfigureMeasuringPointHandler.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/ProcessWebhookHandler.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsApi.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsService.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/MonitorApiEndpoints.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestMonitorApiEndpoints.cs`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/UseCases/OmnidotsWebhookSignatureValidatorTests.cs`

**Interfaces:**
- Produces: `ConfigureMeasuringPointResult(string SerialId, bool Configured)`, `IsValid(string body, string? signature, string secret)`, and an authentication-specific exception mapped to HTTP 401.
- Consumes: exact request body and configured webhook secret.

- [ ] **Step 1: Add TestHost and red API tests**

Add `Microsoft.AspNetCore.TestHost` version `10.0.4`. Follow AirQ's `UseTestServer` helper pattern. Assert configuration returns safe JSON, missing/bad signatures return 401, authenticated malformed JSON returns 400, and neither secret appears in any response.

- [ ] **Step 2: Run red tests**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "FullyQualifiedName~TestMonitorApiEndpoints|FullyQualifiedName~OmnidotsWebhookSignatureValidatorTests"
```

Expected: missing signature returns 200 and the configuration response contains the vendor request.

- [ ] **Step 3: Implement constant-time validation**

Require the exact `sha256=` prefix, decode 64 hex characters with `Convert.TryFromHexString`, compute `HMACSHA256.HashData` over UTF-8 bytes, and call `CryptographicOperations.FixedTimeEquals`. Return false for format errors and never log either digest.

```csharp
public sealed class OmnidotsWebhookAuthenticationException : Exception
{
    public OmnidotsWebhookAuthenticationException() : base("Webhook authentication failed.") { }
}
```

Throw this exception for missing, malformed, or mismatched signatures.

- [ ] **Step 4: Return typed safe results and explicit statuses**

After `response.Ok`, return:

```csharp
return new ConfigureMeasuringPointResult(serialId, Configured: true);
```

Update facade/service return types. Map `OmnidotsWebhookAuthenticationException` to 401, `JsonException` and authenticated adapter validation failures to 400, and success to 200 only after processing completes. Configuration validation uses ProblemDetails. Do not return raw `exception.Message`.

- [ ] **Step 5: Verify and commit**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "FullyQualifiedName~TestMonitorApiEndpoints|FullyQualifiedName~TestProcessAlarm|FullyQualifiedName~TestConfigureMeasuringPoint|FullyQualifiedName~OmnidotsWebhookSignatureValidatorTests"
git add omnidotsmonitor/OmnidotsMonitor omnidotsmonitor/OmnidotsMonitorTests
git commit -m "fix: secure Omnidots API contracts"
```

### Task 4: Correct site-active offline duration

**Files:**
- Create: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/SiteActiveDurationCalculator.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/CheckForOfflineMonitorsHandler.cs`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/UseCases/SiteActiveDurationCalculatorTests.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestOmnidotsApi.cs`

**Interfaces:**
- Produces: `TimeSpan Between(SiteTimes siteTimes, DateTime fromUtc, DateTime toUtc, TimeZoneInfo siteTimeZone)`.
- Consumes: monitor timezone and site schedule.

- [ ] **Step 1: Write red interval tests**

Cover same-day, before-open, after-close, multi-day, closed weekend, spring-forward, and fall-back. The same-day case must assert exactly one hour for 09:00–10:00 local within an 08:00–18:00 site.

- [ ] **Step 2: Run red tests**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter FullyQualifiedName~SiteActiveDurationCalculatorTests
```

- [ ] **Step 3: Implement daily interval intersection**

For every local date touched: choose weekday/Saturday/Sunday times; treat null boundaries as closed; construct unspecified local boundary values; convert with `TimeZoneInfo.ConvertTimeToUtc`; intersect with `[fromUtc,toUtc]`; add only `max(0, min(end)-max(start))`. Preserve the explicit `00:00` to `24:00` always-open fixture.

- [ ] **Step 4: Integrate timezone resolution**

Resolve `monitor.TimeZone`. Missing/invalid IDs are recorded and included in Task 2 aggregate failures. Compare the result to `TimeSpan.FromSeconds(rule.AveragingPeriod)`.

- [ ] **Step 5: Verify and commit**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter "FullyQualifiedName~SiteActiveDurationCalculatorTests|FullyQualifiedName~TestCheckForOfflineMonitors"
git add omnidotsmonitor/OmnidotsMonitor/api/UseCases omnidotsmonitor/OmnidotsMonitorTests
git commit -m "fix: calculate Omnidots offline site time"
```

### Task 5: Make fleet monitoring timezone-safe and configurable

**Files:**
- Create: `omnidotsmonitor/OmnidotsMonitor/model/config/OmnidotsMonitoringOptions.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/IOmnidotsMonitoringNotifier.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/EmailOmnidotsMonitoringNotifier.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/MonitoringHandler.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsApi.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsMonitorServices.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/appsettings.json`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/UseCases/MonitoringHandlerTests.cs`

**Interfaces:**
- Produces: validated options and `IOmnidotsMonitoringNotifier.SendNoDataWarning(string recipient, DateTime utcNow)`.
- Consumes: `TimeProvider` and monitor list.

- [ ] **Step 1: Write red option and handler tests**

Test previous-date data with a later clock time, fresh data across midnight, outside-window suppression, BST/GMT, empty fleet, and invalid options using a fixed `TimeProvider` and strict notifier mock.

- [ ] **Step 2: Run red tests**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter FullyQualifiedName~MonitoringHandlerTests
```

- [ ] **Step 3: Add validated options**

```csharp
public sealed class OmnidotsMonitoringOptions
{
    public const string SectionName = "Omnidots:Monitoring";
    public string Recipient { get; init; } = string.Empty;
    public string TimeZoneId { get; init; } = "Europe/London";
    public TimeSpan WindowStart { get; init; } = new(8, 30, 0);
    public TimeSpan WindowEnd { get; init; } = new(18, 0, 0);
    public TimeSpan StaleAfter { get; init; } = TimeSpan.FromHours(1);
}
```

Validate recipient, timezone, ordered window, and positive threshold at startup following `MyAtmMonitorOptions`.

- [ ] **Step 4: Compare complete UTC instants**

Inject options, notifier, and `TimeProvider`. Convert now to the configured timezone only for the business window. Compare `newest.Value.ToUniversalTime() < utcNow - options.StaleAfter`. Keep `EmailSender` behind the notifier wrapper.

- [ ] **Step 5: Configure, verify, and commit**

Add the current recipient, `Europe/London`, `08:30:00`, `18:00:00`, and `01:00:00` to `appsettings.json`.

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter FullyQualifiedName~MonitoringHandlerTests
git add omnidotsmonitor/OmnidotsMonitor omnidotsmonitor/OmnidotsMonitorTests
git commit -m "fix: harden Omnidots fleet monitoring"
```

### Task 6: Verify and document Phase 1

**Files:**
- Modify: `omnidotsmonitor/README.md`
- Modify: `project_state.md`

**Interfaces:**
- Consumes: all Phase 1 behavior.
- Produces: deployment notes and verification evidence.

- [ ] **Step 1: Document contracts and configuration**

Document safe configuration JSON, webhook 401/400 behavior, positive lookbacks, monitoring keys, and aggregate job-failure behavior.

- [ ] **Step 2: Run complete verification**

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore --nologo
dotnet build omnidotsmonitor/omnidotsmonitor.sln --no-restore --nologo
git diff --check
```

Expected: all tests pass, build reports zero warnings/errors, diff check is empty.

- [ ] **Step 3: Record totals and commit**

```bash
git add omnidotsmonitor/README.md project_state.md
git commit -m "docs: record Omnidots phase 1 remediation"
```
