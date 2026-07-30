# MyATM monitor

MyATM imports the MyAtmosphere customer catalogue, dust measurements, accessory telemetry, and rule-driven notifications into the RVT PostgreSQL monitor database.

## Local configuration

The application reads standard .NET configuration from `appsettings.json` and environment variables. Do not commit real credentials. Configure the database through `ConnectionStrings__DefaultConnection` and the vendor credentials through the `RVT__` variables used by `RvtConfig`.

The MyATM operational settings live in the `MyAtmMonitor` section:

```json
{
  "MyAtmMonitor": {
    "CustomerId": 9,
    "DevicePageSize": 100,
    "MaxDevicePagesPerRun": 100,
    "MeasurementPageSize": 1000,
    "AccessoryPageSize": 1000,
    "MaxPagesPerMonitorPerRun": 10,
    "PortalBaseUrl": "https://www.rvtcloud.com/"
  },
  "MyAtmVendor": {
    "BaseUrl": "https://api.example.invalid/",
    "ApiKey": "<secret>",
    "MaxResponseBytes": 4194304,
    "MaximumAttempts": 5,
    "MinimumRequestIntervalMilliseconds": 500,
    "FallbackRetryCapSeconds": 30,
    "MaximumRetryDelaySeconds": 30
  }
}
```

Set these values with standard .NET environment keys such as `MyAtmVendor__BaseUrl` and `MyAtmVendor__ApiKey`; never put the real API key in a tracked settings file. The existing RVT vendor URL and token settings remain supported as configuration fallbacks. Both option sections are validated at startup.

`DevicePageSize` is sent as `$top` on catalogue requests. `MaxDevicePagesPerRun` bounds a catalogue run, while `MaxPagesPerMonitorPerRun` bounds measurement and accessory pagination for each monitor. A repeated full catalogue page is treated as a vendor paging failure instead of allowing an unbounded loop.

Every MyAtmosphere endpoint shares one request policy. Requests are paced by `MinimumRequestIntervalMilliseconds`; HTTP 408, 429, and 5xx responses are retried up to `MaximumAttempts`, respecting `Retry-After` when supplied and capping delays at `MaximumRetryDelaySeconds`. Successful response bodies larger than `MaxResponseBytes` are rejected while streaming, so a vendor response cannot grow memory use without a configured bound.

MyATM resolves email and SMS through the shared provider-neutral communication adapters. Configure `RVT__EMAIL_ENABLED`, `RVT__EMAIL_PROVIDER`, the selected SendGrid or Microsoft Graph settings, and `RVT__SMS_ENABLED` plus TransmitSMS settings as documented in the root README. Compose disables both channels by default. Outbox delivery is at least once: transient failures retry, permanent/configuration failures dead-letter immediately, and a crash after provider acceptance can create a duplicate.

## Build and test

```sh
dotnet build MyAtmMonitor.sln
dotnet test MyAtmMonitorTests/MyAtmMonitorTests.csproj
```

The PostgreSQL integration tests require `RVT__POSTGRES_INTEGRATION_CONNECTION` to be set. They create and reset their dedicated test fixture schema; use a disposable local database connection.

## Operations

The host exposes:

- `GET /liveness` — process identity only.
- `GET /readiness` — returns `200` only when the configured database can be reached, otherwise `503`.

Jobs can be run as one-shot processes with `--job <name>` or dispatched by the shared Quartz scheduler. Supported names are `StoreMonitors`, `CheckForOfflineMonitors`, the four dust import periods, `Process8HourAverageDustLevels`, `StoreAccessoryInfo`, `ClearOlderErrorMessages`, `DispatchOutbox`, and `CleanupOutbox`.

The checked-in Quartz schedule is UTC. One-minute dust readings are imported every 30 minutes, with pagination filling every sample after the persisted cursor. Import commits and notification delivery are separate: `StoreDustLevels` does not invoke the dispatcher, and `DispatchOutbox` runs every minute. This keeps vendor polling at the approved cadence while allowing durable notifications to progress without a second vendor import or multiple inline delivery attempts.

Fleet jobs isolate failures by monitor or rule. They record each failed item, continue processing independent items, and finally throw a typed aggregate containing all recorded failures. A partially failed scheduled run therefore remains observable as failed without discarding successful work for other monitors.

## UTC and site-hours semantics

MyATM treats vendor timestamps and database date/time boundaries as UTC. Values already marked UTC are preserved, local `DateTime` values are converted once, and `Unspecified` database/provider values keep their clock value and are explicitly marked UTC. Do not add direct local-time conversions at these boundaries; use the shared UTC normalization helper so deployments in different host time zones produce identical cursors and persisted timestamps.

Offline duration is elapsed active-site time, not raw wall-clock time. The monitor's active deployment resolves its site timezone and weekday, Saturday, and Sunday opening intervals. Closed periods do not count toward an offline rule, overnight intervals are supported, and an incomplete start/end pair is a configuration failure for that monitor. All comparisons still enter and leave this calculation in UTC, avoiding daylight-saving and host-timezone conversion errors.

## Shared outbox cutover

Run the cutover from the repository root against PostgreSQL. Keep credentials outside the repository and pause every MyATM scheduler and one-shot trigger before applying schema or moving rows.

1. Verify the checked-in PostgreSQL migrations before use. The SHA-256 for `apps/monitors/myatmmonitor/database/migrations/2026-07-15-add-monitor-delivery-outbox.postgres.sql` is `0b9ec190b7a37b06044842d7a582128bc354a83463ddf5c2b027ec4658154170`; the SHA-256 for `apps/monitors/myatmmonitor/database/migrations/2026-07-15-migrate-myatm-outbox-to-shared.postgres.sql` is `62d1259161576b6fe4f225f9d356dfd59263f7c0187ca961a8f4a544a1afcba9`.
2. Apply `apps/monitors/myatmmonitor/database/migrations/2026-07-15-add-monitor-delivery-outbox.postgres.sql` with `psql -v ON_ERROR_STOP=1`.
3. Apply `apps/monitors/myatmmonitor/database/migrations/2026-07-15-migrate-myatm-outbox-to-shared.postgres.sql` with the same stop-on-error setting.
4. Reconcile legacy and shared counts by mapped status. `Leased` maps to `InProgress`; the reconciliation query must return no rows.
5. Deploy the shared-outbox application with Quartz and external triggers still disabled.
6. Run `dotnet MyAtmMonitor.dll --job DispatchOutbox` and require exit code 0. Confirm expected `Completed`, retryable `Pending`, migrated `InProgress`, or terminal `DeadLetter` outcomes and no new legacy rows.
7. Check outstanding leases:

   ```sql
   SELECT COUNT(*) AS in_progress_count, MAX(lease_until) AS latest_lease_until
   FROM monitor_delivery_outbox
   WHERE producer = 'MyAtm' AND payload_version = 1 AND status = 'InProgress';
   ```

   Wait past `latest_lease_until`, dispatch again, and investigate any expired row that is not reclaimed before resuming producers.
8. Resume recurring execution through exactly one scheduling model, dispatcher first and producer waves afterward.

### Forward Quartz sequencing

.NET environment configuration addresses the `MonitorScheduler:Jobs` array by zero-based index. These indexes are pinned to the checked-in `appsettings.json`: `0` `StoreMonitors`, `1` `CheckForOfflineMonitors`, `2` `StoreDustLevels`, `3` `Store15MinAverageDustLevels`, `4` `Store1HourAverageDustLevels`, `5` `Store24HourAverageDustLevels`, `6` `Process8HourAverageDustLevels`, `7` `ClearOlderErrorMessages`, `8` `StoreAccessoryInfo`, `9` `DispatchOutbox`, and `10` `CleanupOutbox`. Configuration is read at process startup, so restart or redeploy after every wave.

1. Deploy the shared-outbox application with Quartz globally off, every producer disabled, and only the dispatcher selected:

   ```sh
   export Infrastructure=local
   export MonitorScheduler__Enabled=false
   export MonitorScheduler__Jobs__0__Enabled=false
   export MonitorScheduler__Jobs__1__Enabled=false
   export MonitorScheduler__Jobs__2__Enabled=false
   export MonitorScheduler__Jobs__3__Enabled=false
   export MonitorScheduler__Jobs__4__Enabled=false
   export MonitorScheduler__Jobs__5__Enabled=false
   export MonitorScheduler__Jobs__6__Enabled=false
   export MonitorScheduler__Jobs__7__Enabled=false
   export MonitorScheduler__Jobs__8__Enabled=false
   export MonitorScheduler__Jobs__9__Enabled=true
   ```

2. Enable Quartz, redeploy/restart, and verify one successful recurring `DispatchOutbox` pass:

   ```sh
   export MonitorScheduler__Enabled=true
   ```

   Global enablement is safe only after indexes `0` through `8` are explicitly false.
3. Enable and redeploy one import/alert wave at a time, leaving index `9` true and verifying a successful dispatcher pass after each wave:

   1. `export MonitorScheduler__Jobs__2__Enabled=true` (`StoreDustLevels`).
   2. `export MonitorScheduler__Jobs__3__Enabled=true`, then `export MonitorScheduler__Jobs__4__Enabled=true`, then `export MonitorScheduler__Jobs__5__Enabled=true` (the aggregate imports, one at a time).
   3. `export MonitorScheduler__Jobs__6__Enabled=true` (`Process8HourAverageDustLevels`).
   4. `export MonitorScheduler__Jobs__1__Enabled=true` (`CheckForOfflineMonitors`).
   5. Enable the remaining work last: `export MonitorScheduler__Jobs__0__Enabled=true`, then `export MonitorScheduler__Jobs__8__Enabled=true`, then `export MonitorScheduler__Jobs__7__Enabled=true`.

### Forward external CronJob or one-shot sequencing

Keep `MonitorScheduler__Enabled=false` for every externally scheduled workload so that a CronJob process cannot also start Quartz. Export that value in every external job definition:

```sh
export MonitorScheduler__Enabled=false
```

Suspend all external MyATM triggers during migration. After the manual smoke and lease checks, resume only the trigger whose job argument/environment is `--job DispatchOutbox` / `RVT__MONITOR_JOB=DispatchOutbox`; verify a successful recurring execution, then resume the same import/alert waves and remaining jobs listed above. For Kubernetes CronJobs, the control operation is:

```sh
kubectl patch cronjob '<cronjob-name>' --type=merge -p '{"spec":{"suspend":true}}'
kubectl patch cronjob '<dispatch-outbox-cronjob>' --type=merge -p '{"spec":{"suspend":false}}'
```

Unsuspend each later CronJob individually only after the preceding wave and dispatcher verification complete. A one-shot orchestrator follows the same order by enabling only its `DispatchOutbox` schedule first; do not run multiple job names in one process.

## Shared outbox rollback

Rollback is an authoritative shared-to-local synchronization; do not deploy the previous application before it completes.

1. Disable all MyATM schedulers and one-shot triggers, including `DispatchOutbox`, and wait for running jobs to exit.
2. Verify that `apps/monitors/myatmmonitor/database/migrations/2026-07-15-rollback-myatm-outbox-to-local.postgres.sql` has SHA-256 `ab81ff9a588d03cf2620025c1b3afcab28dd1c9a952aeb3f52f41d135873cac4`, then apply it with `psql -v ON_ERROR_STOP=1`.
3. Re-run the status reconciliation. It must return no rows; shared `InProgress` maps to local `Leased`.
4. Deploy the previous local-outbox application while every job remains paused. Do not drop or modify the shared table.
5. Run its one-shot `DispatchOutbox` smoke and require exit code 0.
6. Resume Quartz or external schedules in the same controlled dispatcher-first waves used for forward rollout.

If synchronization or reconciliation fails, keep all writers paused and restore from the verified PostgreSQL backup instead of mixing application generations.

## Vendor notes

The MyAtmosphere API is documented at <https://api.my-atmosphere.cloud/swagger/index.html>. Access uses the vendor token configured for this monitor. Catalogue and telemetry responses are treated as unordered: the importer filters by persisted watermarks and then processes readings in timestamp order.
