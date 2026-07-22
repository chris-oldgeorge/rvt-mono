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

Jobs can be run as one-shot processes with `--job <name>` or dispatched by the shared Quartz scheduler. Supported names are `StoreMonitors`, `CheckForOfflineMonitors`, the four dust import periods, `Process8HourAverageDustLevels`, `StoreAccessoryInfo`, `ClearOlderErrorMessages`, and `DispatchOutbox`.

The checked-in Quartz schedule is UTC. One-minute dust readings are imported every 30 minutes, with pagination filling every sample after the persisted cursor. Import commits and notification delivery are separate: `StoreDustLevels` does not invoke the dispatcher, and `DispatchOutbox` runs every minute. This keeps vendor polling at the approved cadence while allowing durable notifications to progress without a second vendor import or multiple inline delivery attempts.

Fleet jobs isolate failures by monitor or rule. They record each failed item, continue processing independent items, and finally throw a typed aggregate containing all recorded failures. A partially failed scheduled run therefore remains observable as failed without discarding successful work for other monitors.

## UTC and site-hours semantics

MyATM treats vendor timestamps and database date/time boundaries as UTC. Values already marked UTC are preserved, local `DateTime` values are converted once, and `Unspecified` database/provider values keep their clock value and are explicitly marked UTC. Do not add direct local-time conversions at these boundaries; use the shared UTC normalization helper so deployments in different host time zones produce identical cursors and persisted timestamps.

Offline duration is elapsed active-site time, not raw wall-clock time. The monitor's active deployment resolves its site timezone and weekday, Saturday, and Sunday opening intervals. Closed periods do not count toward an offline rule, overnight intervals are supported, and an incomplete start/end pair is a configuration failure for that monitor. All comparisons still enter and leave this calculation in UTC, avoiding daylight-saving and host-timezone conversion errors.

## Shared outbox cutover

Run the cutover from the repository root against the target database. Keep credentials outside the repository. The retained legacy outbox is frozen for one compatibility release: no application may write it and no forward/manual schema change may alter or drop it. The only permitted compatibility-release write or schema adjustment is the documented rollback script, run while every MyATM job is paused.

1. Disable every MyATM scheduler and one-shot trigger, including `StoreDustLevels`, the three average imports, `Process8HourAverageDustLevels`, `CheckForOfflineMonitors`, and `DispatchOutbox`. Set `MonitorScheduler__Enabled=false` on the cutover deployment and wait for every running MyATM job to exit before continuing.
2. Apply the shared schema for the selected provider:

   ```sh
   source_commit=f00d5b8a320945ed08e248da8641ca0c3f7e3b82
   work_dir="$(mktemp -d /private/tmp/rvt-common-migrations.XXXXXX)"
   source_dir="$work_dir/source"
   artifact_dir="$work_dir/artifacts"
   gh repo clone RVT-Group-LTD/rvt-reporting "$source_dir" -- --filter=blob:none --no-checkout
   git -C "$source_dir" fetch --depth=1 origin "$source_commit"
   test "$(git -C "$source_dir" rev-parse FETCH_HEAD)" = "$source_commit"
   mkdir -p "$artifact_dir"
   git -C "$source_dir" archive "$source_commit" \
     database/migrations/2026-07-15-add-monitor-delivery-outbox.postgres.sql \
     database/migrations/2026-07-15-add-monitor-delivery-outbox.sqlserver.sql \
     | tar -xf - -C "$artifact_dir"
   printf '%s  %s\n' \
     0b9ec190b7a37b06044842d7a582128bc354a83463ddf5c2b027ec4658154170 \
       database/migrations/2026-07-15-add-monitor-delivery-outbox.postgres.sql \
     2cd2e4e9403b9c69c9aa282107bcf8221bc3749246163a92d7c17e1eac03769e \
       database/migrations/2026-07-15-add-monitor-delivery-outbox.sqlserver.sql \
     >"$artifact_dir/SHA256SUMS"
   (cd "$artifact_dir" && if command -v sha256sum >/dev/null 2>&1; then
     sha256sum -c SHA256SUMS
   else
     shasum -a 256 -c SHA256SUMS
   fi)

   psql '<connection-string>' -v ON_ERROR_STOP=1 \
     -f "$artifact_dir/database/migrations/2026-07-15-add-monitor-delivery-outbox.postgres.sql"
   sqlcmd -S '<server>' -d '<database>' -E -b \
     -i "$artifact_dir/database/migrations/2026-07-15-add-monitor-delivery-outbox.sqlserver.sql"
   ```

   The exact authoritative `RVT-Group-LTD/rvt-reporting` source commit is verified before its two migration files are exported, and both files must pass the retained SHA-256 checks above before use. The separately designated migration authority remains responsible for applying the shared schema.

3. Backfill the legacy MyATM rows into the shared outbox:

   ```sh
   psql '<connection-string>' -v ON_ERROR_STOP=1 -f myatmmonitor/database/migrations/2026-07-15-migrate-myatm-outbox-to-shared.postgres.sql
   sqlcmd -S '<server>' -d '<database>' -E -b -i myatmmonitor/database/migrations/2026-07-15-migrate-myatm-outbox-to-shared.sqlserver.sql
   ```

4. Reconcile counts by mapped status. The appropriate query below must return no rows; `Leased` is intentionally compared with shared `InProgress`.

   PostgreSQL:

   ```sql
   WITH legacy AS (
       SELECT CASE status WHEN 'Leased' THEN 'InProgress' ELSE status END AS status, COUNT(*) AS row_count
       FROM my_atm_outbox_message
       GROUP BY CASE status WHEN 'Leased' THEN 'InProgress' ELSE status END
   ), shared AS (
       SELECT status, COUNT(*) AS row_count
       FROM monitor_delivery_outbox
       WHERE producer = 'MyAtm' AND payload_version = 1
       GROUP BY status
   )
   SELECT COALESCE(legacy.status, shared.status) AS status,
          COALESCE(legacy.row_count, 0) AS legacy_count,
          COALESCE(shared.row_count, 0) AS shared_count
   FROM legacy
   FULL OUTER JOIN shared ON shared.status = legacy.status
   WHERE COALESCE(legacy.row_count, 0) <> COALESCE(shared.row_count, 0)
   ORDER BY status;
   ```

   SQL Server:

   ```sql
   WITH legacy AS (
       SELECT CASE [Status] WHEN N'Leased' THEN N'InProgress' ELSE [Status] END AS [Status], COUNT_BIG(*) AS [RowCount]
       FROM [dbo].[MyAtmOutboxMessages]
       GROUP BY CASE [Status] WHEN N'Leased' THEN N'InProgress' ELSE [Status] END
   ), shared AS (
       SELECT [Status], COUNT_BIG(*) AS [RowCount]
       FROM [dbo].[MonitorDeliveryOutbox]
       WHERE [Producer] = N'MyAtm' AND [PayloadVersion] = 1
       GROUP BY [Status]
   )
   SELECT COALESCE(legacy.[Status], shared.[Status]) AS [Status],
          COALESCE(legacy.[RowCount], 0) AS [LegacyCount],
          COALESCE(shared.[RowCount], 0) AS [SharedCount]
   FROM legacy
   FULL OUTER JOIN shared ON shared.[Status] = legacy.[Status]
   WHERE COALESCE(legacy.[RowCount], 0) <> COALESCE(shared.[RowCount], 0)
   ORDER BY [Status];
   ```

5. Deploy the shared-outbox MyATM application with Quartz still disabled and all external triggers still paused.
6. Run one smoke dispatch in the deployed application environment and require exit code `0`:

   ```sh
   dotnet MyAtmMonitor.dll --job DispatchOutbox
   ```

   Confirm that the shared MyATM rows show expected `Completed`, retryable `Pending`, migrated `InProgress`, or terminal `DeadLetter` outcomes and that no new legacy rows were written. A legacy `Leased` row is migrated as `InProgress` and cannot be reclaimed until its existing lease expires. Check remaining leases with the provider query below:

   ```sql
   -- PostgreSQL
   SELECT COUNT(*) AS in_progress_count, MAX(lease_until) AS latest_lease_until
   FROM monitor_delivery_outbox
   WHERE producer = 'MyAtm' AND payload_version = 1 AND status = 'InProgress';

   -- SQL Server
   SELECT COUNT_BIG(*) AS [InProgressCount], MAX([LeaseUntil]) AS [LatestLeaseUntil]
   FROM [dbo].[MonitorDeliveryOutbox]
   WHERE [Producer] = N'MyAtm' AND [PayloadVersion] = 1 AND [Status] = N'InProgress';
   ```

   If rows remain, wait until after `latest_lease_until`, rerun `DispatchOutbox`, and recheck. Do not resume any import or alert producer while an expired migrated `InProgress` row remains; investigate it if another dispatch pass does not reclaim it.
7. Resume recurring execution using exactly one of the following models.

### Forward Quartz sequencing

.NET environment configuration addresses the `MonitorScheduler:Jobs` array by zero-based index. These indexes are pinned to the checked-in `appsettings.json`: `0` `StoreMonitors`, `1` `CheckForOfflineMonitors`, `2` `StoreDustLevels`, `3` `Store15MinAverageDustLevels`, `4` `Store1HourAverageDustLevels`, `5` `Store24HourAverageDustLevels`, `6` `Process8HourAverageDustLevels`, `7` `ClearOlderErrorMessages`, `8` `StoreAccessoryInfo`, and `9` `DispatchOutbox`. Configuration is read at process startup, so restart or redeploy after every wave.

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

Rollback is an authoritative shared-to-legacy synchronization; do not redeploy the old application before it completes.

1. Disable every MyATM scheduler and one-shot trigger again, including `DispatchOutbox`, and wait for all running jobs to exit.
2. Synchronize all shared MyATM payload-version-1 rows back to the retained legacy table:

   ```sh
   psql '<connection-string>' -v ON_ERROR_STOP=1 -f myatmmonitor/database/migrations/2026-07-15-rollback-myatm-outbox-to-local.postgres.sql
   sqlcmd -S '<server>' -d '<database>' -E -b -i myatmmonitor/database/migrations/2026-07-15-rollback-myatm-outbox-to-local.sqlserver.sql
   ```

   The SQL Server rollback idempotently widens `[dbo].[MyAtmOutboxMessages].[LastError]` to `nvarchar(1024)` only when its current bounded width is smaller. This compatibility adjustment is intentional, rerun-safe, and the only documented schema change to the frozen legacy table.

3. Run the same status-reconciliation query from the cutover procedure. It must return no rows before deployment proceeds; shared `InProgress` must match legacy `Leased`.
4. Deploy the previous local-outbox MyATM application while all jobs remain paused. Do not drop or modify the shared table.
5. Run the old application's one-shot `DispatchOutbox` smoke and require exit code `0`.
6. For Quartz rollback, configure the old application explicitly and deploy/restart it once with the global scheduler off:

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

   Then enable Quartz and restart the old application:

   ```sh
   export MonitorScheduler__Enabled=true
   ```

   Verify one successful recurring legacy `DispatchOutbox` pass, then apply these exports one wave at a time, restarting and verifying the dispatcher between waves:

   1. `export MonitorScheduler__Jobs__2__Enabled=true`.
   2. `export MonitorScheduler__Jobs__3__Enabled=true`, then `export MonitorScheduler__Jobs__4__Enabled=true`, then `export MonitorScheduler__Jobs__5__Enabled=true`.
   3. `export MonitorScheduler__Jobs__6__Enabled=true`.
   4. `export MonitorScheduler__Jobs__1__Enabled=true`.
   5. `export MonitorScheduler__Jobs__0__Enabled=true`, then `export MonitorScheduler__Jobs__8__Enabled=true`, then `export MonitorScheduler__Jobs__7__Enabled=true`.
7. For external CronJobs or one-shot scheduling, keep the old application's `MonitorScheduler__Enabled=false`, resume only the external `DispatchOutbox` trigger, verify a successful recurring pass, and then unsuspend the same ordered waves. Keep every not-yet-authorized trigger suspended.

## Vendor notes

The MyAtmosphere API is documented at <https://api.my-atmosphere.cloud/swagger/index.html>. Access uses the vendor token configured for this monitor. Catalogue and telemetry responses are treated as unordered: the importer filters by persisted watermarks and then processes readings in timestamp order.
