# Common Durable Alert Ingress Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the recurring Omnidots webhook P0 with a fail-closed HTTP adapter and a reusable RVT Common alert ingress, atomic occurrence/notification/outbox commit, and fenced MQTT/email/SMS delivery.

**Architecture:** Build a new `Rvt.Monitor.Common.Alerts` hexagonal slice around `IAlertIngressPort`, `IAlertCommitStore`, and `IAlertDeliveryAdapter`. Omnidots authenticates exact vendor bytes and translates them into `AlertSignal`; Common owns event-time policy, duplicate-safe persistence, dispatch, retry, auditing, and cleanup through the concrete Omnidots EF context factory.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs and rate limiting, EF Core 10, PostgreSQL/Npgsql, SQL Server/SqlClient, MQTTnet through `IMonitorEventPublisher`, `IMessageService`, Quartz, MSTest 4, Moq.

## Global Constraints

- Work only in `/Users/oldgeorge/Documents/rvt-monitors/rvt-monitors/.worktrees/omnidots-strict-review-remediation` on `codex/omnidots-strict-review-remediation`.
- Read `project_state.md` before execution and preserve the unrelated untracked `.superpowers/sdd/.gitignore`.
- Use `apply_patch` for source, test, SQL, configuration, and documentation edits.
- Follow RED → GREEN → refactor for every behavior change; never write production behavior before observing the focused test fail for the expected reason.
- Keep `IDBClient` as a compatibility facade. New webhook and durable-alert code uses focused handlers, Common ports, and `IMonitorDbContextFactory<OmnidotsMonitorContext>`.
- Keep Mapperly out of `rvt-monitor-common`; vendor alarm translation, suppression/escalation, field selection, cryptography, and delivery planning remain manual and directly tested.
- PostgreSQL is the required live integration target. SQL Server support is protected by EF metadata, parsed migration contracts, and provider-specific claim SQL tests; run live SQL Server verification only when a runtime connection is supplied.
- API mode requires distinct webhook/configuration secrets of at least 32 strict UTF-8 bytes and an absolute HTTPS webhook URL. Scheduler-only and unrelated one-shot jobs must not require those API secrets.
- Both POST endpoints accept only `application/json` with optional charset, reject non-identity content encoding, cap raw bodies at exactly 64 KiB, use zero-length rate-limit queues, and never log or return secrets, signatures, bodies, destinations, vendor raw responses, or raw exception messages.
- Preserve the current `RvtMqttMessage` JSON shape and configured alert topic. External MQTT/email/SMS delivery is at least once, not exactly once.
- Preserve existing Peak, Veff, VDV, Trace, monitoring, battery, and offline behavior. Do not migrate AirQ, Svantek, or the MyATM-specific outbox in this change.
- Exact authenticated-body duplicates create one permanent occurrence, at most one notification, and at most one delivery per channel/destination under sequential and concurrent execution.
- Common delivery defaults are batch size 50, lease 120 seconds, timeout 90 seconds, initial retry 30 seconds, retry cap 30 minutes, eight attempts, one-minute polling, and 90-day completed-outbox retention.
- Finish every task with focused tests, `git diff --check`, a task-scoped review, and the named commit. Do not merge or push until explicitly requested.

---

## File Structure

The implementation creates these focused units:

- `rvt-monitor-common/Rvt.Monitor.Common/Alerts/`: transport-neutral signal, identity, policy, application service, delivery contracts, dispatcher, adapters, options, background worker, and DI registration.
- `rvt-monitor-common/Rvt.Monitor.Common/Alerts/Persistence/`: atomic commit/outbox ports, EF implementations, provider exception classification, and PostgreSQL/SQL Server claim strategies.
- `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/AlertOccurrenceEntity.cs`: permanent deduplication and outcome row.
- `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/AlertDeliveryOutboxEntity.cs`: leased, retryable delivery row.
- `rvt-monitor-common/Rvt.Monitor.Common/Data/EntityFramework/IMonitorDbContextFactory.cs`: reusable concrete-context factory boundary.
- `omnidotsmonitor/OmnidotsMonitor/api/UseCases/`: typed configuration, strict alarm translation, and byte-only webhook adapters.
- `omnidotsmonitor/OmnidotsMonitor/api/Http/BoundedJsonRequestReader.cs`: one reusable bounded transport reader for both POST endpoints.
- `omnidotsmonitor/OmnidotsMonitor/model/config/OmnidotsApiSecurityOptions.cs`: API-only security and concurrency configuration.
- `omnidotsmonitor/OmnidotsMonitor/postgres/` and `sqlserver/`: application deployment scripts for the shared alert tables.
- Common tests own pure policy, identity, mapping, dispatcher, adapter, and provider-contract tests; Omnidots tests own vendor boundary, real-host, migration, architecture, and PostgreSQL end-to-end tests.

---

### Task 1: Add transport-neutral alert contracts, deterministic identity, and policy

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/AlertDeliveryChannels.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/AlertOccurrenceOutcome.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/AlertSignal.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/AlertIngressResult.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/IAlertIngressPort.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/IAlertAcceptancePolicy.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/CautionAlertAcceptancePolicy.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/AlertIdentity.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Alerts/CautionAlertAcceptancePolicyTests.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Alerts/AlertIdentityTests.cs`

**Interfaces:**
- Consumes: existing `Rvt.Monitor.Common.Notifications.AlertType`.
- Produces: `AlertSignal`, `AlertIngressResult`, `IAlertIngressPort.AcceptAsync`, `IAlertAcceptancePolicy.Evaluate`, `AlertIdentity.CreateSourceKeyHash`, and `AlertIdentity.CreateNotificationId` for Tasks 3, 4, and 9.

- [ ] **Step 1: Write the failing policy and identity tests**

Create table-driven MSTest cases for every transition and stable identity/version bits:

```csharp
[TestClass]
public sealed class CautionAlertAcceptancePolicyTests
{
    private readonly CautionAlertAcceptancePolicy policy = new();

    [DataTestMethod]
    [DataRow(AlertType.Ignore, null, AlertOccurrenceOutcome.Ignored)]
    [DataRow(AlertType.Caution, null, AlertOccurrenceOutcome.Accepted)]
    [DataRow(AlertType.Caution, AlertType.Caution, AlertOccurrenceOutcome.Suppressed)]
    [DataRow(AlertType.Caution, AlertType.Alert, AlertOccurrenceOutcome.Suppressed)]
    [DataRow(AlertType.Alert, null, AlertOccurrenceOutcome.Accepted)]
    [DataRow(AlertType.Alert, AlertType.Caution, AlertOccurrenceOutcome.Accepted)]
    [DataRow(AlertType.Alert, AlertType.Alert, AlertOccurrenceOutcome.Suppressed)]
    public void Evaluate_ReturnsExpectedOutcome(
        AlertType incoming,
        AlertType? recent,
        AlertOccurrenceOutcome expected)
    {
        IReadOnlyCollection<AlertType> recentTypes = recent.HasValue
            ? [recent.Value]
            : [];
        Assert.AreEqual(expected, policy.Evaluate(incoming, recentTypes));
    }
}

[TestMethod]
public void NotificationId_IsStableAndUsesRfc9562Version8Variant()
{
    var hash = AlertIdentity.CreateSourceKeyHash("f00d");
    var first = AlertIdentity.CreateNotificationId("omnidots.webhook", hash);
    var second = AlertIdentity.CreateNotificationId("omnidots.webhook", hash);
    var bytes = first.ToByteArray(bigEndian: true);

    Assert.AreEqual(first, second);
    Assert.AreEqual(8, bytes[6] >> 4);
    Assert.AreEqual(0b10, bytes[8] >> 6);
    Assert.AreEqual(32, hash.Length);
}
```

- [ ] **Step 2: Run the focused tests and confirm RED**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --no-restore --nologo --filter "FullyQualifiedName~CautionAlertAcceptancePolicyTests|FullyQualifiedName~AlertIdentityTests"
```

Expected: compilation fails because the `Rvt.Monitor.Common.Alerts` contracts do not exist.

- [ ] **Step 3: Implement the contracts, policy, and SHA-256 UUIDv8 identity**

Use these exact public shapes:

```csharp
[Flags]
public enum AlertDeliveryChannels { None = 0, Mqtt = 1, Email = 2, Sms = 4 }

public enum AlertOccurrenceOutcome { Accepted = 0, Ignored = 1, Suppressed = 2 }

public sealed record AlertSignal(
    string Source,
    string SourceEventKey,
    DateTime EventTime,
    string SerialId,
    AlertType AlertType,
    string Field,
    double Level,
    double Limit,
    int AveragingPeriod,
    string Message,
    AlertDeliveryChannels DeliveryChannels,
    TimeSpan SuppressionWindow);

public sealed record AlertIngressResult(
    Guid OccurrenceId,
    Guid? NotificationId,
    AlertOccurrenceOutcome Outcome,
    bool IsDuplicate);

public interface IAlertIngressPort
{
    Task<AlertIngressResult> AcceptAsync(
        AlertSignal signal,
        CancellationToken cancellationToken = default);
}

public interface IAlertAcceptancePolicy
{
    AlertOccurrenceOutcome Evaluate(
        AlertType incoming,
        IReadOnlyCollection<AlertType> recentAlertTypes);
}
```

`CautionAlertAcceptancePolicy` must reject unsupported incoming values with `ArgumentOutOfRangeException`, return `Ignored` only for `Ignore`, and implement the matrix in Step 1. `AlertIdentity.CreateSourceKeyHash` hashes strict UTF-8 source-key bytes. `CreateNotificationId` hashes `namespaceBytes + UTF8(source) + 0x00 + sourceKeyHash`, takes 16 bytes, sets version 8 and RFC variant bits, then constructs `new Guid(bytes, bigEndian: true)`.

- [ ] **Step 4: Run the focused tests and Common build**

Run the Step 2 command and:

```bash
dotnet build rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj --no-restore --nologo
git diff --check
```

Expected: focused tests pass; build reports zero warnings and errors; diff check is silent.

- [ ] **Step 5: Review and commit Task 1**

Review namespace direction and confirm no vendor type entered Common. Commit:

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Alerts rvt-monitor-common/Rvt.Monitor.CommonTests/Alerts
git commit -m "feat(common): add durable alert contracts and policy"
```

---

### Task 2: Add shared EF entities, mappings, and provider migration assets

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/AlertOccurrenceEntity.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/Entities/AlertDeliveryOutboxEntity.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Data/EntityFramework/IMonitorDbContextFactory.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Data/EntityFramework/MonitorDbContextBase.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Data/EntityFramework/MonitorModelBuilderExtensions.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.CommonTests/Data/EntityFramework/MonitorModelMappingTests.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/db/OmnidotsMonitorDbOptions.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsModelMappingTests.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/postgres/2026-07-15-add-common-durable-alerts.sql`
- Create: `omnidotsmonitor/OmnidotsMonitor/postgres/2026-07-15-rollback-common-durable-alerts.sql`
- Create: `omnidotsmonitor/OmnidotsMonitor/sqlserver/2026-07-15-add-common-durable-alerts.sql`
- Create: `omnidotsmonitor/OmnidotsMonitor/sqlserver/2026-07-15-rollback-common-durable-alerts.sql`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/testdata/create.postgres.sql`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/testdata/reset.postgres.sql`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsAlertMigrationContractTests.cs`

**Interfaces:**
- Consumes: `AlertOccurrenceOutcome` from Task 1 and existing shared monitor/notification entities.
- Produces: mapped `AlertOccurrenceEntity`, `AlertDeliveryOutboxEntity`, and `IMonitorDbContextFactory<TContext>.CreateDbContext()` for Tasks 4 and 5.

- [ ] **Step 1: Write failing provider mapping and migration-contract tests**

Assert both models expose the exact table names, keys, lengths, hashes, timestamps, and indexes:

```csharp
[TestMethod]
public void SharedAlertEntities_MapToBothProviderShapes()
{
    using var sqlServer = CreateContext(MonitorDatabaseProvider.SqlServer);
    using var postgreSql = CreateContext(MonitorDatabaseProvider.PostgreSql);

    AssertAlertOccurrence(sqlServer, "AlertOccurrences", "dbo", "SourceKeyHash", "binary(32)");
    AssertAlertOccurrence(postgreSql, "alert_occurrence", null, "source_key_hash", "bytea");
    AssertAlertOutbox(sqlServer, "AlertDeliveryOutbox", "dbo", "LeaseId", "uniqueidentifier");
    AssertAlertOutbox(postgreSql, "alert_delivery_outbox", null, "lease_id", "uuid");
}
```

Migration tests must parse SQL Server scripts with ScriptDom; require transactional/idempotent forward and rollback scripts; assert the unique occurrence and delivery constraints; assert due-claim indexes; and execute the PostgreSQL forward, rerun, rollback, and rerun against a scoped integration schema.

Implement `AssertAlertOccurrence` and `AssertAlertOutbox` as private test helpers that call `context.Model.FindEntityType`, assert the supplied table/schema, assert every property column/type/max length from the Task 2 mapping list, and inspect `GetIndexes()` for the two named unique indexes plus the due index. Reuse the existing `CreateContext(MonitorDatabaseProvider)` helper already present in `OmnidotsModelMappingTests`.

- [ ] **Step 2: Run mapping and migration tests and confirm RED**

Run:

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore --nologo --filter "FullyQualifiedName~OmnidotsModelMappingTests|FullyQualifiedName~OmnidotsAlertMigrationContractTests"
```

Expected: mapping assertions or compilation fail because the entities and scripts are absent.

- [ ] **Step 3: Add entities, DbSets, mappings, and identifier map entries**

Use these persisted shapes:

```csharp
public sealed class AlertOccurrenceEntity
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public byte[] SourceKeyHash { get; set; } = [];
    public Guid? NotificationId { get; set; }
    public Guid MonitorId { get; set; }
    public string SerialId { get; set; } = string.Empty;
    public DateTime EventTime { get; set; }
    public int AlertType { get; set; }
    public string AlertField { get; set; } = string.Empty;
    public double Level { get; set; }
    public double LimitOn { get; set; }
    public int AveragingPeriod { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class AlertDeliveryOutboxEntity
{
    public Guid Id { get; set; }
    public Guid OccurrenceId { get; set; }
    public string DeliveryKey { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
}

public interface IMonitorDbContextFactory<out TContext>
    where TContext : MonitorDbContextBase
{
    TContext CreateDbContext();
}
```

Add `AlertOccurrences` and `AlertDeliveryOutbox` DbSets to `MonitorDbContextBase`. Map source/serial/field to 128, outcome/kind/status to 32, delivery key to 64, destination to 512, payload to 8192, and last error to 256. Map all instants as PostgreSQL `timestamp with time zone` and SQL Server `datetime2`. Add:

- unique `(Source, SourceKeyHash)` occurrence index;
- unique `DeliveryKey` outbox index;
- due index `(Status, NextAttemptAt, LeaseUntil, CreatedAt)`;
- occurrence-to-monitor and optional occurrence-to-notification restrict FKs; and
- outbox-to-occurrence cascade FK.

Add `AlertOccurrences -> alert_occurrence` and `AlertDeliveryOutbox -> alert_delivery_outbox` to `OmnidotsMonitorDbOptions`.

- [ ] **Step 4: Write exact forward/rollback SQL and fixture schema**

The PostgreSQL forward script must create this logical shape inside `BEGIN`/`COMMIT` and use `CREATE TABLE IF NOT EXISTS`, guarded constraints, and `CREATE INDEX IF NOT EXISTS`:

```sql
CREATE TABLE IF NOT EXISTS alert_occurrence (
    id uuid PRIMARY KEY,
    source varchar(128) NOT NULL,
    source_key_hash bytea NOT NULL CHECK (octet_length(source_key_hash) = 32),
    notification_id uuid NULL REFERENCES notification(id) ON DELETE RESTRICT,
    monitor_id uuid NOT NULL REFERENCES monitor(id) ON DELETE RESTRICT,
    serial_id varchar(128) NOT NULL,
    event_time timestamp with time zone NOT NULL,
    alert_type integer NOT NULL,
    alert_field varchar(128) NOT NULL,
    level double precision NOT NULL,
    limit_on double precision NOT NULL,
    averaging_period integer NOT NULL,
    outcome varchar(32) NOT NULL CHECK (outcome IN ('Accepted','Ignored','Suppressed')),
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT uq_alert_occurrence_source_key UNIQUE (source, source_key_hash)
);

CREATE TABLE IF NOT EXISTS alert_delivery_outbox (
    id uuid PRIMARY KEY,
    occurrence_id uuid NOT NULL REFERENCES alert_occurrence(id) ON DELETE CASCADE,
    delivery_key varchar(64) NOT NULL,
    kind varchar(32) NOT NULL CHECK (kind IN ('MqttAlert','Email','Sms')),
    destination varchar(512) NOT NULL,
    payload varchar(8192) NOT NULL,
    status varchar(32) NOT NULL CHECK (status IN ('Pending','Leased','Completed','DeadLetter')),
    attempt_count integer NOT NULL,
    next_attempt_at timestamp with time zone NOT NULL,
    lease_id uuid NULL,
    lease_until timestamp with time zone NULL,
    completed_at timestamp with time zone NULL,
    last_error varchar(256) NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT uq_alert_delivery_outbox_delivery_key UNIQUE (delivery_key)
);
```

The SQL Server script uses `dbo.AlertOccurrences`, `dbo.AlertDeliveryOutbox`, `binary(32)`, `nvarchar`, `datetime2`, named equivalent constraints, `XACT_ABORT ON`, `TRY/CATCH`, and binary/case-exact checks for status literals. Rollback drops outbox first, then occurrence, and warns that dropping occurrences removes replay protection. Add both tables to the fixture reset before `notification` and `monitor` truncation.

- [ ] **Step 5: Run provider tests and commit Task 2**

Run the Step 2 command, then:

```bash
dotnet build omnidotsmonitor/omnidotsmonitor.sln --no-restore --nologo
git diff --check
```

Expected: provider mapping/migration tests pass and build has zero warnings/errors. Commit:

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Data rvt-monitor-common/Rvt.Monitor.CommonTests/Data omnidotsmonitor/OmnidotsMonitor/api/db/OmnidotsMonitorDbOptions.cs omnidotsmonitor/OmnidotsMonitor/postgres omnidotsmonitor/OmnidotsMonitor/sqlserver omnidotsmonitor/OmnidotsMonitorTests
git commit -m "feat(common): add durable alert persistence schema"
```

---

### Task 3: Add the durable alert application service and persistence port

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/Persistence/AlertCommitRequest.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/Persistence/AlertCommitResult.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/Persistence/IAlertCommitStore.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/DurableAlertService.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Alerts/DurableAlertServiceTests.cs`

**Interfaces:**
- Consumes: Task 1 signal/identity/result and Task 2 persistence boundary.
- Produces: `DurableAlertService : IAlertIngressPort` and `IAlertCommitStore.CommitAsync(AlertCommitRequest, CancellationToken)` for Task 4 and the Omnidots handler in Task 9.

- [ ] **Step 1: Write failing service tests with a capturing store**

Test valid delegation, deterministic identities, returned duplicate flag, cancellation, and every validation boundary:

```csharp
[TestMethod]
public async Task AcceptAsync_CalculatesIdentityAndReturnsStoreResult()
{
    var store = new Mock<IAlertCommitStore>();
    AlertCommitRequest? captured = null;
    store.Setup(x => x.CommitAsync(It.IsAny<AlertCommitRequest>(), It.IsAny<CancellationToken>()))
        .Callback<AlertCommitRequest, CancellationToken>((request, _) => captured = request)
        .ReturnsAsync(new AlertCommitResult(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-8222-8222-222222222222"),
            AlertOccurrenceOutcome.Accepted,
            IsDuplicate: false));
    var timeProvider = new Mock<TimeProvider>();
    timeProvider.Setup(x => x.GetUtcNow()).Returns(
        new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero));
    var service = new DurableAlertService(store.Object, timeProvider.Object);

    var result = await service.AcceptAsync(ValidSignal());

    Assert.IsNotNull(captured);
    CollectionAssert.AreEqual(AlertIdentity.CreateSourceKeyHash("body-digest"), captured.SourceKeyHash);
    Assert.AreEqual(AlertIdentity.CreateNotificationId("omnidots.webhook", captured.SourceKeyHash), captured.NotificationId);
    Assert.IsFalse(result.IsDuplicate);
}
```

Define `ValidSignal()` in this test class as `new AlertSignal("omnidots.webhook", "body-digest", new DateTime(2026, 7, 15, 9, 59, 0, DateTimeKind.Utc), "23423", AlertType.Alert, "vtop x", 12, 10, 0, "Vibration Alert vtop x level=12 limit=10", AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms, TimeSpan.FromMinutes(5))`.

Reject non-UTC event times, non-finite level/limit, blank or oversized source/serial/field/message/source key, unsupported alert type/channel bits, negative averaging period, and nonpositive suppression window before calling the store.

- [ ] **Step 2: Run service tests and confirm RED**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --no-restore --nologo --filter "FullyQualifiedName~DurableAlertServiceTests"
```

Expected: compilation fails because the service and persistence request/port are absent.

- [ ] **Step 3: Implement validation and store delegation**

Use these exact shapes:

```csharp
public sealed record AlertCommitRequest(
    AlertSignal Signal,
    byte[] SourceKeyHash,
    Guid NotificationId,
    DateTime CreatedAt);

public sealed record AlertCommitResult(
    Guid OccurrenceId,
    Guid? NotificationId,
    AlertOccurrenceOutcome Outcome,
    bool IsDuplicate);

public interface IAlertCommitStore
{
    Task<AlertCommitResult> CommitAsync(
        AlertCommitRequest request,
        CancellationToken cancellationToken = default);
}
```

`DurableAlertService.AcceptAsync` validates, hashes `SourceEventKey`, calculates the notification UUIDv8, uses `TimeProvider.GetUtcNow().UtcDateTime`, awaits the store, and maps its result without catch-and-rewrap. Set limits to source/serial/field 128, source event key 512, and message 1024 characters.

- [ ] **Step 4: Verify and commit Task 3**

Run Step 2, Common build, and `git diff --check`. Commit:

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Alerts rvt-monitor-common/Rvt.Monitor.CommonTests/Alerts/DurableAlertServiceTests.cs
git commit -m "feat(common): add durable alert ingress service"
```

---

### Task 4: Implement atomic EF occurrence, policy, notification, and delivery planning

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/AlertDeliveryEnvelope.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/AlertDeliveryIdentity.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/Persistence/AlertPersistenceExceptions.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/Persistence/AlertPersistenceExceptionClassifier.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/Persistence/EfAlertCommitStore.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Alerts/AlertDeliveryIdentityTests.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Alerts/AlertPersistenceExceptionClassifierTests.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/api/db/EntityFramework/OmnidotsMonitorContextFactory.cs`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsAlertCommitStoreTests.cs`

**Interfaces:**
- Consumes: `IAlertCommitStore`, Common mapped entities, `IMonitorDbContextFactory<TContext>`, and `IAlertAcceptancePolicy`.
- Produces: `EfAlertCommitStore<TContext>`, version-1 `AlertDeliveryEnvelope`, duplicate/transient exception classification, and the concrete Omnidots context factory for Tasks 5, 6, and 11.

- [ ] **Step 1: Write failing pure and PostgreSQL transaction tests**

Pure tests cover deterministic delivery keys and provider codes: Npgsql `23505` is duplicate only for `uq_alert_occurrence_source_key`; `40001`/`40P01` are transient; SqlClient 1205/3960 are transient; 2601/2627 are duplicate only when the occurrence constraint is identified.

PostgreSQL tests must seed a monitor, deployment, contract, site user, notification settings, and user, then assert:

```csharp
[TestMethod]
public async Task CommitAsync_AcceptedAlert_CommitsOccurrenceNotificationAndCompleteDeliverySet()
{
    var result = await store.CommitAsync(CommitRequest(
        AlertType.Alert,
        AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms));

    Assert.AreEqual(AlertOccurrenceOutcome.Accepted, result.Outcome);
    Assert.AreEqual(1, await CountAsync("alert_occurrence"));
    Assert.AreEqual(1, await CountAsync("notification"));
    Assert.AreEqual(3, await CountAsync("alert_delivery_outbox"));
    CollectionAssert.AreEquivalent(
        new[] { "MqttAlert", "Email", "Sms" },
        await ReadStringsAsync("SELECT kind FROM alert_delivery_outbox"));
}
```

Also cover ignore with no notification/outbox; repeated caution suppression; caution-to-alert escalation; repeated alert suppression; event-time upper/lower window bounds; no downgrade; contact schedule exclusion; duplicate contact canonicalization; no contacts plus MQTT; exact sequential replay; concurrent identical commit with one winner; forced outbox insert failure rolling back all rows; and missing monitor rolling back the occurrence.

In `OmnidotsAlertCommitStoreTests`, define `CommitRequest` to build the Task 3 request with source `omnidots.webhook`, a fixed 32-byte hash, deterministic notification ID, UTC event/created times, and caller-supplied alert type/channels. Define `CountAsync` and `ReadStringsAsync` as parameterized `NpgsqlCommand` helpers over the scoped fixture connection; accept only hard-coded table/query strings from the test file and never concatenate runtime input.

- [ ] **Step 2: Run focused tests and confirm RED**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --no-restore --nologo --filter "FullyQualifiedName~AlertDeliveryIdentityTests|FullyQualifiedName~AlertPersistenceExceptionClassifierTests"
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore --nologo --filter "FullyQualifiedName~OmnidotsAlertCommitStoreTests"
```

Expected: compilation fails because the EF store, factory, and delivery envelope do not exist.

- [ ] **Step 3: Implement the concrete context factory and persistence classifier**

`OmnidotsMonitorContextFactory.CreateDbContext()` must construct options only through `MonitorDbContextOptionsFactory.CreateOptions<OmnidotsMonitorContext>(connectionString, monitorOptions)` and return a fresh context. It receives connection string and `MonitorDbOptions` in its constructor; it does not expose `DBClient`.

Use explicit exceptions:

```csharp
public sealed class AlertTransientPersistenceException : Exception
{
    public AlertTransientPersistenceException(string message, Exception innerException)
        : base(message, innerException) { }
}

public sealed class AlertOccurrenceConflictException : Exception
{
    public AlertOccurrenceConflictException(Exception innerException)
        : base("The alert occurrence already exists.", innerException) { }
}
```

Classifier output must never include provider messages, SQL, connection values, source keys, or destinations.

- [ ] **Step 4: Implement the serializable atomic commit**

`EfAlertCommitStore<TContext>.CommitAsync` must:

```csharp
await using var context = contextFactory.CreateDbContext();
await using var transaction = await context.Database.BeginTransactionAsync(
    IsolationLevel.Serializable,
    cancellationToken);

context.AlertOccurrences.Add(NewOccurrence(request));
await context.SaveChangesAsync(cancellationToken);

var monitor = await context.Monitors.SingleAsync(
    row => row.SerialId == request.Signal.SerialId,
    cancellationToken);
var recentTypes = await context.Notifications
    .Where(row => row.MonitorId == monitor.Id &&
                  row.NotificationTime >= request.Signal.EventTime - request.Signal.SuppressionWindow &&
                  row.NotificationTime <= request.Signal.EventTime &&
                  (row.AlertType == (int)AlertType.Caution || row.AlertType == (int)AlertType.Alert))
    .Select(row => (AlertType)row.AlertType)
    .ToListAsync(cancellationToken);
var outcome = policy.Evaluate(request.Signal.AlertType, recentTypes);
```

For `Accepted`, set occurrence notification ID, add the existing `NotificationEntity`, query contacts through active deployment → contract → site user → notification setting → user, filter their send window at event time, and add unique pending outbox rows. Always add MQTT when requested. Add email/SMS only when enabled and nonblank. Serialize this exact envelope:

```csharp
public sealed record AlertDeliveryEnvelope(
    int Version,
    Guid NotificationId,
    DateTime Timestamp,
    AlertType AlertType,
    string SerialId,
    int? CustomerId,
    string FleetNr,
    string Message);
```

Set `Version` to 1. Canonical delivery keys are SHA-256 hex of `occurrenceId + NUL + kind + NUL + canonicalDestination`; MQTT uses `alert`, email uses trimmed lowercase invariant, and SMS uses trimmed exact text. Commit once after staging the full graph.

Catch a classified occurrence unique conflict outside the failed transaction, open a new context, read the committed occurrence by source/hash, and return `IsDuplicate=true`. Translate only serialization/deadlock failures into `AlertTransientPersistenceException`; let permanent failures retain a safe top-level message.

- [ ] **Step 5: Verify concurrency/rollback and commit Task 4**

Run both Step 2 commands with the runtime-only PostgreSQL integration connection, then:

```bash
dotnet build rvt-monitors.sln --no-restore --nologo
git diff --check
```

Expected: one concurrent winner, one durable duplicate, atomic rollback tests pass, build is clean. Commit:

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Alerts rvt-monitor-common/Rvt.Monitor.CommonTests/Alerts omnidotsmonitor/OmnidotsMonitor/api/db/EntityFramework/OmnidotsMonitorContextFactory.cs omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsAlertCommitStoreTests.cs
git commit -m "feat(common): commit alerts atomically"
```

---

### Task 5: Add provider-safe fenced outbox claims, outcomes, and cleanup

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/Persistence/ClaimedAlertDelivery.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/Persistence/AlertDeliveryAudit.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/Persistence/IAlertOutboxStore.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/Persistence/AlertOutboxClaimSql.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/Persistence/EfAlertOutboxStore.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Alerts/AlertOutboxClaimSqlTests.cs`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsAlertOutboxStoreTests.cs`

**Interfaces:**
- Consumes: Task 2 outbox/audit mappings and Task 4 context factory/envelope.
- Produces: claim, completion, retry/dead-letter, and cleanup operations for the Task 6 dispatcher.

- [ ] **Step 1: Write failing claim SQL and PostgreSQL fencing tests**

Define the port contract in tests:

```csharp
public interface IAlertOutboxStore
{
    Task<ClaimedAlertDelivery?> ClaimNextDueAsync(DateTime utcNow, TimeSpan lease, CancellationToken cancellationToken = default);
    Task<bool> CompleteAsync(Guid id, Guid leaseId, DateTime completedAt, AlertDeliveryAudit? audit, CancellationToken cancellationToken = default);
    Task<bool> RetryAsync(Guid id, Guid leaseId, DateTime nextAttemptAt, string error, bool deadLetter, AlertDeliveryAudit? audit, CancellationToken cancellationToken = default);
    Task<int> DeleteCompletedBeforeAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}
```

SQL contract tests require PostgreSQL `FOR UPDATE SKIP LOCKED` and SQL Server `UPDLOCK, READPAST, ROWLOCK`, with candidate selection and lease update in one statement/transaction.

Integration tests cover oldest-due ordering, fresh lease ID, attempt increment, two concurrent claimers receiving different rows, expired lease reclaim with a new ID, stale completion/retry returning false, completion plus success audit atomically, final dead-letter plus failure audit atomically, retry clearing lease fields, sanitized 256-character error bound, and cleanup deleting only completed rows older than cutoff.

- [ ] **Step 2: Run focused tests and confirm RED**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --no-restore --nologo --filter "FullyQualifiedName~AlertOutboxClaimSqlTests"
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore --nologo --filter "FullyQualifiedName~OmnidotsAlertOutboxStoreTests"
```

Expected: compilation fails because the outbox port/store are absent.

- [ ] **Step 3: Implement atomic provider claim strategies**

PostgreSQL uses this operation, parameterized through `DbCommand`:

```sql
WITH candidate AS (
    SELECT id
    FROM alert_delivery_outbox
    WHERE (status = 'Pending' AND next_attempt_at <= @now)
       OR (status = 'Leased' AND lease_until <= @now)
    ORDER BY next_attempt_at, created_at, id
    FOR UPDATE SKIP LOCKED
    LIMIT 1
)
UPDATE alert_delivery_outbox AS target
SET status = 'Leased', lease_id = @leaseId, lease_until = @leaseUntil,
    attempt_count = attempt_count + 1
FROM candidate
WHERE target.id = candidate.id
RETURNING target.*;
```

SQL Server uses a CTE selecting `TOP (1)` with `UPDLOCK, READPAST, ROWLOCK`, then `UPDATE candidate ... OUTPUT INSERTED.*`. `AlertOutboxClaimSql.For(provider)` throws for unsupported providers. Materialize all `ClaimedAlertDelivery` fields without logging SQL parameter values.

- [ ] **Step 4: Implement fenced outcomes and cleanup**

Complete and retry predicates must include `Id`, `Status == "Leased"`, and `LeaseId == leaseId`. Affected-row count zero returns false and performs no audit write. A success or final-failure email/SMS audit is inserted in the same transaction as the fenced mutation. Retry sets status to `Pending`, clears lease ID/until, stores the safe error, and sets `NextAttemptAt`; dead letter sets `DeadLetter` and `CompletedAt`. Cleanup deletes only `Completed` rows with `CompletedAt < cutoff`.

- [ ] **Step 5: Verify and commit Task 5**

Run Step 2 commands, Omnidots build, and `git diff --check`. Commit:

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Alerts/Persistence rvt-monitor-common/Rvt.Monitor.CommonTests/Alerts/AlertOutboxClaimSqlTests.cs omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsAlertOutboxStoreTests.cs
git commit -m "feat(common): fence durable alert deliveries"
```

---

### Task 6: Add dispatcher, MQTT/email/SMS adapters, options, and API background worker

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/DurableAlertOptions.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/DurableAlertOptionsValidator.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/IAlertDeliveryAdapter.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/MqttAlertDeliveryAdapter.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/EmailAlertDeliveryAdapter.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/SmsAlertDeliveryAdapter.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/DurableAlertDispatcher.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/DurableAlertCleanupService.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/DurableAlertBackgroundService.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/AlertServiceCollectionExtensions.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Alerts/DurableAlertOptionsTests.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Alerts/AlertDeliveryAdapterTests.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Alerts/DurableAlertDispatcherTests.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Alerts/DurableAlertBackgroundServiceTests.cs`

**Interfaces:**
- Consumes: `IAlertOutboxStore`, `IMonitorEventPublisher`, `IMessageService`, existing `RvtContactDto`, and existing notification audit constants.
- Produces: registered Common durable-alert runtime services for Task 11.

- [ ] **Step 1: Write failing options, adapter, dispatcher, and worker tests**

Assert exact defaults and reject nonpositive values, timeout greater than/equal to lease, retry cap below initial delay, nonpositive retention, and non-absolute portal base URL.

Adapter tests deserialize only version 1, reject other versions safely, and verify:

```csharp
mqttPublisher.Verify(x => x.PublishAlertAsync(
    envelope.Timestamp,
    envelope.SerialId,
    envelope.Message,
    envelope.CustomerId), Times.Once);

messageService.Verify(x => x.SendMessageAsync(
    MessageService.MessageContent.MessageEnum.Alert,
    MessageService.MessageContent.MessageTypeEnum.Email,
    It.Is<RvtContactDto>(contact => contact.EmailAddress == "ops@example.test"),
    envelope.FleetNr,
    $"https://portal.example/Notification/View/{envelope.NotificationId}",
    It.IsAny<CancellationToken>()), Times.Once);
```

Dispatcher tests cover empty queue, batch cap 50, 90-second linked timeout, adapter selection, missing adapter, malformed envelope, cancellation propagation, exponential delays `30, 60, 120, ... 1800`, ownership loss, continuation after a dead letter, final aggregate failure, redacted destination logging, and no raw exception text in stored errors.

Worker tests call an internal `RunIterationAsync(DateTime utcNow, CancellationToken)` seam and assert it runs only when `MonitorApi:Enabled=true` and Quartz is disabled, dispatches one bounded pass, performs cleanup at most once per UTC day, logs safe failures and continues, and exits immediately in one-shot/scheduler modes. The production loop calls that seam immediately, then uses `Task.Delay(TimeSpan.FromSeconds(options.PollIntervalSeconds), TimeProvider.System, cancellationToken)`.

- [ ] **Step 2: Run focused Common tests and confirm RED**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --no-restore --nologo --filter "FullyQualifiedName~DurableAlertOptionsTests|FullyQualifiedName~AlertDeliveryAdapterTests|FullyQualifiedName~DurableAlertDispatcherTests|FullyQualifiedName~DurableAlertBackgroundServiceTests"
```

Expected: compilation fails because runtime delivery types are absent.

- [ ] **Step 3: Implement validated options and delivery adapters**

Use:

```csharp
public sealed class DurableAlertOptions
{
    public const string SectionName = "Alerts:DurableDelivery";
    public int BatchSize { get; set; } = 50;
    public int LeaseSeconds { get; set; } = 120;
    public int DeliveryTimeoutSeconds { get; set; } = 90;
    public int InitialRetrySeconds { get; set; } = 30;
    public int MaxRetrySeconds { get; set; } = 1800;
    public int MaxAttempts { get; set; } = 8;
    public int PollIntervalSeconds { get; set; } = 60;
    public int CompletedRetentionDays { get; set; } = 90;
    public string PortalBaseUrl { get; set; } = "https://www.rvtcloud.com/";
}

public interface IAlertDeliveryAdapter
{
    string Kind { get; }
    Task<AlertDeliveryAudit?> DeliverAsync(
        ClaimedAlertDelivery delivery,
        CancellationToken cancellationToken);
}
```

MQTT calls `PublishAlertAsync(...).WaitAsync(cancellationToken)` and returns no audit. Email/SMS call `SendMessageAsync` and return an audit with `NotificationConstants.SENT_OK`. All adapters validate kind, destination, envelope version, and notification ID before external calls.

- [ ] **Step 4: Implement dispatcher, cleanup, worker, and DI registration**

For each bounded iteration, claim one message, create a linked timeout token, deliver, and fence completion. On failure calculate:

```csharp
var exponent = Math.Max(0, message.AttemptCount - 1);
var seconds = Math.Min(
    options.InitialRetrySeconds * Math.Pow(2, exponent),
    options.MaxRetrySeconds);
var deadLetter = message.AttemptCount >= options.MaxAttempts;
var safeError = $"Alert delivery failed ({exception.GetType().Name}).";
```

Use `SensitiveLogRedactor.Redact(destination)` in logs. Final email/SMS failure audit uses the safe error. Aggregate newly dead-lettered IDs only after the batch is processed.

`AddDurableAlerts<TContext>()` binds/validates options, registers the pure policy, `EfAlertCommitStore<TContext>`, `EfAlertOutboxStore<TContext>`, `IAlertIngressPort`, all three adapters, dispatcher, cleanup service, and background hosted service. It must not register a context factory; the monitor supplies that adapter.

- [ ] **Step 5: Verify MQTT compatibility and commit Task 6**

Add a real `MonitorEventPublisher` assertion that captured MQTT JSON deserializes to the unchanged `RvtMqttMessage` timestamp/customer/serial/message fields and uses the configured alert topic. Run Step 2, complete Common tests, Common build, and `git diff --check`. Commit:

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Alerts rvt-monitor-common/Rvt.Monitor.CommonTests/Alerts
git commit -m "feat(common): dispatch durable alerts through adapters"
```

---

### Task 7: Add API-only Omnidots security options and fail-closed cryptographic guards

**Files:**
- Create: `omnidotsmonitor/OmnidotsMonitor/model/config/OmnidotsApiSecurityOptions.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/model/config/OmnidotsApiSecurityOptionsValidator.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/OmnidotsApiSecurityGuard.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/OmnidotsWebhookSignatureValidator.cs`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/Config/OmnidotsApiSecurityOptionsTests.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/UseCases/OmnidotsWebhookSignatureValidatorTests.cs`

**Interfaces:**
- Consumes: `IConfiguration` execution mode and legacy `RVT__OMNIDOTS_*` configuration aliases.
- Produces: validated `OmnidotsApiSecurityOptions`, `OmnidotsApiSecurityGuard`, and strong-key signature validation for Tasks 8–11.

- [ ] **Step 1: Write failing mode-aware validation and direct-guard tests**

Cover API-enabled missing/blank/31-byte/equal secrets; strict UTF-8 byte counting; invalid Unicode; HTTP/relative URL; zero concurrency; nonpositive notification delay; and generic errors that omit values. Assert API-disabled scheduler and unrelated one-shot configurations succeed without secrets. Assert direct guards throw a value-free `InvalidOperationException`, and signature validation returns false for blank/short keys even with a mathematically matching HMAC.

- [ ] **Step 2: Run security tests and confirm RED**

Run:

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore --nologo --filter "FullyQualifiedName~OmnidotsApiSecurityOptionsTests|FullyQualifiedName~OmnidotsWebhookSignatureValidatorTests"
```

Expected: validation cases fail because current options accept empty keys and no mode-aware validator exists.

- [ ] **Step 3: Implement options, validator, and guard**

Use:

```csharp
public sealed class OmnidotsApiSecurityOptions
{
    public const string SectionName = "Omnidots:Api";
    public string WebhookUrl { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string ConfigSecret { get; set; } = string.Empty;
    public int NotificationDelayMinutes { get; set; } = 5;
    public int WebhookConcurrencyLimit { get; set; } = 8;
    public int ConfigureConcurrencyLimit { get; set; } = 2;
}
```

The validator receives `IConfiguration`; it returns success immediately when `MonitorApi:Enabled` is false. In API mode use `new UTF8Encoding(false, true)` to require at least 32 bytes and reject invalid surrogate sequences, compare secret bytes for distinctness, require `UriKind.Absolute` plus `Uri.UriSchemeHttps`, and return only fixed messages such as `"Omnidots API security configuration is invalid."`.

The guard exposes `EnsureWebhookReady(options)` and `EnsureConfigurationReady(options)` and repeats the relevant checks without exposing values. Register legacy `RVT__OMNIDOTS_WEBHOOK_URL`, `RVT__OMNIDOTS_WEBHOOK_SECRET`, `RVT__OMNIDOTS_CONFIG_SECRET`, and `RVT__NOTIFICATION_DELAY_MINUTES` as fallback aliases when section values are absent.

- [ ] **Step 4: Harden signature validation**

Accept only exact `sha256=` plus 64 hex characters. Parse to 32 bytes, calculate HMAC-SHA256 over the supplied `ReadOnlySpan<byte>`, and compare with `CryptographicOperations.FixedTimeEquals`. Do not trim the signature. Return false for invalid configured keys before HMAC calculation.

- [ ] **Step 5: Verify and commit Task 7**

Run Step 2, Omnidots build, and `git diff --check`. Keep the old options file unchanged until Task 11 removes its final facade call sites.

```bash
git add omnidotsmonitor/OmnidotsMonitor/model/config omnidotsmonitor/OmnidotsMonitor/api/UseCases/OmnidotsApiSecurityGuard.cs omnidotsmonitor/OmnidotsMonitor/api/UseCases/OmnidotsWebhookSignatureValidator.cs omnidotsmonitor/OmnidotsMonitorTests/Config omnidotsmonitor/OmnidotsMonitorTests/UseCases/OmnidotsWebhookSignatureValidatorTests.cs
git commit -m "fix(omnidots): fail closed on API security settings"
```

---

### Task 8: Replace measuring-point configuration with fixed-time typed handling

**Files:**
- Create: `omnidotsmonitor/OmnidotsMonitor/model/dto/ConfigureMeasuringPointRequest.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/model/dto/ConfigureMeasuringPointResult.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/OmnidotsConfigurationAuthenticationException.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/OmnidotsVendorConfigurationException.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/OmnidotsFixedTimeSecretComparer.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/ConfigureMeasuringPointHandler.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/http/OmnidotsHttpGateway.cs`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/UseCases/ConfigureMeasuringPointHandlerTests.cs`

**Interfaces:**
- Consumes: Task 7 security options/guard and existing narrow Omnidots monitor queries/gateway.
- Produces: `ConfigureMeasuringPointHandler.RunAsync(ReadOnlyMemory<byte>, CancellationToken)` returning only `ConfigureMeasuringPointResult(bool Configured)` for Task 10.

- [ ] **Step 1: Write failing authentication, schema, and outbound-request tests**

Cover malformed JSON 400-class exception; missing/wrong secret authentication exception; wrong secret taking precedence over invalid business fields; valid secret plus unknown member; valid secret plus `webhook`; blank serial; NaN/infinite/out-of-range tuning values; fixed-time hash helper; deployed HTTPS URL/secret always used; vendor false/network/invalid response wrapped without raw body; cancellation; and exact result JSON containing only `configured`.

Capture the outbound `ConfigRequest` and assert:

```csharp
Assert.AreEqual("https://alerts.example.test/webhook", request.WebhookRecipient.Url);
Assert.AreEqual(strongWebhookSecret, request.WebhookRecipient.Secret);
Assert.IsFalse(responseJson.Contains("serial", StringComparison.OrdinalIgnoreCase));
Assert.AreEqual("{\"configured\":true}", responseJson);
```

- [ ] **Step 2: Run handler tests and confirm RED**

Run:

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore --nologo --filter "FullyQualifiedName~ConfigureMeasuringPointHandlerTests"
```

Expected: old dynamic handler accepts webhook override, uses ordinal string comparison, and returns serial ID.

- [ ] **Step 3: Implement typed request and fixed-time authentication order**

Use `[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]` and exact JSON names:

```csharp
public sealed class ConfigureMeasuringPointRequest
{
    [JsonPropertyName("secret")] public string? Secret { get; init; }
    [JsonPropertyName("serialid")] public string? SerialId { get; init; }
    [JsonPropertyName("trace_save_level")] public double? TraceSaveLevel { get; init; }
    [JsonPropertyName("trace_pre_trigger")] public double? TracePreTrigger { get; init; }
    [JsonPropertyName("trace_post_trigger")] public double? TracePostTrigger { get; init; }
    [JsonPropertyName("flat_level")] public double? FlatLevel { get; init; }
    [JsonPropertyName("level_alert")] public double? LevelAlert { get; init; }
    [JsonPropertyName("level_caution")] public double? LevelCaution { get; init; }
}

public sealed record ConfigureMeasuringPointResult(bool Configured);
```

Handler order is: security guard → strict `JsonDocument.Parse` syntax check → extract `secret` → compare `SHA256(UTF8(supplied))` and `SHA256(UTF8(configured))` with `FixedTimeEquals` → strict typed deserialize → validate serial/numbers → build vendor DTO using only deployed URL/secret → await vendor calls → return `new(true)`.

- [ ] **Step 4: Add awaited gateway methods and safe vendor classification**

Add `AuthenticateAsync(CancellationToken)` and `ConfigureMeasuringPointAsync(token, serial, json, CancellationToken)` using the existing `IHttpClient` tasks plus `WaitAsync(cancellationToken)`. Preserve existing synchronous gateway methods for scheduled import callers. The configuration handler wraps vendor/network/parse failures in `OmnidotsVendorConfigurationException("Measuring point configuration failed.")` without retaining a printable vendor body.

- [ ] **Step 5: Verify and commit Task 8**

Run Step 2, existing configuration endpoint/API tests that still compile, Omnidots build, and `git diff --check`. Commit:

```bash
git add omnidotsmonitor/OmnidotsMonitor/model/dto omnidotsmonitor/OmnidotsMonitor/api/UseCases/ConfigureMeasuringPointHandler.cs omnidotsmonitor/OmnidotsMonitor/api/UseCases/OmnidotsConfigurationAuthenticationException.cs omnidotsmonitor/OmnidotsMonitor/api/UseCases/OmnidotsVendorConfigurationException.cs omnidotsmonitor/OmnidotsMonitor/api/UseCases/OmnidotsFixedTimeSecretComparer.cs omnidotsmonitor/OmnidotsMonitor/api/http/OmnidotsHttpGateway.cs omnidotsmonitor/OmnidotsMonitorTests/UseCases/ConfigureMeasuringPointHandlerTests.cs
git commit -m "fix(omnidots): type and secure measuring point configuration"
```

---

### Task 9: Translate strict Omnidots alarms into the Common durable ingress

**Files:**
- Modify: `omnidotsmonitor/OmnidotsMonitor/model/json/AlarmV2.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/OmnidotsAlarmTranslator.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/ProcessWebhookHandler.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestAlarms.cs`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/UseCases/OmnidotsAlarmTranslatorTests.cs`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/UseCases/ProcessWebhookHandlerTests.cs`

**Interfaces:**
- Consumes: Task 1 `AlertSignal`, Task 3 `IAlertIngressPort`, Task 7 security/signature components, and vendor `AlarmDataV2`.
- Produces: byte-only `ProcessWebhookHandler.RunAsync(ReadOnlyMemory<byte>, string, CancellationToken)` for Task 10.

- [ ] **Step 1: Write failing strict schema and translation tests**

Cover missing data/alarms/axes/x/y/z/vtop; zero/negative measuring point; non-finite timestamp/value/threshold; unrepresentable Unix time; negative threshold; maximum-axis tie behavior; exact alarm-level boundary; ignore/caution/alert channels; invariant MQTT message; and body digest source key.

The accepted alert translation must equal:

```csharp
Assert.AreEqual("omnidots.webhook", signal.Source);
Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(body)), signal.SourceEventKey);
Assert.AreEqual("23423", signal.SerialId);
Assert.AreEqual(AlertType.Alert, signal.AlertType);
Assert.AreEqual("vtop x", signal.Field);
Assert.AreEqual(
    AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms,
    signal.DeliveryChannels);
Assert.AreEqual("Vibration Alert vtop x level=12 limit=10", signal.Message);
```

Handler tests prove HMAC authenticates the original bytes before UTF-8/JSON work, BOM is authenticated then removed once, invalid UTF-8 after valid authentication is rejected, mutation fails authentication, the exact source digest reaches ingress, cancellation propagates, and a durable duplicate result is returned unchanged.

- [ ] **Step 2: Run translator/handler tests and confirm RED**

Run:

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore --nologo --filter "FullyQualifiedName~OmnidotsAlarmTranslatorTests|FullyQualifiedName~ProcessWebhookHandlerTests"
```

Expected: missing nested data default to zero-filled objects and the old handler writes/sends through `OmnidotsRuleProcessor` instead of `IAlertIngressPort`.

- [ ] **Step 3: Make vendor DTO absence observable and implement translator**

Change deserialization-only nested references in `AlarmDataV2` to nullable with no `new()` defaults. Remove `GetNotification`; the translator manually validates and selects the maximum Vtop axis using current deterministic tie behavior. Convert top-level Unix seconds to a checked UTC `DateTime`, require finite values, determine threshold/severity, and format level/limit with `CultureInfo.InvariantCulture` using the exact message in Step 1.

Use no Common persistence, MQTT, message-service, or database type in the translator.

- [ ] **Step 4: Replace the webhook handler with byte-only durable ingress**

Use this public method only:

```csharp
public async Task<AlertIngressResult> RunAsync(
    ReadOnlyMemory<byte> body,
    string signature,
    CancellationToken cancellationToken = default)
```

Call `EnsureWebhookReady`, validate HMAC over `body.Span`, strictly decode with `new UTF8Encoding(false, true)`, remove one BOM only after authentication, deserialize, translate with the configured suppression window, and await `IAlertIngressPort.AcceptAsync`. Remove monitor/rule queries, `OmnidotsRuleProcessor`, direct notification writes, contact reads, and communication from this handler. Do not add a string overload.

- [ ] **Step 5: Verify and commit Task 9**

Run Step 2, all existing alarm/signature tests, Omnidots build, and `git diff --check`. Commit:

```bash
git add omnidotsmonitor/OmnidotsMonitor/model/json/AlarmV2.cs omnidotsmonitor/OmnidotsMonitor/api/UseCases/OmnidotsAlarmTranslator.cs omnidotsmonitor/OmnidotsMonitor/api/UseCases/ProcessWebhookHandler.cs omnidotsmonitor/OmnidotsMonitorTests/TestAlarms.cs omnidotsmonitor/OmnidotsMonitorTests/UseCases
git commit -m "feat(omnidots): route webhook alarms through durable ingress"
```

---

### Task 10: Enforce bounded HTTP semantics, exact signatures, and rate limits

**Files:**
- Create: `omnidotsmonitor/OmnidotsMonitor/api/Http/BoundedJsonRequestReader.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/api/Http/OmnidotsRequestExceptions.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsRateLimiterOptionsSetup.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/MonitorApiEndpoints.cs`
- Create: `omnidotsmonitor/OmnidotsMonitor/model/dto/ProcessWebhookResult.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestMonitorApiEndpoints.cs`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/Http/BoundedJsonRequestReaderTests.cs`

**Interfaces:**
- Consumes: Task 8 configuration handler, Task 9 webhook handler, Task 7 options, and Common `AlertTransientPersistenceException`.
- Produces: complete external HTTP contract and safe Problem Details behavior for Task 11 real-DI composition.

- [ ] **Step 1: Write failing reader and real TestHost endpoint tests**

Reader tests cover media type with charset, missing media type, text/plain, identity/no encoding, gzip encoding, declared 65,537 bytes, exactly 65,536 bytes, streamed/chunked 65,537th byte, early cancellation, and no unbounded `CopyToAsync` path.

Endpoint tests cover exactly one signature value, comma-combined/multiple/missing/blank/malformed/mismatch 401; authenticated malformed UTF-8/JSON/schema 400; fresh and duplicate 200 with exact `{\"processed\":true}`; transient persistence 503; permanent 500; configured endpoint 401/400/413/415/429/502/500; exact `{\"configured\":true}`; and captured logs/problem bodies excluding supplied sentinel body, signature, secrets, vendor response, destination, and raw exception.

- [ ] **Step 2: Run HTTP tests and confirm RED**

Run:

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore --nologo --filter "FullyQualifiedName~BoundedJsonRequestReaderTests|FullyQualifiedName~TestMonitorApiEndpoints"
```

Expected: current endpoints read unbounded bodies, choose the first signature, return wrong status/result bodies, and have no rate limit.

- [ ] **Step 3: Implement the bounded transport reader**

`BoundedJsonRequestReader.ReadAsync(HttpRequest, CancellationToken)` must:

```csharp
public const int MaxBodyBytes = 64 * 1024;

if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType) ||
    !string.Equals(contentType.MediaType.Value, "application/json", StringComparison.OrdinalIgnoreCase))
    throw new OmnidotsUnsupportedMediaTypeException();
if (request.Headers.TryGetValue("Content-Encoding", out var encodings) &&
    (encodings.Count != 1 ||
     !string.Equals(encodings[0], "identity", StringComparison.OrdinalIgnoreCase)))
{
    throw new OmnidotsUnsupportedMediaTypeException();
}
if (request.ContentLength > MaxBodyBytes)
    throw new OmnidotsRequestBodyTooLargeException();
```

Rent a 65,537-byte buffer from `ArrayPool<byte>`, read until EOF or the extra byte, throw 413 on the extra byte, return a right-sized byte array, clear the used portion, and return the rented buffer in `finally`.

- [ ] **Step 4: Implement rate limiting and safe endpoint mapping**

Register named concurrency policies `OmnidotsWebhook` and `OmnidotsConfigure` through `IConfigureOptions<RateLimiterOptions>` using validated limits, `QueueLimit=0`, `QueueProcessingOrder=OldestFirst`, and 429 Problem Details.

Change the map extension receiver to `WebApplication`, call `UseRateLimiter`, and require the policies on the matching routes. Reject `StringValues.Count != 1`; exact signature grammar then rejects comma-combined values. Map exceptions exactly as the design specifies and never use `exception.Message` in responses/log templates. Return:

```csharp
public sealed record ProcessWebhookResult(bool Processed);

return TypedResults.Ok(new ProcessWebhookResult(Processed: true));
```

Move body reading inside the try/catch. Remove `OmnidotsApi.ReadRequestBody` and `ReadRequestBodyBytes` after all endpoint tests use the bounded reader.

- [ ] **Step 5: Verify status matrix, leakage scans, and commit Task 10**

Run Step 2 and:

```bash
rg -n "ReadToEndAsync|CopyToAsync|FirstOrDefault\(\)" omnidotsmonitor/OmnidotsMonitor/api/MonitorApiEndpoints.cs omnidotsmonitor/OmnidotsMonitor/api/Http
dotnet build omnidotsmonitor/omnidotsmonitor.sln --no-restore --nologo
git diff --check
```

Expected: the search finds no unbounded endpoint reader or first-value signature selection. Commit:

```bash
git add omnidotsmonitor/OmnidotsMonitor/api/Http omnidotsmonitor/OmnidotsMonitor/api/MonitorApiEndpoints.cs omnidotsmonitor/OmnidotsMonitor/api/OmnidotsRateLimiterOptionsSetup.cs omnidotsmonitor/OmnidotsMonitor/model/dto/ProcessWebhookResult.cs omnidotsmonitor/OmnidotsMonitorTests/Http omnidotsmonitor/OmnidotsMonitorTests/TestMonitorApiEndpoints.cs
git commit -m "fix(omnidots): bound and rate limit webhook endpoints"
```

---

### Task 11: Compose Common alerts into Omnidots and expose worker, Quartz, and one-shot jobs

**Files:**
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsMonitorServices.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsApi.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsService.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/MonitorJobRunner.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/MonitorJobDispatcher.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/Program.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/appsettings.json`
- Delete: `omnidotsmonitor/OmnidotsMonitor/model/config/OmnidotsWebhookOptions.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestUtil.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestOmnidotsApi.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestMonitorJobScheduling.cs`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/Architecture/OmnidotsAlertArchitectureTests.cs`
- Create: `omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsWebhookEndToEndTests.cs`

**Interfaces:**
- Consumes: all Common alert services and Omnidots boundary handlers from Tasks 1–10.
- Produces: deployable Omnidots API/worker/scheduler/one-shot behavior and the final architecture boundary.

- [ ] **Step 1: Write failing DI, scheduling, architecture, and end-to-end tests**

Assert API DI resolves both handlers, `IAlertIngressPort`, concrete EF stores, three distinct delivery adapters, publisher, and context factory. Assert API-disabled DI validation does not resolve secret failures; API-enabled startup does.

Scheduling assertions:

```csharp
Assert.IsTrue(jobs.Any(job => job.Name == "DispatchAlerts" && job.Cron == "0 0/1 * * * ?"));
Assert.IsTrue(jobs.Any(job => job.Name == "CleanupAlerts" && job.Cron == "0 15 3 * * ?"));
Assert.IsTrue(dispatcher.SupportedJobNames.Contains("DispatchAlerts"));
Assert.IsTrue(dispatcher.SupportedJobNames.Contains("CleanupAlerts"));
```

One-shot tests verify `DispatchAlerts` resolves only the Common dispatcher and returns 0; a dead-letter aggregate returns exit code 1 through `MonitorHost`; `CleanupAlerts` invokes the 90-day cleanup.

Architecture tests inspect production source/constructors and assert no webhook method accepts `string`, endpoints inject focused handlers rather than `OmnidotsService`, `ProcessWebhookHandler` has no rule/database/message/MQTT dependency, `Rvt.Monitor.Common` has no Omnidots reference, and new alert code never calls broad `IDBClient`.

The PostgreSQL end-to-end test starts a real TestHost with the production endpoints, handlers, and EF stores, sends the same valid signed raw body concurrently in two HTTP requests, and asserts one occurrence, one notification, one MQTT row, each enabled email/SMS destination once, both responses are 200 with `{ "processed": true }`, and replay creates nothing else.

- [ ] **Step 2: Run focused composition tests and confirm RED**

Run:

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore --nologo --filter "FullyQualifiedName~TestMonitorJobScheduling|FullyQualifiedName~OmnidotsAlertArchitectureTests|FullyQualifiedName~OmnidotsWebhookEndToEndTests"
```

Expected: DI/schedules/jobs are absent and legacy facade/service string webhook methods violate architecture tests.

- [ ] **Step 3: Register the Common alert slice and focused endpoint handlers**

In `AddOmnidotsMonitor` register:

```csharp
services.AddSingleton<IMonitorDbContextFactory<OmnidotsMonitorContext>>(
    _ => new OmnidotsMonitorContextFactory(
        RvtConfig.DB_CONNECTION_STRING,
        OmnidotsMonitorDbOptions.Current));
services.AddSingleton<IMonitorEventPublisher>(provider => new MonitorEventPublisher(
    provider.GetRequiredService<IMqttClient>(),
    RvtConfig.INSERT_TOPIC,
    RvtConfig.ALERT_TOPIC));
services.AddDurableAlerts<OmnidotsMonitorContext>();
services.AddSingleton<OmnidotsAlarmTranslator>();
services.AddSingleton<ProcessWebhookHandler>();
services.AddSingleton<ConfigureMeasuringPointHandler>();
```

Bind security options from `Omnidots:Api`, apply legacy secret/url aliases only when section values are blank, register the mode-aware validator with `ValidateOnStart`, and register rate-limiter setup. Set Common `PortalBaseUrl` from `RvtConfig.PORTAL_BASE_URL` without logging it.

Remove configure/webhook handler construction and methods from `OmnidotsApi` and `OmnidotsService`; scheduled battery/offline paths may retain `OmnidotsRuleProcessor`. Delete both public string/byte webhook facade overloads and the old options record.

- [ ] **Step 4: Wire one-shot, Quartz, API background dispatch, and cleanup**

Change `MonitorJobRunner.RunAsync` to accept `IServiceProvider`, resolve `OmnidotsService` only for legacy jobs, resolve `DurableAlertDispatcher` for `DispatchAlerts`, and resolve `DurableAlertCleanupService` for `CleanupAlerts`. Update Program and Quartz dispatcher to pass the provider while preserving the parameterless constructor used only for schedule validation.

Add to `appsettings.json`:

```json
"Omnidots": {
  "Api": {
    "NotificationDelayMinutes": 5,
    "WebhookConcurrencyLimit": 8,
    "ConfigureConcurrencyLimit": 2
  }
},
"Alerts": {
  "DurableDelivery": {
    "BatchSize": 50,
    "LeaseSeconds": 120,
    "DeliveryTimeoutSeconds": 90,
    "InitialRetrySeconds": 30,
    "MaxRetrySeconds": 1800,
    "MaxAttempts": 8,
    "PollIntervalSeconds": 60,
    "CompletedRetentionDays": 90
  }
}
```

Add enabled Quartz jobs `DispatchAlerts` at `0 0/1 * * * ?` and `CleanupAlerts` at `0 15 3 * * ?`. The Common background worker self-enables only for API-without-Quartz, so no process registers two dispatch loops.

- [ ] **Step 5: Run end-to-end and full Omnidots verification, then commit Task 11**

Run Step 2 with the runtime-only PostgreSQL connection, then:

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore --nologo
dotnet build omnidotsmonitor/omnidotsmonitor.sln --no-restore --nologo
git diff --check
```

Expected: complete Omnidots suite passes, including the real duplicate race, and build reports zero warnings/errors. Commit:

```bash
git add omnidotsmonitor/OmnidotsMonitor omnidotsmonitor/OmnidotsMonitorTests
git commit -m "feat(omnidots): compose common durable alerts"
```

---

### Task 12: Document operations, verify both providers, and perform final strict review

**Files:**
- Modify: `omnidotsmonitor/README.md`
- Modify: `project_state.md`
- Modify only when review finds a regression: files introduced or changed in Tasks 1–11 and their focused tests.

**Interfaces:**
- Consumes: complete implementation and approved design.
- Produces: deployment-ready documentation, evidence, and final reviewed branch.

- [ ] **Step 1: Update Omnidots operational documentation**

Document these exact contracts:

- API-only `RVT__OMNIDOTS_WEBHOOK_URL`, `RVT__OMNIDOTS_WEBHOOK_SECRET`, and `RVT__OMNIDOTS_CONFIG_SECRET` requirements;
- 32 strict UTF-8 bytes, distinct secrets, HTTPS URL, safe rotation order;
- 64 KiB JSON/content-encoding/signature/status matrix;
- `{ "processed": true }` and `{ "configured": true }` responses;
- exact-body duplicate guarantees and at-least-once provider limitation;
- forward migration before app deployment and writer/dispatcher shutdown before rollback;
- `--job DispatchAlerts`, Quartz one-minute dispatch, API worker behavior, daily cleanup, retry/dead-letter semantics, and 90-day completed retention; and
- no secret/body/vendor response logging.

- [ ] **Step 2: Run formatter, Roslyn build, Common tests, and Omnidots tests**

Run separately and record exact totals/output in `project_state.md`:

```bash
dotnet format rvt-monitors.sln --verify-no-changes --no-restore --verbosity minimal
dotnet build rvt-monitors.sln --no-restore --nologo
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --no-restore --nologo
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore --nologo
git diff --check
```

Expected: formatter exits 0; build has 0 warnings/0 errors; both complete suites pass; diff check is silent. If solution-level parallel tests encounter the documented Docker contention, keep the sequential Common/Omnidots evidence and do not misreport the environmental failure as passing.

- [ ] **Step 3: Verify migrations and run security leakage scans**

Run PostgreSQL migration integration tests and SQL Server parse/model contracts. Then search production/tests/docs:

```bash
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore --nologo --filter "FullyQualifiedName~OmnidotsAlertMigrationContractTests|FullyQualifiedName~OmnidotsModelMappingTests|FullyQualifiedName~OmnidotsAlertCommitStoreTests|FullyQualifiedName~OmnidotsAlertOutboxStoreTests|FullyQualifiedName~OmnidotsWebhookEndToEndTests"
rg -n "WEBHOOK_SECRET|CONFIG_SECRET|x-omnidots-notifier-signature|ReadToEndAsync|CopyToAsync|ProcessWebhook\(string|Webhook\(string|Dictionary<string, (object|dynamic)>|FirstOrDefault\(\)" omnidotsmonitor rvt-monitor-common
```

Expected: tests pass. Search hits are limited to configuration names, negative architecture tests, bounded-reader assertions, documentation, and intentional legacy code outside the endpoint; no secret values, raw-body logger, string webhook path, dynamic configuration request, or first-signature selection remains.

- [ ] **Step 4: Perform a fresh strict code review against the specification**

Use `superpowers:requesting-code-review` on `git diff cfc977e..HEAD`. Review for security order, exact raw bytes, startup bypasses, duplicate races, transaction boundaries, SQL provider safety, contact/delivery completeness, lease fencing, cancellation, retry/dead-letter behavior, sensitive output, schedule duplication, architecture direction, and test realism.

For each actionable finding, add a focused failing regression, observe RED, implement the smallest correction, and rerun the focused test. After all review fixes pass, commit the reviewed source/test corrections separately:

```bash
git add -u rvt-monitor-common omnidotsmonitor
git add rvt-monitor-common/Rvt.Monitor.CommonTests omnidotsmonitor/OmnidotsMonitorTests
git commit -m "fix(alerts): address final review findings"
```

If review has no actionable finding, skip this commit.

- [ ] **Step 5: Update project state and make the final verification commit**

Record branch/worktree, migration names, actual test totals, formatter/build results, provider coverage, external at-least-once limitation, and any remaining operational prerequisite. Do not record connection strings, secret values, real contact destinations, raw payloads, or vendor responses.

Commit:

```bash
git add omnidotsmonitor/README.md project_state.md
git commit -m "docs(omnidots): record durable alert verification"
```

Finally rerun `git status --short --branch` and `git diff --check HEAD^`. The only untracked file allowed to remain is the pre-existing `.superpowers/sdd/.gitignore`.
