# Shared Durable Delivery Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the reusable delivery contracts, persistence model, migrations, planner, cancellable transports, and fenced dispatcher that MyATM and Svantek will consume.

**Architecture:** `Rvt.Monitor.Common` owns transport-neutral delivery records, deterministic identities, payload versioning, alert planning, the shared EF entity, and dispatcher policy. Each monitor remains responsible for implementing the persistence ports in its own EF context so business transactions stay app-local. One additive outbox table supports PostgreSQL and SQL Server and references the existing notification table.

**Tech Stack:** .NET 10, C# 14, Entity Framework Core 10.0.4, Npgsql EF Core 10.0.2, SQL Server EF Core 10.0.4, MQTTnet, MSTest 4.0.2, Moq 4.20.69.

## Global Constraints

- Keep PostgreSQL first while preserving SQL Server runtime and migration parity.
- Add exactly one shared persistence table: PostgreSQL `monitor_delivery_outbox`, SQL Server `dbo.MonitorDeliveryOutbox`.
- Reuse `notification` / `Notifications` and `notification_sent` / `NotificationsSent`; do not add an alert-occurrence table.
- Keep monitor-specific transactions and Mapperly policy out of `rvt-monitor-common`.
- Use canonical producer values `MyAtm` and `Svantek`, payload version `1`, and ordinal application validation.
- Use statuses `Pending`, `InProgress`, `Completed`, and `DeadLetter`.
- Defaults are batch 50, delivery timeout 30 seconds, lease 120 seconds, initial retry 30 seconds, retry cap 30 minutes, and maximum attempts 8.
- Delivery is durable at-least-once; fenced outcomes prevent stale owners mutating reclaimed rows.
- Never persist exception messages, payloads, destinations, or credentials in `LastError`.
- Preserve existing synchronous alert APIs for AirQ and Omnidots.

---

### Task 1: Define the shared delivery vocabulary and payload codec

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Delivery/MonitorDeliveryContracts.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Delivery/MonitorDeliveryOptions.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Delivery/MonitorDeliveryIdentity.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Delivery/MonitorDeliveryPayloadCodec.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Delivery/MonitorDeliveryOptionsTests.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Delivery/MonitorDeliveryIdentityTests.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Delivery/MonitorDeliveryPayloadCodecTests.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Delivery/MonitorDeliveryTestFixture.cs`

**Interfaces:**
- Produces: `MonitorDeliveryProducers`, `MonitorDeliveryKind`, `MonitorDeliveryFailureMode`, `MonitorDeliveryRequest`, `MonitorDeliveryMessage`, `MonitorDeliveryAudit`, `MonitorDeliveryPayloadV1`, `MonitorDeliveryOptions`, `MonitorDeliveryIdentity.CreateGuid`, and `MonitorDeliveryPayloadCodec`.
- Consumes: `Rvt.Monitor.Common.Notifications.AlertType`.

- [ ] **Step 1: Write failing contract, option, identity, and codec tests**

Add tests that assert exact producer strings, all four enum values, every default, rejection of an unknown producer, invalid durations, deterministic GUID parity, PascalCase JSON, and payload validation:

```csharp
[TestMethod]
public void CreateGuid_PreservesMyAtmSha256Identity()
{
    var actual = MonitorDeliveryIdentity.CreateGuid("notification:fixture-key");
    var expected = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("notification:fixture-key"))[..16]);
    Assert.AreEqual(expected, actual);
}

[TestMethod]
public void DecodeV1_RejectsEmptyNotificationForContactDelivery()
{
    var message = DeliveryFixture.Message(
        MonitorDeliveryKind.Email,
        JsonSerializer.Serialize(new MonitorDeliveryPayloadV1(
            Guid.Empty, DateTime.UtcNow, "157206", null, "SV-1",
            AlertType.Alert, "LAeq", 75, null)));

    Assert.ThrowsExactly<InvalidDataException>(() => MonitorDeliveryPayloadCodec.Decode(message));
}
```

`MonitorDeliveryTestFixture.cs` defines `internal static MonitorDeliveryMessage Message(...)` with valid Svantek defaults and optional kind, payload, attempt count, notification ID, and lease ID overrides. All later common dispatcher tests reuse this helper.

- [ ] **Step 2: Run the focused tests and verify the contracts do not exist**

Run: `dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter "FullyQualifiedName~MonitorDelivery"`

Expected: FAIL with compiler errors for missing `Rvt.Monitor.Common.Delivery` types.

- [ ] **Step 3: Implement the records and validated options**

Define the exact public surface in `MonitorDeliveryContracts.cs`:

```csharp
public static class MonitorDeliveryProducers
{
    public const string MyAtm = "MyAtm";
    public const string Svantek = "Svantek";

    public static bool IsKnown(string producer) =>
        string.Equals(producer, MyAtm, StringComparison.Ordinal) ||
        string.Equals(producer, Svantek, StringComparison.Ordinal);
}

public enum MonitorDeliveryKind { MqttDataInserted, MqttAlert, Email, Sms }
public enum MonitorDeliveryFailureMode { DeadLetterOnly, AnyDeliveryFailure }

public sealed record MonitorDeliveryRequest(
    Guid Id, string Producer, Guid? NotificationId, string? CorrelationKey,
    string DeliveryKey, MonitorDeliveryKind Kind, string Destination,
    int PayloadVersion, string Payload, DateTime CreatedAt);

public sealed record MonitorDeliveryMessage(
    Guid Id, string Producer, Guid? NotificationId, string? CorrelationKey,
    string DeliveryKey, MonitorDeliveryKind Kind, string Destination,
    int PayloadVersion, string Payload, int AttemptCount, Guid LeaseId);

public sealed record MonitorDeliveryAudit(
    Guid NotificationId, string Address, string Result, DateTime SentAt);

public sealed record MonitorDeliveryPayloadV1(
    Guid NotificationId, DateTime Timestamp, string SerialId, int? CustomerId,
    string FleetNr, AlertType AlertType, string Field, double Level,
    string? PortalBaseUrl = null);
```

Implement `MonitorDeliveryOptions.Validate()` with exact positive-duration checks, `RetryCap >= InitialRetryDelay`, `LeaseDuration > DeliveryTimeout`, `MaxAttempts > 0`, a known producer, and an absolute HTTP/HTTPS portal URL.

- [ ] **Step 4: Implement identity and codec validation**

Use the deployed MyATM identity algorithm exactly:

```csharp
public static Guid CreateGuid(string value)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(value);
    return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);
}
```

`MonitorDeliveryPayloadCodec.Decode` must require version `1`, deserialize with default PascalCase names, require a UTC timestamp and non-empty serial ID, allow `Guid.Empty` only for `MqttDataInserted`, and require the row `NotificationId` to match the payload for alert/contact kinds when the row reference is present.

- [ ] **Step 5: Run the focused tests**

Run: `dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter "FullyQualifiedName~MonitorDelivery"`

Expected: PASS with no skipped tests.

- [ ] **Step 6: Commit the delivery vocabulary**

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Delivery rvt-monitor-common/Rvt.Monitor.CommonTests/Delivery
git commit -m "feat(common): define durable delivery contracts"
```

---

### Task 2: Add the shared EF entity and provider migrations

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/MonitorDeliveryOutboxEntity.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Data/EntityFramework/MonitorDbContextBase.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Data/EntityFramework/MonitorModelBuilderExtensions.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.CommonTests/Data/EntityFramework/MonitorModelMappingTests.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Data/EntityFramework/MonitorDeliveryMigrationContractTests.cs`
- Create: `rvt-monitor-common/database/migrations/2026-07-15-add-monitor-delivery-outbox.postgres.sql`
- Create: `rvt-monitor-common/database/migrations/2026-07-15-add-monitor-delivery-outbox.sqlserver.sql`
- Create: `rvt-monitor-common/database/migrations/README.md`

**Interfaces:**
- Consumes: `MonitorDeliveryKind` from Task 1.
- Produces: `MonitorDeliveryOutboxEntity`, `MonitorDbContextBase.DeliveryOutbox`, provider mappings, and idempotent schema scripts.

- [ ] **Step 1: Write failing provider metadata and migration-contract tests**

Assert exact table, schema, column, key, index, and delete behavior for both providers:

```csharp
[DataTestMethod]
[DataRow(MonitorDatabaseProvider.PostgreSql, "monitor_delivery_outbox", null, "producer")]
[DataRow(MonitorDatabaseProvider.SqlServer, "MonitorDeliveryOutbox", "dbo", "Producer")]
public void SharedModel_MapsDeliveryOutbox(
    MonitorDatabaseProvider provider, string table, string? schema, string producerColumn)
{
    using var context = CreateContext(provider);
    var entity = context.Model.FindEntityType(typeof(MonitorDeliveryOutboxEntity))!;
    Assert.AreEqual(table, entity.GetTableName());
    Assert.AreEqual(schema, entity.GetSchema());
    Assert.AreEqual(producerColumn, entity.FindProperty(nameof(MonitorDeliveryOutboxEntity.Producer))!.GetColumnName());
    Assert.IsTrue(entity.GetIndexes().Any(index => index.Properties.Select(p => p.Name)
        .SequenceEqual(new[] { "Producer", "Status", "NextAttemptAt" })));
}
```

The migration test reads both SQL files and asserts the unique `(Producer, DeliveryKey)` constraint, four-status check, notification FK with `ON DELETE SET NULL`, `DeadLetteredAt`, and due-work index.

- [ ] **Step 2: Run the focused tests and verify failure**

Run: `dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter "FullyQualifiedName~MonitorModelMappingTests|FullyQualifiedName~MonitorDeliveryMigrationContractTests"`

Expected: FAIL because the entity and migration files do not exist.

- [ ] **Step 3: Add the entity and base-context set**

Create an entity with exactly these properties: `Id`, `Producer`, `NotificationId`, `CorrelationKey`, `DeliveryKey`, `Kind`, `Destination`, `PayloadVersion`, `Payload`, `Status`, `AttemptCount`, `NextAttemptAt`, `LeaseId`, `LeaseUntil`, `CompletedAt`, `DeadLetteredAt`, `LastError`, and `CreatedAt`. Add:

```csharp
public DbSet<MonitorDeliveryOutboxEntity> DeliveryOutbox => Set<MonitorDeliveryOutboxEntity>();
```

to `MonitorDbContextBase`.

- [ ] **Step 4: Map both providers without monitor-specific references**

In `ApplySharedMonitorMappings`, map PostgreSQL snake_case and SQL Server PascalCase identifiers, configure the notification relationship with `DeleteBehavior.SetNull`, add a unique index over `{ Producer, DeliveryKey }`, and add the due index over `{ Producer, Status, NextAttemptAt }`. Store `Kind` as its string name.

- [ ] **Step 5: Write idempotent PostgreSQL and SQL Server scripts**

The PostgreSQL script must use `CREATE TABLE IF NOT EXISTS`, an idempotent `DO $$` block for constraints, and `CREATE INDEX IF NOT EXISTS`. The SQL Server script must use `IF OBJECT_ID(...) IS NULL`, named constraints, and guarded `sys.indexes` checks. Both scripts must create only the shared table and must not modify `notification` or `notification_sent`.

- [ ] **Step 6: Run metadata and migration tests**

Run: `dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter "FullyQualifiedName~MonitorModelMappingTests|FullyQualifiedName~MonitorDeliveryMigrationContractTests"`

Expected: PASS for PostgreSQL and SQL Server data rows.

- [ ] **Step 7: Commit the shared persistence model**

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Data rvt-monitor-common/Rvt.Monitor.CommonTests/Data rvt-monitor-common/database
git commit -m "feat(common): add shared delivery outbox schema"
```

---

### Task 3: Add side-effect-free alert delivery planning

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Delivery/RuleAlertDeliveryPlanner.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Delivery/RuleAlertDeliveryPlannerTests.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.CommonTests/Rules/SharedRuntimeCompatibilityTests.cs`

**Interfaces:**
- Consumes: `RuleNotificationRequest`, `Rules.RvtContactDto`, `NotificationDto`, `MonitorDeliveryIdentity`, `MonitorDeliveryPayloadCodec`, and Task 1 contracts.
- Produces: `RuleAlertDeliveryPlan` and `RuleAlertDeliveryPlanner.Plan(...)`.

- [ ] **Step 1: Write failing parity and determinism tests**

Use data rows for Alert, Caution, Offline, BatteryAlert, and BatteryCaution. Assert the notification fields, one `MqttAlert`, contact-window filtering, email/SMS destinations, exact keys, deterministic IDs, and no transport calls:

```csharp
var plan = planner.Plan(
    request,
    contacts,
    MonitorDeliveryProducers.Svantek,
    customerId: null,
    correlationKey: "svantek:rule:monitor:rule:Alert:2026-07-15T10:00:00.0000000Z",
    createdAt: utcNow);

Assert.AreEqual(
    "svantek:rule:monitor:rule:Alert:2026-07-15T10:00:00.0000000Z:MqttAlert:alert",
    plan.Deliveries.Single(x => x.Kind == MonitorDeliveryKind.MqttAlert).DeliveryKey);
```

- [ ] **Step 2: Run the planner tests and verify failure**

Run: `dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter "FullyQualifiedName~RuleAlertDeliveryPlannerTests"`

Expected: FAIL because the planner does not exist.

- [ ] **Step 3: Implement the pure planner**

Expose this exact method:

```csharp
public RuleAlertDeliveryPlan Plan(
    RuleNotificationRequest request,
    IReadOnlyList<RvtContactDto> contacts,
    string producer,
    int? customerId,
    string correlationKey,
    DateTime createdAt);
```

Derive `notificationId` from `notification:{correlationKey}`, derive each outbox ID from `outbox:{deliveryKey}`, create the alert MQTT delivery first, then eligible email deliveries, then eligible SMS deliveries. Use `ShouldSendAtTime(request.AlertTime)` and skip blank destinations. Do not inject `IMessageService`, `IMqttClient`, an EF context, or delegates.

- [ ] **Step 4: Run planner and compatibility tests**

Run: `dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter "FullyQualifiedName~RuleAlertDeliveryPlannerTests|FullyQualifiedName~SharedRuntimeCompatibilityTests"`

Expected: PASS and identical notification/message selection across all five alert types.

- [ ] **Step 5: Commit the planner**

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Delivery/RuleAlertDeliveryPlanner.cs rvt-monitor-common/Rvt.Monitor.CommonTests/Delivery/RuleAlertDeliveryPlannerTests.cs rvt-monitor-common/Rvt.Monitor.CommonTests/Rules/SharedRuntimeCompatibilityTests.cs
git commit -m "feat(common): plan durable alert deliveries"
```

---

### Task 4: Make the shared MQTT boundary cancellable

**Files:**
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Mqtt/IRvtMqttClient.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Mqtt/RvtMqttClient.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Mqtt/RvtMqttClientContractTests.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/MyAtmOutboxDispatcherTests.cs`

**Interfaces:**
- Produces: `IMqttClient.PublishAsync(string, string, CancellationToken)` and `ConnectAsync(CancellationToken)` with optional tokens.
- Consumes: MQTTnet cancellation-aware connect and publish operations.

- [ ] **Step 1: Write a failing interface-contract test**

Use reflection to assert both methods end with an optional `CancellationToken`, then update existing MyATM mocks to include `It.IsAny<CancellationToken>()` so compilation identifies every affected call site.

- [ ] **Step 2: Run common and MyATM compile tests**

Run: `dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter "FullyQualifiedName~RvtMqttClientContractTests"`

Expected: FAIL because the token parameters are absent.

- [ ] **Step 3: Flow cancellation through the MQTT implementation**

Change the interface to:

```csharp
Task PublishAsync(string topic, string message, CancellationToken cancellationToken = default);
Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
```

Pass the token to `connectLock.WaitAsync`, MQTTnet `ConnectAsync`, and publish. Preserve disabled-MQTT behavior and release the connect lock in `finally`.

- [ ] **Step 4: Run common and MyATM tests**

Run: `dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter "FullyQualifiedName~RvtMqttClientContractTests"`

Run: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter "FullyQualifiedName~MyAtmOutboxDispatcherTests"`

Expected: PASS; no mock expression uses the obsolete two-argument signature.

- [ ] **Step 5: Commit the cancellation boundary**

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Mqtt rvt-monitor-common/Rvt.Monitor.CommonTests/Mqtt myatmmonitor/MyAtmMonitorTests/MyAtmOutboxDispatcherTests.cs
git commit -m "refactor(common): flow MQTT cancellation"
```

---

### Task 5: Implement the fenced generic dispatcher

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Delivery/IMonitorDeliveryOutboxQueries.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Delivery/IMonitorDeliveryOutboxCommands.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Delivery/IMonitorDeliveryFailureSink.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Delivery/MonitorDeliveryDispatcher.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Delivery/MonitorDeliveryDispatchException.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Delivery/MonitorDeliveryDispatcherTests.cs`

**Interfaces:**
- Consumes: Task 1 contracts/codec/options, cancellable `IMqttClient`, and `IMessageService.SendMessageAsync`.
- Produces: the three narrow persistence/failure ports and `MonitorDeliveryDispatcher.DispatchDueAsync(CancellationToken)`.

- [ ] **Step 1: Port the existing MyATM dispatcher cases as failing common tests**

Cover: no due row, successful alert MQTT, data MQTT without notification, email/SMS audit, batch 50, timeout retry, exponential delays, attempt 8 dead letter, malformed payload immediate dead letter, later-row continuation, stale lease outcome, caller cancellation, producer mismatch, unsupported version, redacted error, `DeadLetterOnly`, and `AnyDeliveryFailure`.

```csharp
[TestMethod]
public async Task DispatchDueAsync_AnyFailureMode_RetriesThenFailsPass()
{
    queries.Enqueue(DeliveryFixture.Message(attemptCount: 1));
    mqtt.Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new TimeoutException("secret destination"));

    var error = await Assert.ThrowsExactlyAsync<MonitorDeliveryDispatchException>(
        () => dispatcher.DispatchDueAsync());

    Assert.AreEqual("Delivery failed (TimeoutException).", commands.Retries.Single().Error);
    Assert.DoesNotContain("secret", commands.Retries.Single().Error);
    Assert.HasCount(1, error.Failures);
}
```

- [ ] **Step 2: Run the dispatcher tests and verify failure**

Run: `dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter "FullyQualifiedName~MonitorDeliveryDispatcherTests"`

Expected: FAIL because the ports and dispatcher do not exist.

- [ ] **Step 3: Define the narrow ports exactly as the approved design**

Implement single-row `ClaimNextDueAsync(producer, utcNow, leaseDuration, token)` plus fenced `CompleteAsync`, `RetryAsync`, and `DeadLetterAsync`. Define `RecordFailureAsync(message, error, terminal, token)` on the failure sink. Do not add bulk-claim or monitor-specific methods.

- [ ] **Step 4: Implement the bounded dispatch loop**

For each slot up to `BatchSize`: claim immediately before dispatch, create a linked 30-second timeout, decode/validate, deliver, and perform exactly one fenced outcome. Compute retry delay with:

```csharp
var exponent = Math.Max(0, message.AttemptCount - 1);
var ticks = options.InitialRetryDelay.Ticks * Math.Pow(2, exponent);
return TimeSpan.FromTicks((long)Math.Min(ticks, options.RetryCap.Ticks));
```

Treat caller cancellation separately from timeout cancellation. Leave a caller-cancelled lease untouched to expire. Invoke the failure sink only after `RetryAsync`/`DeadLetterAsync` returns true. Warning-log ownership loss and make no second mutation.

- [ ] **Step 5: Implement transport formatting and audits**

Publish MQTT to the configured alert/insert topics. Use `Dust` for MyATM and `Noise` for Svantek alert text. Use `IMessageService.SendMessageAsync` for contact delivery. Create `MonitorDeliveryAudit` only when kind is Email/Sms and the row/payload notification IDs match. MyATM always receives a notification URL; Svantek receives one only for Alert/Caution.

- [ ] **Step 6: Run dispatcher and communication tests**

Run: `dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter "FullyQualifiedName~MonitorDeliveryDispatcherTests|FullyQualifiedName~MessageServiceAsyncTests"`

Expected: PASS with all failure-mode and cancellation cases compiled and executed.

- [ ] **Step 7: Commit the dispatcher**

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Delivery rvt-monitor-common/Rvt.Monitor.CommonTests/Delivery
git commit -m "feat(common): dispatch fenced durable deliveries"
```

---

### Task 6: Verify the independently deployable shared foundation

**Files:**
- Modify: `rvt-monitor-common/database/migrations/README.md`
- Modify: `project_state.md` with actual common-suite/root-build verification totals.
- Modify: `docs/superpowers/specs/2026-07-15-shared-durable-delivery-and-svantek-remediation-design.md` only if implementation names changed during review; otherwise leave it untouched.

**Interfaces:**
- Consumes: every output from Tasks 1–5.
- Produces: a reviewed shared-foundation checkpoint ready for MyATM migration.

- [ ] **Step 1: Run the entire common suite**

Run: `dotnet test rvt-monitor-common/rvt-monitor-common.sln --no-restore`

Expected: PASS with zero failed and zero skipped tests.

- [ ] **Step 2: Build the root solution to catch cross-monitor interface breaks**

Run: `dotnet build rvt-monitors.sln --no-restore`

Expected: exit 0 with zero errors; warnings must not increase from the pre-task baseline.

- [ ] **Step 3: Validate migration assets and formatting**

Run: `rg -n "monitor_delivery_outbox|MonitorDeliveryOutbox|ON DELETE SET NULL|DeadLetteredAt|dead_lettered_at" rvt-monitor-common/database rvt-monitor-common/Rvt.Monitor.Common`

Expected: both provider scripts, shared mapping, and entity are returned.

Run: `git diff --check`

Expected: no output.

- [ ] **Step 4: Review the diff for forbidden coupling**

Run: `rg -n "MyAtm\.Api|Svantek\.Api|Mapperly" rvt-monitor-common/Rvt.Monitor.Common`

Expected: no results. Producer constants and formatting branches may use only the strings `MyAtm` and `Svantek`.

- [ ] **Step 5: Record and commit the verified shared checkpoint**

Add the actual common test count, root-build result, migration-contract result, and the statement that no monitor consumes the table yet to `project_state.md`.

```bash
git add rvt-monitor-common/database/migrations/README.md docs/superpowers/specs/2026-07-15-shared-durable-delivery-and-svantek-remediation-design.md project_state.md
git commit -m "docs(common): record delivery foundation verification"
```

The shared foundation is complete only after this gate passes. Do not begin the MyATM or Svantek plans against an unverified common contract.
