using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Omnidots.Api.Db.EntityFramework;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.IntegrationTesting;

namespace OmnidotsMonitorTests.EntityFramework;

[TestClass]
[TestCategory("PostgreSqlIntegration")]
public sealed class OmnidotsAlertCommitStoreTests
{
    private const string SerialId = "23423";
    private const string DefaultEmail = " Ops@Example.Test ";
    private const string DefaultPhone = " +15550001111 ";
    private static readonly DateTime EventTime =
        new(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SiteId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid MonitorId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ContractId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId =
        Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SiteUserId =
        Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid NotificationSettingId =
        Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static PostgreSqlIntegrationDatabase? database;
    private IAlertCommitStore store = null!;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        database = await PostgreSqlIntegrationDatabase.CreateAsync(
            OmnidotsAdapterTests.TestUtil.ReadTextFromFile("testdata/create.postgres.sql"),
            OmnidotsAdapterTests.TestUtil.ReadTextFromFile("testdata/reset.postgres.sql"),
            timeout.Token);
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
        await database!.ResetAsync(
            OmnidotsAdapterTests.TestUtil.ReadTextFromFile("testdata/reset.postgres.sql"));
        await SeedContactGraphAsync();

        var monitorOptions = new MonitorDbOptions(
            MonitorDatabaseProvider.PostgreSql,
            new Dictionary<string, string>());
        store = new EfAlertCommitStore<OmnidotsMonitorContext>(
            new OmnidotsMonitorContextFactory(database.ConnectionString, monitorOptions),
            new CautionAlertAcceptancePolicy());
    }

    [TestMethod]
    public async Task CommitAsync_AcceptedAlert_CommitsOccurrenceNotificationAndCompleteDeliverySet()
    {
        var request = CommitRequest(
            AlertType.Alert,
            AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms);

        var result = await store.CommitAsync(request);

        Assert.AreEqual(AlertOccurrenceOutcome.Accepted, result.Outcome);
        Assert.IsFalse(result.IsDuplicate);
        Assert.AreEqual(request.NotificationId, result.NotificationId);
        Assert.AreEqual(1, await CountAsync("alert_occurrence"));
        Assert.AreEqual(1, await CountAsync("notification"));
        Assert.AreEqual(3, await CountAsync("alert_delivery_outbox"));
        CollectionAssert.AreEquivalent(
            new[] { "MqttAlert", "Email", "Sms" },
            await ReadStringsAsync("SELECT kind FROM alert_delivery_outbox"));

        foreach (var payload in await ReadStringsAsync("SELECT payload FROM alert_delivery_outbox"))
        {
            var envelope = JsonSerializer.Deserialize<AlertDeliveryEnvelope>(payload);
            Assert.IsNotNull(envelope);
            Assert.AreEqual(1, envelope.Version);
            Assert.AreEqual(request.NotificationId, envelope.NotificationId);
            Assert.AreEqual(EventTime, envelope.Timestamp);
            Assert.AreEqual(AlertType.Alert, envelope.AlertType);
            Assert.AreEqual(SerialId, envelope.SerialId);
            Assert.AreEqual(42, envelope.CustomerId);
            Assert.AreEqual("test-fleet", envelope.FleetNr);
            Assert.AreEqual("Vibration threshold exceeded.", envelope.Message);
        }
    }

    [TestMethod]
    public async Task CommitAsync_Ignore_CommitsOnlyIgnoredOccurrence()
    {
        var result = await store.CommitAsync(CommitRequest(
            AlertType.Ignore,
            AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms));

        Assert.AreEqual(AlertOccurrenceOutcome.Ignored, result.Outcome);
        Assert.IsNull(result.NotificationId);
        Assert.AreEqual(1, await CountAsync("alert_occurrence"));
        Assert.AreEqual(0, await CountAsync("notification"));
        Assert.AreEqual(0, await CountAsync("alert_delivery_outbox"));
    }

    [TestMethod]
    public async Task CommitAsync_RepeatedCaution_SuppressesSecondOccurrence()
    {
        var first = await store.CommitAsync(CommitRequest(
            AlertType.Caution,
            AlertDeliveryChannels.None,
            hashSeed: 1));
        var second = await store.CommitAsync(CommitRequest(
            AlertType.Caution,
            AlertDeliveryChannels.None,
            eventTime: EventTime.AddMinutes(5),
            hashSeed: 2));

        Assert.AreEqual(AlertOccurrenceOutcome.Accepted, first.Outcome);
        Assert.AreEqual(AlertOccurrenceOutcome.Suppressed, second.Outcome);
        Assert.IsNull(second.NotificationId);
        Assert.AreEqual(2, await CountAsync("alert_occurrence"));
        Assert.AreEqual(1, await CountAsync("notification"));
    }

    [TestMethod]
    public async Task CommitAsync_CautionThenAlert_AcceptsEscalation()
    {
        var caution = await store.CommitAsync(CommitRequest(
            AlertType.Caution,
            AlertDeliveryChannels.None,
            hashSeed: 1));
        var alert = await store.CommitAsync(CommitRequest(
            AlertType.Alert,
            AlertDeliveryChannels.None,
            eventTime: EventTime.AddMinutes(5),
            hashSeed: 2));

        Assert.AreEqual(AlertOccurrenceOutcome.Accepted, caution.Outcome);
        Assert.AreEqual(AlertOccurrenceOutcome.Accepted, alert.Outcome);
        Assert.AreEqual(2, await CountAsync("notification"));
    }

    [TestMethod]
    public async Task CommitAsync_RepeatedAlert_SuppressesSecondOccurrence()
    {
        await store.CommitAsync(CommitRequest(
            AlertType.Alert,
            AlertDeliveryChannels.None,
            hashSeed: 1));

        var second = await store.CommitAsync(CommitRequest(
            AlertType.Alert,
            AlertDeliveryChannels.None,
            eventTime: EventTime.AddMinutes(5),
            hashSeed: 2));

        Assert.AreEqual(AlertOccurrenceOutcome.Suppressed, second.Outcome);
        Assert.AreEqual(1, await CountAsync("notification"));
    }

    [TestMethod]
    public async Task CommitAsync_NotificationAtLowerWindowBoundary_IsIncluded()
    {
        var incomingTime = EventTime.AddHours(1);
        await store.CommitAsync(CommitRequest(
            AlertType.Caution,
            AlertDeliveryChannels.None,
            eventTime: EventTime,
            hashSeed: 1));

        var boundary = await store.CommitAsync(CommitRequest(
            AlertType.Caution,
            AlertDeliveryChannels.None,
            eventTime: incomingTime,
            hashSeed: 2));

        Assert.AreEqual(AlertOccurrenceOutcome.Suppressed, boundary.Outcome);
        Assert.AreEqual(1, await CountAsync("notification"));
    }

    [TestMethod]
    public async Task CommitAsync_NotificationAfterEventTime_IsExcluded()
    {
        await store.CommitAsync(CommitRequest(
            AlertType.Caution,
            AlertDeliveryChannels.None,
            eventTime: EventTime.AddMinutes(1),
            hashSeed: 1));

        var olderEvent = await store.CommitAsync(CommitRequest(
            AlertType.Caution,
            AlertDeliveryChannels.None,
            eventTime: EventTime,
            hashSeed: 2));

        Assert.AreEqual(AlertOccurrenceOutcome.Accepted, olderEvent.Outcome);
        Assert.AreEqual(2, await CountAsync("notification"));
    }

    [TestMethod]
    public async Task CommitAsync_AlertThenCaution_DoesNotDowngrade()
    {
        await store.CommitAsync(CommitRequest(
            AlertType.Alert,
            AlertDeliveryChannels.None,
            hashSeed: 1));

        var caution = await store.CommitAsync(CommitRequest(
            AlertType.Caution,
            AlertDeliveryChannels.None,
            eventTime: EventTime.AddMinutes(1),
            hashSeed: 2));

        Assert.AreEqual(AlertOccurrenceOutcome.Suppressed, caution.Outcome);
        Assert.AreEqual(1, await CountAsync("notification"));
    }

    [TestMethod]
    public async Task CommitAsync_ContactOutsideEventTimeSchedule_IsExcluded()
    {
        await ExecuteAsync(
            "UPDATE notification_setting SET start_time = @start, end_time = @end;",
            command =>
            {
                command.Parameters.AddWithValue("start", NpgsqlDbType.Time, new TimeSpan(11, 0, 0));
                command.Parameters.AddWithValue("end", NpgsqlDbType.Time, new TimeSpan(12, 0, 0));
            });

        await store.CommitAsync(CommitRequest(
            AlertType.Alert,
            AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms));

        CollectionAssert.AreEqual(
            new[] { "MqttAlert" },
            await ReadStringsAsync("SELECT kind FROM alert_delivery_outbox ORDER BY kind"));
    }

    [TestMethod]
    public async Task CommitAsync_DuplicateCanonicalContacts_CreateOneDeliveryPerKindAndDestination()
    {
        await SeedAdditionalContactAsync(
            Guid.Parse("77777777-7777-7777-7777-777777777777"),
            Guid.Parse("88888888-8888-8888-8888-888888888888"),
            Guid.Parse("99999999-9999-9999-9999-999999999999"),
            "ops@example.test",
            "+15550001111");

        await store.CommitAsync(CommitRequest(
            AlertType.Alert,
            AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms));

        Assert.AreEqual(3, await CountAsync("alert_delivery_outbox"));
        CollectionAssert.AreEquivalent(
            new[] { "MqttAlert", "Email", "Sms" },
            await ReadStringsAsync("SELECT kind FROM alert_delivery_outbox"));
    }

    [TestMethod]
    public async Task CommitAsync_AspNetUserIdWithDifferentTextCasing_PlansContactDeliveries()
    {
        var casedUserId = Guid.Parse("abcdefab-cdef-abcd-efab-cdefabcdefab");
        await ExecuteAsync(
            """
            UPDATE site_user SET user_id = @user_id WHERE id = @site_user_id;
            UPDATE "AspNetUsers" SET "Id" = @user_id_text WHERE "Id" = @old_user_id_text;
            """,
            command =>
            {
                command.Parameters.AddWithValue("user_id", casedUserId);
                command.Parameters.AddWithValue("site_user_id", SiteUserId);
                command.Parameters.AddWithValue("user_id_text", casedUserId.ToString("D").ToUpperInvariant());
                command.Parameters.AddWithValue("old_user_id_text", UserId.ToString("D"));
            });

        await store.CommitAsync(CommitRequest(
            AlertType.Alert,
            AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms));

        CollectionAssert.AreEquivalent(
            new[] { "Email", "Sms" },
            await ReadStringsAsync("SELECT kind FROM alert_delivery_outbox"));
    }

    [TestMethod]
    public async Task CommitAsync_NoContactsStillPlansRequestedMqtt()
    {
        await ExecuteAsync("DELETE FROM notification_setting; DELETE FROM site_user; DELETE FROM \"AspNetUsers\";");

        await store.CommitAsync(CommitRequest(
            AlertType.Alert,
            AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms));

        Assert.AreEqual(1, await CountAsync("alert_delivery_outbox"));
        CollectionAssert.AreEqual(
            new[] { "MqttAlert" },
            await ReadStringsAsync("SELECT kind FROM alert_delivery_outbox ORDER BY kind"));
    }

    [TestMethod]
    public async Task CommitAsync_ExactSequentialReplay_ReturnsDurableDuplicateWithoutNewRows()
    {
        var request = CommitRequest(
            AlertType.Alert,
            AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms);

        var first = await store.CommitAsync(request);
        var replay = await store.CommitAsync(request);

        Assert.IsFalse(first.IsDuplicate);
        Assert.IsTrue(replay.IsDuplicate);
        Assert.AreEqual(first.OccurrenceId, replay.OccurrenceId);
        Assert.AreEqual(first.NotificationId, replay.NotificationId);
        Assert.AreEqual(first.Outcome, replay.Outcome);
        Assert.AreEqual(1, await CountAsync("alert_occurrence"));
        Assert.AreEqual(1, await CountAsync("notification"));
        Assert.AreEqual(3, await CountAsync("alert_delivery_outbox"));
    }

    [TestMethod]
    public async Task CommitAsync_UncommittedConflictingOccurrence_RecoversDurableDuplicate()
    {
        var request = CommitRequest(
            AlertType.Alert,
            AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms);
        var occurrenceId = Guid.NewGuid();
        var applicationName = $"rvt-duplicate-{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(database!.ConnectionString)
        {
            ApplicationName = applicationName
        }.ConnectionString;
        var monitorOptions = new MonitorDbOptions(
            MonitorDatabaseProvider.PostgreSql,
            new Dictionary<string, string>());
        var concurrentStore = new EfAlertCommitStore<OmnidotsMonitorContext>(
            new OmnidotsMonitorContextFactory(connectionString, monitorOptions),
            new CautionAlertAcceptancePolicy());

        await using var blockingConnection = database.OpenConnection();
        await blockingConnection.OpenAsync();
        await using var blockingTransaction = await blockingConnection.BeginTransactionAsync();
        await using (var insert = new NpgsqlCommand(
            """
            INSERT INTO alert_occurrence
                (id, source, source_key_hash, notification_id, monitor_id, serial_id,
                 event_time, alert_type, alert_field, level, limit_on, averaging_period,
                 outcome, created_at)
            VALUES
                (@id, @source, @source_key_hash, NULL, @monitor_id, @serial_id,
                 @event_time, @alert_type, @alert_field, @level, @limit_on, @averaging_period,
                 'Ignored', @created_at);
            """,
            blockingConnection,
            blockingTransaction))
        {
            insert.Parameters.AddWithValue("id", occurrenceId);
            insert.Parameters.AddWithValue("source", request.Signal.Source);
            insert.Parameters.AddWithValue("source_key_hash", NpgsqlDbType.Bytea, request.SourceKeyHash);
            insert.Parameters.AddWithValue("monitor_id", MonitorId);
            insert.Parameters.AddWithValue("serial_id", request.Signal.SerialId);
            insert.Parameters.AddWithValue("event_time", request.Signal.EventTime);
            insert.Parameters.AddWithValue("alert_type", (int)request.Signal.AlertType);
            insert.Parameters.AddWithValue("alert_field", request.Signal.Field);
            insert.Parameters.AddWithValue("level", request.Signal.Level);
            insert.Parameters.AddWithValue("limit_on", request.Signal.Limit);
            insert.Parameters.AddWithValue("averaging_period", request.Signal.AveragingPeriod);
            insert.Parameters.AddWithValue("created_at", request.CreatedAt);
            await insert.ExecuteNonQueryAsync();
        }

        var commitTask = concurrentStore.CommitAsync(request);
        await WaitForDatabaseLockAsync(applicationName);
        await blockingTransaction.CommitAsync();

        var result = await commitTask.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.IsTrue(result.IsDuplicate);
        Assert.AreEqual(occurrenceId, result.OccurrenceId);
        Assert.AreEqual(AlertOccurrenceOutcome.Ignored, result.Outcome);
        Assert.IsNull(result.NotificationId);
        Assert.AreEqual(1, await CountAsync("alert_occurrence"));
        Assert.AreEqual(0, await CountAsync("notification"));
        Assert.AreEqual(0, await CountAsync("alert_delivery_outbox"));
    }

    [TestMethod]
    public async Task CommitAsync_OutboxInsertFailure_RollsBackOccurrenceAndNotification()
    {
        await ExecuteAsync(
            "UPDATE \"AspNetUsers\" SET \"Email\" = @email, \"PhoneNumber\" = NULL;",
            command => command.Parameters.AddWithValue("email", new string('x', 513)));

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => store.CommitAsync(CommitRequest(
                AlertType.Alert,
                AlertDeliveryChannels.Email)));

        Assert.IsFalse(exception.Message.Contains(new string('x', 513), StringComparison.Ordinal));
        Assert.AreEqual(0, await CountAsync("alert_occurrence"));
        Assert.AreEqual(0, await CountAsync("notification"));
        Assert.AreEqual(0, await CountAsync("alert_delivery_outbox"));
    }

    [TestMethod]
    public async Task CommitAsync_MissingMonitor_DoesNotCreateOccurrence()
    {
        var request = CommitRequest(
            AlertType.Alert,
            AlertDeliveryChannels.Mqtt) with
        {
            Signal = CommitRequest(AlertType.Alert, AlertDeliveryChannels.Mqtt).Signal with
            {
                SerialId = "missing-monitor"
            }
        };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => store.CommitAsync(request));

        Assert.AreEqual(0, await CountAsync("alert_occurrence"));
        Assert.AreEqual(0, await CountAsync("notification"));
        Assert.AreEqual(0, await CountAsync("alert_delivery_outbox"));
    }

    private static AlertCommitRequest CommitRequest(
        AlertType alertType,
        AlertDeliveryChannels channels,
        DateTime? eventTime = null,
        byte hashSeed = 1)
    {
        var sourceKeyHash = Enumerable.Repeat(hashSeed, 32).ToArray();
        return new AlertCommitRequest(
            new AlertSignal(
                "omnidots.webhook",
                $"alarm/{SerialId}/{hashSeed}",
                eventTime ?? EventTime,
                SerialId,
                alertType,
                "Vtop",
                7.5,
                5.0,
                60,
                "Vibration threshold exceeded.",
                channels,
                TimeSpan.FromHours(1)),
            sourceKeyHash,
            AlertIdentity.CreateNotificationId("omnidots.webhook", sourceKeyHash),
            EventTime.AddMinutes(2));
    }

    private static async Task SeedContactGraphAsync()
    {
        const string sql = """
            INSERT INTO site (id, site_name, create_date)
            VALUES (@site_id, 'Task 4 site', @created_at);

            INSERT INTO monitor
                (id, serial_id, customer_id, listed_at_time, model, manufacturer,
                 firmware_version, type_of_monitor)
            VALUES
                (@monitor_id, @serial_id, 42, @listed_at, 'SWARM', 'Omnidots', '1.0', 2);

            DELETE FROM deployment WHERE monitor_id = @monitor_id;
            DELETE FROM contract WHERE id = @contract_id;

            INSERT INTO contract
                (id, contract_number, on_hire_date, off_hire_date, company_id, site_id)
            VALUES
                (@contract_id, 'task-4-contract', @start_date, NULL,
                 @company_id, @site_id);

            INSERT INTO deployment
                (id, start_date, end_date, lng, lat, contract_id, monitor_id)
            VALUES
                (@deployment_id, @start_date, NULL, 0, 0, @contract_id, @monitor_id);

            INSERT INTO "AspNetUsers"
                ("Id", is_disabled, "Email", email_confirmed, "PhoneNumber",
                 phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count)
            VALUES
                (@user_id_text, false, @email, true, @phone, true, false, false, 0);

            INSERT INTO site_user (id, start_date, end_date, user_id, site_id)
            VALUES (@site_user_id, @start_date, NULL, @user_id, @site_id);

            INSERT INTO notification_setting
                (id, email, sms, start_time, end_time, site_user_id)
            VALUES (@setting_id, true, true, NULL, NULL, @site_user_id);
            """;

        await ExecuteAsync(
            sql,
            command =>
            {
                command.Parameters.AddWithValue("site_id", SiteId);
                command.Parameters.AddWithValue("created_at", EventTime.AddYears(-1));
                command.Parameters.AddWithValue("monitor_id", MonitorId);
                command.Parameters.AddWithValue("serial_id", SerialId);
                command.Parameters.AddWithValue("listed_at", EventTime.AddYears(-1));
                command.Parameters.AddWithValue("contract_id", ContractId);
                command.Parameters.AddWithValue(
                    "company_id",
                    Guid.Parse("33333333-3333-3333-3333-333333333333"));
                command.Parameters.AddWithValue(
                    "deployment_id",
                    Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
                command.Parameters.AddWithValue("user_id_text", UserId.ToString("D"));
                command.Parameters.AddWithValue("email", DefaultEmail);
                command.Parameters.AddWithValue("phone", DefaultPhone);
                command.Parameters.AddWithValue("site_user_id", SiteUserId);
                command.Parameters.AddWithValue("start_date", EventTime.AddYears(-1));
                command.Parameters.AddWithValue("user_id", UserId);
                command.Parameters.AddWithValue("setting_id", NotificationSettingId);
            });
    }

    private static Task SeedAdditionalContactAsync(
        Guid userId,
        Guid siteUserId,
        Guid settingId,
        string email,
        string phone) =>
        ExecuteAsync(
            """
            INSERT INTO "AspNetUsers"
                ("Id", is_disabled, "Email", email_confirmed, "PhoneNumber",
                 phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count)
            VALUES
                (@user_id_text, false, @email, true, @phone, true, false, false, 0);

            INSERT INTO site_user (id, start_date, end_date, user_id, site_id)
            VALUES (@site_user_id, @start_date, NULL, @user_id, @site_id);

            INSERT INTO notification_setting
                (id, email, sms, start_time, end_time, site_user_id)
            VALUES (@setting_id, true, true, NULL, NULL, @site_user_id);
            """,
            command =>
            {
                command.Parameters.AddWithValue("user_id_text", userId.ToString("D"));
                command.Parameters.AddWithValue("email", email);
                command.Parameters.AddWithValue("phone", phone);
                command.Parameters.AddWithValue("site_user_id", siteUserId);
                command.Parameters.AddWithValue("start_date", EventTime.AddYears(-1));
                command.Parameters.AddWithValue("user_id", userId);
                command.Parameters.AddWithValue("site_id", SiteId);
                command.Parameters.AddWithValue("setting_id", settingId);
            });

    private static async Task<int> CountAsync(string table)
    {
        var sql = table switch
        {
            "alert_occurrence" => "SELECT COUNT(*) FROM alert_occurrence;",
            "notification" => "SELECT COUNT(*) FROM notification;",
            "alert_delivery_outbox" => "SELECT COUNT(*) FROM alert_delivery_outbox;",
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };

        await using var connection = database!.OpenConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<string[]> ReadStringsAsync(string query)
    {
        var sql = query switch
        {
            "SELECT kind FROM alert_delivery_outbox" =>
                "SELECT kind FROM alert_delivery_outbox;",
            "SELECT kind FROM alert_delivery_outbox ORDER BY kind" =>
                "SELECT kind FROM alert_delivery_outbox ORDER BY kind;",
            "SELECT payload FROM alert_delivery_outbox" =>
                "SELECT payload FROM alert_delivery_outbox;",
            _ => throw new ArgumentOutOfRangeException(nameof(query))
        };

        var values = new List<string>();
        await using var connection = database!.OpenConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values.ToArray();
    }

    private static async Task ExecuteAsync(
        string sql,
        Action<NpgsqlCommand>? addParameters = null)
    {
        await using var connection = database!.OpenConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        addParameters?.Invoke(command);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task WaitForDatabaseLockAsync(string applicationName)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            await using var connection = database!.OpenConnection();
            await connection.OpenAsync(timeout.Token);
            await using var command = new NpgsqlCommand(
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_stat_activity
                    WHERE application_name = @application_name
                      AND wait_event_type = 'Lock');
                """,
                connection);
            command.Parameters.AddWithValue("application_name", applicationName);
            if ((bool)(await command.ExecuteScalarAsync(timeout.Token))!)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
        }
    }
}
