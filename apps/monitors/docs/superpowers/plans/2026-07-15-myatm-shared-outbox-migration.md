# MyATM Shared Outbox Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move MyATM from `my_atm_outbox_message` to the common delivery outbox and dispatcher without losing pending work or changing alert-occurrence, suppression, escalation, fencing, scheduling, or rollback behavior.

**Architecture:** Keep `my_atm_alert_occurrence` and MyATM atomic commit commands app-local. Backfill the shared table during a paused scheduler cutover, then make `DBClient` implement the common outbox ports over the inherited shared entity set. Retain the legacy table, frozen, for one compatibility release and provide tested reverse synchronization.

**Tech Stack:** .NET 10, C# 14, Entity Framework Core 10.0.4, PostgreSQL/Npgsql, SQL Server, MSTest, Moq, shared delivery foundation from `2026-07-15-shared-durable-delivery-foundation.md`.

## Global Constraints

- Prerequisite: every task in `docs/superpowers/plans/2026-07-15-shared-durable-delivery-foundation.md` is merged and verified.
- Keep `my_atm_alert_occurrence`; do not move its suppression/escalation rules into common.
- Copy every legacy delivery row and preserve IDs, keys, payload JSON, attempts, leases, errors, timestamps, and terminal state.
- Map legacy `Leased` to shared `InProgress`; rollback maps it back.
- Populate `NotificationId` only when the referenced `notification` row exists.
- Use producer `MyAtm`, payload version `1`, and existing deterministic delivery keys.
- Keep `DispatchOutbox` job name, schedule, immediate post-import dispatch, batch size, 90-second MyATM timeout, lease, retry, and max attempts.
- Configure MyATM with `MonitorDeliveryFailureMode.DeadLetterOnly`.
- Keep PostgreSQL and SQL Server forward/rollback scripts equivalent and idempotent.
- Do not drop `my_atm_outbox_message` in this plan.
- Pause outbox and import jobs before forward or rollback synchronization.

---

### Task 1: Create and prove the forward/rollback data migrations

**Files:**
- Create: `myatmmonitor/database/migrations/2026-07-15-migrate-myatm-outbox-to-shared.postgres.sql`
- Create: `myatmmonitor/database/migrations/2026-07-15-migrate-myatm-outbox-to-shared.sqlserver.sql`
- Create: `myatmmonitor/database/migrations/2026-07-15-rollback-myatm-outbox-to-local.postgres.sql`
- Create: `myatmmonitor/database/migrations/2026-07-15-rollback-myatm-outbox-to-local.sqlserver.sql`
- Create: `myatmmonitor/MyAtmMonitorTests/MyAtmSharedOutboxMigrationContractTests.cs`
- Create: `myatmmonitor/MyAtmMonitorTests/MyAtmSharedOutboxMigrationTests.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/testdata/create.postgres.sql`
- Modify: `myatmmonitor/MyAtmMonitorTests/testdata/reset.postgres.sql`

**Interfaces:**
- Consumes: the shared table scripts/schema from Plan 1 and legacy `my_atm_outbox_message` / `my_atm_alert_occurrence`.
- Produces: idempotent provider-specific forward and reverse synchronization.

- [ ] **Step 1: Write failing migration contract tests**

Read all four scripts and assert prerequisite guards, producer/version constants, status mappings, notification existence join, conflict handling, and absence of any `DROP TABLE`:

```csharp
[DataTestMethod]
[DataRow("2026-07-15-migrate-myatm-outbox-to-shared.postgres.sql", "'Leased' THEN 'InProgress'")]
[DataRow("2026-07-15-migrate-myatm-outbox-to-shared.sqlserver.sql", "'Leased' THEN 'InProgress'")]
public void ForwardMigration_MapsTheLegacyLeaseState(string file, string mapping)
{
    var sql = MigrationText(file);
    StringAssert.Contains(sql, mapping);
    StringAssert.Contains(sql, "MyAtm");
    StringAssert.Contains(sql, "PayloadVersion");
    Assert.IsFalse(sql.Contains("DROP TABLE", StringComparison.OrdinalIgnoreCase));
}
```

Define `MigrationText(string file)` in the test class by locating the repository root from `AppContext.BaseDirectory` and reading `myatmmonitor/database/migrations/<file>`; fail with `FileNotFoundException` when the asset is absent.

- [ ] **Step 2: Write failing PostgreSQL round-trip tests**

Seed Pending, Leased, Completed, DeadLetter, data-MQTT, valid-notification, and orphan-notification rows. Apply the forward SQL twice and assert one shared row per delivery key. Mutate the shared states, add a post-cutover shared row, apply rollback twice, and assert the legacy table exactly reflects shared MyATM version-1 state.

- [ ] **Step 3: Run migration tests and verify missing assets**

Run: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~MyAtmSharedOutboxMigration"`

Expected: FAIL because the four scripts and shared fixture table are absent.

- [ ] **Step 4: Implement the forward scripts**

Both scripts must:

1. abort when the shared table is absent;
2. abort on a legacy status outside Pending/Leased/Completed/DeadLetter or kind outside MqttDataInserted/MqttAlert/Email/Sms;
3. insert producer `MyAtm`, payload version `1`, and map `OccurrenceKey` to `CorrelationKey`;
4. preserve the legacy payload unchanged;
5. map `Leased` to `InProgress`;
6. left join occurrence and notification so only an existing notification ID is copied;
7. set `DeadLetteredAt` null for legacy rows; and
8. on conflict, never replace a higher-attempt shared row or a shared terminal row with a non-terminal legacy row.

- [ ] **Step 5: Implement authoritative rollback scripts**

Select only `Producer='MyAtm' AND PayloadVersion=1`. Insert missing legacy rows and overwrite existing delivery state from shared, mapping `InProgress` to `Leased` and restoring `OccurrenceKey` only when `CorrelationKey` still references an occurrence. Leave the common table untouched.

- [ ] **Step 6: Run migration tests**

Run: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~MyAtmSharedOutboxMigration"`

Expected: PASS; the PostgreSQL test reports the same row count and field values before and after repeated scripts.

- [ ] **Step 7: Commit the migration assets**

```bash
git add myatmmonitor/database/migrations myatmmonitor/MyAtmMonitorTests/MyAtmSharedOutboxMigrationContractTests.cs myatmmonitor/MyAtmMonitorTests/MyAtmSharedOutboxMigrationTests.cs myatmmonitor/MyAtmMonitorTests/testdata
git commit -m "feat(myatm): add shared outbox cutover migrations"
```

---

### Task 2: Replace the local EF outbox model with common fenced ports

**Files:**
- Modify: `myatmmonitor/MyAtmMonitor/api/db/DBClient.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/IDBClient.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/EntityFramework/MyAtmMonitorContext.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/EntityFramework/MyAtmEntities.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmOutboxDispatcher.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmMonitorServices.cs`
- Delete: `myatmmonitor/MyAtmMonitor/api/db/Commands/IMyAtmOutboxCommands.cs`
- Delete: `myatmmonitor/MyAtmMonitor/api/db/Queries/IMyAtmOutboxQueries.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/EntityFramework/MyAtmModelMappingTests.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/TestDbClient.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/MyAtmOutboxContractTests.cs`

**Interfaces:**
- Consumes: `MonitorDbContextBase.DeliveryOutbox`, `IMonitorDeliveryOutboxQueries`, `IMonitorDeliveryOutboxCommands`, and common records.
- Produces: MyATM's fenced EF adapter over `monitor_delivery_outbox`.

- [ ] **Step 1: Rewrite existing DB tests against the common ports**

Change claims to pass `MonitorDeliveryProducers.MyAtm`; assert statuses use `InProgress`; add a foreign producer row and assert it is never claimed. Preserve tests for oldest-first order, three contention retries, attempt increments, expired lease reclamation, stale lease rejection, atomic audit completion/dead-letter, and replay idempotency.

```csharp
var claim = await ((IMonitorDeliveryOutboxQueries)testObj!).ClaimNextDueAsync(
    MonitorDeliveryProducers.MyAtm, utcNow, TimeSpan.FromMinutes(2));
Assert.AreEqual(MonitorDeliveryProducers.MyAtm, claim!.Producer);
Assert.AreEqual(1, claim.AttemptCount);
```

- [ ] **Step 2: Run the rewritten tests and verify failure**

Run: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~MyAtmOutboxContractTests|FullyQualifiedName~TestDbClient"`

Expected: FAIL because `DBClient` still implements the MyATM-local ports/table.

- [ ] **Step 3: Remove only the local outbox EF type and mapping**

Delete `MyAtmOutboxMessageEntity`, the app-local `OutboxMessages` set, the `MyAtmOutboxMessages` identifier entry, and its mapping block. Keep `MyAtmAlertOccurrenceEntity` and its relationship-independent `OccurrenceKey` data.

- [ ] **Step 4: Implement the common query/command interfaces**

Update `IDBClient` and `DBClient` to implement both common ports. Query `context.DeliveryOutbox`, require a known producer, and use the approved eligibility/order predicate:

```csharp
(row.Status == "Pending" && row.NextAttemptAt <= utcNow) ||
(row.Status == "InProgress" && row.LeaseUntil <= utcNow)
```

Order by `NextAttemptAt`, `CreatedAt`, then `Id`. Fence updates with `Id`, `LeaseId`, and `Status == "InProgress"`. `CompleteAsync` clears error/lease and sets `CompletedAt`; `RetryAsync` sets Pending/next/error; `DeadLetterAsync` sets `DeadLetteredAt`. Write contact audits inside the same transaction.

Update the still-temporary `MyAtmOutboxDispatcher` to depend directly on the common query/command interfaces, pass producer MyAtm when claiming, and consume `MonitorDeliveryMessage` / `MonitorDeliveryAudit`. This keeps Task 2 buildable; Task 4 removes the now-thin monitor-local dispatcher.

Remove the deleted MyATM-local port registrations from `MyAtmMonitorServices` and register `IDBClient` as `IMonitorDeliveryOutboxQueries` and `IMonitorDeliveryOutboxCommands`. Do not register the common dispatcher until Task 4.

- [ ] **Step 5: Run DB and mapping tests**

Run: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~MyAtmOutboxContractTests|FullyQualifiedName~MyAtmModelMappingTests|FullyQualifiedName~TestDbClient"`

Expected: PASS, including producer isolation and stale-owner tests.

- [ ] **Step 6: Commit the persistence adapter**

```bash
git add myatmmonitor/MyAtmMonitor/api/db myatmmonitor/MyAtmMonitor/api/MyAtmOutboxDispatcher.cs myatmmonitor/MyAtmMonitor/api/MyAtmMonitorServices.cs myatmmonitor/MyAtmMonitorTests/EntityFramework myatmmonitor/MyAtmMonitorTests/TestDbClient.cs myatmmonitor/MyAtmMonitorTests/MyAtmOutboxContractTests.cs
git commit -m "refactor(myatm): use common delivery persistence ports"
```

---

### Task 3: Move atomic commits to common delivery requests and planner

**Files:**
- Modify: `myatmmonitor/MyAtmMonitor/model/dto/MyAtmDustImportCommit.cs`
- Modify: `myatmmonitor/MyAtmMonitor/model/dto/MyAtmAlertCommit.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmRuleProcessor.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmOutboxDispatcher.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/DBClient.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/MyAtmRuleEvaluatorTests.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/ProcessDustLevelsAlertCommitTests.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/OfflineAlertCommitTests.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/TestDbClient.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/TestRules.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/TestRules2.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/TestUtil.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/TestMyAtmApi.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/TestMyAtmApiMonitors.cs`

**Interfaces:**
- Consumes: `RuleAlertDeliveryPlanner`, `MonitorDeliveryIdentity`, `MonitorDeliveryPayloadV1`, and `MonitorDeliveryRequest`.
- Produces: unchanged MyATM transaction semantics writing `MonitorDeliveryOutboxEntity`.

- [ ] **Step 1: Change result-type tests before implementation**

Replace `IReadOnlyList<MyAtmOutboxMessageDto>` expectations with `IReadOnlyList<MonitorDeliveryRequest>`. Assert the exact pre-migration IDs, delivery keys, correlation keys, payload JSON properties, notification IDs, and data-inserted null notification.

- [ ] **Step 2: Run alert/import tests and verify compile failure**

Run: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~ProcessDustLevelsAlertCommitTests|FullyQualifiedName~OfflineAlertCommitTests|FullyQualifiedName~MyAtmRuleEvaluatorTests"`

Expected: FAIL while DTOs and commit code still use MyATM-local outbox types.

- [ ] **Step 3: Replace local payload/message DTOs**

Delete `MyAtmOutboxMessageDto`, `MyAtmOutboxPayload`, `MyAtmOutboxDeliveryInput`, and `MyAtmDeliveryAudit`. Use common `MonitorDeliveryRequest` and `MonitorDeliveryAudit`. Keep commit/result records app-local and change `MyAtmAlertOccurrenceInput` to carry `RuleAlertDeliveryPlan? DeliveryPlan`; suppressed occurrences carry null, accepted occurrences carry the exact notification/deliveries to persist. Change the temporary dispatcher to decode `MonitorDeliveryPayloadV1` through the common codec so the solution compiles until Task 4 deletes it.

- [ ] **Step 4: Generate alerts through the shared planner**

Inject/use `RuleAlertDeliveryPlanner` when building occurrence proposals. Preserve the current correlation key and use:

```csharp
var notificationId = MonitorDeliveryIdentity.CreateGuid($"notification:{occurrenceKey}");
var outboxId = MonitorDeliveryIdentity.CreateGuid($"outbox:{deliveryKey}");
```

Do not change event-time suppression, Caution-to-Alert escalation, rule ordering, contact windows, or recent-notification guards.

- [ ] **Step 5: Persist common entities inside existing transactions**

Map each request to `MonitorDeliveryOutboxEntity` with producer MyAtm, version 1, status Pending, `NextAttemptAt = commit.UtcNow`, and the existing correlation key. Keep the two-phase save that inserts occurrence/notification before its outbox dependents. Data-inserted key remains:

```csharp
$"data:{commit.Monitor.Id:N}:{DustMonitorDto.PeriodToSeconds(commit.Period)}:{commit.Watermark.ToUniversalTime():O}"
```

- [ ] **Step 6: Run commit and replay tests**

Run: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~ProcessDustLevelsAlertCommitTests|FullyQualifiedName~OfflineAlertCommitTests|FullyQualifiedName~MyAtmRuleEvaluatorTests|FullyQualifiedName~TestDbClient"`

Expected: PASS with identical notification/delivery counts and empty replay results.

- [ ] **Step 7: Commit the atomic-write migration**

```bash
git add myatmmonitor/MyAtmMonitor/model/dto myatmmonitor/MyAtmMonitor/api/MyAtmRuleProcessor.cs myatmmonitor/MyAtmMonitor/api/db/DBClient.cs myatmmonitor/MyAtmMonitorTests
git commit -m "refactor(myatm): write common delivery requests atomically"
```

---

### Task 4: Replace the monitor-local dispatcher in composition

**Files:**
- Create: `myatmmonitor/MyAtmMonitor/api/MyAtmDeliveryFailureSink.cs`
- Delete: `myatmmonitor/MyAtmMonitor/api/MyAtmOutboxDispatcher.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmMonitorServices.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmApi.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/UseCases/StoreDustLevelsHandler.cs`
- Modify: `myatmmonitor/MyAtmMonitor/model/config/MyAtmMonitorOptions.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/MyAtmMonitorOptionsTests.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/MyAtmMonitorServiceRegistrationTests.cs`
- Replace: `myatmmonitor/MyAtmMonitorTests/MyAtmOutboxDispatcherTests.cs` with app-wiring parity tests using `MonitorDeliveryDispatcher`.

**Interfaces:**
- Consumes: common dispatcher/options/ports and `IMyAtmOperationalCommands`.
- Produces: one configured MyATM common dispatcher and terminal-only failure sink.

- [ ] **Step 1: Write failing option and DI parity tests**

Assert producer MyAtm, `DeadLetterOnly`, existing configured batch/lease/retry/max values, and the existing 90-second MyATM timeout. Resolve `MonitorDeliveryDispatcher`, both common ports, and `IMonitorDeliveryFailureSink` from DI and assert singleton lifetimes.

- [ ] **Step 2: Run option/registration tests and verify failure**

Run: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~MyAtmMonitorOptionsTests|FullyQualifiedName~MyAtmMonitorServiceRegistrationTests|FullyQualifiedName~MyAtmOutboxDispatcherTests"`

Expected: FAIL because composition still registers local ports and constructs `MyAtmOutboxDispatcher`.

- [ ] **Step 3: Add the MyATM failure sink**

Implement `IMonitorDeliveryFailureSink.RecordFailureAsync`. Return immediately when `terminal == false`; for terminal failures call `IMyAtmOperationalCommands.HandleException("Outbox delivery dead-lettered", new InvalidOperationException(error))` and return `Task.CompletedTask`.

- [ ] **Step 4: Map existing MyATM settings into common options**

Construct validated `MonitorDeliveryOptions` with producer MyAtm, failure mode DeadLetterOnly, current topics/base URL, and all `MyAtmMonitorOptions` values. Do not silently replace `OutboxDeliveryTimeoutSeconds = 90` with the common 30-second default.

- [ ] **Step 5: Register and inject the common dispatcher**

Register `IDBClient` as both common outbox ports, register the failure sink, and register one `MonitorDeliveryDispatcher`. Change `MyAtmApi`, `StoreDustLevelsHandler`, and `DispatchOutboxAsync` fields/parameters from `MyAtmOutboxDispatcher` to `MonitorDeliveryDispatcher`. Preserve compatibility constructors by routing them through one private dispatcher factory.

- [ ] **Step 6: Run dispatcher parity and DI tests**

Run: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~MyAtmMonitorOptionsTests|FullyQualifiedName~MyAtmMonitorServiceRegistrationTests|FullyQualifiedName~MyAtmOutboxDispatcherTests"`

Expected: PASS for MQTT/contact formatting, retries, dead letters, audits, and `DeadLetterOnly` behavior.

- [ ] **Step 7: Commit the dispatcher replacement**

```bash
git add myatmmonitor/MyAtmMonitor myatmmonitor/MyAtmMonitorTests
git commit -m "refactor(myatm): use common durable dispatcher"
```

---

### Task 5: Protect scheduling, atomic boundaries, and legacy-table isolation

**Files:**
- Modify: `myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmDependencyBoundaryTests.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmScheduledAlertCommitBoundaryTests.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/MyAtmOperationalConfigurationTests.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/TestMyAtmApi.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/TestMyAtmApiMonitors.cs`
- Modify: `myatmmonitor/README.md`

**Interfaces:**
- Consumes: completed MyATM common outbox path.
- Produces: regression gates preventing local-table or direct-delivery reintroduction.

- [ ] **Step 1: Add failing architecture assertions**

Scan production C# and assert no `MyAtmOutboxMessageEntity`, `IMyAtmOutbox`, `MyAtmOutboxDispatcher`, or `context.OutboxMessages` references remain. Assert scheduled handlers use atomic commit ports and do not inject `IMessageService`, `IMqttClient`, or `IMonitorEventPublisher`.

- [ ] **Step 2: Add scheduler parity assertions**

Assert `DispatchOutbox` remains in appsettings, `MonitorJobRunner`, and `MyAtmMonitorJobDispatcher.SupportedJobNames`; verify runner cancellation reaches `MonitorDeliveryDispatcher`. Assert `StoreDustLevelsHandler` performs immediate bounded dispatch after a successful commit.

- [ ] **Step 3: Run architecture and scheduler tests**

Run: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~Architecture|FullyQualifiedName~OperationalConfiguration|FullyQualifiedName~TestMyAtmApi"`

Expected: PASS with no excluded or skipped tests.

- [ ] **Step 4: Document cutover and rollback runbooks**

In `myatmmonitor/README.md`, add exact steps: pause import/outbox jobs, apply shared schema, run forward migration, compare counts by status, deploy, smoke `DispatchOutbox`, resume dispatcher then imports; rollback pauses jobs, runs reverse synchronization, deploys old app, then resumes. State the legacy table remains frozen for one release.

- [ ] **Step 5: Commit guards and runbook**

```bash
git add myatmmonitor/MyAtmMonitorTests/Architecture myatmmonitor/MyAtmMonitorTests/MyAtmOperationalConfigurationTests.cs myatmmonitor/MyAtmMonitorTests/TestMyAtmApi.cs myatmmonitor/MyAtmMonitorTests/TestMyAtmApiMonitors.cs myatmmonitor/README.md
git commit -m "test(myatm): protect shared outbox cutover"
```

---

### Task 6: Execute the MyATM release gate and rollback drill

**Files:**
- Modify: `project_state.md` with actual verification totals and the compatibility-release status.
- No production changes expected; fix only defects revealed by this gate in the task that owns them.

**Interfaces:**
- Consumes: Tasks 1–5 and the shared foundation.
- Produces: a deployable MyATM compatibility release.

- [ ] **Step 1: Run the complete MyATM suite**

Run: `dotnet test myatmmonitor/myatmmonitor.sln --no-restore`

Expected: PASS with zero failed and zero skipped tests.

- [ ] **Step 2: Run PostgreSQL migration and persistence tests with the isolated fixture**

Run: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-restore --filter "FullyQualifiedName~Migration|FullyQualifiedName~TestDbClient|FullyQualifiedName~Outbox"`

Expected: PASS; if `RVT__POSTGRES_INTEGRATION_CONNECTION` is intentionally absent, use the repository fixture's isolated-container behavior rather than a developer database.

- [ ] **Step 3: Build the root solution**

Run: `dotnet build rvt-monitors.sln --no-restore`

Expected: exit 0 with zero errors.

- [ ] **Step 4: Verify no runtime reference to the legacy table remains**

Run: `rg -n "my_atm_outbox_message|MyAtmOutboxMessages|MyAtmOutboxMessage" myatmmonitor/MyAtmMonitor`

Expected: no production C# results; SQL migration/readme references are expected outside `MyAtmMonitor`.

- [ ] **Step 5: Verify formatting and inspect the final diff**

Run: `git diff --check`

Expected: no output. Review that unrelated worktree files remain untouched and the legacy table is not dropped.

- [ ] **Step 6: Record and commit the verified release checkpoint**

Add the actual test counts, root-build result, forward/rollback round-trip result, and the statement that production cutover still requires paused schedulers to `project_state.md`.

```bash
git add project_state.md
git commit -m "docs(myatm): record shared outbox verification"
```

The MyATM release is ready only after the forward migration, shared dispatch smoke test, and reverse synchronization test all pass.
