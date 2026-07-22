# MyATM Strict Review Remediation Design

**Status:** Approved design direction on 2026-07-14

**Scope:** MyATM reliability, alert durability, and shared MQTT completion semantics

**Supersedes:** The incomplete portions of `2026-07-14-myatm-reliability-refactor-design.md` where this document is more specific

## Outcome

MyATM will poll dust data every 30 minutes and import every missing vendor sample after the last committed watermark. A monitor import will never advance its watermark past an unread page or past rule decisions that have not been durably recorded. Alert delivery will use the same persistent hysteresis behavior as Omnidots' polled rule processing, plus a transactionally created, per-destination outbox so retries do not create a new logical alert or resend destinations that already succeeded.

The implementation keeps MyATM's EF Core, PostgreSQL-first architecture. `IDBClient` remains a compatibility facade; new behavior is exposed through narrow MyATM query and command interfaces. Vendor parsing, field selection, rule state transitions, and outbox decisions remain explicit application logic and are covered by focused tests.

## Review Findings Covered

| Finding | Design response |
| --- | --- |
| Telemetry endpoints return only the vendor default page | Use explicit server-side watermark filters, ascending ordering, page size, and keyset pagination for all dust periods and accessory data. |
| Catalogue refresh clears persisted offline state | Treat `Offline` and other runtime-owned fields as update-ignored Mapperly targets; initialize them only for new monitors. |
| `ProcessRulesV2` ignores rule activity windows | Evaluate `RuleActiveTime.IsActive` for every sample using the sample timestamp and correct the false-positive test setup. |
| Watermark is committed before rule processing | Commit readings, watermark, rule mutations, logical notifications, and outbox messages in one transaction per page. |
| Cancellation becomes an adapter or aggregate failure | Propagate caller cancellation immediately and never log it as an operational error. |
| MQTT publishing is fire-and-forget | Make the shared publisher awaitable and dispatch MyATM MQTT work through the durable outbox. |
| `customerId` is discarded when reading monitors | Require customer scope on MyATM monitor queries and enforce it in EF Core. |
| Averaged measurements dereference nullable values | Preserve missing aggregates as `null`; rules skip missing fields instead of treating them as zero. |
| A one-minute cron cannot finish a throttled fleet pass | Change `StoreDustLevels` to `0 0/30 * * * ?` and keep non-concurrent Quartz execution. |

## Architectural Shape

The main flow is:

1. `StoreDustLevelsHandler` asks `MyAtmMonitorReader` for monitors scoped to the configured customer.
2. `MyAtmHttpGateway` reads one bounded, complete page strictly after the committed watermark.
3. A pure MyATM rule evaluator processes the page in timestamp order and returns rule mutations plus zero or more alert occurrences.
4. A narrow `IMyAtmDustImportCommands` implementation commits the page as one EF Core transaction.
5. The transaction inserts deduplicated measurements, conditionally updates rule state, advances the matching period watermark, writes logical notifications, and enqueues contact and MQTT deliveries.
6. `MyAtmOutboxDispatcher` claims pending deliveries and sends them one destination at a time. Successful destinations are never claimed again; transient failures are retried with backoff.
7. The handler requests the next vendor page until the endpoint reports a complete final page or the per-run page budget is reached.

Business rules do not move into `DBClient`. The evaluator produces a commit model; the command validates expected rule state and persists that model atomically. A stale rule-state conflict rolls back the page, reloads current rule state, and evaluates the same page again.

## Vendor Retrieval and Completeness

### Keyset pagination

All measurement endpoints use explicit OData parameters rather than relying on the vendor default of 50 records:

- `$filter=timestamp gt <UTC cursor>`
- `$orderby=timestamp asc`
- `$top=<configured page size>`
- the existing `$select` projection for one-minute measurements

The 15-minute, hourly, daily, and accessory endpoints receive the same filter/order/page contract. The cursor starts at the committed watermark and advances only to the maximum timestamp of a transactionally committed page. Keyset pagination is preferred to `$skip` because records may arrive while a long backfill is running.

The gateway normalizes timestamps to UTC, rejects null deserialization results, sorts defensively, removes duplicate timestamps within a response, and returns a page result containing the records, page cursor, and `HasMore` indicator. A full page means another request is required; it is never interpreted as a complete response.

If a live vendor contract check shows that an averaged or accessory endpoint does not support one of the required OData operators, that endpoint must use a proven vendor continuation mechanism. The implementation must not fall back to local filtering of a default-sized response. An unsupported completeness contract fails the job without advancing the watermark.

### Bounded catch-up

`MyAtmMonitorOptions` gains validated retrieval settings:

- `MeasurementPageSize` (default 1,000)
- `AccessoryPageSize` (default 1,000)
- `MaxPagesPerMonitorPerRun` (default 10)

Each page commits independently. Reaching the page budget is a successful partial catch-up, not data loss: the next 30-minute run resumes from the last committed page. This prevents a newly deployed or long-offline monitor from monopolizing the fleet run.

The existing shared request policy remains the single rate-limit boundary. It applies pacing before every MyATM request, honors `Retry-After`, uses bounded exponential backoff with jitter for 429 and transient 408/5xx responses, and respects cancellation during both permit waits and retry delays.

## Transactional Import

### Commit model

The application-layer commit request contains:

- customer, monitor, serial, and period identity;
- the expected current watermark;
- a sorted page of `DustDto` records;
- the new watermark derived from the maximum page timestamp;
- rule mutations with expected and new `IsActive`/`Accessed` values;
- logical alert occurrences;
- notification rows;
- per-contact and MQTT outbox deliveries;
- one deduplicated data-inserted MQTT delivery for the committed page.

The database command performs all writes in one transaction. Measurement primary keys retain idempotent insertion semantics. The monitor watermark is updated only when its persisted value still matches the request's expected watermark. Rule updates similarly use their expected state. A mismatch raises a concurrency result; it does not partially commit.

If rule evaluation, notification construction, or outbox construction fails, no reading from that page and no watermark change is committed. A subsequent run therefore evaluates the same samples again.

### Transaction size

The unit of work is one vendor page for one monitor and one period, not the entire fleet. This keeps locks bounded and makes backfill progress durable. Pages are processed sequentially per monitor. Fleet concurrency remains one job at a time under the current Quartz policy.

## Rule Evaluation and Anti-Spam Semantics

### Omnidots behavior retained

The polled Omnidots rule path uses persistent `IsActive` state and `LimitOn`/`LimitOff` hysteresis. MyATM will make this behavior explicit:

- an inactive rule crossing `LimitOn` creates one alert occurrence and becomes active;
- an active rule above `LimitOff` creates no additional occurrence;
- a rule becomes inactive only when a present field value is at or below `LimitOff`;
- only a later inactive-to-active transition can create a new occurrence;
- Alert rules are evaluated before Caution rules for the same field;
- an Alert suppresses a Caution for the same field and sample;
- a Caution may escalate to Alert, creating one new Alert occurrence;
- deleted active rules are deactivated without sending;
- readings outside `RuleActiveTime` do not trigger or clear the rule, matching Omnidots' polled evaluator.

Each dust page is evaluated in ascending sample-time order. Activity windows and notification contact windows use the triggering sample's timestamp, not the batch maximum or wall-clock processing time.

### Missing values

The field selector returns `double?`. Unknown fields are rejected and logged as configuration errors. A known field with a missing value is skipped for that rule and sample. It is never coerced to zero, because zero could incorrectly clear an active rule.

The averaged JSON-to-DTO conversion uses null-safe access for `Pm1`, `Pm2_5`, `Pm10`, and `PmTotal`. Partial averaged records remain importable and rules for present fields can still run.

### Logical alert identity

Every inactive-to-active transition has a stable occurrence key:

`customer / monitor / rule / period / severity / triggering-sample-utc`

The key is stored in a MyATM-owned alert-occurrence table with a unique constraint and maps to one shared `notification` row. Replaying the same page cannot create another notification. An app-local deterministic notification identifier is derived from a fixed MyATM namespace plus the occurrence key, making the shared notification primary key idempotent as well.

### Recent-notification guard

Omnidots' webhook path suppresses recent Caution/Alert notifications while allowing Caution-to-Alert escalation. MyATM adds the equivalent check as defense-in-depth, scoped to the same monitor, field, period, and severity family. It is not a reminder timer: an active rule remains silent regardless of how many 30-minute polls occur.

The guard must never suppress Alert escalation from Caution. A genuine re-arm after falling below `LimitOff` may create a new transition; `AlertSuppressionWindowMinutes` defaults to 30 and prevents rapid oscillation from spamming contacts. Suppressed transitions still update rule state consistently, but do not create notification or delivery rows.

## Durable Outbox

### Rows and deduplication

MyATM owns an app-local outbox table rather than placing monitor-specific policy in `rvt-monitor-common`. Each row represents exactly one destination:

- email recipient;
- SMS recipient;
- MQTT alert event; or
- MQTT data-inserted event.

The row stores an immutable payload snapshot, normalized destination/channel, a hashed destination component for its unique delivery key, occurrence or import identity, attempt count, status, next-attempt time, lease expiry, last error, created time, and delivered time. Destination values are never written to normal logs without redaction.

Unique delivery keys are:

- alert contact: occurrence key + channel + normalized destination hash;
- alert MQTT: occurrence key + alert topic;
- data MQTT: customer + serial + period + committed page watermark + insert topic.

Contacts outside their configured send window are not enqueued, preserving current Omnidots/MyATM behavior. A single skipped audit record may be written for traceability.

### Dispatch and retries

`IMyAtmOutboxQueries` and `IMyAtmOutboxCommands` expose claim, complete, retry, and dead-letter operations. The dispatcher claims a small batch using an atomic lease so a crash makes rows available again after lease expiry. It processes each row independently and records the success before moving to the next destination.

The dispatcher claims at most 50 rows for a two-minute lease. Transient failures use exponential backoff with jitter, starting at 30 seconds and capped at 30 minutes. Permanent validation failures are dead-lettered immediately; repeated transient failures are dead-lettered after eight attempts. Operational telemetry exposes pending, retrying, oldest-pending, and dead-letter counts without logging contact details.

Delivery is at-least-once because an external provider may accept a message immediately before the process crashes and before the database success marker is written. Per-destination rows, stable correlation identifiers, leases, and provider idempotency keys where supported make that ambiguity as small as possible. The design guarantees exactly one logical notification and prevents routine poll/retry spam; it does not claim impossible cross-system exactly-once delivery.

### Execution modes

After each page commit, `StoreDustLevelsHandler` awaits an immediate, bounded dispatch attempt for the rows it created. A separate lightweight `DispatchMyAtmOutbox` Quartz/one-shot job runs every minute with do-nothing misfire handling and drains pending and retryable rows, so delivery recovery does not depend on another threshold crossing. Scheduler-disabled API containers do not start background dispatch implicitly.

The shared `IMonitorEventPublisher` changes from `void` fire-and-forget methods to cancellation-aware `Task` methods. All monitor call sites are updated mechanically to await completion. MyATM's application path invokes it only from the outbox dispatcher; other monitor behavior is unchanged apart from observing publish failures instead of losing them during process exit.

## Catalogue State Preservation

Vendor catalogue DTOs describe vendor-owned metadata, not local operational state. Mapperly update mappings must ignore at least:

- `Offline`;
- `Archived`;
- local deployment/runtime timestamps not supplied authoritatively by MyAtmosphere; and
- any local status field documented as owned by offline or deployment workflows.

New monitor inserts initialize required local values explicitly. Existing monitor updates retain their persisted offline state. The catalogue handler cannot clear an offline flag; only the dedicated offline/online state commands may change it.

Mapperly remains analyzer-only and app-local. Architecture and mapping tests protect the ownership boundary.

## Customer Isolation

MyATM monitor queries require `customerId`:

- `ReadMonitorList(int customerId, DateTime? lastDataTime)`;
- `ReadMonitor(int customerId, string serialId)`;
- corresponding narrow async variants where the handler is converted to async.

EF Core applies the customer predicate before materialization. The reader forwards its customer argument instead of discarding it. Serial-specific jobs and test-local filters apply after customer scope, never instead of it. Commands validate that the monitor/customer relationship matches the commit request before updating timestamps or rule state.

Compatibility methods on `IDBClient` may delegate to these scoped operations during migration, but new application code cannot call an unscoped MyATM monitor query.

## Cancellation and Error Semantics

Every async boundary accepts and forwards `CancellationToken`. A caller-requested `OperationCanceledException` is rethrown before broad exception handling in the gateway, handler, reader, dispatcher, and request policy. Cancellation:

- is not wrapped as `AdapterException`;
- is not added to the per-monitor failure aggregate;
- is not written to the operational error table; and
- stops the fleet loop immediately.

Non-cancellation failures remain monitor-scoped where safe. The handler can continue to later monitors, records each operational failure once, and throws an aggregate at job completion so Quartz and one-shot modes report failure. A page transaction failure does not advance that monitor's watermark.

HTTP or parsing errors include endpoint/monitor context but never credentials or unredacted vendor bodies that may contain sensitive data.

## Scheduling and Capacity

`StoreDustLevels` changes from every minute to every 30 minutes:

```text
0 0/30 * * * ?
```

The description states that the job imports all missing one-minute readings. With 151 eligible monitors and 500 ms minimum request spacing, the pacing floor is about 75.5 seconds before paging or retries, leaving substantial headroom inside a 30-minute interval.

Quartz remains globally non-concurrent for this monitor process. Misfires use a do-not-catch-up policy: the next normal run resumes from durable watermarks instead of launching overlapping fleet passes. The scheduler configuration test asserts the exact cron and policy.

The 15-minute, hourly, daily, accessory, offline, and catalogue schedules are unchanged unless implementation testing identifies a direct collision that prevents their documented cadence. All vendor-reading jobs share the same request policy and therefore the same throttle protection.

## Database Changes

PostgreSQL is the primary schema target, with current SQL Server test compatibility retained. New MyATM tables are mapped in `MyAtmMonitorContext` and added to both PostgreSQL integration setup and SQL Server-compatible fixture definitions:

### `my_atm_alert_occurrence`

- occurrence key, primary or unique;
- notification ID, unique;
- customer ID, monitor ID, rule ID, period, alert type, field;
- triggering sample timestamp and creation timestamp;
- suppression status/reason when a transition is intentionally not delivered.

### `my_atm_outbox_message`

- ID and unique delivery key;
- kind, channel, destination snapshot/hash, serialized immutable payload;
- occurrence/import identity;
- status, attempts, next attempt, lease expiry;
- created, delivered, and last-error metadata.

Indexes support `(status, next_attempt_at)` claiming, lease recovery, notification lookup, and unique deduplication. Payloads contain no API/database credentials.

The existing shared `notification` and `notification_sent` tables remain the portal-visible logical notification and delivery-audit stores. Successful and final failed outbox deliveries write one audit result; transient attempts remain on the outbox row to avoid flooding audits.

## Configuration

`MyAtmMonitorOptions` remains the owner of MyATM runtime settings and gains validated nested retrieval, alert-suppression, and outbox options. Defaults are safe for the known fleet but are overrideable through normal configuration providers. No token, connection string, destination, or credential is added to tracked files.

Configuration validation fails startup for non-positive page sizes, page budgets, lease durations, retry counts, suppression durations, or invalid delay ranges. Scheduler cron remains in `MonitorScheduler:Jobs`.

## Code Consistency

The refactor follows the Omnidots project shape where it improves shared readability:

- handlers coordinate one use case;
- gateways own vendor HTTP and JSON concerns;
- pure evaluators own state-machine decisions;
- narrow query/command ports own persistence capabilities;
- composition roots bind options and register implementations;
- Mapperly handles only simple DTO/entity copying;
- manual code handles vendor fields, rule state, aggregate selection, and outbox keys.

The repository-level instruction that style and architectural consistency among monitor subprojects matters remains authoritative. Vendor-specific behavior is not forced into a misleading shared abstraction merely to make filenames match.

## Testing Strategy

Tests are added before implementation changes and include:

### Gateway and throttling

- one-minute, 15-minute, hourly, daily, and accessory URLs include filter/order/top parameters;
- a full first page requests a second page from the last timestamp;
- unordered and duplicate vendor rows are normalized safely;
- an unsupported/incomplete paging response fails without advancing state;
- 429, `Retry-After`, 408/5xx retry, jitter bounds, and cancellation are deterministic under a fake clock/delay.

### Catalogue and customer scope

- catalogue update preserves `Offline` and other local state;
- new monitor initialization remains correct;
- customer 9 cannot read or update a monitor owned by another customer;
- serial-specific and test-local paths retain the customer predicate;
- architecture tests reject unscoped monitor-query use from MyATM handlers.

### Rules and anti-spam

- inactive-to-active sends once across repeated samples and repeated polls;
- active stays silent until a value is at or below `LimitOff`;
- re-armed rules can alert again;
- Caution is suppressed by Alert, while Caution-to-Alert escalation is delivered;
- activity-window exclusion uses the correct `ReadRules(serial, period)` setup and verifies no writes;
- missing averaged fields neither trigger nor clear rules;
- unknown fields produce a controlled configuration error;
- recent-notification suppression prevents rapid oscillation without blocking escalation;
- replay of the same trigger creates one occurrence, notification, and destination set.

### Transactions and outbox

- measurement/rule/notification/outbox failure rolls back readings and watermark;
- successful page commit persists every part together;
- concurrency mismatch rolls back and reevaluates;
- duplicate page commit is idempotent;
- one failed destination does not resend a successful destination;
- expired leases are reclaimed;
- transient failures back off and eventually succeed;
- permanent/max-attempt failures dead-letter once;
- one-shot dispatch awaits MQTT completion;
- cancellation releases work without recording an application error.

### Scheduling and integration

- `StoreDustLevels` is exactly every 30 minutes with non-concurrent/misfire behavior;
- PostgreSQL fixture tests prove unique occurrence and delivery constraints;
- a multi-page PostgreSQL import proves all readings and the final watermark;
- a process-level test proves restart recovery from pending outbox rows;
- all shared publisher consumers compile and their focused tests await completion.

## Rollout

1. Add schema and persistence mappings with the dispatcher disabled.
2. Deploy code that can read/write the new tables while retaining the current synchronous path behind a temporary option.
3. Enable transactional import and outbox creation for a test customer/monitor.
4. Verify measurement counts, watermarks, one logical notification per transition, outbox drain, and no repeated alerts across multiple 30-minute runs.
5. Enable outbox delivery for the full MyATM customer.
6. Remove the temporary legacy-path option after one stable observation window.

Rollback disables new outbox creation/dispatch and restores the previous handler path without deleting occurrence or delivery rows. Schema removal is a separate later migration, never part of an emergency rollback.

## Acceptance Criteria

The remediation is complete when:

- every telemetry endpoint can retrieve more than 50 missing records without gaps;
- no page watermark advances without its readings and rule decisions;
- catalogue refresh cannot change offline state;
- all rules honor activity windows and nullable fields safely;
- a sustained threshold crossing creates one logical alert and one delivery per eligible destination;
- retries do not recreate the notification or resend destinations already marked delivered;
- Caution-to-Alert escalation still works;
- caller cancellation exits promptly and is not reported as an adapter failure;
- all MyATM database reads and writes are customer-scoped;
- the dust cron is every 30 minutes and the job imports every intervening sample;
- MyATM unit, architecture, scheduling, and PostgreSQL integration tests pass;
- the full solution builds with no new warnings; and
- `project_state.md` records the implemented schema, configuration, tests, and operational rollout state without secrets.
