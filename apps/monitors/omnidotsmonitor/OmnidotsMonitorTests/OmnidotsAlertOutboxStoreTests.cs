using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using NpgsqlTypes;
using Omnidots.Api.Db.EntityFramework;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.IntegrationTesting;

namespace OmnidotsAdapterTests;

[TestClass]
[TestCategory("PostgreSqlIntegration")]
public sealed class OmnidotsAlertOutboxStoreTests
{
    private static readonly DateTime _utcNow =
        new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid _monitorId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid _notificationId =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid _occurrenceId =
        Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static PostgreSqlIntegrationDatabase? _database;
    private EfAlertOutboxStore<OmnidotsMonitorContext> _store = null!;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(45));
        _database = await PostgreSqlIntegrationDatabase.CreateAsync(
            OmnidotsAdapterTests.TestUtil.ReadTextFromFile("testdata/create.postgres.sql"),
            OmnidotsAdapterTests.TestUtil.ReadTextFromFile("testdata/reset.postgres.sql"),
            timeout.Token);
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        await _database!.ResetAsync(
            OmnidotsAdapterTests.TestUtil.ReadTextFromFile("testdata/reset.postgres.sql"),
            TestContext.CancellationToken);
        await SeedAlertGraphAsync();

        MonitorDbOptions monitorOptions = new(
            new Dictionary<string, string>());
        _store = new EfAlertOutboxStore<OmnidotsMonitorContext>(
            new OmnidotsMonitorContextFactory(_database.ConnectionString, monitorOptions));
    }

    [TestMethod]
    public async Task ClaimNextDueAsync_ClaimsOldestDueAndMaterializesAllFields()
    {
        Guid oldestId = Guid.NewGuid();
        Guid newerId = Guid.NewGuid();
        await InsertOutboxAsync(
            oldestId,
            "Email",
            "ops@example.test",
            "{\"version\":1}",
            "Pending",
            _utcNow.AddMinutes(-10),
            attemptCount: 2,
            createdAt: _utcNow.AddMinutes(-20));
        await InsertOutboxAsync(
            newerId,
            "Sms",
            "+15550001111",
            "{\"version\":1}",
            "Pending",
            _utcNow.AddMinutes(-5),
            createdAt: _utcNow.AddMinutes(-15));

        ClaimedAlertDelivery? claim = await _store.ClaimNextDueAsync(_utcNow, TimeSpan.FromMinutes(2), TestContext.CancellationToken);

        Assert.IsNotNull(claim);
        Assert.AreEqual(oldestId, claim.Id);
        Assert.AreEqual(_occurrenceId, claim.OccurrenceId);
        Assert.AreEqual(_notificationId, claim.NotificationId);
        Assert.AreEqual($"delivery:{oldestId:N}", claim.DeliveryKey);
        Assert.AreEqual("Email", claim.Kind);
        Assert.AreEqual("ops@example.test", claim.Destination);
        Assert.AreEqual("{\"version\":1}", claim.Payload);
        Assert.AreEqual("Leased", claim.Status);
        Assert.AreEqual(3, claim.AttemptCount);
        Assert.AreEqual(_utcNow.AddMinutes(-10), claim.NextAttemptAt);
        Assert.AreNotEqual(Guid.Empty, claim.LeaseId);
        Assert.AreEqual(_utcNow.AddMinutes(2), claim.LeaseUntil);
        Assert.IsNull(claim.CompletedAt);
        Assert.IsNull(claim.LastError);
        Assert.AreEqual(_utcNow.AddMinutes(-20), claim.CreatedAt);
        Assert.AreEqual(
            "Pending",
            await ReadStringAsync("SELECT status FROM alert_delivery_outbox WHERE id = @id;", newerId));
    }

    [TestMethod]
    public async Task ClaimNextDueAsync_SkipsLockedOldestRowAndClaimsNextDueRow()
    {
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        await InsertOutboxAsync(firstId, nextAttemptAt: _utcNow.AddMinutes(-2));
        await InsertOutboxAsync(secondId, nextAttemptAt: _utcNow.AddMinutes(-1));

        await using NpgsqlConnection blockingConnection = _database!.OpenConnection();
        await blockingConnection.OpenAsync(TestContext.CancellationToken);
        await using NpgsqlTransaction blockingTransaction = await blockingConnection.BeginTransactionAsync(TestContext.CancellationToken);
        await using (NpgsqlCommand lockCommand = new(
            "SELECT id FROM alert_delivery_outbox WHERE id = @id FOR UPDATE;",
            blockingConnection,
            blockingTransaction))
        {
            lockCommand.Parameters.AddWithValue("id", firstId);
            Assert.AreEqual(firstId, await lockCommand.ExecuteScalarAsync(TestContext.CancellationToken));
        }

        ClaimedAlertDelivery? skippedClaim = await _store.ClaimNextDueAsync(_utcNow, TimeSpan.FromMinutes(2), TestContext.CancellationToken);

        Assert.IsNotNull(skippedClaim);
        Assert.AreEqual(secondId, skippedClaim.Id);

        await blockingTransaction.RollbackAsync(TestContext.CancellationToken);
        ClaimedAlertDelivery? unlockedClaim = await _store.ClaimNextDueAsync(_utcNow, TimeSpan.FromMinutes(2), TestContext.CancellationToken);

        Assert.IsNotNull(unlockedClaim);
        Assert.AreEqual(firstId, unlockedClaim.Id);
        Assert.AreNotEqual(skippedClaim.LeaseId, unlockedClaim.LeaseId);
        Assert.AreEqual(2, await CountAsync("SELECT COUNT(*) FROM alert_delivery_outbox WHERE status = 'Leased';"));
    }

    [TestMethod]
    public async Task ClaimNextDueAsync_ExpiredLeaseIsReclaimedWithFreshFence()
    {
        Guid id = Guid.NewGuid();
        Guid expiredLeaseId = Guid.NewGuid();
        await InsertOutboxAsync(
            id,
            status: "Leased",
            nextAttemptAt: _utcNow.AddMinutes(-5),
            attemptCount: 4,
            leaseId: expiredLeaseId,
            leaseUntil: _utcNow.AddSeconds(-1));

        ClaimedAlertDelivery? claim = await _store.ClaimNextDueAsync(_utcNow, TimeSpan.FromMinutes(2), TestContext.CancellationToken);

        Assert.IsNotNull(claim);
        Assert.AreEqual(id, claim.Id);
        Assert.AreEqual(5, claim.AttemptCount);
        Assert.AreNotEqual(expiredLeaseId, claim.LeaseId);
        Assert.AreEqual(_utcNow.AddMinutes(2), claim.LeaseUntil);
    }

    [TestMethod]
    public async Task FencedOutcomes_RejectStaleLeaseWithoutChangingRowOrWritingAudit()
    {
        Guid id = Guid.NewGuid();
        await InsertOutboxAsync(id, kind: "Email", nextAttemptAt: _utcNow);
        ClaimedAlertDelivery? claim = await _store.ClaimNextDueAsync(_utcNow, TimeSpan.FromMinutes(2), TestContext.CancellationToken);
        Assert.IsNotNull(claim);
        Guid staleLeaseId = Guid.NewGuid();
        AlertDeliveryAudit audit = new(
            _notificationId,
            "ops@example.test",
            "Sent ok",
            _utcNow.AddSeconds(1));

        bool completed = await _store.CompleteAsync(
            id,
            staleLeaseId,
            _utcNow.AddSeconds(1),
            audit, TestContext.CancellationToken);
        bool retried = await _store.RetryAsync(
            id,
            staleLeaseId,
            _utcNow.AddMinutes(1),
            "Alert delivery failed (HttpRequestException).",
            deadLetter: true,
            audit, TestContext.CancellationToken);

        Assert.IsFalse(completed);
        Assert.IsFalse(retried);
        Assert.AreEqual("Leased", await ReadStringAsync("SELECT status FROM alert_delivery_outbox WHERE id = @id;", id));
        Assert.AreEqual(claim.LeaseId, await ReadNullableGuidAsync(id));
        Assert.AreEqual(0, await CountAsync("SELECT COUNT(*) FROM notification_sent;"));
    }

    [TestMethod]
    public async Task CompleteAsync_CommitsCompletionAndSuccessAuditTogether()
    {
        Guid id = Guid.NewGuid();
        await InsertOutboxAsync(id, kind: "Email", destination: "ops@example.test", nextAttemptAt: _utcNow);
        ClaimedAlertDelivery? claim = await _store.ClaimNextDueAsync(_utcNow, TimeSpan.FromMinutes(2), TestContext.CancellationToken);
        Assert.IsNotNull(claim);
        DateTime completedAt = _utcNow.AddSeconds(1);

        bool completed = await _store.CompleteAsync(
            id,
            claim.LeaseId,
            completedAt,
            new AlertDeliveryAudit(_notificationId, claim.Destination, "Sent ok", completedAt), TestContext.CancellationToken);

        Assert.IsTrue(completed);
        Assert.AreEqual("Completed", await ReadStringAsync("SELECT status FROM alert_delivery_outbox WHERE id = @id;", id));
        Assert.IsNull(await ReadNullableGuidAsync(id));
        Assert.AreEqual(completedAt, await ReadDateTimeAsync(
            "SELECT completed_at FROM alert_delivery_outbox WHERE id = @id;",
            id));
        Assert.AreEqual(1, await CountAsync("SELECT COUNT(*) FROM notification_sent;"));
        Assert.AreEqual("Sent ok", await ReadStringAsync(
            "SELECT error_message FROM notification_sent WHERE notification_id = @id;",
            _notificationId));
    }

    [TestMethod]
    public async Task CompleteAsync_AuditInsertFailureRollsBackCompletionAndPreservesFence()
    {
        Guid id = Guid.NewGuid();
        await InsertOutboxAsync(id, kind: "Email", destination: "ops@example.test", nextAttemptAt: _utcNow);
        ClaimedAlertDelivery? claim = await _store.ClaimNextDueAsync(_utcNow, TimeSpan.FromMinutes(2), TestContext.CancellationToken);
        Assert.IsNotNull(claim);

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() =>
            _store.CompleteAsync(
                id,
                claim.LeaseId,
                _utcNow.AddSeconds(1),
                new AlertDeliveryAudit(Guid.NewGuid(), claim.Destination, "Sent ok", _utcNow.AddSeconds(1)), TestContext.CancellationToken));

        Assert.AreEqual("Leased", await ReadStringAsync("SELECT status FROM alert_delivery_outbox WHERE id = @id;", id));
        Assert.AreEqual(claim.LeaseId, await ReadNullableGuidAsync(id));
        Assert.IsNull(await ReadNullableDateTimeAsync("completed_at", id));
        Assert.AreEqual(0, await CountAsync("SELECT COUNT(*) FROM notification_sent;"));
    }

    [TestMethod]
    public async Task RetryAsync_FinalFailureCommitsDeadLetterAndFailureAuditTogether()
    {
        Guid id = Guid.NewGuid();
        await InsertOutboxAsync(id, kind: "Sms", destination: "+15550001111", nextAttemptAt: _utcNow);
        ClaimedAlertDelivery? claim = await _store.ClaimNextDueAsync(_utcNow, TimeSpan.FromMinutes(2), TestContext.CancellationToken);
        Assert.IsNotNull(claim);
        DateTime failedAt = _utcNow.AddSeconds(2);
        const string safeError = "Alert delivery failed (HttpRequestException).";

        bool deadLettered = await _store.RetryAsync(
            id,
            claim.LeaseId,
            failedAt,
            safeError,
            deadLetter: true,
            new AlertDeliveryAudit(_notificationId, claim.Destination, safeError, failedAt), TestContext.CancellationToken);

        Assert.IsTrue(deadLettered);
        Assert.AreEqual("DeadLetter", await ReadStringAsync("SELECT status FROM alert_delivery_outbox WHERE id = @id;", id));
        Assert.IsNull(await ReadNullableGuidAsync(id));
        Assert.AreEqual(failedAt, await ReadDateTimeAsync(
            "SELECT completed_at FROM alert_delivery_outbox WHERE id = @id;",
            id));
        Assert.AreEqual(failedAt, await ReadDateTimeAsync(
            "SELECT next_attempt_at FROM alert_delivery_outbox WHERE id = @id;",
            id));
        Assert.AreEqual(safeError, await ReadStringAsync(
            "SELECT last_error FROM alert_delivery_outbox WHERE id = @id;",
            id));
        Assert.AreEqual(1, await CountAsync("SELECT COUNT(*) FROM notification_sent;"));
    }

    [TestMethod]
    public async Task DispatchAsync_MalformedTerminalPayload_AtomicallyDeadLettersWithAuthoritativeAudit()
    {
        const string rawPayload = "{raw-payload-secret";
        const string destination = "ops@example.test";
        Guid id = Guid.NewGuid();
        DateTime failedAt = _utcNow.AddSeconds(2);
        await InsertOutboxAsync(
            id,
            kind: "Email",
            destination,
            payload: rawPayload,
            nextAttemptAt: _utcNow,
            attemptCount: 7);
        Mock<IAlertDeliveryAdapter> adapter = new();
        adapter.SetupGet(candidate => candidate.Kind).Returns("Email");
        adapter.Setup(candidate => candidate.DeliverAsync(
                It.IsAny<ClaimedAlertDelivery>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new JsonException(rawPayload));
        DurableAlertDispatcher dispatcher = new(
            _store,
            [adapter.Object],
            Options.Create(new DurableAlertOptions { BatchSize = 1, MaxAttempts = 8 }),
            new FixedTimeProvider(failedAt),
            NullLogger<DurableAlertDispatcher>.Instance);

        AggregateException exception = await Assert.ThrowsExactlyAsync<AggregateException>(
            () => dispatcher.DispatchAsync(TestContext.CancellationToken));

        Assert.AreEqual("DeadLetter", await ReadStringAsync(
            "SELECT status FROM alert_delivery_outbox WHERE id = @id;",
            id));
        Assert.AreEqual(failedAt, await ReadDateTimeAsync(
            "SELECT next_attempt_at FROM alert_delivery_outbox WHERE id = @id;",
            id));
        Assert.AreEqual(failedAt, await ReadDateTimeAsync(
            "SELECT completed_at FROM alert_delivery_outbox WHERE id = @id;",
            id));
        Assert.AreEqual(_notificationId, await ReadGuidAsync(
            "SELECT notification_id FROM notification_sent WHERE address = @address;",
            destination));
        string safeError = await ReadStringAsync(
            "SELECT last_error FROM alert_delivery_outbox WHERE id = @id;",
            id);
        Assert.AreEqual("Alert delivery failed (JsonException).", safeError);
        Assert.IsFalse(safeError.Contains(rawPayload, StringComparison.Ordinal));
        Assert.IsFalse(exception.ToString().Contains(rawPayload, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RetryAsync_FinalAuditInsertFailureRollsBackDeadLetterAndPreservesFence()
    {
        Guid id = Guid.NewGuid();
        await InsertOutboxAsync(id, kind: "Sms", destination: "+15550001111", nextAttemptAt: _utcNow);
        ClaimedAlertDelivery? claim = await _store.ClaimNextDueAsync(_utcNow, TimeSpan.FromMinutes(2), TestContext.CancellationToken);
        Assert.IsNotNull(claim);
        const string safeError = "Alert delivery failed (HttpRequestException).";

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() =>
            _store.RetryAsync(
                id,
                claim.LeaseId,
                _utcNow.AddSeconds(2),
                safeError,
                deadLetter: true,
                new AlertDeliveryAudit(Guid.NewGuid(), claim.Destination, safeError, _utcNow.AddSeconds(2)), TestContext.CancellationToken));

        Assert.AreEqual("Leased", await ReadStringAsync("SELECT status FROM alert_delivery_outbox WHERE id = @id;", id));
        Assert.AreEqual(claim.LeaseId, await ReadNullableGuidAsync(id));
        Assert.IsNull(await ReadNullableDateTimeAsync("completed_at", id));
        Assert.IsNull(await ReadNullableStringAsync("last_error", id));
        Assert.AreEqual(0, await CountAsync("SELECT COUNT(*) FROM notification_sent;"));
    }

    [TestMethod]
    public async Task RetryAsync_RetryClearsLeaseAndBoundsPersistedError()
    {
        Guid id = Guid.NewGuid();
        await InsertOutboxAsync(id, kind: "Email", nextAttemptAt: _utcNow);
        ClaimedAlertDelivery? claim = await _store.ClaimNextDueAsync(_utcNow, TimeSpan.FromMinutes(2), TestContext.CancellationToken);
        Assert.IsNotNull(claim);
        DateTime nextAttemptAt = _utcNow.AddMinutes(1);
        string oversizedSafeError = new('x', 1100);

        bool retried = await _store.RetryAsync(
            id,
            claim.LeaseId,
            nextAttemptAt,
            oversizedSafeError,
            deadLetter: false,
            new AlertDeliveryAudit(_notificationId, claim.Destination, "not final", _utcNow), TestContext.CancellationToken);

        Assert.IsTrue(retried);
        Assert.AreEqual("Pending", await ReadStringAsync("SELECT status FROM alert_delivery_outbox WHERE id = @id;", id));
        Assert.IsNull(await ReadNullableGuidAsync(id));
        Assert.AreEqual(nextAttemptAt, await ReadDateTimeAsync(
            "SELECT next_attempt_at FROM alert_delivery_outbox WHERE id = @id;",
            id));
        Assert.AreEqual(new string('x', 1024), await ReadStringAsync(
            "SELECT last_error FROM alert_delivery_outbox WHERE id = @id;",
            id));
        Assert.AreEqual(0, await CountAsync("SELECT COUNT(*) FROM notification_sent;"));
    }

    [TestMethod]
    public async Task DeleteCompletedBeforeAsync_DeletesOnlyCompletedRowsOlderThanCutoff()
    {
        Guid oldCompleted = Guid.NewGuid();
        Guid boundaryCompleted = Guid.NewGuid();
        Guid newCompleted = Guid.NewGuid();
        Guid oldDeadLetter = Guid.NewGuid();
        Guid oldPending = Guid.NewGuid();
        DateTime cutoff = _utcNow;
        await InsertOutboxAsync(
            oldCompleted,
            status: "Completed",
            nextAttemptAt: _utcNow.AddDays(-3),
            completedAt: _utcNow.AddDays(-2));
        await InsertOutboxAsync(
            boundaryCompleted,
            status: "Completed",
            nextAttemptAt: _utcNow.AddDays(-2),
            completedAt: cutoff);
        await InsertOutboxAsync(
            newCompleted,
            status: "Completed",
            nextAttemptAt: _utcNow.AddDays(-1),
            completedAt: _utcNow.AddMinutes(1));
        await InsertOutboxAsync(
            oldDeadLetter,
            status: "DeadLetter",
            nextAttemptAt: _utcNow.AddDays(-3),
            completedAt: _utcNow.AddDays(-2));
        await InsertOutboxAsync(
            oldPending,
            status: "Pending",
            nextAttemptAt: _utcNow.AddDays(-3));

        int deleted = await _store.DeleteCompletedBeforeAsync(cutoff, TestContext.CancellationToken);

        Assert.AreEqual(1, deleted);
        CollectionAssert.AreEquivalent(
            new[] { boundaryCompleted, newCompleted, oldDeadLetter, oldPending },
            await ReadIdsAsync());
    }

    [TestMethod]
    public async Task ClaimNextDueAsync_PreCanceled_DoesNotClaimRow()
    {
        Guid id = Guid.NewGuid();
        await InsertOutboxAsync(id, nextAttemptAt: _utcNow);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            _store.ClaimNextDueAsync(_utcNow, TimeSpan.FromMinutes(2), cancellation.Token));

        Assert.AreEqual("Pending", await ReadStringAsync("SELECT status FROM alert_delivery_outbox WHERE id = @id;", id));
        Assert.AreEqual(0, await ReadIntAsync(
            "SELECT attempt_count FROM alert_delivery_outbox WHERE id = @id;",
            id));
    }

    private static Task SeedAlertGraphAsync() =>
        ExecuteAsync(
            """
            INSERT INTO monitor
                (id, serial_id, customer_id, listed_at_time, model, manufacturer,
                 firmware_version, type_of_monitor)
            VALUES
                (@monitor_id, 'task-5-monitor', 42, @created_at, 'SWARM', 'Omnidots', '1.0', 2);

            INSERT INTO notification
                (id, notification_time, limit_on, averaging_period, level, monitor_id, alert_field, alert_type)
            VALUES
                (@notification_id, @created_at, 5, 60, 7.5, @monitor_id, 'Vtop', 2);

            INSERT INTO alert_occurrence
                (id, source, source_key_hash, notification_id, monitor_id, serial_id,
                 event_time, alert_type, alert_field, level, limit_on, averaging_period,
                 outcome, created_at)
            VALUES
                (@occurrence_id, 'task-5', @source_hash, @notification_id, @monitor_id,
                 'task-5-monitor', @created_at, 2, 'Vtop', 7.5, 5, 60, 'Accepted', @created_at);
            """,
            command =>
            {
                command.Parameters.AddWithValue("monitor_id", _monitorId);
                command.Parameters.AddWithValue("notification_id", _notificationId);
                command.Parameters.AddWithValue("occurrence_id", _occurrenceId);
                command.Parameters.AddWithValue("created_at", _utcNow.AddHours(-1));
                command.Parameters.AddWithValue("source_hash", NpgsqlDbType.Bytea, Enumerable.Repeat((byte)5, 32).ToArray());
            });

    private static Task InsertOutboxAsync(
        Guid id,
        string kind = "MqttAlert",
        string destination = "alert",
        string payload = "{}",
        string status = "Pending",
        DateTime? nextAttemptAt = null,
        int attemptCount = 0,
        Guid? leaseId = null,
        DateTime? leaseUntil = null,
        DateTime? completedAt = null,
        string? lastError = null,
        DateTime? createdAt = null) =>
        ExecuteAsync(
            """
            INSERT INTO alert_delivery_outbox
                (id, occurrence_id, delivery_key, kind, destination, payload, status,
                 attempt_count, next_attempt_at, lease_id, lease_until, completed_at,
                 last_error, created_at)
            VALUES
                (@id, @occurrence_id, @delivery_key, @kind, @destination, @payload, @status,
                 @attempt_count, @next_attempt_at, @lease_id, @lease_until, @completed_at,
                 @last_error, @created_at);
            """,
            command =>
            {
                command.Parameters.AddWithValue("id", id);
                command.Parameters.AddWithValue("occurrence_id", _occurrenceId);
                command.Parameters.AddWithValue("delivery_key", $"delivery:{id:N}");
                command.Parameters.AddWithValue("kind", kind);
                command.Parameters.AddWithValue("destination", destination);
                command.Parameters.AddWithValue("payload", payload);
                command.Parameters.AddWithValue("status", status);
                command.Parameters.AddWithValue("attempt_count", attemptCount);
                command.Parameters.AddWithValue("next_attempt_at", nextAttemptAt ?? _utcNow);
                command.Parameters.AddWithValue("lease_id", (object?)leaseId ?? DBNull.Value);
                command.Parameters.AddWithValue("lease_until", (object?)leaseUntil ?? DBNull.Value);
                command.Parameters.AddWithValue("completed_at", (object?)completedAt ?? DBNull.Value);
                command.Parameters.AddWithValue("last_error", (object?)lastError ?? DBNull.Value);
                command.Parameters.AddWithValue("created_at", createdAt ?? (nextAttemptAt ?? _utcNow).AddMinutes(-1));
            });

    private static async Task ExecuteAsync(string sql, Action<NpgsqlCommand> configure)
    {
        await using NpgsqlConnection connection = _database!.OpenConnection();
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        configure(command);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountAsync(string query)
    {
        string sql = query switch
        {
            "SELECT COUNT(*) FROM alert_delivery_outbox WHERE status = 'Leased';" => query,
            "SELECT COUNT(*) FROM notification_sent;" => query,
            _ => throw new ArgumentOutOfRangeException(nameof(query))
        };

        await using NpgsqlConnection connection = _database!.OpenConnection();
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<string> ReadStringAsync(string query, Guid id)
    {
        string sql = query switch
        {
            "SELECT status FROM alert_delivery_outbox WHERE id = @id;" => query,
            "SELECT last_error FROM alert_delivery_outbox WHERE id = @id;" => query,
            "SELECT error_message FROM notification_sent WHERE notification_id = @id;" => query,
            _ => throw new ArgumentOutOfRangeException(nameof(query))
        };

        await using NpgsqlConnection connection = _database!.OpenConnection();
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<int> ReadIntAsync(string query, Guid id)
    {
        if (query != "SELECT attempt_count FROM alert_delivery_outbox WHERE id = @id;")
        {
            throw new ArgumentOutOfRangeException(nameof(query));
        }

        await using NpgsqlConnection connection = _database!.OpenConnection();
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(query, connection);
        command.Parameters.AddWithValue("id", id);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<Guid?> ReadNullableGuidAsync(Guid id)
    {
        await using NpgsqlConnection connection = _database!.OpenConnection();
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT lease_id FROM alert_delivery_outbox WHERE id = @id;",
            connection);
        command.Parameters.AddWithValue("id", id);
        object? value = await command.ExecuteScalarAsync();
        return value is DBNull ? null : (Guid?)value;
    }

    private static async Task<DateTime> ReadDateTimeAsync(string query, Guid id)
    {
        string sql = query switch
        {
            "SELECT completed_at FROM alert_delivery_outbox WHERE id = @id;" => query,
            "SELECT next_attempt_at FROM alert_delivery_outbox WHERE id = @id;" => query,
            _ => throw new ArgumentOutOfRangeException(nameof(query))
        };

        await using NpgsqlConnection connection = _database!.OpenConnection();
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return (DateTime)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> ReadGuidAsync(string query, string address)
    {
        if (query != "SELECT notification_id FROM notification_sent WHERE address = @address;")
        {
            throw new ArgumentOutOfRangeException(nameof(query));
        }

        await using NpgsqlConnection connection = _database!.OpenConnection();
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(query, connection);
        command.Parameters.AddWithValue("address", address);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<DateTime?> ReadNullableDateTimeAsync(string column, Guid id)
    {
        string sql = column switch
        {
            "completed_at" => "SELECT completed_at FROM alert_delivery_outbox WHERE id = @id;",
            _ => throw new ArgumentOutOfRangeException(nameof(column))
        };

        await using NpgsqlConnection connection = _database!.OpenConnection();
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("id", id);
        object? value = await command.ExecuteScalarAsync();
        return value is DBNull ? null : (DateTime?)value;
    }

    private static async Task<string?> ReadNullableStringAsync(string column, Guid id)
    {
        string sql = column switch
        {
            "last_error" => "SELECT last_error FROM alert_delivery_outbox WHERE id = @id;",
            _ => throw new ArgumentOutOfRangeException(nameof(column))
        };

        await using NpgsqlConnection connection = _database!.OpenConnection();
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("id", id);
        object? value = await command.ExecuteScalarAsync();
        return value is DBNull ? null : (string?)value;
    }

    private static async Task<Guid[]> ReadIdsAsync()
    {
        List<Guid> ids = [];
        await using NpgsqlConnection connection = _database!.OpenConnection();
        await connection.OpenAsync();
        await using NpgsqlCommand command = new("SELECT id FROM alert_delivery_outbox;", connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetGuid(0));
        }

        return [.. ids];
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    public TestContext TestContext { get; set; } = null!;
}
