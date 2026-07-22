# Omnidots Veff and VDV Scheduling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing Omnidots Veff and VDV import handlers available as one-shot jobs and run them automatically two hours apart by 15 minutes.

**Architecture:** Keep the current handler boundaries unchanged. Add the missing Veff/VDV forwarding methods to the existing service façade, extend the string-based job runner and Quartz dispatcher support list, then declare the two configuration-driven jobs in `appsettings.json`. Test the one-shot behavior through the existing internal runner via reflection, which preserves the production assembly’s internal boundary.

**Tech Stack:** .NET 10, MSTest, Moq, Quartz, JSON configuration.

## Global Constraints

- Preserve the exact job names `StoreVeffRecords` and `StoreVdvRecords`.
- Call each handler with a 120-minute fetch window.
- Use UTC Quartz cron expressions `0 0 0/2 * * ?` and `0 15 0/2 * * ?`.
- Do not modify the existing Veff/VDV handler, data-access, or database-mapping logic.

---

### Task 1: Add red tests for one-shot dispatch and schedule configuration

**Files:**
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/TestMonitorJobScheduling.cs`

**Interfaces:**
- Consumes: internal `Omnidots.Api.MonitorJobRunner.RunAsync(string, OmnidotsService)` through reflection and the existing `TestUtil.CreateApiAndMocks` helper.
- Produces: regression coverage that requires successful Veff/VDV dispatch and enabled, staggered schedule declarations.

- [x] **Step 1: Copy the production schedule configuration to the test output**

Add this item group to `omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj`:

```xml
<ItemGroup>
  <None Include="..\OmnidotsMonitor\appsettings.json" Link="appsettings.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [x] **Step 2: Write failing one-shot and configuration tests**

Create `omnidotsmonitor/OmnidotsMonitorTests/TestMonitorJobScheduling.cs` with tests that:

```csharp
[DataTestMethod]
[DataRow("StoreVeffRecords")]
[DataRow("StoreVdvRecords")]
public async Task RunAsync_ImportsRequestedVibrationSeries(string jobName)
{
    var api = TestUtil.CreateApiAndMocks(out var httpClient, out var dbClient, out var mqttClient, out var messageService);
    httpClient.Setup(client => client.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>()))
        .Returns(OmnidotsFixture.AuthenticateTask());
    dbClient.Setup(client => client.ReadMonitorList(null)).Returns([]);

    var service = new OmnidotsService(api);
    var runner = typeof(OmnidotsApi).Assembly.GetType("Omnidots.Api.MonitorJobRunner", throwOnError: true)!;
    var method = runner.GetMethod("RunAsync", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
    var task = (Task<int>)method.Invoke(null, [jobName, service])!;

    Assert.AreEqual(0, await task);
    httpClient.Verify(client => client.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>()), Times.Once);
}

[TestMethod]
public void AppSettings_ContainsStaggeredVeffAndVdvSchedules()
{
    var configuration = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
        .Build();
    var jobs = MonitorSchedulerOptions.Bind(configuration).GetEnabledJobs();

    Assert.IsTrue(jobs.Any(job => job.Name == "StoreVeffRecords" && job.Cron == "0 0 0/2 * * ?"));
    Assert.IsTrue(jobs.Any(job => job.Name == "StoreVdvRecords" && job.Cron == "0 15 0/2 * * ?"));
}
```

Include the required `System.Reflection`, `Microsoft.Extensions.Configuration`, `Moq`, `Omnidots.Api`, and `Rvt.Monitor.Common.Scheduling` imports.

- [x] **Step 3: Run the focused test class and verify the expected red state**

Run:

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter FullyQualifiedName~TestMonitorJobScheduling
```

Expected: both dispatch data rows return exit code `2` for an unknown job, and the schedule assertions fail because no Veff/VDV jobs exist yet.

- [x] **Step 4: Commit the test-only red state**

```bash
git add omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj omnidotsmonitor/OmnidotsMonitorTests/TestMonitorJobScheduling.cs
git commit -m "test: cover Omnidots Veff VDV scheduling"
```

### Task 2: Wire Veff and VDV into one-shot and Quartz scheduling

**Files:**
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/MonitorJobDispatcher.cs:24-33`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/MonitorJobRunner.cs:19-45`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsService.cs:43-55`
- Modify: `omnidotsmonitor/OmnidotsMonitor/appsettings.json:6-53`
- Test: `omnidotsmonitor/OmnidotsMonitorTests/TestMonitorJobScheduling.cs`

**Interfaces:**
- Consumes: `OmnidotsService.StoreVeffRecords(int)` and `OmnidotsService.StoreVdvRecords(int)`.
- Produces: supported one-shot job names and enabled Quartz schedules validated by `MonitorQuartzServiceCollectionExtensions`.

- [x] **Step 1: Add the two supported Quartz job names**

In `OmnidotsMonitorJobDispatcher.SupportedJobNames`, add:

```csharp
"StoreVeffRecords",
"StoreVdvRecords",
```

Place them immediately after `StorePeakRecordsLastDataTime`.

- [x] **Step 2: Add the Veff and VDV service façade methods**

In `OmnidotsService`, add the forwarding methods:

```csharp
public void StoreVeffRecords(int minutesSinceLastExecuted)
{
    omnidotsApi.StoreVeffRecords(minutesSinceLastExecuted);
}

public void StoreVdvRecords(int minutesSinceLastExecuted)
{
    omnidotsApi.StoreVdvRecords(minutesSinceLastExecuted);
}
```

- [x] **Step 3: Add the two one-shot runner cases**

In `MonitorJobRunner.RunAsync`, add the cases after the peak-records case:

```csharp
case "StoreVeffRecords":
    service.StoreVeffRecords(120);
    return 0;
case "StoreVdvRecords":
    service.StoreVdvRecords(120);
    return 0;
```

- [x] **Step 4: Declare the staggered Quartz schedules**

In `omnidotsmonitor/OmnidotsMonitor/appsettings.json`, add these enabled entries after `StorePeakRecordsLastDataTime`:

```json
{
  "Name": "StoreVeffRecords",
  "Cron": "0 0 0/2 * * ?",
  "Enabled": true,
  "Description": "Store effective vibration records every two hours."
},
{
  "Name": "StoreVdvRecords",
  "Cron": "0 15 0/2 * * ?",
  "Enabled": true,
  "Description": "Store vibration dose records every two hours, fifteen minutes after Veff."
},
```

- [x] **Step 5: Run the focused regression test and verify green**

Run:

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter FullyQualifiedName~TestMonitorJobScheduling
```

Expected: 3 tests pass: both one-shot job data rows return `0`, and the configuration declares the enabled staggered schedules.

- [x] **Step 6: Run the full Omnidots test project and build**

Run:

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj
dotnet build omnidotsmonitor/omnidotsmonitor.sln --no-restore
git diff --check
```

Expected: all Omnidots tests pass, the solution builds without errors, and the diff check produces no output.

- [x] **Step 7: Commit the production scheduling change**

```bash
git add omnidotsmonitor/OmnidotsMonitor/api/MonitorJobDispatcher.cs omnidotsmonitor/OmnidotsMonitor/api/MonitorJobRunner.cs omnidotsmonitor/OmnidotsMonitor/api/OmnidotsService.cs omnidotsmonitor/OmnidotsMonitor/appsettings.json omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj omnidotsmonitor/OmnidotsMonitorTests/TestMonitorJobScheduling.cs
git commit -m "feat: schedule Omnidots Veff and VDV imports"
```
