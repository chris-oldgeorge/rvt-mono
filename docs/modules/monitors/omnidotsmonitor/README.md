# Omnidots Monitor

The Omnidots monitor is a .NET application that imports measuring-point metadata, peak, Veff, VDV, and trace data from the Omnidots Honeycomb API. It also evaluates offline and fleet-health conditions, exposes configuration and webhook endpoints, and supports one-shot or Quartz-scheduled execution.

The current host is ASP.NET Core; the previous Azure Functions deployment commands no longer apply. Omnidots vendor API documentation is available at <https://honeycomb.omnidots.com/api/docs>.

## Build and test

Run these commands from the repository root:

```bash
dotnet build omnidotsmonitor/omnidotsmonitor.sln --no-restore --nologo
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore --nologo
```

Run a supported operation once with `--job` or `RVT__MONITOR_JOB`:

```bash
dotnet run --project omnidotsmonitor/OmnidotsMonitor -- --job StoreVeffRecords
```

Set `MonitorApi__Enabled=true` to run the minimal API, or set `MonitorScheduler__Enabled=true` with `Infrastructure=local` to run the configured Quartz schedules. Do not enable the in-process Quartz scheduler for `Infrastructure=azure`; Azure Container Apps Jobs should invoke one-shot jobs instead.

## Required secret configuration

Supply credentials and secrets through deployment secrets or environment variables. Do not put their values in JSON examples, logs, source control, or this document.

| Setting | Purpose |
| --- | --- |
| `RVT__OMNIDOTS_USE_TOKEN` | Selects authentication mode. Defaults to `true`. |
| `RVT__OMNIDOTS_TOKEN` | Honeycomb token used when `RVT__OMNIDOTS_USE_TOKEN=true`. |
| `RVT__OMNIDOTS_USER_ID` / `RVT__OMNIDOTS_USER_AUTH` | Honeycomb username/password used when `RVT__OMNIDOTS_USE_TOKEN=false`. |
| `RVT__OMNIDOTS_WEBHOOK_SECRET` | HMAC secret shared with Omnidots for webhook signing. |
| `RVT__OMNIDOTS_CONFIG_SECRET` | Adapter secret required in configuration requests. |
| `RVT__OMNIDOTS_WEBHOOK_URL` | Public HTTPS URL registered with the measuring point. |
| `RVT__OMNIDOTS_MONITORING_ALERT_TO` | Preferred fleet-watchdog email recipient; falls back to `Omnidots:Monitoring:Recipient`. |
| `ConnectionStrings__DefaultConnection` | Monitor database connection string. |

`RVT__OMNIDOTS_WEBHOOK_URL`, `RVT__OMNIDOTS_WEBHOOK_SECRET`, and `RVT__OMNIDOTS_CONFIG_SECRET` are required only when `MonitorApi__Enabled=true`. Scheduler-only hosts and one-shot jobs that do not expose or call these endpoints may omit them. API startup and the endpoint handlers fail closed unless both secrets are nonblank, encode to at least 32 bytes under strict UTF-8, and have different byte values. The webhook URL must be an absolute HTTPS URL. Invalid startup diagnostics identify only the configuration contract and never include a configured value.

Rotate the secrets independently; the application accepts only one current value for each secret and has no overlap window. To rotate the webhook secret, keep the current configuration secret unchanged, quiesce webhook writers during a maintenance window, deploy and restart the API with the new webhook secret, reconfigure every measuring point through the authenticated configuration endpoint, verify a newly signed webhook, and then resume webhook traffic. If both secrets must change, rotate the configuration secret only after the webhook-secret rollout is verified, coordinating the configuration client and API update. Never change both secrets in one unverified step.

With the default `RVT__OMNIDOTS_USE_TOKEN=true`, the HTTP adapter satisfies its authentication step from `RVT__OMNIDOTS_TOKEN` and does not send a username/password authentication request to Honeycomb. Set the selector to `false` to POST `RVT__OMNIDOTS_USER_ID` and `RVT__OMNIDOTS_USER_AUTH` to Honeycomb and use the token returned by that response. Configure only the credentials needed for the selected mode, and never log either form of credential.

The fleet-watchdog warning is awaited through the shared email port. Configure `RVT__EMAIL_ENABLED`, `RVT__EMAIL_PROVIDER`, and the selected SendGrid or Microsoft Graph settings from the root README. The one-shot `Monitoring` job now fails/cancels with delivery instead of returning before an in-flight send. Durable webhook alert delivery remains at least once: transient failures retry, permanent/configuration failures dead-letter, and provider acceptance followed by a process failure can create a duplicate.

## Measuring-point configuration API

Enable the API and `POST /configure-measuring-point`. The request must use `application/json`, include `secret` and `serialid`, and may contain only the supported numeric tuning fields. The deployed `RVT__OMNIDOTS_WEBHOOK_URL` and `RVT__OMNIDOTS_WEBHOOK_SECRET` are always sent to Omnidots; a caller-supplied `webhook` member or any other unknown member is rejected. Optional configuration fields are shown below with placeholders only:

```json
{
  "secret": "<RVT__OMNIDOTS_CONFIG_SECRET>",
  "serialid": "<monitor-serial-id>",
  "level_caution": 7,
  "level_alert": 10,
  "trace_save_level": 10.0,
  "trace_pre_trigger": 3.0,
  "trace_post_trigger": 3.0,
  "flat_level": 10.0
}
```

The request secret is validated by the adapter and is never returned. After Omnidots confirms the configuration, the adapter returns HTTP 200 with only this response:

```json
{
  "configured": true
}
```

The request secret is authenticated before the remaining business fields. Response behavior is:

| Status | Meaning |
| --- | --- |
| HTTP 200 | Vendor configuration succeeded; the body contains only `{ "configured": true }`. |
| HTTP 400 | JSON is malformed or, after authentication, a serial/tuning field is invalid or an unsupported member is present. |
| HTTP 401 | The configuration secret is missing or incorrect. A wrong secret takes precedence over other business-field errors in syntactically valid JSON. |
| HTTP 413 | The raw request body exceeds 64 KiB. |
| HTTP 415 | The media type or content encoding is unsupported. |
| HTTP 429 | The no-queue configuration concurrency limit rejected the request. |
| HTTP 502 | Sanitized Omnidots authentication, network, or vendor configuration failure. |
| HTTP 500 | Unexpected internal failure, returned as generic Problem Details. |

Problem Details and operational logs never echo the request, deployed callback URL, secret, vendor request or response, contact destination, or raw exception message.

The monitor must have a database configuration rule with its serial ID, monitor ID, and `AlertField = 'config-rule'`. Alarm level 1 remains disabled; levels 2 and 3 are derived from the caution/alert values and flat level.

## Webhook contract

`POST /webhook` requires exactly one raw `x-omnidots-notifier-signature` header value. Multiple values and comma-combined values are rejected. Its value must be exactly:

```text
sha256=<64 hexadecimal HMAC-SHA256 characters>
```

The digest is HMAC-SHA256 over the exact request-body bytes using `RVT__OMNIDOTS_WEBHOOK_SECRET`. The endpoint buffers and authenticates those bytes before UTF-8 decoding or UTF-8 BOM removal. The prefix is lowercase and case-sensitive, malformed digests are rejected, and digest comparison is constant-time. After successful authentication, a UTF-8 BOM is accepted and removed for JSON processing; invalid UTF-8 is rejected as an authenticated invalid payload.

Response behavior is:

| Status | Meaning |
| --- | --- |
| HTTP 401 | Signature is missing, blank, malformed, or does not match. Authentication happens before JSON deserialization. |
| HTTP 400 | Signature is valid, but JSON is malformed or the authenticated payload fails adapter validation. |
| HTTP 200 | Durable acceptance committed, or an existing committed exact-body occurrence was found; the body contains only `{ "processed": true }`. |
| HTTP 413 | The raw request body exceeds 64 KiB. |
| HTTP 415 | The media type or content encoding is unsupported. |
| HTTP 429 | The no-queue webhook concurrency limit rejected the request. |
| HTTP 503 | A classified transient persistence failure occurred; the exact request may be retried. |
| HTTP 500 | An unexpected permanent authenticated processing failure occurred. |

Both POST endpoints accept only `application/json` with an optional charset. `Content-Encoding` must be absent or contain one `identity` value. The raw body limit is 64 KiB: exactly 65,536 bytes is accepted, while a declared or streamed 65,537th byte returns 413. Non-identity encoding returns 415, and each endpoint has a zero-length rate-limit queue.

Authentication, source identity, and replay protection use the exact raw body bytes; decoding, BOM removal, and JSON parsing happen afterward. Replaying the same authenticated bytes, including concurrent requests, commits one occurrence, at most one shared notification, and at most one outbox row for each channel and canonical destination. Occurrence, notification, and the complete delivery set commit atomically. Semantically equivalent JSON encoded into different bytes is a different occurrence, although normal event-time suppression still applies.

The endpoint returns 200 for accepted, ignored, suppressed, and durable duplicate occurrences; it does not wait for MQTT, email, or SMS. External delivery is at least once, not exactly once: if a provider accepts a delivery and the process stops before fenced completion commits, a later dispatcher may send it again.

Problem Details and operational logs use fixed safe messages. They do not include the request body, supplied or expected signature, configured secrets, vendor request or response, contact destination, stored delivery payload, or raw exception text.

## Durable alert migration, dispatch, and cleanup

Apply the provider-specific durable-alert forward migration before deploying this application version:

- PostgreSQL: `OmnidotsMonitor/postgres/2026-07-15-add-common-durable-alerts.sql`
- SQL Server: `OmnidotsMonitor/sqlserver/2026-07-15-add-common-durable-alerts.sql`

The matching `2026-07-15-rollback-common-durable-alerts.sql` assets remove the shared delivery outbox and permanent occurrence records. Before an application rollback, stop or disable every webhook writer and every dispatcher, including the API worker, Quartz `DispatchAlerts`, and one-shot backlog drains. Roll back the application first; run schema rollback only after no deployed version can read or write the shared tables. Dropping occurrence rows removes exact-body replay protection.

Dispatch is available in these mutually exclusive in-process modes:

- One shot: `dotnet run --project omnidotsmonitor/OmnidotsMonitor -- --job DispatchAlerts` drains one bounded batch and is suitable for smoke tests or controlled backlog draining.
- Quartz: the enabled `DispatchAlerts` job runs every minute with cron `0 0/1 * * * ?`.
- API host without Quartz: the Common background worker polls every 60 seconds. It is not enabled when Quartz is active, so one process does not register two dispatch loops.

Database leases make overlapping executions and multiple replicas safe. Each claim has a 120-second lease and unique fencing ID; delivery has a 90-second timeout. The claim also resolves the occurrence-owned notification ID, and delivery rejects a payload whose notification ID disagrees with that authoritative value. Failures retry exponentially from 30 seconds up to 30 minutes. The eighth failed attempt becomes a dead letter at the actual failure time without adding a retry delay, stores only a safe failure classification, writes the applicable email/SMS failure audit even when the payload is malformed, emits sanitized operational logging, and makes that dispatch run fail rather than report successful delivery. Lease expiry makes canceled work reclaimable, while fencing prevents a stale worker from completing or retrying work claimed by another worker.

Quartz runs `CleanupAlerts` daily at 03:15 UTC with cron `0 15 3 * * ?`. Cleanup deletes completed outbox rows after 90 days. Dead-letter rows remain for explicit operational resolution, and occurrence rows are retained permanently for replay protection.

## Veff and VDV fetch windows and schedules

Both jobs use a positive two-hour lookback plus a five-minute overlap. Each run captures one UTC instant and requests the closed window from `utcNow - 2 hours - 5 minutes` through that same `utcNow`; it never requests a future start time. The handler rejects non-positive lookbacks and negative overlaps.

Quartz uses UTC (`MonitorScheduler:TimeZoneId` is `UTC`) and the checked-in schedules are:

| Job | Quartz cron | UTC schedule |
| --- | --- | --- |
| `StoreVeffRecords` | `0 0 0/2 * * ?` | At minute 00 every two hours, beginning at 00:00. |
| `StoreVdvRecords` | `0 15 0/2 * * ?` | At minute 15 every two hours, beginning at 00:15. |

The same two-hour lookback is supplied by one-shot and Quartz dispatch because both paths use the shared job runner.

## Import cursors and database migration

Apply the provider-specific forward migration before deploying this application version:

- PostgreSQL: `OmnidotsMonitor/postgres/2026-07-14-add-import-cursors-and-trace-order.sql`
- SQL Server: `OmnidotsMonitor/sqlserver/2026-07-14-add-import-cursors-and-trace-order.sql`

The migration creates one import cursor per monitor serial and series (`Peak`, `Veff`, or `Vdv`) and adds the trace-sample ordinal used by the `(TraceId, SampleIndex)` key. The matching `2026-07-14-rollback-import-cursors-and-trace-order.sql` file in each provider directory is the rollback asset. Do not run the rollback while this application version is active because the runtime depends on both the cursor table and ordered trace key.

Each measurement page and its cursor advance commit in one database transaction. Cursors never move backward, a replay cannot duplicate an existing sample, and the three measurement series advance independently. Peak alone continues to update the compatibility `LastDataTime1Min` field. Each trace index and its ordered sample collection also commit atomically; failed sample persistence leaves neither an orphaned index nor a partial trace.

## Import failure behavior

Peak, Veff, VDV, and Trace fleet imports isolate monitor-specific failures. A failed monitor is recorded once through the operational-command boundary, later monitors are still attempted, and the job throws one `OmnidotsImportException` after the fleet loop. One-shot execution therefore exits unsuccessfully and Quartz records a failed job instead of reporting success after a partial import.

The aggregate exception's printable message contains only the operation, failure count, and affected serial IDs. It has no printable inner-exception chain, so job telemetry cannot expose raw vendor or database exception text through the aggregate. The original import exception and any secondary error-recording exception remain available to code through typed failure properties. A secondary recording failure does not stop fleet continuation.

Authentication and the initial fleet query occur outside the per-monitor catch boundary and fail immediately because there is no safe monitor-specific continuation in those cases.

## Trace collection rollout

Trace collection binds and validates `Omnidots:TraceCollection`:

```json
{
  "Omnidots": {
    "TraceCollection": {
      "Enabled": true,
      "AllowedSerialIds": ["23423"],
      "MaxMonitorsPerRun": 1
    }
  }
}
```

The checked-in values preserve the former single-monitor rollout while moving eligibility into configuration. Set `Enabled=false` to suppress all trace vendor calls. A non-empty `AllowedSerialIds` list limits eligibility case-insensitively; an empty list makes the complete filtered fleet eligible. `MaxMonitorsPerRun` must be positive, serial values must be non-blank and unique, and invalid settings fail host startup. Environment overrides use `Omnidots__TraceCollection__Enabled`, `Omnidots__TraceCollection__AllowedSerialIds__0`, and `Omnidots__TraceCollection__MaxMonitorsPerRun`.

Selection prioritizes monitors that have never stored a trace, then the oldest latest-trace time. Equal-priority monitors rotate deterministically by five-minute UTC slot so an empty or repeatedly failing monitor cannot permanently starve the rest of the fleet. Only the configured maximum is queried per run. Selected monitors are attempted independently; all selected failures are recorded and reported by the aggregate job contract above.

## Offline site schedules and DST policy

Offline duration is measured only during each monitor site's configured local operating intervals. The calculation intersects the UTC interval from the monitor's last sample through the current UTC instant with weekday, Saturday, and Sunday site intervals, then sums the elapsed UTC duration. Closed days use null boundaries, overnight intervals are supported, and `00:00` through `24:00` represents an always-open day.

Each monitor must have a valid IANA/host timezone ID such as `Europe/London`. Missing or invalid timezones are recorded as sanitized per-monitor failures; processing continues for later monitors and the job fails in aggregate afterward.

DST boundary policy is fail-closed. A site interval whose local start or end falls in a spring-forward gap or a fall-back ambiguous period is rejected with a fixed sanitized configuration error. Valid intervals still use real elapsed UTC time, so a valid interval spanning a DST transition contributes 23 or 25 hours as appropriate. The offline threshold is compared with `TimeSpan.FromSeconds(rule.AveragingPeriod)` and a monitor is marked offline only when active elapsed time is greater than that threshold.

## Fleet monitoring options

Fleet freshness monitoring binds and validates this configuration section:

```json
{
  "Omnidots": {
    "Monitoring": {
      "Recipient": "<operations-email-address>",
      "TimeZoneId": "Europe/London",
      "WindowStart": "08:30:00",
      "WindowEnd": "18:00:00",
      "StaleAfter": "01:00:00"
    }
  }
}
```

Environment-variable overrides use the standard .NET double-underscore form, for example `Omnidots__Monitoring__Recipient` and `Omnidots__Monitoring__StaleAfter`.

Startup validation requires a valid email recipient, a resolvable timezone, an ordered same-day window within 00:00-24:00, and a positive stale threshold. Validation runs on host startup in API, Quartz, and one-shot modes. Validation messages identify only the invalid field contract and do not include recipient or timezone values.

Freshness comparisons use complete UTC instants; timezone conversion is used only to determine whether the current time is inside the configured business window. Database timestamps follow this policy:

- `Utc` timestamps are used unchanged.
- `Local` timestamps are converted to UTC.
- `Unspecified` timestamps retain their ticks and are explicitly treated as UTC, matching SQL Server `datetime` materialization of stored UTC values.
- A null newest timestamp is stale.

The monitoring job does nothing outside the configured local window or for an empty fleet. Inside the window it sends one no-data warning when the newest fleet timestamp is null or older than `StaleAfter`. The checked-in Quartz trigger is `0 0/30 9-17 ? * MON-FRI` in UTC; the handler's `Europe/London` window check supplies the GMT/BST policy.

## Health endpoint

When the minimal API is enabled, `GET /liveness` returns the configured service name and version as plain text.
