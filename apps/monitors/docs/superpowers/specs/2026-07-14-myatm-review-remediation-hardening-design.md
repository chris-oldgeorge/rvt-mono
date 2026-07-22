# MyATM Review Remediation Hardening Design

**Status:** Approved on 2026-07-14

**Scope:** Alert suppression, outbox lease fencing, durable aggregate/offline alerts, monitor-read failure semantics, accessory pagination, and dead-letter observability

**Builds on:** `docs/superpowers/specs/2026-07-14-myatm-strict-review-remediation-design.md`

## Outcome

MyATM will retain its PostgreSQL-first EF Core architecture, narrow data-access ports, 30-minute dust polling, and transactional per-page dust imports. This hardening increment closes six reliability gaps found after the initial durable outbox implementation:

1. backfilled and same-commit alert transitions obey one event-time anti-spam policy;
2. outbox workers cannot complete or retry a lease owned by another worker;
3. eight-hour aggregate and offline alerts use the durable occurrence/outbox path;
4. database monitor-read failures fault scheduled work instead of returning success;
5. accessory imports consume bounded keyset pages rather than one page per day; and
6. final delivery failures produce durable audit, operational, and job-failure signals.

The delivery contract remains at-least-once. Stable occurrence and delivery identities prevent routine poll/retry duplication, while fenced leases prevent stale workers from corrupting current ownership. The design does not claim cross-system exactly-once delivery because a provider can accept a message immediately before the process stops and before the database completion is recorded.

## Constraints

- Keep `IDBClient` as a compatibility facade; new scheduled behavior uses narrow MyATM interfaces.
- Keep Mapperly app-local and analyzer-only. Rule transitions, occurrence identity, destination selection, and lease logic remain manual and test-covered.
- Keep PostgreSQL as the primary integration target and preserve SQL Server runtime compatibility.
- Keep the existing 30-minute `StoreDustLevels` schedule and one-minute `DispatchOutbox` schedule.
- Preserve existing external behavior: normal and aggregate alerts retain email/SMS and MQTT alert delivery; offline alerts retain portal notification and email/SMS delivery without adding a new MQTT event.
- Do not persist credentials or write unredacted contact destinations to normal logs.
- Use event time for rule, suppression, and contact-window decisions; use UTC processing time for lease, retry, and operational timestamps.

## Architecture and Boundaries

### Shared alert transition evaluator

Add a pure `MyAtmAlertTransitionEvaluator` that decides one rule transition from its current state, a timestamped value, and the rule activity window. It owns:

- inactive-to-active crossing at `LimitOn`;
- active-to-inactive rearming at or below `LimitOff`;
- deleted-rule deactivation;
- missing-value behavior;
- activity-window behavior; and
- Alert-before-Caution precedence.

`MyAtmRuleEvaluator` continues to coordinate ordered page samples and uses the transition evaluator. The eight-hour aggregate handler uses the same transition evaluator for each completed period. This removes divergent normal/aggregate implementations without moving business policy into `DBClient`.

### Narrow persistence ports

Add `IMyAtmAlertCommitCommands` for state transitions that do not include a dust page. Its command model supports:

- expected/new rule state for aggregate alerts;
- expected/new monitor `Offline` state for offline alerts;
- stable logical occurrences;
- portal notification data;
- immutable per-destination outbox payloads; and
- channel selection that preserves each workflow's current behavior.

`IMyAtmDustImportCommands` remains the atomic dust-page command and shares the same occurrence, suppression, notification, and outbox persistence helpers. `IDBClient` implements both narrow ports only as the compatibility facade.

Add a focused `IMyAtmAccessoryCommands` interface with this batch operation:

```csharp
Task InsertAccessoryPageAsync(
    IReadOnlyList<AccessoryInfoDto> page,
    CancellationToken cancellationToken = default);
```

The command commits one normalized, deduplicated accessory page atomically. Existing per-row methods remain only on the compatibility facade until their callers are removed.

### Scheduled handlers

Scheduled handlers become coordinators:

- dust: page fetch -> ordered evaluation -> atomic page commit -> bounded immediate dispatch;
- aggregate: period query -> transition evaluation -> atomic alert commit;
- offline: stale-state evaluation -> atomic offline/alert commit; and
- accessory: page fetch -> atomic page insert -> next page.

`MyAtmRuleProcessor` no longer performs email, SMS, MQTT, notification, or audit side effects from scheduled paths. Its compatibility methods may remain temporarily for unsupported historical callers, but architecture tests prevent scheduled handlers from depending on those methods.

## Event-Time Anti-Spam Policy

### Scope and ordering

Recent suppression applies to the Alert/Caution family and is scoped by:

- monitor ID;
- normalized alert field;
- averaging period; and
- triggering event time.

For a candidate at event time `T`, the command considers accepted, unsuppressed Alert/Caution occurrences in `[T - AlertSuppressionWindow, T]`. It combines persisted rows with accepted candidates earlier in the current transaction. A database query alone is insufficient because EF queries do not include newly added, unsaved notifications.

### Severity behavior

- A recent Alert suppresses a new Alert.
- A recent Alert suppresses a later Caution downgrade.
- A recent Caution suppresses another Caution.
- A recent Caution does not suppress escalation to Alert.
- A transition outside the suppression window may notify after the rule has rearmed below `LimitOff`.

Suppressed transitions are persisted as alert occurrences with `IsSuppressed=true`, so replay and audit remain deterministic. They update rule state consistently but create no shared notification row and no outbox deliveries.

### Backfill behavior

Suppression compares event timestamps with event timestamps, never `NotificationTime` with wall-clock processing time. Historical samples one minute apart therefore receive the same anti-spam behavior whether imported live, in one page, or across multiple backfill pages.

Add an occurrence lookup index covering monitor, field, period, triggering timestamp, severity, and suppression state. Destination or contact data is not part of the index.

## One-at-a-Time Fenced Outbox Claims

### Claim contract

Replace batch leasing with a repeated one-row claim operation:

```csharp
Task<MyAtmOutboxMessageDto?> ClaimNextDueOutboxAsync(
    DateTime utcNow,
    TimeSpan lease,
    CancellationToken cancellationToken = default);
```

Each successful claim:

1. reads the oldest Pending-due or expired-Leased candidate;
2. generates a new random `LeaseId`;
3. conditionally updates only that due candidate with `Status=Leased`, the new lease ID, `LeaseUntil`, and incremented attempt count; and
4. returns the claimed snapshot only when exactly one row was updated.

The provider-neutral implementation uses EF Core conditional `ExecuteUpdateAsync`. If another process wins the candidate, the claimant repeats selection up to three times before returning no claim for that pass. It does not use PostgreSQL-only `SKIP LOCKED` or SQL Server-only locking hints.

### Completion and retry contracts

Commands require both message and lease identity and return ownership success:

```csharp
Task<bool> CompleteOutboxAsync(
    Guid id,
    Guid leaseId,
    DateTime completedAt,
    MyAtmDeliveryAudit? audit,
    CancellationToken cancellationToken = default);

Task<bool> RetryOutboxAsync(
    Guid id,
    Guid leaseId,
    DateTime nextAttemptAt,
    string error,
    bool deadLetter,
    MyAtmDeliveryAudit? finalAudit,
    CancellationToken cancellationToken = default);
```

Both update only a row whose status is Leased and whose `LeaseId` matches. A stale worker receives `false`, emits an ownership-loss warning, and performs no further state mutation. It must not convert a completed or newly leased row back to Pending.

### Dispatcher loop

`DispatchDueAsync` loops at most `OutboxBatchSize` times, but each iteration claims one row immediately before delivery and reaches a database outcome before the next claim. This removes pre-expired tail rows from large batches.

Outbound delivery receives a linked cancellation token with `OutboxDeliveryTimeoutSeconds`. Startup validation requires:

```text
0 < OutboxDeliveryTimeoutSeconds < OutboxLeaseSeconds
```

The defaults are 90 seconds for delivery and 120 seconds for the lease. Retry delay remains exponential from 30 seconds, capped at 30 minutes, with the existing eight-attempt limit.

### Awaited communications

Add awaited, cancellation-aware email and SMS methods to the shared communication abstraction while retaining synchronous compatibility wrappers for untouched monitors. MyATM outbox delivery uses only the awaited methods. Provider clients use bounded HTTP timeouts no greater than `OutboxDeliveryTimeoutSeconds`.

MQTT remains awaited. A provider correlation/idempotency value uses the stable delivery key where the provider supports metadata, but provider support is not assumed.

## Audit and Dead-Letter Semantics

The dispatcher performs the external send and then invokes the fenced database command:

- Email/SMS success atomically marks the row Completed and inserts one success audit.
- MQTT success marks the row Completed without a contact audit.
- Intermediate failure returns the owned row to Pending with its next attempt and last error.
- Final failure atomically marks the row DeadLetter and inserts one failure audit when the row represents email or SMS.

An audit write is no longer a separate failure point between external delivery and outbox completion. The unavoidable provider-accepted/database-not-completed crash window remains documented as at-least-once.

Every newly dead-lettered row produces:

- a structured error log containing message ID, kind, attempt count, and a redacted destination;
- a best-effort MyATM operational error record; and
- a failed dispatcher job result after the bounded pass completes.

The dispatcher continues processing other claimed messages before reporting the aggregate dead-letter failure. Previously dead-lettered rows are not reclaimed and do not repeatedly fail later runs. Existing common job telemetry therefore records the failed pass without introducing a MyATM-only metrics subsystem.

## Durable Aggregate and Offline Alerts

### Eight-hour aggregate path

For each completed aggregate period, the handler evaluates the rule and submits one `MyAtmAlertCommit` containing expected/new rule state and any candidate occurrence. The database transaction conditionally updates the rule, applies recent suppression, inserts the occurrence/notification, and enqueues the same email/SMS/MQTT alert behavior currently produced by `MyAtmRuleProcessor`.

If the expected rule state changed concurrently, the command returns a concurrency result or throws `DbUpdateConcurrencyException`; no partial notification or delivery is committed. The handler reloads/re-evaluates according to the existing bounded concurrency policy.

### Offline path

The offline handler submits expected/current monitor state and the offline rule occurrence. The transaction conditionally changes `monitor.Offline`, writes the logical notification, and enqueues eligible email/SMS destinations. It does not add an offline MQTT event.

Online recovery commits only the expected `Offline=true` to `false` state transition and creates no alert occurrence. Repeated offline checks see the persisted flag and remain silent.

## Monitor-Read Failure Semantics

`MyAtmMonitorReader` no longer returns null after a query exception. It:

1. logs the original exception with customer/query context;
2. attempts the existing database operational-error write as best effort;
3. suppresses any secondary operational-write exception after logging it; and
4. rethrows the original exception with its stack intact.

Dust and accessory handlers therefore fail before their fleet loop when the monitor query fails. One-shot execution returns nonzero and Quartz records a failed job. Caller-requested cancellation remains exempt from operational error recording and propagates immediately.

Per-monitor vendor or page failures remain isolated: the handler records the failure once, continues other monitors where safe, and throws one aggregate at the end.

## Complete Accessory Pagination

Accessory retrieval calls `HttpGetAccessoryInfoPageAsync` directly and uses the existing keyset contract:

- `timestamp gt <UTC cursor>`;
- ascending timestamp order;
- configured page size; and
- `HasMore` based on a full vendor page.

For each monitor, the initial cursor is the latest persisted accessory timestamp. Each normalized page is deduplicated and committed as one batch. The local cursor advances only after the batch succeeds. The loop stops on a final page, a missing/nonadvancing cursor, or `MaxPagesPerMonitorPerRun`.

Reaching the page budget is a successful partial catch-up. The next daily run resumes from persisted data. A failed page leaves no partial rows from that page and leaves the durable cursor at the last successful page. Per-monitor failures are aggregated while later monitors continue.

## Schema and Migration

Create additive PostgreSQL and SQL Server forward and rollback scripts for this hardening increment.

Forward changes:

- nullable `lease_id`/`LeaseId` on the outbox table;
- EF mapping and DTO support for `LeaseId`;
- recent-occurrence lookup index; and
- fixture schema/reset updates.

The lease column remains nullable for compatibility with rows created by the previous application. Pending legacy rows receive a lease ID on their first new claim. No payload rewrite is required.

Rollback order is application first, schema second. The rollback removes the new index and lease column only after the previous application is active. Scripts are idempotent and limited to MyATM-owned schema objects.

## Testing Strategy

### Pure/unit tests

- historical same-severity retriggers use event-time suppression;
- same-commit oscillations see earlier accepted candidates;
- Caution-to-Alert escalation is allowed;
- Alert-to-Caution downgrade is suppressed within the window;
- deleted, missing-value, and activity-window decisions remain stable;
- dispatcher claims one row per iteration;
- every claim receives a new lease ID;
- stale completion/retry returns false and does not mutate state;
- delivery timeout is less than lease duration;
- dead-letter transitions emit one operational signal and fail the pass;
- aggregate/offline handlers call the atomic alert command and perform no direct send;
- monitor-reader failures rethrow the original exception even if operational recording fails; and
- accessory import covers two pages, page budget, nonadvancing cursors, rollback, and per-monitor failure aggregation.

### PostgreSQL integration tests

- concurrent claimers cannot own the same row;
- an expired lease can be reclaimed with a different lease ID;
- the old lease cannot complete or retry the reclaimed row;
- success and final failure audits are atomic with the fenced transition;
- replay preserves one logical occurrence and delivery key;
- historical backfill suppression works across separate commits;
- aggregate/offline state, occurrence, notification, and outbox writes roll back together; and
- accessory page inserts are deduplicated and atomic.

### SQL Server and architecture coverage

- provider-specific names and nullable lease mapping are correct;
- forward/rollback scripts target the current SQL Server table names;
- scheduled handlers depend on narrow interfaces;
- MyATM scheduled paths do not call direct `IMessageService`, `IMonitorEventPublisher`, notification writes, or broad concrete database fields; and
- `IDBClient` remains the only compatibility facade.

## Rollout and Verification

1. Apply the additive forward migration before deploying application code.
2. Deploy the fenced dispatcher and migrated scheduled alert paths together.
3. Run one-shot smoke checks for dust, aggregate, offline, accessory, and outbox jobs using non-production recipients or mocked delivery infrastructure.
4. Observe pending/retry/dead-letter logs and common job-failure telemetry for at least one full scheduling cycle.
5. Verify no sustained outbox backlog and no repeated logical occurrence/delivery keys.
6. Remove or obsolete unused direct-alert compatibility methods in a later cleanup change after caller analysis; this is not required for the initial hardening deployment.

Required completion gates:

- complete MyATM PostgreSQL-backed test suite passes;
- affected shared communication tests pass;
- solution build succeeds with zero warnings and errors;
- `git diff --check` passes;
- forward and rollback scripts receive a manual identifier/table-name review; and
- no credential, destination, or live connection string is tracked.
