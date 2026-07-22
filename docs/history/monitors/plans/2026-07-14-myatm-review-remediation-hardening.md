# MyATM Review Remediation Hardening Implementation Plan

> **For implementation:** Use `superpowers:executing-plans` or subagent-driven development and complete each task in order. Every behavior change follows RED → GREEN → refactor, with a commit after the task review gate.

**Goal:** Implement the approved MyATM hardening design: event-time alert anti-spam, fenced one-at-a-time outbox delivery, durable aggregate/offline alerts, explicit monitor-read failures, complete accessory pagination, and actionable dead-letter outcomes.

**Architecture:** Keep `IDBClient` as the compatibility facade and route new scheduled behavior through focused MyATM query/command ports. Keep domain transition policy pure, with EF Core owning atomic state/occurrence/notification/outbox commits. Use provider-neutral conditional updates for fenced leasing; PostgreSQL remains the integration-test target and SQL Server migrations remain supported.

**Tech stack:** .NET / ASP.NET Core, EF Core, PostgreSQL + SQL Server migration scripts, Quartz, xUnit/Moq, Mapperly analyzer.

**Global constraints:** Work only in the isolated implementation worktree; do not change unrelated AirQ documents or track credentials/destinations. Preserve the existing 30-minute dust and one-minute outbox schedules. Use `apply_patch` for every file edit. Do not change public legacy `IDBClient` compatibility members unless a narrow replacement is already adopted by MyATM code.

## Task 1: Establish contracts, option validation, and additive schema support

**Files:**
- Modify: `myatmmonitor/MyAtmMonitor/api/db/Interfaces/IMyAtmOutboxQueries.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/Interfaces/IMyAtmOutboxCommands.cs`
- Add: `myatmmonitor/MyAtmMonitor/api/db/Interfaces/IMyAtmAlertCommitCommands.cs`
- Add: `myatmmonitor/MyAtmMonitor/api/db/Interfaces/IMyAtmAccessoryCommands.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/Interfaces/IMyAtmMeasurementCommands.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/Models/MyAtmOutboxMessageDto.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/Entities/MyAtmOutboxMessage.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmMonitorOptions.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/Program.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/DBClient.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/MyAtmDbContext.cs`
- Add: `myatmmonitor/database/migrations/2026-07-14-add-myatm-hardening.postgres.sql`
- Add: `myatmmonitor/database/migrations/2026-07-14-add-myatm-hardening.sqlserver.sql`
- Add: `myatmmonitor/database/migrations/2026-07-14-remove-myatm-hardening.postgres.sql`
- Add: `myatmmonitor/database/migrations/2026-07-14-remove-myatm-hardening.sqlserver.sql`
- Modify: `myatmmonitor/MyAtmMonitorTests/testdata/create.postgres.sql`
- Modify: `myatmmonitor/MyAtmMonitorTests/testdata/reset.postgres.sql`
- Test: `myatmmonitor/MyAtmMonitorTests/*Outbox*Tests.cs`, `*Options*Tests.cs`, `*Architecture*Tests.cs`, `*Mapping*Tests.cs`

**Step 1: Write failing tests.**

Add tests that compile against the one-at-a-time outbox query and fenced command signatures, assert a nullable `LeaseId` maps correctly, assert the new alert/accessory ports are registered, and assert invalid timeout/lease combinations fail options validation.

**Step 2: Run the focused tests and confirm RED.**

Run the new focused test classes with `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-restore --nologo --filter "..."`. Record the expected compilation/assertion failure in the task report.

**Step 3: Implement the smallest contract/schema change.**

Introduce DTO/command models needed by subsequent tasks, including an alert-commit result model and immutable occurrence/delivery inputs. Add `LeaseId` to entity/configuration. Add `OutboxDeliveryTimeoutSeconds` (default 90) and validate `0 < timeout < lease`. Register interfaces without migrating scheduled callers yet. Add idempotent forward/rollback scripts and update the PostgreSQL fixture schema/reset.

**Step 4: Run focused tests and static checks.**

Run the focused tests, `dotnet build myatmmonitor/myatmmonitor.sln --no-restore --nologo`, and `git diff --check`.

**Step 5: Commit.**

Commit only Task 1 files with `feat(myatm): add hardening contracts and schema`.

## Task 2: Implement one-at-a-time fenced claims and atomic outcomes

**Files:**
- Modify: `myatmmonitor/MyAtmMonitor/api/db/DBClient.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/Interfaces/IMyAtmOutboxQueries.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/Interfaces/IMyAtmOutboxCommands.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/Entities/MyAtmOutboxMessage.cs`
- Test: `myatmmonitor/MyAtmMonitorTests/TestDBClient.cs`
- Test: `myatmmonitor/MyAtmMonitorTests/*Outbox*Tests.cs`

**Step 1: Write failing unit/integration tests.**

Cover a single returned claim with a fresh lease ID, conditional contention retry (bounded to three), reclaim of an expired lease with a different ID, and stale complete/retry returning `false` without changing the row. Add PostgreSQL tests for concurrent claims and atomic completed/dead-letter audit rows.

**Step 2: Run the focused tests and confirm RED.**

Use the PostgreSQL integration connection only through the runtime environment; do not record it in files or reports.

**Step 3: Implement provider-neutral fencing.**

Select the oldest due candidate, generate a GUID, and conditionally `ExecuteUpdateAsync` it to `Leased` with lease ID/until and incremented attempt. Retry contention at most three times. Fence complete/retry by `Id`, `Status=Leased`, and `LeaseId`; return ownership success. Make success/final-failure contact audit writes part of the same transaction as the matching fenced update.

**Step 4: Verify.**

Run focused unit + PostgreSQL tests, then `git diff --check`.

**Step 5: Commit.**

Commit as `feat(myatm): fence outbox claims and outcomes`.

## Task 3: Add awaited communication delivery and a bounded dispatcher

**Files:**
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Communications/IMessageService.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Communications/MessageService.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Communications/EmailSender.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Communications/SmsSender.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/UseCases/MyAtmOutboxDispatcher.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmMonitorOptions.cs`
- Test: `rvt-monitor-common/Rvt.Monitor.Common.Tests/*Message*Tests.cs`
- Test: `myatmmonitor/MyAtmMonitorTests/*OutboxDispatcher*Tests.cs`

**Step 1: Write failing tests.**

Test cancellation-aware async email/SMS calls while preserving legacy sync wrappers. Test one claim immediately before each delivery, the batch cap, delivery timeout less than lease, ownership-loss logging path, dead-letter continuation, redacted operational recording, and a failed pass after all bounded work is attempted.

**Step 2: Confirm RED.**

Run common communication and MyATM dispatcher tests before changing production code.

**Step 3: Implement.**

Add awaited cancellation-aware methods to `IMessageService`, retaining existing synchronous wrappers as compatibility adapters. Ensure provider calls honor the dispatcher-linked timeout. Change the dispatcher to claim, deliver, and fence exactly one row per loop iteration. Continue after newly dead-lettered rows, record a best-effort redacted operational error, then fail after the bounded pass. MyATM uses only the async shared methods.

**Step 4: Verify and commit.**

Run focused common/MyATM tests, build both affected solutions, `git diff --check`, then commit `feat(myatm): dispatch fenced outbox messages asynchronously`.

## Task 4: Extract event-time transition policy and harden dust commit suppression

**Files:**
- Add: `myatmmonitor/MyAtmMonitor/api/Rules/MyAtmAlertTransitionEvaluator.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/Rules/MyAtmRuleEvaluator.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/DBClient.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/Entities/MyAtmAlertOccurrence.cs`
- Test: `myatmmonitor/MyAtmMonitorTests/*Rule*Tests.cs`
- Test: `myatmmonitor/MyAtmMonitorTests/TestDBClient.cs`

**Step 1: Write failing pure tests.**

Specify active/inactive hysteresis, Alert-before-Caution precedence, missing/deleted/activity-window behavior, and event-time suppression: historical same severity, Alert-to-Caution downgrade, Caution-to-Alert escalation, and same-commit accepted-candidate visibility.

**Step 2: Confirm RED.**

Run the evaluator and commit integration tests before implementation.

**Step 3: Implement.**

Move transition decisions into a pure evaluator. Have the dust evaluator coordinate ordered samples through it. In the atomic dust commit, look up unsuppressed accepted occurrences by monitor/normalized field/period/event time; merge persisted results with accepted candidates accumulated in the current transaction. Persist suppressed occurrences with `IsSuppressed=true`, update rule state, and omit notification/outbox writes.

**Step 4: Verify and commit.**

Run pure tests and PostgreSQL replay/backfill tests, then commit `feat(myatm): enforce event-time alert suppression`.

## Task 5: Route aggregate and offline state changes through atomic alert commits

**Files:**
- Modify: `myatmmonitor/MyAtmMonitor/api/db/DBClient.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/UseCases/ProcessDustLevelsHandler.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/UseCases/CheckForOfflineMonitorsHandler.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/UseCases/MyAtmRuleProcessor.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/Program.cs`
- Test: `myatmmonitor/MyAtmMonitorTests/*ProcessDustLevels*Tests.cs`
- Test: `myatmmonitor/MyAtmMonitorTests/*Offline*Tests.cs`
- Test: `myatmmonitor/MyAtmMonitorTests/*Architecture*Tests.cs`
- Test: `myatmmonitor/MyAtmMonitorTests/TestDBClient.cs`

**Step 1: Write failing tests.**

Assert completed aggregate periods and offline transitions submit `IMyAtmAlertCommitCommands`, create state/occurrence/notification/outbox atomically, preserve aggregate MQTT and omit offline MQTT, and make no direct `IMessageService`, `IMonitorEventPublisher`, notification write, or broad concrete DB call from scheduled handlers.

**Step 2: Confirm RED.**

Run focused handler/architecture tests and PostgreSQL transaction tests.

**Step 3: Implement.**

Implement conditional alert commits using expected state to avoid partial concurrent updates. Migrate aggregate and offline coordinators to evaluate then commit. Keep compatibility processor methods only for legacy callers and remove them from scheduled paths. Re-evaluate/retry only within the existing bounded concurrency policy.

**Step 4: Verify and commit.**

Run focused tests and MyATM PostgreSQL suite, then commit `feat(myatm): persist aggregate and offline alerts atomically`.

## Task 6: Make monitor reads fail explicitly and paginate accessory imports

**Files:**
- Modify: `myatmmonitor/MyAtmMonitor/api/UseCases/MyAtmMonitorReader.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/UseCases/StoreAccessoryInfoHandler.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/DBClient.cs`
- Modify: `myatmmonitor/MyAtmMonitor/api/db/Interfaces/IMyAtmAccessoryCommands.cs`
- Test: `myatmmonitor/MyAtmMonitorTests/*MonitorReader*Tests.cs`
- Test: `myatmmonitor/MyAtmMonitorTests/*StoreAccessory*Tests.cs`
- Test: `myatmmonitor/MyAtmMonitorTests/TestDBClient.cs`

**Step 1: Write failing tests.**

Assert a database monitor-read failure rethrows the original exception after best-effort operational recording, while cancellation propagates without recording. Add accessory tests for two pages, page cap, nonadvancing cursor, page-batch rollback/deduplication, and aggregation of one monitor failure while later monitors continue.

**Step 2: Confirm RED.**

Run focused tests before production edits.

**Step 3: Implement.**

Rethrow original monitor query exceptions with `ExceptionDispatchInfo` or bare rethrow; isolate operational-record failures. Implement `InsertAccessoryPageAsync` as an atomic normalized/deduplicated batch, then change the handler to call keyset page retrieval, commit each successful page, advance only after commit, and throw an aggregate after other monitors are attempted.

**Step 4: Verify and commit.**

Run focused unit/integration tests, build MyATM, `git diff --check`, commit `feat(myatm): harden monitor reads and accessory paging`.

## Task 7: Full verification, migration review, documentation, and final code review

**Files:**
- Modify: `project_state.md`
- Modify: relevant MyATM README/configuration documentation only if public options/contracts need documenting

**Step 1: Verify the whole change.**

Run shared communication tests, complete MyATM PostgreSQL-backed suite, `dotnet build rvt-monitors.sln --no-restore --nologo`, `git diff --check`, and manually inspect all four forward/rollback scripts for table/identifier/provider accuracy.

**Step 2: Update project state.**

Record the implemented behavior, migration names, tests actually run, branch/worktree state, and the fact that no credentials were tracked. Do not include connection strings, contact destinations, or live run data.

**Step 3: Independent final review.**

Have a fresh reviewer inspect the full implementation diff for design compliance, correctness, tests, migration safety, narrow-port boundaries, secret leakage, and unexpected unrelated changes. Resolve every actionable finding with a new RED/GREEN regression when applicable.

**Step 4: Final commit.**

Commit documentation/verification fixes as `docs(myatm): record hardening verification`. Do not merge or push until the user asks.
