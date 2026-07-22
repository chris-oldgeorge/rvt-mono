# Omnidots Strict Review Remediation Design

## Purpose

Correct the nine validated Omnidots review findings without removing PostgreSQL or SQL Server support. The remediation protects scheduled data completeness, webhook secrets and authentication, offline-state correctness, job failure visibility, trace integrity, and fleet monitoring.

## Scope

The work covers:

1. Veff and VDV fetch-window semantics.
2. Independent Peak, Veff, and VDV import cursors.
3. Non-sensitive measuring-point configuration responses.
4. Mandatory, constant-time webhook signature validation.
5. Correct offline-duration intersection across site schedules.
6. Failed job status when one or more per-monitor imports fail.
7. Configuration-driven fleet trace collection.
8. Ordered, transactional trace persistence.
9. Full-timestamp fleet staleness checks in an explicit UK timezone.

The design preserves the shared host, focused handlers, narrow data-access ports, app-local database mappings, and the current one-shot and Quartz execution models.

## Delivery Strategy

The changes ship in three independently reviewable phases.

### Phase 1: Runtime Correctness and Security

This phase requires no database migration. It corrects fetch-window calculation, API response contracts, webhook authentication, offline-duration calculation, per-monitor failure propagation, and fleet watchdog time handling.

### Phase 2: Durable Data Integrity

This phase adds independent series cursors and ordered trace samples. It includes idempotent forward and rollback scripts for PostgreSQL and SQL Server, EF mapping updates, atomic import commands, and provider-aware tests.

The forward migrations must be deployed before the phase 2 application. Cursor reads may fall back during initialization, but phase 2 writes require the cursor table and trace sample-index column to exist.

### Phase 3: Fleet Trace Activation

This phase replaces the hard-coded serial filter with validated configuration, retains a rollout throttle, and activates trace collection across the configured fleet only after trace duration and failure behavior have been verified.

## Phase 1 Design

### Fetch Windows

Replace the signed `minutesSinceLastExecuted` convention with an explicit positive `TimeSpan lookback`. A shared helper validates that lookback is greater than zero and calculates:

```text
start = utcNow - lookback - overlap
end   = utcNow
```

Veff and VDV use a two-hour lookback and a five-minute overlap. Existing `(serial_id, sample_time)` uniqueness keeps the overlap idempotent. Once phase 2 cursors are present, the cursor becomes the preferred starting point and the five-minute overlap is subtracted from that cursor; when no cursor exists, the two-hour lookback is used.

The one-shot runner passes `TimeSpan.FromHours(2)` rather than a signed integer. Tests must capture the generated vendor URL and assert that its start time is in the past and its end time is not in the future.

### Measuring-Point Configuration API

The handler continues building and sending the vendor request internally, including the webhook secret required by Omnidots. It no longer returns the serialized vendor body.

On success, the endpoint returns HTTP 200 with a response DTO containing only:

```json
{
  "serialId": "23423",
  "configured": true
}
```

The endpoint uses typed results and ProblemDetails-compatible failures. Neither responses nor logs may contain the webhook or configuration secret. Existing JSON log redaction remains in place.

### Webhook Authentication

The signature header is mandatory. Processing follows this order:

1. Reject a missing or blank signature with HTTP 401.
2. Parse the expected `sha256=<hex>` format.
3. Compute the HMAC-SHA256 over the exact request-body UTF-8 bytes.
4. Compare the decoded digest bytes with `CryptographicOperations.FixedTimeEquals`.
5. Reject malformed or mismatched signatures with HTTP 401.
6. Deserialize and process only an authenticated payload.
7. Return HTTP 400 for an authenticated but malformed alarm payload.

The API must never acknowledge a rejected or unprocessed alarm with HTTP 200.

### Offline Duration

Extract the offline-time calculation into a pure, directly testable component. For each local site date touched by the interval, construct that day's configured active interval and intersect it with `[lastDataTime, currentTime]`. Add only positive overlap.

Site schedules are interpreted in the monitor's configured timezone. Instants are converted to UTC for comparison and persistence. The calculation covers same-day intervals, closed days, multi-day spans, overnight boundaries, and daylight-saving transitions. If a monitor timezone is missing or invalid, the handler records an operational error for that monitor and the job participates in aggregate failure reporting rather than silently using host-local time.

### Import Failure Semantics

Handlers continue processing the remaining fleet after an individual monitor fails. Each failure is logged and written through `IOmnidotsOperationalCommands`. After the loop, the handler throws an aggregate import exception containing the failed serial IDs and inner failures.

Consequences:

- one-shot execution returns non-zero;
- Quartz records a failed execution;
- successful monitors in the same run remain committed; and
- a run in which every monitor fails cannot appear successful.

Authentication or fleet-query failures occur before the loop and fault immediately.

### Fleet Monitoring

The watchdog compares complete UTC instants:

```text
newestLastDataTime < utcNow - one hour
```

The notification window uses the explicit `Europe/London` timezone, not `DateTime.Now`. The recipient address, timezone ID, business-window start/end, and stale threshold move into validated Omnidots monitoring options. Invalid configuration fails at host startup.

## Phase 2 Persistence Design

### Independent Import Cursors

Create a monitor-specific cursor table.

PostgreSQL shape:

```sql
CREATE TABLE omnidots_import_cursor (
    serial_id text NOT NULL,
    series text NOT NULL,
    last_sample_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    PRIMARY KEY (serial_id, series),
    CHECK (series IN ('Peak', 'Veff', 'Vdv'))
);
```

SQL Server shape:

```sql
CREATE TABLE dbo.OmnidotsImportCursor (
    SerialId nvarchar(128) NOT NULL,
    Series nvarchar(16) NOT NULL,
    LastSampleAt datetime2 NOT NULL,
    UpdatedAt datetime2 NOT NULL,
    CONSTRAINT PK_OmnidotsImportCursor PRIMARY KEY (SerialId, Series),
    CONSTRAINT CK_OmnidotsImportCursor_Series CHECK (Series IN ('Peak', 'Veff', 'Vdv'))
);
```

Use an `OmnidotsMeasurementSeries` enum in application code. A narrow cursor query port returns the current cursor. A narrow import command writes a series batch and advances its cursor in one database transaction. Cursor updates are monotonic: an older timestamp cannot replace a newer cursor.

Initialization rules are:

- Peak: use the Peak cursor; if absent, seed it from `MonitorsList.LastDataTime1Min`.
- Veff: use the Veff cursor; if absent, initialize from `MAX(sample_time)` / `MAX(SampleTime)` in the Veff table.
- VDV: use the VDV cursor; if absent, initialize from the corresponding VDV maximum.
- If neither a cursor nor stored measurement exists, use the phase 1 two-hour lookback.

`LastDataTime1Min` remains a compatibility and fleet-status field updated only by successful Peak imports. Veff and VDV never update it.

For each series import:

1. Read the series cursor or fallback.
2. Request from `cursor - five minutes`, or from `utcNow - two hours - five minutes` without a cursor.
3. Filter duplicates and order records chronologically.
4. Begin a database transaction.
5. Insert new measurement rows idempotently.
6. Advance only that series cursor to the newest committed sample.
7. For Peak only, update `LastDataTime1Min` in the same transaction.
8. Commit.
9. Publish the data-inserted event after commit.

If insertion or cursor advancement fails, the complete series batch rolls back.

### Ordered Trace Persistence

New trace rows include a zero-based sample index.

PostgreSQL shape:

```sql
ALTER TABLE omnidots_trace ADD COLUMN sample_index integer;
-- Historical rows are assigned a stable migration-time row number per trace.
ALTER TABLE omnidots_trace ALTER COLUMN sample_index SET NOT NULL;
ALTER TABLE omnidots_trace ADD PRIMARY KEY (trace_id, sample_index);
```

SQL Server shape:

```sql
ALTER TABLE dbo.OmnidotsTraces ADD SampleIndex int NULL;
-- Historical rows are assigned a migration-time ROW_NUMBER per TraceId.
ALTER TABLE dbo.OmnidotsTraces ALTER COLUMN SampleIndex int NOT NULL;
ALTER TABLE dbo.OmnidotsTraces
    ADD CONSTRAINT PK_OmnidotsTraces PRIMARY KEY (TraceId, SampleIndex);
```

Historical order cannot be recovered because the old schema did not store it. The migration assigns a stable index to the rows as encountered and documents that limitation. All post-migration traces preserve vendor sample order exactly.

Each trace is written in one transaction:

1. Begin transaction.
2. Insert the trace-index row.
3. Add trace samples with indexes `0..n-1` through EF batch insertion.
4. Save once.
5. Commit.

A failure rolls back both the trace index and every sample. The implementation removes the per-sample raw-SQL round trip.

### Migration Assets

Create idempotent forward and rollback scripts under:

- `omnidotsmonitor/OmnidotsMonitor/postgres/`
- `omnidotsmonitor/OmnidotsMonitor/sqlserver/`

Forward scripts create the cursor table and rebuild or alter trace persistence safely. Rollback scripts remove the cursor table and return trace storage to its previous shape, with an explicit warning that dropping `sample_index` discards ordering metadata.

PostgreSQL integration fixtures adopt the new canonical schema. SQL Server provider support is protected by EF metadata tests and migration-contract tests; live SQL Server verification is run when an environment connection is supplied but is not required for ordinary local tests.

## Phase 3 Trace Collection Design

Add validated options:

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

Semantics:

- `Enabled=false` skips trace collection explicitly.
- An empty `AllowedSerialIds` list means every deployed Omnidots monitor.
- A non-empty list limits collection to those serial IDs for staged rollout.
- `MaxMonitorsPerRun` caps work in one scheduled run and must be greater than zero.

The initial checked-in configuration preserves the current single-monitor behavior without embedding that serial in code. When the allow-list contains more eligible monitors than `MaxMonitorsPerRun`, the handler prioritizes monitors with no stored trace, followed by the oldest latest trace end-time. Serial ID supplies a deterministic base order, and monitors tied at the same priority rotate by the current five-minute UTC run slot. The rotation ensures that a monitor which repeatedly returns no traces cannot starve other unseen monitors.

Trace requests remain sequential initially because the vendor operation is known to be slow. Structured completion logs record monitors eligible, attempted, succeeded, failed, traces stored, samples stored, and total duration. Any per-monitor failures are aggregated after eligible monitors have been attempted.

## Components and Boundaries

The implementation keeps handlers focused and adds narrow ports rather than expanding direct `DBClient` use:

- a pure fetch-window calculator;
- a pure site-active-duration calculator;
- typed configuration and API response DTOs;
- cursor queries and atomic measurement-import commands;
- transactional trace commands;
- validated monitoring and trace-collection options; and
- an aggregate import exception carrying failed serial IDs.

`IDBClient` remains a compatibility facade and implements the new narrow ports. Vendor JSON parsing, webhook signature formatting, business rules, and aggregate selection remain manual and directly tested. Mapperly is not used for these non-trivial behaviors.

## Test Strategy

### Unit Tests

Cover:

- positive two-hour lookback and five-minute overlap;
- no future request timestamps;
- cursor-based restart after a missed schedule;
- same-day, multi-day, closed-day, midnight, and DST offline intervals;
- full-UTC watchdog staleness and `Europe/London` business windows;
- missing, malformed, valid, and invalid webhook signatures;
- non-sensitive measuring-point responses and log redaction;
- aggregate failure after later monitors still run; and
- trace allow-list and per-run limit behavior.
- fair trace selection across repeated throttled runs.

### API Integration Tests

Use the ASP.NET Core test host to verify:

- HTTP 200 with the safe configuration response;
- HTTP 401 for missing, malformed, or mismatched webhook signatures;
- HTTP 400 for authenticated malformed alarm payloads; and
- no secret appears in response bodies.

### Persistence Tests

PostgreSQL integration tests verify:

- independent Peak, Veff, and VDV cursors;
- monotonic cursor advancement;
- measurement and cursor atomicity;
- Peak-only updates to `LastDataTime1Min`;
- ordered trace reconstruction; and
- rollback of partially failed trace writes.

Provider metadata and migration-contract tests verify both PostgreSQL and SQL Server table, column, key, check-constraint, and type mappings.

### Job Tests

Use non-empty monitor fixtures and capture outbound vendor requests. Verify the one-shot and Quartz paths use past start times, retain the fifteen-minute schedule offset, return success when all monitors succeed, and fault when any monitor fails.

## Rollout and Rollback

1. Deploy phase 1 and verify request windows, webhook rejection metrics, job exit status, and watchdog behavior.
2. Back up Omnidots tables.
3. Apply phase 2 forward migrations to the selected provider.
4. Verify cursor initialization and trace keys before deploying the phase 2 application.
5. Deploy phase 2 and run one-shot Peak, Veff, VDV, and trace smoke executions.
6. Deploy phase 3 with a small trace allow-list and conservative `MaxMonitorsPerRun`.
7. Expand the allow-list or clear it only after vendor latency and database volume are acceptable.

Phase 1 can be rolled back as an application-only release. Phase 2 application rollback requires the old application to be deployed before running schema rollback; the forward schema may safely remain during an application rollback. Phase 3 can be disabled entirely through configuration.

## Acceptance Criteria

- Scheduled Veff and VDV requests never start in the future.
- A missed schedule resumes from the independent series cursor without leaving a two-hour gap.
- Veff and VDV cannot advance the Peak cursor or `LastDataTime1Min`.
- No API response or log exposes either Omnidots secret.
- Missing or invalid webhook signatures never receive HTTP 200.
- Same-day and timezone-sensitive offline calculations count only elapsed active-site time.
- Any monitor import failure makes the Quartz or one-shot execution fail after remaining monitors are attempted.
- Watchdog staleness uses complete UTC timestamps and an explicit UK notification window.
- New traces preserve sample order and are committed all-or-nothing.
- Trace eligibility is controlled only by validated configuration, with no source-code serial filter.
- Forward and rollback scripts exist and are contract-tested for PostgreSQL and SQL Server.
- The complete Omnidots test suite, focused PostgreSQL integration tests, solution build, and `git diff --check` pass.

## Non-Goals

- Removing SQL Server runtime support.
- Replacing the Omnidots vendor API or changing its payload formats.
- Parallelizing trace downloads before sequential fleet behavior is measured.
- Reconstructing the true order of historical trace samples whose old schema never stored an ordinal.
- Refactoring unrelated notification and rule-processing behavior.
