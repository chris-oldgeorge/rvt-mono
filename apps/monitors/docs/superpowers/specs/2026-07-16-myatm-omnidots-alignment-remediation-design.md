# MyATM Omnidots-Alignment Remediation Design

**Status:** Approved on 2026-07-16

**Base:** `main` at `71a99c9`, after the shared durable-delivery, Omnidots, and Svantek reliability merges

**Scope:** MyATM UTC correctness, offline site-hours behavior, vendor-boundary resilience, fleet failure isolation, scheduled composition, and regression coverage

## Outcome

MyATM will retain its existing 30-minute dust polling cadence and shared durable outbox while adopting the current Omnidots reliability patterns. Scheduled work will be composed from focused handlers and narrow ports, timestamps will be UTC at every vendor and persistence boundary, offline decisions will count only configured site operating time, vendor requests and pagination will remain bounded, and independent monitor/device failures will be reported without preventing useful fleet progress.

The implementation remains EF Core-backed and PostgreSQL-first while preserving SQL Server compatibility. `IDBClient` and `MyAtmApi` remain compatibility facades only. No new database table is required: existing monitor, deployment, contract, site, notification, alert-occurrence, and shared outbox storage is sufficient.

## Findings Covered

1. Dust ingestion currently attempts up to 50 external deliveries after every committed page, allowing notification latency to consume or exceed the 30-minute polling interval.
2. Offline detection measures wall-clock elapsed time instead of active site time, creating night, weekend, and DST false positives.
3. Operational error recording can mask the original error and abort later fleet work.
4. Catalogue pagination has no total page bound or repeated-page detection, and one device-detail failure aborts later devices/pages.
5. Vendor and monitor options are assembled manually and may fail after startup.
6. HTTP responses, error bodies, and server-provided retry delays are insufficiently bounded.
7. Direct `ToUniversalTime()` calls can reinterpret SQL Server `DateTimeKind.Unspecified` values through the host timezone.
8. Scheduled production work still routes through the broad `MyAtmApi`/`IDBClient` compatibility surface.
9. The repository Mapperly architecture assertion relies on an exact project allow-list and breaks whenever another valid monitor app adopts Mapperly.

## Architecture

`MyAtmService` will depend on focused use-case handlers, `MyAtmMonitorOptions`, and `MonitorDeliveryDispatcher`. Each scheduled method invokes one handler directly. `MyAtmApi` stays available for legacy callers and tests but is not in the scheduled production path.

The composition root will register:

- validated typed MyATM monitor and vendor options;
- `TimeProvider.System`, replaceable by tests;
- the shared, paced MyATM request policy and vendor gateway;
- focused handlers and readers;
- narrow monitor, measurement, rule, site-schedule, operational, alert-commit, and outbox ports;
- the existing shared durable-delivery dispatcher.

`IDBClient` remains the implementation-compatible facade registered behind those narrow interfaces. New handler code cannot acquire `IDBClient` directly.

## UTC Invariant

Every instant entering or leaving MyATM application logic must be UTC. A single shared normalization operation provides these exact semantics:

```text
Utc         -> unchanged
Local       -> converted to UTC
Unspecified -> identical ticks marked as UTC
```

The Unspecified rule is deliberate: SQL Server `datetime2` does not preserve `DateTime.Kind`, while the stored monitor schema treats these values as UTC. Calling `ToUniversalTime()` directly on such a value would incorrectly apply the machine timezone.

The UTC operation is used immediately after vendor deserialization and database reads, and immediately before database writes, vendor cursor formatting, comparisons, grouping, deduplication, occurrence-key construction, notification/outbox creation, and site-duration calculation. UTC-only domain components reject non-UTC input rather than guessing.

PostgreSQL `timestamptz` values remain UTC. SQL Server values preserve their ticks and regain `DateTimeKind.Utc` at the repository boundary. Tests fence all three input kinds and both provider read contracts.

## Site-Hours-Aware Offline Evaluation

Offline behavior will match the Omnidots calculator and failure policy:

1. Capture one `utcNow` from `TimeProvider` for the run.
2. Normalize each monitor's last one-minute reading to UTC.
3. Resolve the monitor's configured `TimeZoneInfo`.
4. Read the active deployment's contract/site weekday, Saturday, and Sunday hours through a narrow site-schedule query.
5. Calculate the intersection of `[lastReadingUtc, utcNow]` with each configured local operating interval.
6. Mark the monitor offline only when accumulated active duration is greater than the offline rule period.

The calculator supports closed days and schedules crossing midnight. It converts local schedule boundaries to UTC before calculating elapsed duration, so spring-forward removes the missing hour and fall-back counts the repeated hour. A boundary inside an invalid spring-forward gap or ambiguous fall-back overlap is rejected as a site configuration failure instead of choosing an arbitrary offset.

Missing/invalid monitor timezone or invalid site schedule affects only that monitor. The failure is recorded best-effort, the monitor state is not changed, and later monitors continue. A valid recent reading or insufficient accumulated active time recovers a previously offline monitor. Existing occurrence identity, suppression, Caution-to-Alert escalation, atomic notification/outbox persistence, and delivery semantics remain unchanged.

## Fleet Failure Contract

All scheduled handlers use the same failure rules:

- caller-requested cancellation propagates immediately and is never recorded as an operational failure;
- independent monitor/device failures are wrapped with a safe identifier and collected;
- operational recording is attempted once per primary failure;
- a recording failure is attached as secondary diagnostic context but never replaces the primary failure;
- later independent units continue;
- an immutable aggregate job exception is thrown after useful work completes;
- setup failures before an independent loop begins propagate directly.

This contract applies to catalogue, dust, accessory, aggregate-processing, and offline workflows at their natural independent-unit boundary.

## Catalogue Pagination

Catalogue retrieval keeps the configured page size and gains a validated maximum page count. Each successful page is fingerprinted from stable device identifiers. A repeated full-page fingerprint, a page that makes no offset progress, or a still-full response at the page cap fails the job rather than looping forever or silently declaring completeness.

Device-detail calls are isolated per device. Successful device DTOs from a page are persisted even when another device fails. Later devices and pages continue where the page-list contract remains trustworthy. Detail failures are included in the final aggregate. A list-page failure stops further catalogue pagination because the continuation point is unknown, then faults the job with the original failure.

## Dust Ingestion and Delivery Decoupling

Dust page commits continue to atomically persist measurements, watermarks, rule state, logical occurrences/notifications, and shared delivery requests. `StoreDustLevelsHandler` will no longer call the dispatcher after a page commit.

The existing `DispatchOutbox` Quartz job remains the only scheduled external-delivery path and runs once per minute. This prevents vendor ingestion time from depending on email, SMS, or MQTT latency while preserving durable retry, lease, dead-letter, suppression, escalation, and at-least-once delivery behavior.

## Configuration and HTTP Boundary

MyATM configuration will use the .NET options pipeline with `IValidateOptions<T>` and `ValidateOnStart`. Validation covers customer ID, device and measurement page sizes, all page budgets, API key, absolute vendor base URL, response-size limit, pacing interval, retry attempts, fallback delay, and maximum honored retry delay. Secrets are supplied only through normal runtime configuration and are never logged or persisted.

The HTTP client will:

- request headers-first completion;
- pace every MyATM endpoint through the same shared policy;
- retry only 408, 429, and transient 5xx responses;
- honor `Retry-After` only up to the configured maximum;
- use bounded exponential fallback delay with jitter;
- enforce a maximum success-body size before JSON deserialization;
- avoid reading or logging arbitrary vendor error bodies;
- preserve safe endpoint/status context;
- dispose requests/responses and propagate cancellation.

The 30-minute `StoreDustLevels` and one-minute `DispatchOutbox` schedules remain unchanged.

## Mapperly and Dependency Boundaries

The Mapperly architecture test will express repository rules rather than enumerate today’s monitor projects. Any project referencing `Riok.Mapperly` must:

- be a monitor application project, not a test or shared-common project;
- use `PrivateAssets="all"`;
- use `OutputItemType="Analyzer"`;
- keep Mapperly out of `rvt-monitor-common`.

Focused architecture tests will also ensure scheduled service composition does not regress to direct `MyAtmApi` or `IDBClient` dependencies and dust ingestion does not invoke delivery.

## Test Strategy

Implementation is test-first. Coverage includes:

- UTC normalization for UTC, Local, and Unspecified inputs without tick drift;
- vendor cursor/read/write normalization and stable occurrence/deduplication keys;
- weekday, weekend, closed-day, overnight, spring-forward, fall-back, and invalid/ambiguous schedule boundaries;
- offline transition, recovery, suppression/escalation preservation, missing timezone, invalid schedule, per-monitor continuation, and aggregate failure;
- catalogue page cap, repeated pages, no progress, per-device detail failures, persistence of successful devices, and cancellation;
- no immediate dispatcher invocation from dust ingestion;
- startup option validation, capped `Retry-After`, response-size enforcement, error-body privacy, retry classifications, pacing, and cancellation;
- DI graph resolution, scheduled dispatcher parity, compatibility-facade isolation, and rule-based Mapperly constraints;
- PostgreSQL integration coverage for active deployment to contract/site schedule retrieval and UTC persistence;
- SQL Server mapping/static contract coverage for tick-preserving UTC recovery.

The release gate runs focused tests after each slice, the complete MyATM suite with the runtime-only integration connection, affected common tests, MyATM and root solution builds, formatter verification, `git diff --check`, and a secret/configuration leakage review.

## Documentation and Consistency

MyATM operational documentation will describe its UTC-only boundary, validated vendor settings, catalogue limits, site-hours offline semantics, and independent delivery schedule. `project_state.md` will record the completed work without credentials or connection values.

Folder-wide consistency remains mandatory: monitor subprojects should use the shared host, focused handlers, narrow ports, async/cancellation conventions, PostgreSQL-first EF Core persistence, app-local Mapperly, focused tests, and current deployment documentation. Vendor-specific deviations must be documented at the boundary where they are required.

## Non-Goals

- Replacing the existing shared durable-delivery implementation or changing its schema.
- Removing `MyAtmApi` or `IDBClient` compatibility facades in this remediation.
- Changing alert thresholds, suppression duration, escalation rules, recipient selection, or delivery guarantees.
- Changing the approved polling schedules.
- Persisting live credentials, connection strings, destinations, or vendor response bodies.
