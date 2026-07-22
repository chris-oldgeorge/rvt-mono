# Shared Durable Delivery and Svantek Remediation Design

## Purpose

Create one reusable durable-delivery foundation in `rvt-monitor-common`, migrate MyATM from its monitor-local outbox without losing pending work, and use the same foundation to remediate Svantek ingestion, alert delivery, scheduling, cancellation, health, and test coverage.

The design retains PostgreSQL as the primary provider, preserves SQL Server runtime support, keeps `IDBClient` as a compatibility facade, and routes new application behavior through narrow ports.

## Scope and Delivery Units

The work is divided into three independently reviewable and deployable units.

1. **Shared durable-delivery foundation**
   - common outbox entity, DTOs, ports, dispatcher, payload contracts, options, and provider mappings;
   - one canonical outbox table for PostgreSQL and SQL Server;
   - common alert-delivery planning that does not perform network I/O.
2. **MyATM shared-outbox migration**
   - preserve `my_atm_alert_occurrence` and all suppression/escalation semantics;
   - backfill and cut over from `my_atm_outbox_message` to the shared table;
   - keep a rollback path for one compatibility release.
3. **Svantek reliability remediation**
   - async bounded ingestion, aggregate failure reporting, atomic measurement/aggregate/watermark/rule/notification/outbox commits, durable delivery, site-hours-aware offline behavior, readiness, and restored tests.

Each unit receives its own implementation plan and verification gate. The shared foundation must merge before the MyATM or Svantek plans begin. The MyATM migration is deployed before Svantek so the existing production outbox behavior proves the common dispatcher and table contract.

## Architectural Decisions

### Reuse the Existing Generic Alert Model

`rvt-monitor-common` already owns:

- `NoiseRuleEvaluator` and the shared rule DTOs;
- `RuleNotificationRequest` and notification message semantics;
- `RuleAlertNotificationDispatcher` for the current synchronous path;
- `NotificationEntity` / `notification` for portal-visible alerts;
- `NotificationSentEntity` / `notification_sent` for delivery audits; and
- `IMonitorEventPublisher` for common MQTT payload formatting.

The durable design reuses these contracts and tables. It does not create a Svantek alert-occurrence table.

The existing synchronous dispatcher remains available to AirQ and Omnidots until those monitors are deliberately migrated. New durable paths use a side-effect-free planner that produces a notification and delivery requests for an application-owned transaction.

### Add One Shared Outbox Table

Durability requires state that neither `notification` nor `notification_sent` contains: pending status, payload, delivery kind, retry schedule, lease/fencing identity, completion, and terminal error. Overloading `notification_sent` would mix pending commands with immutable audit history and still require the same additional columns.

Create one common table named:

- PostgreSQL: `monitor_delivery_outbox`
- SQL Server: `dbo.MonitorDeliveryOutbox`

The common entity is `MonitorDeliveryOutboxEntity` under `Rvt.Monitor.Common/Data/Entities/`. `MonitorDbContextBase` exposes `DbSet<MonitorDeliveryOutboxEntity> DeliveryOutbox` and `MonitorModelBuilderExtensions` maps the provider-specific identifiers.

PostgreSQL shape:

```sql
CREATE TABLE monitor_delivery_outbox (
    id uuid PRIMARY KEY,
    producer text NOT NULL,
    notification_id uuid NULL REFERENCES notification (id) ON DELETE SET NULL,
    correlation_key text NULL,
    delivery_key text NOT NULL,
    kind text NOT NULL,
    destination text NOT NULL,
    payload_version integer NOT NULL,
    payload text NOT NULL,
    status text NOT NULL,
    attempt_count integer NOT NULL,
    next_attempt_at timestamp with time zone NOT NULL,
    lease_id uuid NULL,
    lease_until timestamp with time zone NULL,
    completed_at timestamp with time zone NULL,
    dead_lettered_at timestamp with time zone NULL,
    last_error text NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT uq_monitor_delivery_outbox_producer_delivery
        UNIQUE (producer, delivery_key),
    CONSTRAINT ck_monitor_delivery_outbox_status
        CHECK (status IN ('Pending', 'InProgress', 'Completed', 'DeadLetter'))
);

CREATE INDEX ix_monitor_delivery_outbox_due
    ON monitor_delivery_outbox (producer, status, next_attempt_at);
```

SQL Server uses the same logical columns with `uniqueidentifier`, `nvarchar`, `int`, and `datetime2`; it defines the same primary key, `(Producer, DeliveryKey)` uniqueness, status check, notification foreign key with `ON DELETE SET NULL`, and due-work index.

`producer` is a stable canonical application identifier. The initial values are exactly `MyAtm` and `Svantek`; the application validates these values with ordinal comparison before inserting or dispatching. Claims always filter by producer, so one monitor process cannot deliver another monitor's messages even when both use the same database.

`notification_id` is populated for alert email, SMS, and alert MQTT deliveries. It is null for data-inserted MQTT messages. `correlation_key` preserves a monitor-specific logical key without coupling the common table to a monitor-specific foreign key; MyATM stores its `my_atm_alert_occurrence.occurrence_key` there.

`payload_version` begins at `1`. During the MyATM rollback-compatibility release, MyATM emits only version 1 payloads so messages can be synchronized back to the legacy table.

### Keep Business Transactions in Monitor Adapters

`rvt-monitor-common` does not own monitor transactions or reference monitor-specific EF contexts. It defines common delivery contracts and dispatcher behavior. MyATM and Svantek implement their own atomic commit commands inside their app-local `DBClient` and EF context.

This preserves the existing rule that common infrastructure must remain free of monitor-specific persistence and mapping policy.

## Shared Contracts

### Delivery Kinds and Payloads

Create the following common contracts under `Rvt.Monitor.Common/Delivery/`:

```csharp
public static class MonitorDeliveryProducers
{
    public const string MyAtm = "MyAtm";
    public const string Svantek = "Svantek";
}

public enum MonitorDeliveryKind
{
    MqttDataInserted,
    MqttAlert,
    Email,
    Sms
}

public sealed record MonitorDeliveryRequest(
    Guid Id,
    string Producer,
    Guid? NotificationId,
    string? CorrelationKey,
    string DeliveryKey,
    MonitorDeliveryKind Kind,
    string Destination,
    int PayloadVersion,
    string Payload,
    DateTime CreatedAt);

public sealed record MonitorDeliveryMessage(
    Guid Id,
    string Producer,
    Guid? NotificationId,
    string? CorrelationKey,
    string DeliveryKey,
    MonitorDeliveryKind Kind,
    string Destination,
    int PayloadVersion,
    string Payload,
    int AttemptCount,
    Guid LeaseId);
```

The shared version 1 payload deliberately preserves the existing `MyAtmOutboxPayload` JSON wire shape so legacy rows can be copied without rewriting JSON:

```csharp
public sealed record MonitorDeliveryPayloadV1(
    Guid NotificationId,
    DateTime Timestamp,
    string SerialId,
    int? CustomerId,
    string FleetNr,
    AlertType AlertType,
    string Field,
    double Level,
    string? PortalBaseUrl = null);
```

The codec uses the existing PascalCase JSON property names and rejects missing required fields, an empty serial ID, a non-UTC timestamp, or an alert/contact payload whose `NotificationId` is empty. `MqttDataInserted` is the sole kind allowed to carry `Guid.Empty`. The dispatcher formats the existing MQTT messages from this payload: `MyAtm` uses the `Dust` prefix, `Svantek` uses `Noise`, and data-inserted messages retain `Dto Inserted`. Email/SMS select the existing `MessageService` template from `AlertType`, fleet number, and the dispatcher's validated portal base URL. To preserve the deployed MyATM durable path, MyATM includes the notification URL for every contact kind; Svantek follows the existing common dispatcher and includes it only for Alert and Caution. This producer-specific compatibility branch lives only in the common delivery formatter and is covered by parity tests. The row's `Destination`, rather than duplicated JSON contact data, supplies the email address or phone number. Secrets and transport credentials are never serialized.

### Narrow Outbox Ports

Create:

```csharp
public interface IMonitorDeliveryOutboxQueries
{
    Task<MonitorDeliveryMessage?> ClaimNextDueAsync(
        string producer,
        DateTime utcNow,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);
}

public interface IMonitorDeliveryOutboxCommands
{
    Task<bool> CompleteAsync(
        Guid id,
        Guid leaseId,
        DateTime completedAt,
        MonitorDeliveryAudit? audit,
        CancellationToken cancellationToken = default);

    Task<bool> RetryAsync(
        Guid id,
        Guid leaseId,
        DateTime nextAttemptAt,
        string error,
        CancellationToken cancellationToken = default);

    Task<bool> DeadLetterAsync(
        Guid id,
        Guid leaseId,
        DateTime failedAt,
        string error,
        MonitorDeliveryAudit? audit,
        CancellationToken cancellationToken = default);
}

public interface IMonitorDeliveryFailureSink
{
    Task RecordFailureAsync(
        MonitorDeliveryMessage message,
        string error,
        bool terminal,
        CancellationToken cancellationToken = default);
}
```

`MonitorDeliveryAudit` is `record MonitorDeliveryAudit(Guid NotificationId, string Address, string Result, DateTime SentAt)`. The dispatcher creates it only for email/SMS when the outbox row has a non-null `NotificationId` that matches the decoded payload. The app-local adapter writes `notification_sent` in the same database outcome transaction as completion or dead-letter. MQTT never writes `notification_sent`; malformed contact payloads and migrated orphan references cannot produce an audit. Each app implements `IMonitorDeliveryFailureSink` through its existing operational-error command. The dispatcher invokes it after a fenced retry/dead-letter mutation succeeds; MyATM records terminal failures only to preserve current behavior, while Svantek records every failed attempt. Failure-sink errors are warning-logged and never undo or mask the durable outbox outcome.

Claiming is one row at a time. An eligible row is either `Pending` with `NextAttemptAt <= utcNow`, or `InProgress` with `LeaseUntil <= utcNow`. The adapter orders eligible rows by `NextAttemptAt`, then `CreatedAt`, then `Id`; conditionally changes the oldest row to `InProgress`, increments `AttemptCount`, assigns a fresh `LeaseId`, and sets `LeaseUntil`. It retains `LastError` until success or the next failed attempt, retries at most three lost conditional claims before returning null, and never returns a row it did not successfully fence. Completion, retry, and dead-letter mutations require `Id`, `LeaseId`, and `Status='InProgress'`; stale owners cannot mutate a reclaimed row. Retry returns the row to `Pending`, clears its lease fields, and records `NextAttemptAt` and `LastError`. Completion clears the lease and error fields and sets `CompletedAt`; dead-letter clears the lease fields and sets `DeadLetteredAt` and `LastError`. Both leave the immutable payload intact for audit and rollback.

### Generic Dispatcher

Create `MonitorDeliveryDispatcher` in common. It depends on the two narrow ports, `IMonitorDeliveryFailureSink`, `IMqttClient`, `IMessageService`, `ILogger<MonitorDeliveryDispatcher>`, and validated `MonitorDeliveryOptions`. The options include the exact producer, portal base URL, and `MonitorDeliveryFailureMode` (`DeadLetterOnly` or `AnyDeliveryFailure`) as well as the limits below. MyATM uses `DeadLetterOnly` to preserve its current scheduled-job result semantics; Svantek uses `AnyDeliveryFailure` so a failed transport attempt cannot yield a successful job result.

Extend the shared `IMqttClient.PublishAsync` and `ConnectAsync` signatures with an optional `CancellationToken`, and flow it through `RvtMqttClient`'s connect lock, MQTTnet connect, and publish operations. Existing callers remain source-compatible apart from mocks that must match the added parameter.

Default options preserve the proven MyATM behavior:

- batch size: 50;
- delivery timeout: 30 seconds;
- lease: 120 seconds;
- initial retry: 30 seconds;
- retry cap: 30 minutes;
- maximum attempts: 8.

For each bounded pass, the dispatcher:

1. claims one row immediately before delivery;
2. validates producer, kind, and payload version;
3. delivers using the appropriate transport;
4. completes with an audit after success;
5. retries transient failures after `min(30 seconds * 2^(AttemptCount - 1), 30 minutes)`;
6. dead-letters malformed/unsupported payloads immediately and transport failures after the eighth claimed attempt;
7. continues with later rows after an individual failure; and
8. throws an aggregate dispatch exception after the pass for every dead-letter, plus transient failures when configured with `AnyDeliveryFailure`.

Persisted delivery errors contain only `Delivery failed (<exception type>)`, truncated to 1,024 characters; exception messages, destinations, payloads, and credentials are not copied into `LastError`. Cancellation stops further claims and is passed to every database and transport operation. Caller cancellation rethrows immediately and leaves the current lease to expire; the dispatcher's 30-second timeout is treated as a transient failed attempt and fenced back to `Pending`. MQTT uses `IMqttClient.PublishAsync`; email/SMS use the existing `IMessageService.SendMessageAsync`. The dispatcher introduces no synchronous-over-async wrapper.

### Durable Alert Planning

Add `MonitorDeliveryIdentity` and `RuleAlertDeliveryPlanner` in common. `MonitorDeliveryIdentity.CreateGuid(string)` uses the existing MyATM algorithm—SHA-256 over UTF-8 input and the first 16 bytes passed to `Guid`—so migrated notification and outbox identifiers do not change. The planner replaces network side effects with a returned plan:

```csharp
public sealed record RuleAlertDeliveryPlan(
    NotificationDto Notification,
    IReadOnlyList<MonitorDeliveryRequest> Deliveries);
```

The planner consumes `RuleNotificationRequest`, eligible contacts, producer, optional customer ID, a caller-supplied stable correlation key, and creation time. The caller derives `NotificationId` as `CreateGuid($"notification:{correlationKey}")`; the planner does not generate random IDs. Per-destination delivery keys retain MyATM's exact `$"{correlationKey}:{kind}:{destination}"` format, including destinations `alert` and `insert` for MQTT. Svantek rule correlation keys are `$"svantek:rule:{monitorId:N}:{ruleId:N}:{alertType}:{alertTime.ToUniversalTime():O}"`; offline, battery, and site-average handlers use the same prefix with their transition type in place of `rule`. The planner creates one shared notification, one alert MQTT request, and the email/SMS requests whose contacts pass `ShouldSendAtTime(alertTime)`. It performs the same message-template selection as `RuleAlertNotificationDispatcher`.

The existing `RuleAlertNotificationDispatcher` remains unchanged for non-migrated monitors. Tests compare planner output with the existing dispatcher semantics for Alert, Caution, Offline, BatteryAlert, and BatteryCaution.

## MyATM Migration Design

### Preserved MyATM Behavior

`my_atm_alert_occurrence` remains app-local. It continues to provide stable event-time suppression, Caution-to-Alert escalation, recent-notification guards, and logical occurrence identity. No common component interprets those rows.

`IMyAtmDustImportCommands` and `IMyAtmAlertCommitCommands` remain MyATM-owned because they atomically combine MyATM measurements, watermarks, rule state, occurrences, notifications, and delivery requests. Their result types expose newly created, still-pending rows as common `MonitorDeliveryRequest` values; `MonitorDeliveryMessage` is reserved for rows that have actually been claimed and therefore have a lease ID.

### Cutover Migration

The PostgreSQL and SQL Server forward scripts perform these idempotent steps:

1. create the shared table, constraints, and due-work index;
2. copy every `my_atm_outbox_message` row into the shared table with `Producer='MyAtm'`;
3. preserve `Id`, `DeliveryKey`, `Kind`, `Destination`, `Payload`, `AttemptCount`, `NextAttemptAt`, `LeaseId`, `LeaseUntil`, `CompletedAt`, `LastError`, and `CreatedAt`, set `DeadLetteredAt` null because the legacy schema has no such timestamp, and map legacy `Leased` status to shared `InProgress`;
4. store `OccurrenceKey` as `CorrelationKey`;
5. left join `my_atm_alert_occurrence` and `notification` to populate `NotificationId` only when the referenced notification row exists, otherwise leave it null;
6. set `PayloadVersion=1`; and
7. abort before copying if a legacy status is outside `Pending`, `Leased`, `Completed`, and `DeadLetter`; and
8. leave an existing shared row unchanged when it has a greater attempt count or is terminal while the legacy row is non-terminal; otherwise copy the legacy state, with `Completed` and `DeadLetter` treated as terminal and newer `CompletedAt` breaking a `Completed` tie.

The unique key becomes `(Producer, DeliveryKey)`, so existing MyATM delivery keys remain unchanged.

### Compatibility Release

The cutover is a controlled scheduler deployment:

1. disable MyATM one-shot/Quartz import and outbox jobs;
2. apply the shared schema and backfill migration;
3. deploy MyATM code that reads and writes only the shared table;
4. run focused claim, delivery, and replay smoke tests;
5. re-enable the dispatcher, then import jobs; and
6. retain `my_atm_outbox_message` unchanged for one release.

The rollback script treats each shared `Producer='MyAtm'`, version 1 row as authoritative because the legacy table has remained frozen since cutover. It inserts missing legacy rows and overwrites matching legacy delivery state, mapping shared `InProgress` back to `Leased`. It restores `OccurrenceKey` from `CorrelationKey`; rows without a matching occurrence retain a null occurrence key. The shared table is not dropped during rollback because Svantek may already depend on it.

Dropping `my_atm_outbox_message` is a later cleanup migration after the compatibility release and is outside these three plans.

### MyATM Code Migration

- replace `MyAtmOutboxMessageEntity` and app-local mappings with `MonitorDeliveryOutboxEntity` from common;
- make `IDBClient` implement the common outbox query/command ports;
- replace `MyAtmOutboxDispatcher` with `MonitorDeliveryDispatcher` configured for producer `MyAtm`;
- preserve the existing `DispatchOutbox` one-shot and Quartz job name and schedule;
- preserve immediate bounded dispatch after successful imports;
- keep MyATM options values and bind them into common `MonitorDeliveryOptions`; and
- retain architecture tests that scheduled handlers only use atomic commit boundaries.

All existing MyATM outbox tests are ported rather than deleted. PostgreSQL integration tests prove backfill, claims, fencing, retries, dead letters, replay idempotency, and the occurrence correlation link. Migration-contract tests protect SQL Server equivalence.

## Svantek Remediation Design

### Async Vendor Gateway and Request Windows

Convert `SvantekHttpGateway`, `IHttpClient`, service methods, job runner, and dispatcher paths to `Task` plus `CancellationToken`. Remove every `.Result` and synchronous-over-async wrapper from scheduled paths.

Noise request windows use a pure calculator. For each monitor:

- with a watermark, start at `max(deploymentStart, LastDataTime15Min - 5 minutes)`;
- without a watermark, start at `max(deploymentStart, utcNow - MaximumInitialBackfill)`;
- use `LastStatusTimestamp + 1 hour` as the candidate end when status exists, otherwise use `utcNow`, then clamp the result to `utcNow`;
- return no request when the clamped end is not after start, and never produce a future endpoint; and
- split a long interval into bounded vendor requests rather than one indefinite request.

`SvantekImportOptions` supplies two validated limits: `MaximumInitialBackfill = 7 days` and `MaximumRequestWindow = 12 hours`. Both must be greater than zero, and the request window cannot exceed the initial backfill. Operators can increase the backfill deliberately without changing code. Twelve-hour windows follow the bounded request convention already proven by Omnidots and are implemented as repeated SvanNET multi-data requests, not as an invented vendor pagination contract.

### Aggregate Fleet Failure Semantics

Catalogue, noise, offline, battery, site-average, sound-recording, and outbox jobs continue processing independent monitors/projects after individual failures. Each failure is written through `ISvantekOperationalCommands`. At the end of the bounded pass, the handler throws an aggregate exception containing the affected project/serial identifiers and inner failures.

Authentication, fleet-query, and configuration failures before iteration fault immediately. One-shot returns non-zero and Quartz records a failed execution whenever any unit fails.

### Catalogue State Preservation

Catalogue DTOs never overwrite runtime-owned fields. `SvantekDbMapper.UpdateMonitorEntity` explicitly ignores:

- all latest-data timestamps;
- `Offline`;
- `BatteryStatus`;
- customer/location ownership fields; and
- deployment state.

Status telemetry remains updatable. Mapper and PostgreSQL integration regressions prove an hourly catalogue refresh preserves offline and watermark state.

### Atomic Noise Import

Add `ISvantekNoiseImportCommands.CommitNoiseImportAsync`. One transaction commits:

- idempotent chronologically normalized noise samples;
- each newly closed eight-hour aggregate;
- monotonic `LastDataTime15Min`;
- offline recovery when the newest committed sample is no older than 24 hours at commit time;
- rule state transitions;
- shared `notification` rows;
- data-inserted MQTT requests;
- alert MQTT requests; and
- email/SMS delivery requests.

Rule evaluation becomes side-effect-free before the commit. It produces rule transitions and `RuleAlertDeliveryPlan` values. The commit command conditionally applies transitions against current database state so concurrent or replayed imports do not create duplicate notifications. Unique `(Producer, DeliveryKey)` rows provide per-destination idempotency.

Data-inserted MQTT is created only when at least one new measurement is committed. It is queued in the same transaction with a null `NotificationId`.

After a successful commit, the handler invokes one bounded Svantek outbox pass. Dispatch failure faults the job but does not roll back committed measurements or duplicate delivery commands.

### Scheduled Alert Commits

Add `ISvantekAlertCommitCommands.CommitAlertAsync` for offline, battery, and site-average transitions. Each command conditionally updates its owning state, writes the shared notification, and queues all delivery rows atomically.

The handlers do not inject `IMessageService`, `IMonitorEventPublisher`, or direct notification-write delegates. Architecture tests enforce this boundary across every file under `api/UseCases`.

### Offline Site Hours

Use a pure active-site interval calculator based on the intended Omnidots site-hours behavior. It intersects `[lastDataTime, utcNow]` with each local site's configured active interval and sums the intersections in UTC. If no sample has ever arrived, deployment start replaces `lastDataTime`. Weekday, Saturday, and Sunday schedules are selected from `SiteTimes` in the monitor's local timezone with these exact rules:

- both start and end null means the site is closed that day;
- exactly one boundary null is invalid configuration;
- start before end is a same-day interval;
- start after end crosses local midnight; and
- equal start/end means a 24-hour interval.

For a spring-forward invalid local boundary, advance to the first valid local instant. For a fall-back ambiguous boundary, choose the earlier UTC instant for an interval start and the later UTC instant for its end. The monitor becomes offline only when accumulated active seconds are strictly greater than the offline rule's `AveragingPeriod`; a new qualifying sample transitions it online atomically.

Missing or invalid timezones produce operational failures and participate in aggregate job failure reporting.

### Sound Recordings

Sound retrieval remains a separate follow-up because blob storage cannot participate in the relational transaction. It retains the deterministic `{notificationId}.wav` object key and per-run listing cache.

The vendor download, blob write, and notification link update are fully async and cancellable. A blob write followed by a link-update failure is safe to retry because the object key is idempotent. Per-alert failures are aggregated after remaining alerts are attempted.

### Health and Scheduling

Add `/readiness` backed by a narrow `ISvantekHealthQueries.CanConnectAsync` port. `/liveness` remains process-only.

Add `DispatchOutbox` to `MonitorJobRunner`, `SupportedJobNames`, and `appsettings.json` on a one-minute schedule. Quartz cancellation flows through `SvantekMonitorJobDispatcher` to every async job. Focused tests prove configured job names exactly match dispatcher support.

### Test Restoration

Remove compile exclusions for:

- `TestRules.cs`;
- `TestSvantekApi.cs`;
- `TestSvantekApiException.cs`; and
- `TestSvantekApiNoiseLevels.cs`.

Tests are rewritten against current handler and narrow-port contracts rather than restoring obsolete facade overloads. No excluded replacement file remains at completion.

Architecture tests scan all production C# under `svantekmonitor/SvantekMonitor/api`, including `UseCases`, and enforce:

- no concrete `DBClient` fields outside composition/persistence;
- no direct messaging or MQTT from scheduled handlers;
- atomic commit ports for ingestion and scheduled alerts;
- no `.Result`, `.Wait()`, or `GetAwaiter().GetResult()` in scheduled/vendor paths; and
- app-local Mapperly only.

## Migration and Deployment Assets

Canonical shared-table scripts live under:

- `rvt-monitor-common/database/migrations/2026-07-15-add-monitor-delivery-outbox.postgres.sql`
- `rvt-monitor-common/database/migrations/2026-07-15-add-monitor-delivery-outbox.sqlserver.sql`

MyATM cutover/rollback scripts live under `myatmmonitor/database/migrations/`. Svantek forward/rollback scripts live under `svantekmonitor/database/migrations/` and call out the prerequisite shared-table deployment.

Every script is idempotent and contract-tested. Forward scripts are applied before application deployment. Rollback scripts preserve delivery rows and never drop the shared table while either monitor uses it.

## Verification Strategy

### Shared Foundation

- unit tests for delivery planning across all alert types and contact windows;
- unit tests for payload versions and malformed payload rejection;
- dispatcher tests for one-at-a-time claims, batch limit, timeout, retry delay, dead letter, cancellation, ownership loss, and aggregate failure;
- EF metadata tests for PostgreSQL and SQL Server identifiers, constraints, and indexes; and
- migration-contract tests for both providers.

### MyATM Migration

- existing MyATM commit and dispatcher suites ported to common contracts;
- PostgreSQL backfill test with Pending, InProgress, Completed, and DeadLetter rows;
- preservation of IDs, leases, attempts, errors, occurrence correlations, and notification IDs;
- rollback synchronization test;
- complete MyATM PostgreSQL and non-PostgreSQL suites; and
- root solution build.

### Svantek

- unit tests for bounded past-only request windows and multi-window imports;
- partial-failure tests proving later monitors run and the job ultimately fails;
- mapper/integration tests for offline and watermark preservation;
- PostgreSQL atomicity tests that force failure between each logical persistence stage;
- replay and concurrent-commit tests proving no duplicate notification or delivery;
- outbox data-insert, alert, email, SMS, retry, and dead-letter tests;
- offline interval tests including DST;
- TestHost liveness/readiness tests;
- scheduler/cancellation tests;
- rewritten rule, ingestion, exception, and offline suites; and
- complete Svantek suite plus solution build.

All database integration tests use the isolated `RVT__POSTGRES_INTEGRATION_CONNECTION` fixture convention. No connection string, API key, destination, or credential is committed.

## Rollout Sequence

1. Merge and deploy the shared common code and schema with no monitor using it.
2. Pause MyATM scheduled work, backfill the shared table, deploy the migrated MyATM app, smoke the dispatcher, and resume work.
3. Observe at least one complete retry/dead-letter window and confirm no pending legacy-only rows.
4. Deploy Svantek schema additions and application with the seven-day initial backfill and twelve-hour request-window defaults.
5. Run Svantek one-shot catalogue, noise, offline, sound, and outbox smoke jobs.
6. Enable Svantek Quartz scheduling.
7. Remove the MyATM legacy table only in a later separately approved cleanup.

## Rollback

- Shared foundation rollback leaves the additive table in place.
- MyATM rollback pauses jobs, synchronizes shared MyATM version 1 rows back to `my_atm_outbox_message`, deploys the old app, then resumes jobs.
- Svantek rollback pauses Svantek jobs and deploys the prior app. Shared outbox rows remain preserved for a later forward deployment; they are not deleted.
- No rollback drops `notification`, `notification_sent`, `my_atm_alert_occurrence`, or the common outbox table while pending delivery rows exist.

## Acceptance Criteria

- MyATM and Svantek use the same `monitor_delivery_outbox` table and common dispatcher contracts.
- MyATM suppression/escalation and all existing fenced delivery behavior remain unchanged.
- Migration preserves every MyATM delivery row and provides a tested rollback synchronization path.
- Svantek never reports a successful scheduled run after an import/delivery unit failed.
- Svantek vendor calls are async, cancellable, bounded, and never request future data.
- Catalogue refreshes preserve runtime-owned offline and watermark state.
- Measurements, aggregates, watermarks, rule transitions, notifications, and delivery commands commit atomically.
- Data-inserted and alert MQTT, email, and SMS delivery survive process termination after commit.
- Stale lease holders cannot complete, retry, or dead-letter reclaimed messages.
- Svantek offline duration respects site hours and timezone/DST boundaries.
- `/readiness` fails when the database is unavailable while `/liveness` remains process-only.
- All four excluded Svantek test suites are restored or rewritten and compiled.
- PostgreSQL integration suites, provider mapping/migration contracts, complete MyATM and Svantek suites, root solution build, and `git diff --check` pass.

## Non-Goals

- Migrating AirQ or Omnidots to the durable dispatcher in this delivery.
- Replacing MyATM's alert-occurrence business table.
- Dropping `my_atm_outbox_message` during the compatibility release.
- Guaranteeing exactly-once external delivery; the contract is durable at-least-once delivery with idempotent keys and fenced database outcomes.
- Making blob storage transactional with the relational database.
- Removing SQL Server support.
- Refactoring unrelated reporting-monitor storage work.
