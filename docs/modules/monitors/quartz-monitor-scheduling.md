# Monitor Scheduling

The monitor apps support two scheduling infrastructures:

- `Infrastructure=local`: the app is an always-on container and may initialize Quartz.NET when `MonitorScheduler__Enabled=true`.
- `Infrastructure=azure`: the app is an Azure Container Apps Job and runs a single job from `--job <name>` or `RVT__MONITOR_JOB`; Quartz is not initialized.

Omnidots webhook/API hosting remains an always-on API process. Scheduled imports should run as Azure Container Apps Jobs.

Each monitor app reads local Quartz schedules from its own `appsettings.json` under `MonitorScheduler`. Local scheduler containers enable Quartz with:

```text
Infrastructure=local
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

The monitor entry points preserve one-shot execution through `--job` or `RVT__MONITOR_JOB`; that path still runs before scheduler mode.

Azure Container Apps Jobs should set:

```text
Infrastructure=azure
MonitorApi__Enabled=false
MonitorScheduler__Enabled=false
RVT__MONITOR_JOB=<job-name>
```

The same one-shot job can also be passed as `--job <job-name>`. If `Infrastructure=azure` is set, `MonitorScheduler__Enabled=true` is ignored so Quartz does not register hosted services in job containers.

The Omnidots Veff and VDV jobs both run every two hours with a 15-minute offset: Veff uses `0 0 0/2 * * ?` and VDV uses `0 15 0/2 * * ?`, in UTC. Both one-shot and Quartz dispatch supply the same two-hour lookback; the handler adds a five-minute replay overlap and never opens a future request window.

Omnidots `StoreTraces` runs every five minutes (`0 0/5 * * * ?`). Its trace selector is controlled by `Omnidots:TraceCollection`: `Enabled` can disable calls, `AllowedSerialIds` can stage an allow-list (empty means all filtered monitors), and positive `MaxMonitorsPerRun` throttles each execution. Selection favors unseen and oldest-traced monitors and rotates equal-priority candidates by five-minute UTC slot.

For local always-on scheduler containers, run only one scheduler replica per monitor unless Quartz clustering or external leader election is added. Multiple replicas with `Infrastructure=local` and `MonitorScheduler__Enabled=true` will each run the same configured schedules.
