using Npgsql;
using Rvt.Monitor.IntegrationTesting;

namespace MyAtmMonitorTests;

[TestClass]
[TestCategory("PostgreSqlIntegration")]
public sealed class MyAtmSharedOutboxMigrationTests
{
    private const string ForwardMigration = "2026-07-15-migrate-myatm-outbox-to-shared.postgres.sql";
    private const string RollbackMigration = "2026-07-15-rollback-myatm-outbox-to-local.postgres.sql";
    private static PostgreSqlIntegrationDatabase? database;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        var setupSql = File.ReadAllText(RepositoryPath("myatmmonitor", "MyAtmMonitorTests", "testdata", "create.postgres.sql"));
        var resetSql = File.ReadAllText(RepositoryPath("myatmmonitor", "MyAtmMonitorTests", "testdata", "reset.postgres.sql"));
        database = await PostgreSqlIntegrationDatabase.CreateAsync(setupSql, resetSql);
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        var resetSql = File.ReadAllText(RepositoryPath("myatmmonitor", "MyAtmMonitorTests", "testdata", "reset.postgres.sql"));
        await database!.ResetAsync(resetSql);
        await ExecuteAsync(SeedLegacyRowsSql);
    }

    [TestMethod]
    public async Task ForwardMigration_ReplaysEveryLegacyStateIdempotently()
    {
        var legacySource = await ReadLegacySnapshotAsync();
        Assert.AreEqual(5, legacySource.Count);

        await ApplyMigrationAsync(ForwardMigration);
        AssertMigratedSnapshot(legacySource, await ReadSharedSnapshotAsync());

        await ApplyMigrationAsync(ForwardMigration);
        AssertMigratedSnapshot(legacySource, await ReadSharedSnapshotAsync());
    }

    [TestMethod]
    public async Task ForwardMigration_DoesNotReplaceHigherAttemptOrTerminalSharedState()
    {
        await ApplyMigrationAsync(ForwardMigration);
        await ExecuteAsync(
            """
            UPDATE monitor_delivery_outbox
            SET status = 'Completed', attempt_count = 1, completed_at = '2026-07-15T13:00:00Z', last_error = NULL
            WHERE delivery_key = 'pending-alert';

            UPDATE monitor_delivery_outbox
            SET status = 'Pending', attempt_count = 51, last_error = 'newer shared state'
            WHERE delivery_key = 'leased-email';

            UPDATE monitor_delivery_outbox
            SET completed_at = '2026-07-15T13:00:00Z', last_error = 'newer shared completion'
            WHERE delivery_key = 'completed-sms';
            """);

        await ApplyMigrationAsync(ForwardMigration);

        Assert.AreEqual("Completed", await ScalarStringAsync("SELECT status FROM monitor_delivery_outbox WHERE delivery_key = 'pending-alert';"));
        Assert.AreEqual(1, await ScalarIntAsync("SELECT attempt_count FROM monitor_delivery_outbox WHERE delivery_key = 'pending-alert';"));
        Assert.AreEqual("Pending", await ScalarStringAsync("SELECT status FROM monitor_delivery_outbox WHERE delivery_key = 'leased-email';"));
        Assert.AreEqual(51, await ScalarIntAsync("SELECT attempt_count FROM monitor_delivery_outbox WHERE delivery_key = 'leased-email';"));
        Assert.AreEqual("newer shared state", await ScalarStringAsync("SELECT last_error FROM monitor_delivery_outbox WHERE delivery_key = 'leased-email';"));
        Assert.AreEqual(
            new DateTime(2026, 7, 15, 13, 0, 0, DateTimeKind.Utc),
            await ScalarDateTimeAsync("SELECT completed_at FROM monitor_delivery_outbox WHERE delivery_key = 'completed-sms';"));
        Assert.AreEqual("newer shared completion", await ScalarStringAsync("SELECT last_error FROM monitor_delivery_outbox WHERE delivery_key = 'completed-sms';"));
    }

    [TestMethod]
    public async Task RollbackMigration_AuthoritativelySynchronizesOnlyMyAtmVersionOneRows()
    {
        await ApplyMigrationAsync(ForwardMigration);
        await ExecuteAsync(MutateSharedRowsSql);

        await ApplyMigrationAsync(RollbackMigration);
        await ApplyMigrationAsync(RollbackMigration);

        Assert.AreEqual(6, await ScalarIntAsync("SELECT COUNT(*) FROM my_atm_outbox_message;"));
        Assert.AreEqual(6, await ScalarIntAsync("SELECT COUNT(*) FROM monitor_delivery_outbox WHERE producer = 'MyAtm' AND payload_version = 1;"));
        Assert.AreEqual(0, await ScalarIntAsync(StateMismatchSql));
        Assert.AreEqual("Leased", await ScalarStringAsync("SELECT status FROM my_atm_outbox_message WHERE delivery_key = 'completed-sms';"));
        Assert.AreEqual(1, await ScalarIntAsync("SELECT COUNT(*) FROM my_atm_outbox_message WHERE delivery_key = 'completed-sms' AND occurrence_key IS NULL;"));
        Assert.AreEqual("occ-valid", await ScalarStringAsync("SELECT occurrence_key FROM my_atm_outbox_message WHERE delivery_key = 'post-cutover';"));
        Assert.AreEqual(0, await ScalarIntAsync("SELECT COUNT(*) FROM my_atm_outbox_message WHERE delivery_key IN ('foreign-producer', 'myatm-version-two');"));
        Assert.AreEqual(8, await ScalarIntAsync("SELECT COUNT(*) FROM monitor_delivery_outbox;"));
    }

    private static async Task ApplyMigrationAsync(string fileName) =>
        await ExecuteAsync(File.ReadAllText(RepositoryPath("myatmmonitor", "database", "migrations", fileName)));

    private static async Task ExecuteAsync(string sql)
    {
        await using var connection = database!.OpenConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ScalarIntAsync(string sql)
    {
        await using var connection = database!.OpenConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<string?> ScalarStringAsync(string sql)
    {
        await using var connection = database!.OpenConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToString(await command.ExecuteScalarAsync());
    }

    private static async Task<DateTime?> ScalarDateTimeAsync(string sql)
    {
        await using var connection = database!.OpenConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return value is DBNull or null ? null : (DateTime)value;
    }

    private static async Task<IReadOnlyList<LegacyDeliverySnapshot>> ReadLegacySnapshotAsync()
    {
        const string sql =
            """
            SELECT legacy.id, legacy.occurrence_key, legacy.delivery_key, legacy.kind, legacy.destination,
                   legacy.payload, legacy.status, legacy.attempt_count, legacy.next_attempt_at, legacy.lease_id,
                   legacy.lease_until, legacy.completed_at, legacy.last_error, legacy.created_at,
                   notification.id AS notification_id
            FROM my_atm_outbox_message AS legacy
            LEFT JOIN my_atm_alert_occurrence AS occurrence
              ON occurrence.occurrence_key = legacy.occurrence_key
            LEFT JOIN notification
              ON notification.id = occurrence.notification_id
            ORDER BY legacy.delivery_key;
            """;

        await using var connection = database!.OpenConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<LegacyDeliverySnapshot>();
        while (await reader.ReadAsync())
        {
            rows.Add(new LegacyDeliverySnapshot(
                reader.GetGuid(0),
                GetNullableString(reader, 1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetInt32(7),
                reader.GetDateTime(8),
                GetNullableGuid(reader, 9),
                GetNullableDateTime(reader, 10),
                GetNullableDateTime(reader, 11),
                GetNullableString(reader, 12),
                reader.GetDateTime(13),
                GetNullableGuid(reader, 14)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<SharedDeliverySnapshot>> ReadSharedSnapshotAsync()
    {
        const string sql =
            """
            SELECT id, producer, notification_id, correlation_key, delivery_key, kind, destination, payload_version,
                   payload, status, attempt_count, next_attempt_at, lease_id, lease_until, completed_at,
                   dead_lettered_at, last_error, created_at
            FROM monitor_delivery_outbox
            WHERE producer = 'MyAtm' AND payload_version = 1
            ORDER BY delivery_key;
            """;

        await using var connection = database!.OpenConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<SharedDeliverySnapshot>();
        while (await reader.ReadAsync())
        {
            rows.Add(new SharedDeliverySnapshot(
                reader.GetGuid(0),
                reader.GetString(1),
                GetNullableGuid(reader, 2),
                GetNullableString(reader, 3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetInt32(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetInt32(10),
                reader.GetDateTime(11),
                GetNullableGuid(reader, 12),
                GetNullableDateTime(reader, 13),
                GetNullableDateTime(reader, 14),
                GetNullableDateTime(reader, 15),
                GetNullableString(reader, 16),
                reader.GetDateTime(17)));
        }

        return rows;
    }

    private static void AssertMigratedSnapshot(
        IReadOnlyList<LegacyDeliverySnapshot> legacyRows,
        IReadOnlyList<SharedDeliverySnapshot> sharedRows)
    {
        Assert.AreEqual(legacyRows.Count, sharedRows.Count);
        Assert.AreEqual(sharedRows.Count, sharedRows.Select(row => row.DeliveryKey).Distinct(StringComparer.Ordinal).Count());

        for (var index = 0; index < legacyRows.Count; index++)
        {
            var legacy = legacyRows[index];
            var shared = sharedRows[index];

            Assert.AreEqual(legacy.Id, shared.Id, legacy.DeliveryKey);
            Assert.AreEqual("MyAtm", shared.Producer, legacy.DeliveryKey);
            Assert.AreEqual(legacy.NotificationId, shared.NotificationId, legacy.DeliveryKey);
            Assert.AreEqual(legacy.OccurrenceKey, shared.CorrelationKey, legacy.DeliveryKey);
            Assert.AreEqual(legacy.DeliveryKey, shared.DeliveryKey);
            Assert.AreEqual(legacy.Kind, shared.Kind, legacy.DeliveryKey);
            Assert.AreEqual(legacy.Destination, shared.Destination, legacy.DeliveryKey);
            Assert.AreEqual(1, shared.PayloadVersion, legacy.DeliveryKey);
            Assert.AreEqual(legacy.Payload, shared.Payload, legacy.DeliveryKey);
            Assert.AreEqual(legacy.Status == "Leased" ? "InProgress" : legacy.Status, shared.Status, legacy.DeliveryKey);
            Assert.AreEqual(legacy.AttemptCount, shared.AttemptCount, legacy.DeliveryKey);
            Assert.AreEqual(legacy.NextAttemptAt, shared.NextAttemptAt, legacy.DeliveryKey);
            Assert.AreEqual(legacy.LeaseId, shared.LeaseId, legacy.DeliveryKey);
            Assert.AreEqual(legacy.LeaseUntil, shared.LeaseUntil, legacy.DeliveryKey);
            Assert.AreEqual(legacy.CompletedAt, shared.CompletedAt, legacy.DeliveryKey);
            Assert.IsNull(shared.DeadLetteredAt, legacy.DeliveryKey);
            Assert.AreEqual(legacy.LastError, shared.LastError, legacy.DeliveryKey);
            Assert.AreEqual(legacy.CreatedAt, shared.CreatedAt, legacy.DeliveryKey);
        }
    }

    private static string? GetNullableString(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static Guid? GetNullableGuid(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);

    private static DateTime? GetNullableDateTime(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);

    private static string RepositoryPath(params string[] segments) =>
        Path.Combine([FindRepositoryRoot(), .. segments]);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var gitPath = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the repository root from the test output directory.");
    }

    private const string StateMismatchSql =
        """
        SELECT COUNT(*)
        FROM monitor_delivery_outbox shared
        LEFT JOIN my_atm_alert_occurrence occurrence
          ON occurrence.occurrence_key = shared.correlation_key
        LEFT JOIN my_atm_outbox_message legacy
          ON legacy.delivery_key = shared.delivery_key
        WHERE shared.producer = 'MyAtm'
          AND shared.payload_version = 1
          AND (
              legacy.id IS NULL
              OR legacy.id <> shared.id
              OR legacy.occurrence_key IS DISTINCT FROM occurrence.occurrence_key
              OR legacy.kind <> shared.kind
              OR legacy.destination <> shared.destination
              OR legacy.payload <> shared.payload
              OR legacy.status <> CASE shared.status WHEN 'InProgress' THEN 'Leased' ELSE shared.status END
              OR legacy.attempt_count <> shared.attempt_count
              OR legacy.next_attempt_at <> shared.next_attempt_at
              OR legacy.lease_id IS DISTINCT FROM shared.lease_id
              OR legacy.lease_until IS DISTINCT FROM shared.lease_until
              OR legacy.completed_at IS DISTINCT FROM shared.completed_at
              OR legacy.last_error IS DISTINCT FROM shared.last_error
              OR legacy.created_at <> shared.created_at
          );
        """;

    private const string SeedLegacyRowsSql =
        """
        INSERT INTO monitor
          (id, serial_id, listed_at_time, model, manufacturer, firmware_version, type_of_monitor, offline)
        VALUES
          ('10000000-0000-0000-0000-000000000010', 'migration-monitor', '2026-07-15T10:00:00Z', 'Dust', 'RVT', '1', 0, false);

        INSERT INTO notification
          (id, notification_time, limit_on, averaging_period, level, monitor_id, alert_field, alert_type)
        VALUES
          ('10000000-0000-0000-0000-000000000020', '2026-07-15T10:00:00Z', 10, 60, 12, '10000000-0000-0000-0000-000000000010', 'PM10', 2);

        INSERT INTO my_atm_alert_occurrence
          (occurrence_key, notification_id, monitor_id, rule_id, period, alert_type, field, level, triggered_at, is_suppressed, created_at)
        VALUES
          ('occ-valid', '10000000-0000-0000-0000-000000000020', '10000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000001', 60, 2, 'PM10', 12, '2026-07-15T10:00:00Z', false, '2026-07-15T10:00:00Z'),
          ('occ-orphan', '10000000-0000-0000-0000-000000000021', '10000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000001', 60, 1, 'PM2.5', 8, '2026-07-15T10:01:00Z', false, '2026-07-15T10:01:00Z');

        INSERT INTO my_atm_outbox_message
          (id, occurrence_key, delivery_key, kind, destination, payload, status, attempt_count, next_attempt_at, lease_id, lease_until, completed_at, last_error, created_at)
        VALUES
          ('10000000-0000-0000-0000-000000000101', 'occ-valid', 'pending-alert', 'MqttAlert', 'alerts', '{"message":"pending"}', 'Pending', 1, '2026-07-15T11:00:00Z', NULL, NULL, NULL, 'legacy pending', '2026-07-15T10:00:00Z'),
          ('10000000-0000-0000-0000-000000000102', 'occ-valid', 'leased-email', 'Email', 'person@example.test', '{"message":"leased"}', 'Leased', 2, '2026-07-15T11:01:00Z', '10000000-0000-0000-0000-000000000202', '2026-07-15T11:06:00Z', NULL, 'legacy leased', '2026-07-15T10:01:00Z'),
          ('10000000-0000-0000-0000-000000000103', 'occ-orphan', 'completed-sms', 'Sms', '+440000000001', '{"message":"completed"}', 'Completed', 3, '2026-07-15T11:02:00Z', NULL, NULL, '2026-07-15T11:03:00Z', NULL, '2026-07-15T10:02:00Z'),
          ('10000000-0000-0000-0000-000000000104', NULL, 'dead-alert', 'MqttAlert', 'alerts', '{"message":"dead"}', 'DeadLetter', 8, '2026-07-15T11:03:00Z', NULL, NULL, NULL, 'legacy dead letter', '2026-07-15T10:03:00Z'),
          ('10000000-0000-0000-0000-000000000105', NULL, 'data-inserted', 'MqttDataInserted', 'inserted', '{"message":"data"}', 'Pending', 0, '2026-07-15T11:04:00Z', NULL, NULL, NULL, NULL, '2026-07-15T10:04:00Z');
        """;

    private const string MutateSharedRowsSql =
        """
        UPDATE monitor_delivery_outbox
        SET status = 'Completed', attempt_count = 7, next_attempt_at = '2026-07-16T12:00:00Z', lease_id = NULL,
            lease_until = NULL, completed_at = '2026-07-15T12:00:00Z', dead_lettered_at = NULL, last_error = NULL
        WHERE delivery_key = 'pending-alert';

        UPDATE monitor_delivery_outbox
        SET status = 'DeadLetter', attempt_count = 8, next_attempt_at = '2026-07-16T12:01:00Z', lease_id = NULL,
            lease_until = NULL, completed_at = NULL, dead_lettered_at = '2026-07-15T12:01:00Z', last_error = 'shared dead letter'
        WHERE delivery_key = 'leased-email';

        UPDATE monitor_delivery_outbox
        SET correlation_key = 'missing-occurrence', status = 'InProgress', attempt_count = 9,
            next_attempt_at = '2026-07-16T12:02:00Z', lease_id = '10000000-0000-0000-0000-000000000303',
            lease_until = '2026-07-16T12:07:00Z', completed_at = NULL, dead_lettered_at = NULL, last_error = 'shared lease'
        WHERE delivery_key = 'completed-sms';

        UPDATE monitor_delivery_outbox
        SET status = 'Pending', attempt_count = 10, next_attempt_at = '2026-07-16T12:03:00Z', lease_id = NULL,
            lease_until = NULL, completed_at = NULL, dead_lettered_at = NULL, last_error = 'shared retry'
        WHERE delivery_key = 'dead-alert';

        INSERT INTO monitor_delivery_outbox
          (id, producer, notification_id, correlation_key, delivery_key, kind, destination, payload_version, payload, status,
           attempt_count, next_attempt_at, lease_id, lease_until, completed_at, dead_lettered_at, last_error, created_at)
        VALUES
          ('10000000-0000-0000-0000-000000000106', 'MyAtm', '10000000-0000-0000-0000-000000000020', 'occ-valid', 'post-cutover', 'Email', 'new@example.test', 1, '{"message":"new"}', 'InProgress', 4, '2026-07-16T12:04:00Z', '10000000-0000-0000-0000-000000000306', '2026-07-16T12:09:00Z', NULL, NULL, 'post-cutover lease', '2026-07-15T12:04:00Z'),
          ('10000000-0000-0000-0000-000000000107', 'Svantek', NULL, NULL, 'foreign-producer', 'MqttDataInserted', 'inserted', 1, '{}', 'Pending', 0, '2026-07-16T12:05:00Z', NULL, NULL, NULL, NULL, NULL, '2026-07-15T12:05:00Z'),
          ('10000000-0000-0000-0000-000000000108', 'MyAtm', NULL, NULL, 'myatm-version-two', 'MqttDataInserted', 'inserted', 2, '{}', 'Pending', 0, '2026-07-16T12:06:00Z', NULL, NULL, NULL, NULL, NULL, '2026-07-15T12:06:00Z');
        """;

    private sealed record LegacyDeliverySnapshot(
        Guid Id,
        string? OccurrenceKey,
        string DeliveryKey,
        string Kind,
        string Destination,
        string Payload,
        string Status,
        int AttemptCount,
        DateTime NextAttemptAt,
        Guid? LeaseId,
        DateTime? LeaseUntil,
        DateTime? CompletedAt,
        string? LastError,
        DateTime CreatedAt,
        Guid? NotificationId);

    private sealed record SharedDeliverySnapshot(
        Guid Id,
        string Producer,
        Guid? NotificationId,
        string? CorrelationKey,
        string DeliveryKey,
        string Kind,
        string Destination,
        int PayloadVersion,
        string Payload,
        string Status,
        int AttemptCount,
        DateTime NextAttemptAt,
        Guid? LeaseId,
        DateTime? LeaseUntil,
        DateTime? CompletedAt,
        DateTime? DeadLetteredAt,
        string? LastError,
        DateTime CreatedAt);
}
