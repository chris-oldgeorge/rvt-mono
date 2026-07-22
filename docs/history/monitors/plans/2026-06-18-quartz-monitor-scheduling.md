# Quartz Monitor Scheduling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Azure Functions `TimerTrigger` scheduling for containerized monitor deployments with Quartz.NET jobs whose schedules are loaded from app configuration.

**Architecture:** Keep the existing `MonitorJobRunner` switch maps as the execution bridge, and add Quartz only as the scheduler. A shared scheduling layer in `Rvt.Monitor.Common` binds `MonitorScheduler` config, validates job names, registers Quartz jobs, and invokes a monitor-specific dispatcher. Each monitor project supplies an `appsettings.json` schedule and a tiny dispatcher that calls its existing `MonitorJobRunner.Run(...)`.

**Tech Stack:** .NET 10, Quartz.NET hosted service integration, Microsoft.Extensions.Configuration, Microsoft.Extensions.Hosting, MSTest, existing monitor service/runner classes.

---

## Reference Notes

- Quartz hosted-service integration uses `services.AddQuartz(...)` plus `services.AddQuartzHostedService(...)`, and `WaitForJobsToComplete = true` lets jobs complete during shutdown.
- Quartz cron expressions use seconds first. They support six mandatory fields and an optional year field. Use `?` in day-of-month or day-of-week when that field is intentionally unspecified.
- Azure NCRONTAB schedules from `docs/monitor-timer-triggers.md` convert mostly by changing the last field from `*` to `?`.

## File Structure

### Shared scheduling files

- Create: `rvt-monitor-common/Rvt.Monitor.Common/Scheduling/MonitorJobSchedule.cs`
  - Holds one configured job entry: name, Quartz cron expression, enabled flag, optional description.
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Scheduling/MonitorSchedulerOptions.cs`
  - Holds scheduler enabled flag, scheduler time zone, and job list.
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Scheduling/IMonitorJobDispatcher.cs`
  - Boundary between Quartz and each monitor's existing `MonitorJobRunner`.
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Scheduling/MonitorQuartzJob.cs`
  - Quartz `IJob` implementation. Reads `JobName` from job data and calls `IMonitorJobDispatcher`.
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Scheduling/MonitorQuartzServiceCollectionExtensions.cs`
  - Binds config, validates job names and cron expressions, registers Quartz jobs/triggers, and starts the hosted scheduler.
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj`
  - Add Quartz hosting package reference.

### Shared scheduling tests

- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Scheduling/MonitorSchedulerOptionsTests.cs`
  - Verifies config binding and job filtering.
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Scheduling/MonitorQuartzJobTests.cs`
  - Verifies a Quartz job dispatches the configured job name and fails on non-zero exit.
- Modify: `rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj`
  - Add Moq test package reference for Quartz job tests.

### Monitor project files

For each monitor project:

- Create: `<MonitorProject>/api/MonitorJobDispatcher.cs`
  - Implements `IMonitorJobDispatcher` and `SupportedJobNames` by delegating to existing `MonitorJobRunner`.
- Create: `<MonitorProject>/appsettings.json`
  - Contains `MonitorScheduler` config and converted Quartz cron schedules.
- Modify: `<MonitorProject>/Program.cs`
  - Preserve one-shot `--job` / `RVT__MONITOR_JOB` behavior.
  - If `MonitorScheduler:Enabled=true`, start a Quartz generic host.
  - Otherwise, keep the existing Azure Functions host path for local/legacy use.
- Modify: `<MonitorProject>/<MonitorProject>.csproj`
  - Copy `appsettings.json` and `appsettings.*.json` to output.

## Configuration Shape

Use this shape in every monitor app:

```json
{
  "MonitorScheduler": {
    "Enabled": false,
    "TimeZoneId": "UTC",
    "Jobs": [
      {
        "Name": "StoreMonitors",
        "Cron": "0 2 * * * ?",
        "Enabled": true,
        "Description": "Fetch and store monitor list hourly at :02"
      }
    ]
  }
}
```

Container deployments should enable scheduling with either config or an environment override:

```text
MonitorScheduler__Enabled=true
```

For the first implementation, run one scheduler replica per monitor container. If a deployment scales a monitor container above one replica, every replica will run the same jobs unless Quartz clustering or an external leader election is added.

---

### Task 1: Add Shared Scheduling Models

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Scheduling/MonitorJobSchedule.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Scheduling/MonitorSchedulerOptions.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Scheduling/IMonitorJobDispatcher.cs`
- Test: `rvt-monitor-common/Rvt.Monitor.CommonTests/Scheduling/MonitorSchedulerOptionsTests.cs`

- [ ] **Step 1: Write failing tests for config filtering**

Create `rvt-monitor-common/Rvt.Monitor.CommonTests/Scheduling/MonitorSchedulerOptionsTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Rvt.Monitor.Common.Scheduling;

namespace Rvt.Monitor.CommonTests.Scheduling;

[TestClass]
public sealed class MonitorSchedulerOptionsTests
{
    [TestMethod]
    public void Bind_ReturnsEnabledJobsOnly()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MonitorScheduler:Enabled"] = "true",
                ["MonitorScheduler:TimeZoneId"] = "UTC",
                ["MonitorScheduler:Jobs:0:Name"] = "StoreMonitors",
                ["MonitorScheduler:Jobs:0:Cron"] = "0 2 * * * ?",
                ["MonitorScheduler:Jobs:0:Enabled"] = "true",
                ["MonitorScheduler:Jobs:1:Name"] = "StoreNoiseLevels",
                ["MonitorScheduler:Jobs:1:Cron"] = "0 0/5 * * * ?",
                ["MonitorScheduler:Jobs:1:Enabled"] = "false"
            })
            .Build();

        var options = MonitorSchedulerOptions.Bind(configuration);

        Assert.IsTrue(options.Enabled);
        Assert.AreEqual("UTC", options.TimeZoneId);
        CollectionAssert.AreEqual(
            new[] { "StoreMonitors" },
            options.EnabledJobs.Select(job => job.Name).ToArray());
    }
}
```

- [ ] **Step 2: Run the failing test**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter MonitorSchedulerOptionsTests
```

Expected: FAIL because `Rvt.Monitor.Common.Scheduling` types do not exist.

- [ ] **Step 3: Add scheduling model files**

Create `rvt-monitor-common/Rvt.Monitor.Common/Scheduling/MonitorJobSchedule.cs`:

```csharp
namespace Rvt.Monitor.Common.Scheduling;

public sealed record MonitorJobSchedule
{
    public string Name { get; init; } = "";
    public string Cron { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public string Description { get; init; } = "";
}
```

Create `rvt-monitor-common/Rvt.Monitor.Common/Scheduling/MonitorSchedulerOptions.cs`:

```csharp
using Microsoft.Extensions.Configuration;

namespace Rvt.Monitor.Common.Scheduling;

public sealed record MonitorSchedulerOptions
{
    public bool Enabled { get; init; }
    public string TimeZoneId { get; init; } = "UTC";
    public List<MonitorJobSchedule> Jobs { get; init; } = [];

    public IReadOnlyList<MonitorJobSchedule> EnabledJobs =>
        Jobs.Where(job => job.Enabled).ToArray();

    public static MonitorSchedulerOptions Bind(IConfiguration configuration)
    {
        var options = new MonitorSchedulerOptions();
        configuration.GetSection("MonitorScheduler").Bind(options);
        return options;
    }
}
```

Create `rvt-monitor-common/Rvt.Monitor.Common/Scheduling/IMonitorJobDispatcher.cs`:

```csharp
namespace Rvt.Monitor.Common.Scheduling;

public interface IMonitorJobDispatcher
{
    IReadOnlySet<string> SupportedJobNames { get; }
    Task<int> RunAsync(string jobName, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Run the test until it passes**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter MonitorSchedulerOptionsTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Scheduling rvt-monitor-common/Rvt.Monitor.CommonTests/Scheduling
git commit -m "feat: add monitor scheduler configuration models"
```

---

### Task 2: Add Shared Quartz Job and Registration

**Files:**
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Scheduling/MonitorQuartzJob.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Scheduling/MonitorQuartzServiceCollectionExtensions.cs`
- Test: `rvt-monitor-common/Rvt.Monitor.CommonTests/Scheduling/MonitorQuartzJobTests.cs`

- [ ] **Step 1: Add Quartz and Moq packages**

Run:

```bash
dotnet add rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj package Quartz.Extensions.Hosting
dotnet add rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj package Moq
```

Expected: package reference is added and restore succeeds.

- [ ] **Step 2: Write failing dispatch tests**

Create `rvt-monitor-common/Rvt.Monitor.CommonTests/Scheduling/MonitorQuartzJobTests.cs`:

```csharp
using Moq;
using Quartz;
using Rvt.Monitor.Common.Scheduling;

namespace Rvt.Monitor.CommonTests.Scheduling;

[TestClass]
public sealed class MonitorQuartzJobTests
{
    [TestMethod]
    public async Task Execute_DispatchesConfiguredJobName()
    {
        var dispatcher = new CapturingDispatcher(0);
        var job = new MonitorQuartzJob(dispatcher);
        var context = CreateContext("StoreMonitors");

        await job.Execute(context);

        Assert.AreEqual("StoreMonitors", dispatcher.JobName);
    }

    [TestMethod]
    public async Task Execute_ThrowsWhenDispatcherReturnsFailure()
    {
        var dispatcher = new CapturingDispatcher(2);
        var job = new MonitorQuartzJob(dispatcher);
        var context = CreateContext("MissingJob");

        await Assert.ThrowsExactlyAsync<JobExecutionException>(() => job.Execute(context));
    }

    private sealed class CapturingDispatcher(int exitCode) : IMonitorJobDispatcher
    {
        public string? JobName { get; private set; }
        public IReadOnlySet<string> SupportedJobNames { get; } = new HashSet<string> { "StoreMonitors", "MissingJob" };

        public Task<int> RunAsync(string jobName, CancellationToken cancellationToken)
        {
            JobName = jobName;
            return Task.FromResult(exitCode);
        }
    }

    private static IJobExecutionContext CreateContext(string jobName)
    {
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(current => current.MergedJobDataMap)
            .Returns(new JobDataMap { ["JobName"] = jobName });
        context.SetupGet(current => current.CancellationToken)
            .Returns(CancellationToken.None);
        return context.Object;
    }
}
```

- [ ] **Step 3: Run the failing tests**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter MonitorQuartzJobTests
```

Expected: FAIL because `MonitorQuartzJob` does not exist.

- [ ] **Step 4: Add the Quartz job**

Create `rvt-monitor-common/Rvt.Monitor.Common/Scheduling/MonitorQuartzJob.cs`:

```csharp
using Quartz;

namespace Rvt.Monitor.Common.Scheduling;

[DisallowConcurrentExecution]
public sealed class MonitorQuartzJob(IMonitorJobDispatcher dispatcher) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var jobName = context.MergedJobDataMap.GetString("JobName");
        if (string.IsNullOrWhiteSpace(jobName))
        {
            throw new JobExecutionException("Quartz monitor job is missing JobName.");
        }

        var exitCode = await dispatcher.RunAsync(jobName, context.CancellationToken);
        if (exitCode != 0)
        {
            throw new JobExecutionException($"Monitor job '{jobName}' failed with exit code {exitCode}.");
        }
    }
}
```

- [ ] **Step 5: Add Quartz registration extension**

Create `rvt-monitor-common/Rvt.Monitor.Common/Scheduling/MonitorQuartzServiceCollectionExtensions.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Rvt.Monitor.Common.Scheduling;

public static class MonitorQuartzServiceCollectionExtensions
{
    public static IServiceCollection AddMonitorQuartzScheduler<TDispatcher>(
        this IServiceCollection services,
        IConfiguration configuration,
        string monitorName)
        where TDispatcher : class, IMonitorJobDispatcher
    {
        var options = MonitorSchedulerOptions.Bind(configuration);
        if (!options.Enabled)
        {
            return services;
        }

        services.AddSingleton<IMonitorJobDispatcher, TDispatcher>();
        services.AddQuartz(quartz =>
        {
            quartz.UseInMemoryStore();
            quartz.UseDefaultThreadPool(threadPool => threadPool.MaxConcurrency = 1);

            var timeZone = ResolveTimeZone(options.TimeZoneId);
            var dispatcher = Activator.CreateInstance<TDispatcher>();
            foreach (var schedule in options.EnabledJobs)
            {
                if (!dispatcher.SupportedJobNames.Contains(schedule.Name))
                {
                    throw new InvalidOperationException(
                        $"Configured Quartz job '{schedule.Name}' is not supported by {monitorName}.");
                }

                var jobKey = new JobKey(schedule.Name, monitorName);
                quartz.AddJob<MonitorQuartzJob>(job => job
                    .WithIdentity(jobKey)
                    .UsingJobData("JobName", schedule.Name));

                quartz.AddTrigger(trigger => trigger
                    .WithIdentity($"{schedule.Name}.trigger", monitorName)
                    .ForJob(jobKey)
                    .WithCronSchedule(schedule.Cron, cron => cron.InTimeZone(timeZone)));
            }
        });

        services.AddQuartzHostedService(hosting =>
        {
            hosting.WaitForJobsToComplete = true;
        });

        return services;
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }
}
```

- [ ] **Step 6: Run shared tests**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add rvt-monitor-common/Rvt.Monitor.Common rvt-monitor-common/Rvt.Monitor.CommonTests
git commit -m "feat: add shared Quartz monitor scheduler"
```

---

### Task 3: Wire AirQMonitor to Config-Driven Quartz

**Files:**
- Create: `airqmonitor/AirQMonitor/api/MonitorJobDispatcher.cs`
- Create: `airqmonitor/AirQMonitor/appsettings.json`
- Modify: `airqmonitor/AirQMonitor/Program.cs`
- Modify: `airqmonitor/AirQMonitor/AirQMonitor.csproj`

- [ ] **Step 1: Add AirQ dispatcher**

Create `airqmonitor/AirQMonitor/api/MonitorJobDispatcher.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Scheduling;

namespace AirQ.Api;

internal sealed class AirQMonitorJobDispatcher : IMonitorJobDispatcher
{
    private readonly ILoggerFactory loggerFactory;

    public AirQMonitorJobDispatcher()
        : this(LoggerFactory.Create(builder => { }))
    {
    }

    public AirQMonitorJobDispatcher(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
    }

    public IReadOnlySet<string> SupportedJobNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "StoreMonitors",
        "CheckForOfflineMonitors",
        "StoreNoiseLevels",
        "StoreAllNoiseLevelsForYesterday",
        "NotifySiteAverages",
        "ClearOlderErrorMessages"
    };

    public Task<int> RunAsync(string jobName, CancellationToken cancellationToken)
    {
        return Task.FromResult(MonitorJobRunner.Run(jobName, loggerFactory));
    }
}
```

- [ ] **Step 2: Add AirQ appsettings schedule**

Create `airqmonitor/AirQMonitor/appsettings.json`:

```json
{
  "MonitorScheduler": {
    "Enabled": false,
    "TimeZoneId": "UTC",
    "Jobs": [
      {
        "Name": "StoreMonitors",
        "Cron": "0 2 * * * ?",
        "Enabled": true,
        "Description": "Fetch monitor list hourly at :02."
      },
      {
        "Name": "CheckForOfflineMonitors",
        "Cron": "0 5,25,45 * * * ?",
        "Enabled": true,
        "Description": "Check offline monitor state three times per hour."
      },
      {
        "Name": "StoreNoiseLevels",
        "Cron": "0 5,20,35,50 * * * ?",
        "Enabled": true,
        "Description": "Store noise samples four times per hour."
      },
      {
        "Name": "StoreAllNoiseLevelsForYesterday",
        "Cron": "0 3 0 * * ?",
        "Enabled": true,
        "Description": "Backfill yesterday's noise levels daily."
      },
      {
        "Name": "NotifySiteAverages",
        "Cron": "0 5 0 * * ?",
        "Enabled": true,
        "Description": "Notify daily site averages."
      },
      {
        "Name": "ClearOlderErrorMessages",
        "Cron": "0 0 3 * * ?",
        "Enabled": true,
        "Description": "Clean old error messages daily."
      }
    ]
  }
}
```

- [ ] **Step 3: Modify AirQ project file to copy appsettings**

In `airqmonitor/AirQMonitor/AirQMonitor.csproj`, add this item group:

```xml
  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Update="appsettings.*.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
```

- [ ] **Step 4: Modify AirQ Program.cs**

Replace `airqmonitor/AirQMonitor/Program.cs` with:

```csharp
using AirQ.Api;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Scheduling;

var configuration = BuildConfiguration(args);
var jobName = MonitorJobRunner.GetJobName(args);
if (!string.IsNullOrWhiteSpace(jobName))
{
    using var loggerFactory = LoggerFactory.Create(builder => { });
    try
    {
        return MonitorJobRunner.Run(jobName, loggerFactory);
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception);
        return 1;
    }
}

if (configuration.GetValue<bool>("MonitorScheduler:Enabled"))
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            services.AddMonitorQuartzScheduler<AirQMonitorJobDispatcher>(context.Configuration, "AirQMonitor");
        })
        .Build();

    await host.RunAsync();
    return 0;
}

var functionsHost = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
    })
    .Build();

functionsHost.Run();
return 0;

static IConfiguration BuildConfiguration(string[] args)
{
    var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? "Production";

    return new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
        .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
        .AddEnvironmentVariables()
        .Build();
}
```

- [ ] **Step 5: Build AirQ**

Run:

```bash
dotnet build airqmonitor/airqmonitor.sln
```

Expected: build succeeds.

- [ ] **Step 6: Commit**

```bash
git add airqmonitor/AirQMonitor rvt-monitor-common/Rvt.Monitor.Common
git commit -m "feat: wire AirQ monitor to Quartz scheduler"
```

---

### Task 4: Wire MyAtmMonitor to Config-Driven Quartz

**Files:**
- Create: `myatmmonitor/MyAtmMonitor/api/MonitorJobDispatcher.cs`
- Create: `myatmmonitor/MyAtmMonitor/appsettings.json`
- Modify: `myatmmonitor/MyAtmMonitor/Program.cs`
- Modify: `myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj`

- [ ] **Step 1: Add MyAtm dispatcher**

Create `myatmmonitor/MyAtmMonitor/api/MonitorJobDispatcher.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Scheduling;

namespace MyAtm.Api;

internal sealed class MyAtmMonitorJobDispatcher : IMonitorJobDispatcher
{
    private readonly ILoggerFactory loggerFactory;

    public MyAtmMonitorJobDispatcher()
        : this(LoggerFactory.Create(builder => { }))
    {
    }

    public MyAtmMonitorJobDispatcher(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
    }

    public IReadOnlySet<string> SupportedJobNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "StoreMonitors",
        "CheckForOfflineMonitors",
        "StoreDustLevels",
        "Store15MinAverageDustLevels",
        "Store1HourAverageDustLevels",
        "Store24HourAverageDustLevels",
        "Process8HourAverageDustLevels",
        "ClearOlderErrorMessages"
    };

    public Task<int> RunAsync(string jobName, CancellationToken cancellationToken)
    {
        return Task.FromResult(MonitorJobRunner.Run(jobName, loggerFactory));
    }
}
```

- [ ] **Step 2: Add MyAtm appsettings schedule**

Create `myatmmonitor/MyAtmMonitor/appsettings.json`:

```json
{
  "MonitorScheduler": {
    "Enabled": false,
    "TimeZoneId": "UTC",
    "Jobs": [
      { "Name": "StoreMonitors", "Cron": "0 2 * * * ?", "Enabled": true, "Description": "Update device list hourly at :02." },
      { "Name": "CheckForOfflineMonitors", "Cron": "0 5,25,45 * * * ?", "Enabled": true, "Description": "Check offline monitor state three times per hour." },
      { "Name": "StoreDustLevels", "Cron": "0 0/1 * * * ?", "Enabled": true, "Description": "Store one-minute dust levels." },
      { "Name": "Store15MinAverageDustLevels", "Cron": "0 14,29,44,59 * * * ?", "Enabled": true, "Description": "Store fifteen-minute dust averages." },
      { "Name": "Store1HourAverageDustLevels", "Cron": "0 59 * * * ?", "Enabled": true, "Description": "Store hourly dust averages." },
      { "Name": "Store24HourAverageDustLevels", "Cron": "0 59 23 * * ?", "Enabled": true, "Description": "Store daily dust averages." },
      { "Name": "Process8HourAverageDustLevels", "Cron": "0 1 * * * ?", "Enabled": true, "Description": "Process eight-hour average dust levels." },
      { "Name": "ClearOlderErrorMessages", "Cron": "0 0 3 * * ?", "Enabled": true, "Description": "Clean old error messages daily." }
    ]
  }
}
```

- [ ] **Step 3: Replace Program.cs with a Quartz-aware host switch**

Replace `myatmmonitor/MyAtmMonitor/Program.cs` with the same host-mode structure shown in Task 3 Step 4. Use the MyAtm namespace and scheduler registration below:

```csharp
using MyAtm.Api;
...
services.AddMonitorQuartzScheduler<MyAtmMonitorJobDispatcher>(context.Configuration, "MyAtmMonitor");
```

- [ ] **Step 4: Modify MyAtm csproj to copy appsettings**

In `myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj`, add the same appsettings item group from Task 3 Step 3.

- [ ] **Step 5: Build MyAtm**

Run:

```bash
dotnet build myatmmonitor/myatmmonitor.sln
```

Expected: build succeeds.

- [ ] **Step 6: Commit**

```bash
git add myatmmonitor/MyAtmMonitor
git commit -m "feat: wire MyAtm monitor to Quartz scheduler"
```

---

### Task 5: Wire OmnidotsMonitor to Config-Driven Quartz

**Files:**
- Create: `omnidotsmonitor/OmnidotsMonitor/api/MonitorJobDispatcher.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/appsettings.json`
- Modify: `omnidotsmonitor/OmnidotsMonitor/Program.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj`

- [ ] **Step 1: Add Omnidots dispatcher**

Create `omnidotsmonitor/OmnidotsMonitor/api/MonitorJobDispatcher.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Scheduling;

namespace Omnidots.Api;

internal sealed class OmnidotsMonitorJobDispatcher : IMonitorJobDispatcher
{
    private readonly ILoggerFactory loggerFactory;

    public OmnidotsMonitorJobDispatcher()
        : this(LoggerFactory.Create(builder => { }))
    {
    }

    public OmnidotsMonitorJobDispatcher(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
    }

    public IReadOnlySet<string> SupportedJobNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "StoreMonitors",
        "CheckForOfflineMonitors",
        "StorePeakRecordsLastDataTime",
        "StoreTraces",
        "NotifyBatteryLevels",
        "ClearOlderErrorMessages",
        "Monitoring"
    };

    public Task<int> RunAsync(string jobName, CancellationToken cancellationToken)
    {
        return Task.FromResult(MonitorJobRunner.Run(jobName, loggerFactory));
    }
}
```

- [ ] **Step 2: Add Omnidots appsettings schedule**

Create `omnidotsmonitor/OmnidotsMonitor/appsettings.json`:

```json
{
  "MonitorScheduler": {
    "Enabled": false,
    "TimeZoneId": "UTC",
    "Jobs": [
      { "Name": "StoreMonitors", "Cron": "0 29,59 * * * ?", "Enabled": true, "Description": "Store measuring point and sensor metadata twice per hour." },
      { "Name": "CheckForOfflineMonitors", "Cron": "0 5,25,45 * * * ?", "Enabled": true, "Description": "Check offline monitor state three times per hour." },
      { "Name": "StorePeakRecordsLastDataTime", "Cron": "0 0/5 * * * ?", "Enabled": true, "Description": "Store peak records every five minutes." },
      { "Name": "StoreTraces", "Cron": "0 0/5 * * * ?", "Enabled": true, "Description": "Store traces every five minutes." },
      { "Name": "NotifyBatteryLevels", "Cron": "0 0/15 * * * ?", "Enabled": true, "Description": "Notify battery levels every fifteen minutes." },
      { "Name": "ClearOlderErrorMessages", "Cron": "0 0 3 * * ?", "Enabled": true, "Description": "Clean old error messages daily." },
      { "Name": "Monitoring", "Cron": "0 0/30 9-17 ? * MON-FRI", "Enabled": true, "Description": "Run business-hours monitoring every thirty minutes Monday through Friday." }
    ]
  }
}
```

- [ ] **Step 3: Replace Program.cs with a Quartz-aware host switch**

Replace `omnidotsmonitor/OmnidotsMonitor/Program.cs` with the same host-mode structure shown in Task 3 Step 4. Use the Omnidots namespace and scheduler registration below:

```csharp
using Omnidots.Api;
...
services.AddMonitorQuartzScheduler<OmnidotsMonitorJobDispatcher>(context.Configuration, "OmnidotsMonitor");
```

- [ ] **Step 4: Modify Omnidots csproj to copy appsettings**

In `omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj`, add the same appsettings item group from Task 3 Step 3.

- [ ] **Step 5: Build Omnidots**

Run:

```bash
dotnet build omnidotsmonitor/omnidotsmonitor.sln
```

Expected: build succeeds.

- [ ] **Step 6: Commit**

```bash
git add omnidotsmonitor/OmnidotsMonitor
git commit -m "feat: wire Omnidots monitor to Quartz scheduler"
```

---

### Task 6: Wire SvantekMonitor to Config-Driven Quartz

**Files:**
- Create: `svantekmonitor/SvantekMonitor/api/MonitorJobDispatcher.cs`
- Create: `svantekmonitor/SvantekMonitor/appsettings.json`
- Modify: `svantekmonitor/SvantekMonitor/Program.cs`
- Modify: `svantekmonitor/SvantekMonitor/SvantekMonitor.csproj`

- [ ] **Step 1: Add Svantek dispatcher**

Create `svantekmonitor/SvantekMonitor/api/MonitorJobDispatcher.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Scheduling;

namespace Svantek.Api;

internal sealed class SvantekMonitorJobDispatcher : IMonitorJobDispatcher
{
    private readonly ILoggerFactory loggerFactory;

    public SvantekMonitorJobDispatcher()
        : this(LoggerFactory.Create(builder => { }))
    {
    }

    public SvantekMonitorJobDispatcher(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
    }

    public IReadOnlySet<string> SupportedJobNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "StoreMonitors",
        "StoreNoiseLevels",
        "NotifySiteAverages",
        "CheckForOfflineMonitors",
        "NotifyBatteryLevels",
        "CheckForSoundRecordings"
    };

    public Task<int> RunAsync(string jobName, CancellationToken cancellationToken)
    {
        return Task.FromResult(MonitorJobRunner.Run(jobName, loggerFactory));
    }
}
```

- [ ] **Step 2: Add Svantek appsettings schedule**

Create `svantekmonitor/SvantekMonitor/appsettings.json`:

```json
{
  "MonitorScheduler": {
    "Enabled": false,
    "TimeZoneId": "UTC",
    "Jobs": [
      { "Name": "StoreMonitors", "Cron": "0 2 * * * ?", "Enabled": true, "Description": "Fetch monitor list hourly at :02." },
      { "Name": "StoreNoiseLevels", "Cron": "0 0/5 * * * ?", "Enabled": true, "Description": "Store noise levels every five minutes." },
      { "Name": "NotifySiteAverages", "Cron": "0 5 0 * * ?", "Enabled": true, "Description": "Notify daily site averages." },
      { "Name": "CheckForOfflineMonitors", "Cron": "0 5,25,45 * * * ?", "Enabled": true, "Description": "Check offline monitor state three times per hour." },
      { "Name": "NotifyBatteryLevels", "Cron": "0 0/15 * * * ?", "Enabled": true, "Description": "Notify battery levels every fifteen minutes." },
      { "Name": "CheckForSoundRecordings", "Cron": "0 11/41 * * * ?", "Enabled": true, "Description": "Check for sound recordings at :11 and :52 each hour." }
    ]
  }
}
```

- [ ] **Step 3: Replace Program.cs with a Quartz-aware host switch**

Replace `svantekmonitor/SvantekMonitor/Program.cs` with the same host-mode structure shown in Task 3 Step 4. Use the Svantek namespace and scheduler registration below:

```csharp
using Svantek.Api;
...
services.AddMonitorQuartzScheduler<SvantekMonitorJobDispatcher>(context.Configuration, "SvantekMonitor");
```

Preserve the existing Svantek console logging block in the Azure Functions legacy host path.

- [ ] **Step 4: Modify Svantek csproj to copy appsettings**

In `svantekmonitor/SvantekMonitor/SvantekMonitor.csproj`, add the same appsettings item group from Task 3 Step 3.

- [ ] **Step 5: Build Svantek**

Run:

```bash
dotnet build svantekmonitor/svantekmonitor.sln
```

Expected: build succeeds.

- [ ] **Step 6: Commit**

```bash
git add svantekmonitor/SvantekMonitor
git commit -m "feat: wire Svantek monitor to Quartz scheduler"
```

---

### Task 7: Add Documentation and End-to-End Verification

**Files:**
- Modify: `docs/monitor-timer-triggers.md`
- Create: `docs/quartz-monitor-scheduling.md`

- [ ] **Step 1: Add Quartz deployment documentation**

Create `docs/quartz-monitor-scheduling.md`:

```markdown
# Quartz Monitor Scheduling

The containerized monitor apps use Quartz.NET instead of Azure Functions `TimerTrigger`.

Each monitor app reads schedules from its own `appsettings.json` under `MonitorScheduler`.
Container deployments enable the scheduler with:

```text
MonitorScheduler__Enabled=true
```

Schedule entries use Quartz cron syntax. Quartz cron has seconds first and uses `?` for either day-of-month or day-of-week when that field is intentionally unspecified.

Example:

```json
{
  "MonitorScheduler": {
    "Enabled": true,
    "TimeZoneId": "UTC",
    "Jobs": [
      {
        "Name": "StoreMonitors",
        "Cron": "0 2 * * * ?",
        "Enabled": true
      }
    ]
  }
}
```

Run only one scheduler replica per monitor unless Quartz clustering or external leader election is added.
```

- [ ] **Step 2: Link the Quartz doc from timer inventory**

Append this paragraph near the top of `docs/monitor-timer-triggers.md`:

```markdown
For container deployments, these schedules are converted into Quartz cron expressions and stored in each monitor app's `appsettings.json`. See `docs/quartz-monitor-scheduling.md`.
```

- [ ] **Step 3: Build all solutions**

Run:

```bash
dotnet build airqmonitor/airqmonitor.sln
dotnet build myatmmonitor/myatmmonitor.sln
dotnet build omnidotsmonitor/omnidotsmonitor.sln
dotnet build svantekmonitor/svantekmonitor.sln
dotnet build rvt-monitor-common/rvt-monitor-common.sln
```

Expected: every build succeeds.

- [ ] **Step 4: Run relevant tests**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj
dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj
dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj
```

Expected: all tests pass. If Testcontainers-backed DB tests cannot run in the current environment, record the exact failure and run the shared scheduling tests plus all non-container tests that the project supports.

- [ ] **Step 5: Verify one scheduler starts without running production jobs**

Temporarily override all job entries except one harmless test schedule by environment variable in a local shell, or set `MonitorScheduler:Enabled=false` and verify legacy host still starts. Do not enable production schedules locally with real credentials.

Run:

```bash
dotnet run --project airqmonitor/AirQMonitor/AirQMonitor.csproj -- --job MissingJob
```

Expected: process exits non-zero and prints `Unknown AirQ monitor job 'MissingJob'.`

- [ ] **Step 6: Commit docs and verification fixes**

```bash
git add docs
git commit -m "docs: document Quartz monitor scheduling"
```

---

## Self-Review

- Spec coverage: the plan preserves existing one-shot `MonitorJobRunner` behavior, adds Quartz hosted scheduling, and loads schedules from app configuration.
- Config coverage: every active timer trigger from `docs/monitor-timer-triggers.md` has a matching Quartz cron expression in an `appsettings.json` example.
- Concurrency coverage: `MonitorQuartzJob` uses `[DisallowConcurrentExecution]`, Quartz thread pool is configured with `MaxConcurrency = 1`, and docs require one scheduler replica per monitor until clustering is added.
- Migration safety: `MonitorScheduler:Enabled` defaults to `false`, so legacy Azure Functions host behavior remains unchanged until containers explicitly enable Quartz.
- Known limitation: HTTP-triggered endpoints still require Azure Functions or a later ASP.NET Core/minimal API migration. This plan only replaces timer scheduling.
