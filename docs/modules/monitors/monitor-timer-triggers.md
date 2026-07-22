# Monitor Timer Triggers

Azure Functions timer triggers use NCRONTAB format:

```text
{second} {minute} {hour} {day} {month} {day-of-week}
```

This inventory includes only active timer-triggered functions in the four monitor service classes. A function is treated as active only when the method has an uncommented `[Function(...)]` attribute and an uncommented `[TimerTrigger(...)]` parameter.

For container deployments, these schedules are converted into Quartz cron expressions and stored in each monitor app's `appsettings.json`. See `docs/modules/monitors/quartz-monitor-scheduling.md`.

## AirQMonitor

Source: `airqmonitor/AirQMonitor/api/AirQService.cs`

| Function | Trigger | Runs |
|---|---:|---|
| `StoreMonitors` | `0 2 * * * *` | Hourly at `:02` |
| `CheckForOfflineMonitors` | `0 5,25,45 * * * *` | Hourly at `:05`, `:25`, `:45` |
| `StoreNoiseLevels` | `0 5,20,35,50 * * * *` | Hourly at `:05`, `:20`, `:35`, `:50` |
| `StoreAllNoiseLevelsForYesterday` | `0 3 0 * * *` | Daily at `00:03` |
| `NotifySiteAverages` | `0 5 0 * * *` | Daily at `00:05` |
| `ClearOlderErrorMessages` | `0 0 3 * * *` | Daily at `03:00` |

## MyAtmMonitor

Source: `myatmmonitor/MyAtmMonitor/api/MyAtmService.cs`

| Function | Trigger | Runs |
|---|---:|---|
| `StoreMonitors` | `0 2 * * * *` | Hourly at `:02` |
| `CheckForOfflineMonitors` | `0 5,25,45 * * * *` | Hourly at `:05`, `:25`, `:45` |
| `StoreDustLevels` | `0 */1 * * * *` | Every minute |
| `Store15MinAverageDustLevels` | `0 14,29,44,59 * * * *` | Hourly at `:14`, `:29`, `:44`, `:59` |
| `Store1HourAverageDustLevels` | `0 59 * * * *` | Hourly at `:59` |
| `Store24HourAverageDustLevels` | `0 59 23 * * *` | Daily at `23:59` |
| `Process8HourAverageDustLevels` | `0 1 * * * *` | Hourly at `:01` |
| `ClearOlderErrorMessages` | `0 0 3 * * *` | Daily at `03:00` |

Excluded: `StoreAccessoryInfo` has a `TimerTrigger`, but its `[Function("StoreAccessoryInfo")]` attribute is commented out, so Azure Functions should not discover it as active.

## OmnidotsMonitor

Source: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsService.cs`

| Function | Trigger | Runs |
|---|---:|---|
| `StoreMonitors` | `0 29,59 * * * *` | Hourly at `:29`, `:59` |
| `CheckForOfflineMonitors` | `0 5,25,45 * * * *` | Hourly at `:05`, `:25`, `:45` |
| `StorePeakRecordsLastDataTime` | `0 */5 * * * *` | Every 5 minutes |
| `StoreTraces` | `0 */5 * * * *` | Every 5 minutes |
| `NotifyBatteryLevels` | `0 */15 * * * *` | Every 15 minutes |
| `ClearOlderErrorMessages` | `0 0 3 * * *` | Daily at `03:00` |
| `Monitoring` | `0 */30 9-17 * * 1-5` | Every 30 minutes from `09:00` through `17:30`, Monday-Friday |

Excluded: `StorePeakRecords`, `StoreVdvRecords`, and `StoreVeffRecords` are commented out.

## SvantekMonitor

Source: `svantekmonitor/SvantekMonitor/api/SvantekService.cs`

| Function | Trigger | Runs |
|---|---:|---|
| `StoreMonitors` | `0 2 * * * *` | Hourly at `:02` |
| `StoreNoiseLevels` | `0 */5 * * * *` | Every 5 minutes |
| `NotifySiteAverages` | `0 5 0 * * *` | Daily at `00:05` |
| `CheckForOfflineMonitors` | `0 5,25,45 * * * *` | Hourly at `:05`, `:25`, `:45` |
| `NotifyBatteryLevels` | `0 */15 * * * *` | Every 15 minutes |
| `CheckForSoundRecordings` | `0 11/41 * * * *` | Hourly at `:11` and `:52` |
