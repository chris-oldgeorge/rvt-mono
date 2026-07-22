# MyATM Strict Review Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make MyATM imports complete, customer-isolated, cancellation-safe, and transactionally durable, with Omnidots-style anti-spam alert transitions and retryable per-destination delivery.

**Architecture:** The MyATM handler fetches keyset-paginated vendor pages and evaluates them in timestamp order. `IMyAtmDustImportCommands` atomically writes a page, its matching watermark, rule-state mutations, logical notification occurrences, and outbox rows. A dedicated outbox job performs external message and MQTT delivery after commit; all new persistence is MyATM-local while `IDBClient` remains a compatibility facade.

**Tech Stack:** .NET 10, ASP.NET Core shared host, EF Core, PostgreSQL-first mappings with SQL Server fixture compatibility, Quartz, Mapperly, MSTest, Moq.

## Global Constraints

- Work only in `/Users/oldgeorge/Documents/rvt-monitors/rvt-monitors/.worktrees/codex-myatm-strict-remediation` on `codex/myatm-strict-remediation`.
- Keep MyATM vendor parsing, field selection, rule state transitions, and outbox key construction manual and test-covered.
- Keep Mapperly app-local, analyzer-only, and use it only for simple DTO/entity copying.
- New application code uses narrow MyATM ports; `IDBClient` delegates only as a compatibility facade.
- PostgreSQL is primary; update the PostgreSQL fixture SQL and retain SQL Server mapping coverage.
- Do not track credentials, live connection strings, recipients, or vendor keys.
- Preserve the repository-wide monitor style: focused handlers, composition-root registration, async cancellation, and redacted operational logs.

---

## File Structure

- `myatmmonitor/MyAtmMonitor/api/http/MyAtmHttpGateway.cs`: paged vendor reads and cancellation propagation.
- `myatmmonitor/MyAtmMonitor/model/config/MyAtmMonitorOptions.cs`: page, suppression, and outbox settings with startup validation.
- `myatmmonitor/MyAtmMonitor/api/db/Queries/IMyAtmMonitorQueries.cs`: customer-scoped monitor reads.
- `myatmmonitor/MyAtmMonitor/api/db/Commands/IMyAtmDustImportCommands.cs`: atomic page-import command.
- `myatmmonitor/MyAtmMonitor/api/db/Commands/IMyAtmOutboxCommands.cs` and `Queries/IMyAtmOutboxQueries.cs`: outbox claim and completion ports.
- `myatmmonitor/MyAtmMonitor/api/db/EntityFramework/MyAtmEntities.cs` and `MyAtmMonitorContext.cs`: occurrence and outbox entities/mapping.
- `myatmmonitor/MyAtmMonitor/api/db/DBClient.cs`: EF transaction, scoped reads, and outbox persistence.
- `myatmmonitor/MyAtmMonitor/api/MyAtmRuleEvaluator.cs`: side-effect-free dust rule state machine.
- `myatmmonitor/MyAtmMonitor/api/MyAtmOutboxDispatcher.cs`: per-row delivery, lease, retry, and audit writes.
- `myatmmonitor/MyAtmMonitor/api/UseCases/StoreDustLevelsHandler.cs`: page loop, bounded catch-up, atomic commit, and immediate dispatch.
- `myatmmonitor/MyAtmMonitor/api/UseCases/DispatchMyAtmOutboxHandler.cs`: one-shot/Quartz dispatch slice.
- `myatmmonitor/MyAtmMonitor/api/MyAtmService.cs`, `MonitorJobDispatcher.cs`, `MonitorJobRunner.cs`, `IMyAtmMonitorJobs.cs`, and `appsettings.json`: job exposure and 30-minute schedule.
- `rvt-monitor-common/Rvt.Monitor.Common/Mqtt/MonitorEventPublisher.cs`: awaited publisher API used by the MyATM outbox path.
- `myatmmonitor/MyAtmMonitorTests/*`: red/green coverage for every behavior below.
- `myatmmonitor/MyAtmMonitorTests/testdata/create.postgres.sql` and `reset.postgres.sql`: fixture schema and cleanup.

## Task 1: Establish retrieval options and complete keyset pages

**Files:**
- Modify: `model/config/MyAtmMonitorOptions.cs`, `api/http/MyAtmHttpGateway.cs`, `api/http/HttpWebClient.cs`, `api/UseCases/StoreDustLevelsHandler.cs`
- Test: `MyAtmMonitorTests/TestMyAtmApi.cs`, `MyAtmMonitorTests/HttpWebClientTests.cs`

**Interfaces:**
- Produce: `Task<MyAtmMeasurementPage<T>> HttpGetDeviceMeasurementPageAsync<T>(int customerId, string serialId, DateTime cursor, Period period, CancellationToken cancellationToken)`.
- Produce: `sealed record MyAtmMeasurementPage<T>(IReadOnlyList<T> Measurements, DateTime? NextCursor, bool HasMore)`.

- [ ] Write failing gateway tests asserting every period URL has `$filter=timestamp gt`, `$orderby=timestamp asc`, and `$top=<MeasurementPageSize>`; a full page has `HasMore=true` and advances from its maximum timestamp.
- [ ] Run `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-restore --filter "FullyQualifiedName~TestMyAtmApi|FullyQualifiedName~HttpWebClientTests" --nologo`; verify the new assertions fail against the unpaged URLs.
- [ ] Add positive `MeasurementPageSize`, `AccessoryPageSize`, and `MaxPagesPerMonitorPerRun` settings (defaults 1000, 1000, 10) and validate each in `MyAtmMonitorOptions.Validate`.
- [ ] Build the OData query with invariant UTC `O` timestamps, URL escaping, ascending order, and explicit top for minute, average, and accessory routes. Normalize, deduplicate, sort, and derive the cursor from the returned maximum.
- [ ] Extend `MyAtmRequestPolicy`/`HttpWebClient` tests for 408/5xx retry and caller cancellation; preserve `Retry-After` precedence for 429.
- [ ] Re-run the focused test filter; expected result: all selected tests pass.
- [ ] Commit: `feat: page MyATM telemetry imports`.

## Task 2: Preserve local state and enforce customer scope

**Files:**
- Modify: `api/db/Mapping/MyAtmDbMapper.cs`, `api/db/Queries/IMyAtmMonitorQueries.cs`, `api/db/DBClient.cs`, `api/MyAtmMonitorReader.cs`, `api/UseCases/ProcessDustLevelsHandler.cs`
- Test: `MyAtmMonitorTests/Mapping/MyAtmDbMapperTests.cs`, `TestMyAtmApiMonitors.cs`, `TestDbClient.cs`, `Architecture/MyAtmDependencyBoundaryTests.cs`

**Interfaces:**
- Change: `List<DustMonitorDto> ReadMonitorList(int customerId, DateTime? lastDataTime)`.
- Change: `DustMonitorDto? ReadMonitor(int customerId, string serialId)`.

- [ ] Write failing mapper tests that update an existing monitor with vendor `Offline=false` and assert persisted `Offline=true` remains true.
- [ ] Write failing query tests with two customer IDs and assert that customer A cannot return or mutate customer B’s monitor.
- [ ] Run the focused mapper/query tests; verify the offline and customer assertions fail.
- [ ] Add Mapperly target/source ignores for `Offline` and all runtime-owned monitor fields; set only new-entity defaults in `ToMonitorEntity`.
- [ ] Require customer ID in the narrow query interface and apply the customer predicate before EF materialization; forward it from `MyAtmMonitorReader` and serial-specific paths.
- [ ] Update compatibility calls and architecture tests so no MyATM handler calls the former unscoped read.
- [ ] Re-run the focused tests; expected result: pass.
- [ ] Commit: `fix: preserve MyATM monitor runtime state`.

## Task 3: Make dust rules a pure timestamp-ordered state machine

**Files:**
- Create: `api/MyAtmRuleEvaluator.cs`, `model/dto/MyAtmDustImportCommit.cs`
- Modify: `model/dto/DustDto.cs`, `api/MyAtmRuleProcessor.cs`
- Test: `MyAtmMonitorTests/TestRules.cs`, `MyAtmMonitorTests/TestRules2.cs`

**Interfaces:**
- Produce: `MyAtmRuleEvaluation Evaluate(DustMonitorDto monitor, Period period, IReadOnlyList<RvtAlertRuleDto> rules, IReadOnlyList<DustDto> samples, DateTime utcNow)`.
- Produce: `RuleStateMutation(Guid RuleId, bool ExpectedIsActive, DateTime? ExpectedAccessed, bool IsActive, DateTime? Accessed)`.
- Produce: `AlertOccurrenceProposal(string Key, Guid MonitorId, Guid RuleId, Period Period, AlertType AlertType, string Field, double Level, DateTime TriggeredAt, IReadOnlyList<RvtContactDto> Contacts)`.

- [ ] Write failing tests for activity-window exclusion using `ReadRules(serial, period)`, missing averaged fields not clearing an active rule, repeated above-limit samples creating one proposal, `LimitOff` rearming, Alert suppressing Caution, and Caution-to-Alert escalation.
- [ ] Run only those tests; verify each fails before evaluator implementation.
- [ ] Change `DustDto` averaged mapping from null-forgiving access to null-propagating values.
- [ ] Implement the evaluator over samples ordered by `SampleTime`: skip missing fields; skip activity-window-excluded samples; deactivate deleted/low rules; mutate inactive-to-active rules once; retain Alert-before-Caution semantics; create proposals without external I/O.
- [ ] Reduce `MyAtmRuleProcessor` to compatibility forwarding only, or remove it if no callers remain; it must no longer send contacts or publish MQTT during import.
- [ ] Re-run rule tests; expected result: pass.
- [ ] Commit: `refactor: make MyATM dust rules deterministic`.

## Task 4: Add atomic import, alert occurrence, and outbox persistence

**Files:**
- Create: `api/db/Commands/IMyAtmDustImportCommands.cs`, `api/db/Commands/IMyAtmOutboxCommands.cs`, `api/db/Queries/IMyAtmOutboxQueries.cs`
- Modify: `api/db/IDBClient.cs`, `api/db/DBClient.cs`, `api/db/EntityFramework/MyAtmEntities.cs`, `api/db/EntityFramework/MyAtmMonitorContext.cs`, `MyAtmMonitorDbOptions.cs`
- Test: `MyAtmMonitorTests/EntityFramework/MyAtmModelMappingTests.cs`, `TestDbClient.cs`, `testdata/create.postgres.sql`, `testdata/reset.postgres.sql`

**Interfaces:**
- Produce: `Task<DustImportCommitResult> CommitDustImportAsync(MyAtmDustImportCommit commit, CancellationToken cancellationToken)`.
- Produce: `Task<IReadOnlyList<MyAtmOutboxMessageDto>> ClaimDueOutboxAsync(int take, DateTime utcNow, TimeSpan lease, CancellationToken cancellationToken)`.
- Produce: `Task CompleteOutboxAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken)`, `Task RetryOutboxAsync(Guid id, DateTime nextAttempt, string error, bool deadLetter, CancellationToken cancellationToken)`.

- [ ] Write failing mapping and fixture tests for `my_atm_alert_occurrence` and `my_atm_outbox_message`, including unique occurrence/delivery keys and a `(status, next_attempt_at)` index.
- [ ] Write a failing PostgreSQL fixture test: a commit that throws after adding a notification/outbox row leaves no readings, rule changes, or watermark; a successful replay creates exactly one notification and each delivery key once.
- [ ] Run the focused fixture tests with `RVT__POSTGRES_INTEGRATION_CONNECTION`; verify the new cases fail before the schema/command exists.
- [ ] Add local entities: occurrence key, notification ID, monitor/rule/period/type/field/triggered time/suppression state; and outbox kind/channel/destination/hash/payload/status/attempt/lease/timestamps/error.
- [ ] Map PostgreSQL snake_case and SQL Server names, update both fixture SQL scripts, and expose only the three narrow ports through `IDBClient`.
- [ ] In one EF transaction: deduplicate readings; compare expected watermark/rule state; persist mutations; derive deterministic notification ID from the occurrence key; apply the 30-minute recent-notification guard without blocking Caution-to-Alert; persist occurrences, notifications, and per-destination MQTT/contact rows; then advance watermark.
- [ ] Re-run mapping and fixture tests; expected result: pass.
- [ ] Commit: `feat: persist MyATM alert outbox atomically`.

## Task 5: Dispatch deliveries after commit without fire-and-forget MQTT

**Files:**
- Create: `api/MyAtmOutboxDispatcher.cs`, `api/UseCases/DispatchMyAtmOutboxHandler.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Mqtt/MonitorEventPublisher.cs`, `api/MyAtmMonitorServices.cs`, `api/MyAtmApi.cs`, `api/MyAtmService.cs`, `api/IMyAtmMonitorJobs.cs`, `api/MonitorJobDispatcher.cs`, `api/MonitorJobRunner.cs`
- Test: `MyAtmMonitorTests/TestMyAtmApi.cs`, new `MyAtmMonitorTests/MyAtmOutboxDispatcherTests.cs`, `rvt-monitor-common/Rvt.Monitor.CommonTests/Rules/NoiseRuleEvaluatorTests.cs`

**Interfaces:**
- Produce: `Task DispatchAsync(int take, CancellationToken cancellationToken)`.
- Change: `Task PublishDataInsertedAsync(DateTime timestamp, string serialId, int? customerId, CancellationToken cancellationToken)` and `Task PublishAlertAsync(DateTime timestamp, string serialId, string message, int? customerId, CancellationToken cancellationToken)`.
- Produce: `Task DispatchMyAtmOutboxAsync(CancellationToken cancellationToken = default)` on `IMyAtmMonitorJobs`.

- [ ] Write failing dispatcher tests showing a successful email/SMS/MQTT delivery is completed once, a failed destination is retried without re-sending completed siblings, expired leases are reclaimable, and cancellation does not write an operational error.
- [ ] Run the dispatcher tests; verify they fail before dispatch implementation.
- [ ] Make the shared MQTT publisher await the underlying `IMqttClient.PublishAsync`; retain obsolete synchronous compatibility methods only for untouched monitor paths and migrate MyATM to the awaited API.
- [ ] Implement atomic leasing, exponential retry from 30 seconds capped at 30 minutes, eight-attempt dead-lettering, redacted audit messages, and a two-minute lease for batches of 50.
- [ ] Register dispatcher/handler in the composition root. Immediately dispatch the just-committed page’s deliveries and expose `DispatchMyAtmOutbox` as one-shot/Quartz work.
- [ ] Re-run dispatcher and shared publisher tests; expected result: pass.
- [ ] Commit: `feat: dispatch MyATM alert outbox`.

## Task 6: Integrate bounded imports, cancellation, and scheduling

**Files:**
- Modify: `api/UseCases/StoreDustLevelsHandler.cs`, `api/http/MyAtmHttpGateway.cs`, `api/MyAtmMonitorReader.cs`, `appsettings.json`, `README.md`
- Test: `TestMyAtmApi.cs`, `TestRules.cs`, `TestMyAtmApiExceptions.cs`, `TestMonitorJobScheduling.cs`, `MyAtmOperationalConfigurationTests.cs`

**Interfaces:**
- Consume: `CommitDustImportAsync`, `Evaluate`, and `DispatchAsync` from Tasks 3–5.
- Produce: `RunAsync<T>` that retries a concurrency conflict once, pages until final page or configured budget, and propagates cancellation.

- [ ] Write failing handler tests for a two-page response importing both pages and the final watermark, failure after rule proposal leaving the initial watermark, configured page-budget resumability, and `OperationCanceledException` escaping without `HandleException` or an aggregate.
- [ ] Write a failing schedule test asserting `StoreDustLevels` has `Cron == "0 0/30 * * * ?"`, and `DispatchMyAtmOutbox` has a one-minute cron and do-nothing misfire behavior.
- [ ] Run the selected tests; verify they fail against the one-page, pre-rule watermark flow and minute dust schedule.
- [ ] Replace the insert/watermark/rule/MQTT sequence with page fetch → pure evaluation → atomic commit → awaited bounded dispatch. Catch only non-cancellation monitor failures; rethrow caller cancellation before recording errors.
- [ ] Change the dust schedule to every 30 minutes; add the dispatcher schedule and documentation stating watermarks catch up all missing one-minute samples.
- [ ] Re-run handler, exception, and scheduling tests; expected result: pass.
- [ ] Commit: `fix: make MyATM dust imports durable`.

## Task 7: Full verification, operational documentation, and handoff

**Files:**
- Modify: `project_state.md`, `docs/superpowers/specs/2026-07-14-myatm-strict-review-remediation-design.md` only if implementation decisions differ materially
- Verify: `myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj`, `rvt-monitors.sln`

- [ ] Run `git diff --check`; expected result: no output and exit 0.
- [ ] Run `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-restore --nologo` with the local PostgreSQL integration connection; expected result: 0 failed tests.
- [ ] Run `dotnet build rvt-monitors.sln --no-restore --nologo`; expected result: 0 warnings and 0 errors.
- [ ] Run focused common/other-monitor tests affected by the MQTT publisher contract; expected result: 0 failures.
- [ ] Update `project_state.md` with actual schema, settings, test counts, branch, and deployment caveats without secrets.
- [ ] Commit: `docs: record MyATM remediation verification`.
- [ ] Push `codex/myatm-strict-remediation` and present verified integration options.
