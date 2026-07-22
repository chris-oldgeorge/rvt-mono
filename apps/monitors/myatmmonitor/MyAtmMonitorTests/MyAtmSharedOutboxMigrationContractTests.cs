using System.Text.RegularExpressions;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmSharedOutboxMigrationContractTests
{
    private const string ForwardPostgreSql = "2026-07-15-migrate-myatm-outbox-to-shared.postgres.sql";
    private const string ForwardSqlServer = "2026-07-15-migrate-myatm-outbox-to-shared.sqlserver.sql";
    private const string RollbackPostgreSql = "2026-07-15-rollback-myatm-outbox-to-local.postgres.sql";
    private const string RollbackSqlServer = "2026-07-15-rollback-myatm-outbox-to-local.sqlserver.sql";

    [DataTestMethod]
    [DataRow(ForwardPostgreSql, "'Leased' THEN 'InProgress'")]
    [DataRow(ForwardSqlServer, "N'Leased' THEN N'InProgress'")]
    public void ForwardMigration_MapsTheLegacyLeaseState(string file, string mapping)
    {
        var sql = MigrationText(file);

        StringAssert.Contains(sql, mapping);
        StringAssert.Contains(sql, "MyAtm");
        StringAssert.Contains(sql, "PayloadVersion");
        Assert.IsFalse(sql.Contains("DROP TABLE", StringComparison.OrdinalIgnoreCase));
    }

    [DataTestMethod]
    [DataRow(ForwardPostgreSql, "to_regclass('monitor_delivery_outbox') IS NULL", "RAISE EXCEPTION", "status NOT IN ('Pending', 'Leased', 'Completed', 'DeadLetter')", "kind NOT IN ('MqttDataInserted', 'MqttAlert', 'Email', 'Sms')")]
    [DataRow(ForwardSqlServer, "OBJECT_ID(N'[dbo].[MonitorDeliveryOutbox]', N'U') IS NULL", "THROW", "[Status] NOT IN (N'Pending', N'Leased', N'Completed', N'DeadLetter')", "[Kind] NOT IN (N'MqttDataInserted', N'MqttAlert', N'Email', N'Sms')")]
    public void ForwardMigration_GuardsPrerequisitesAndLegacyValues(
        string file,
        string prerequisite,
        string abort,
        string statusGuard,
        string kindGuard)
    {
        var sql = MigrationText(file);

        StringAssert.Contains(sql, prerequisite);
        StringAssert.Contains(sql, abort);
        StringAssert.Contains(sql, statusGuard);
        StringAssert.Contains(sql, kindGuard);
    }

    [DataTestMethod]
    [DataRow(ForwardPostgreSql, "'MyAtm',", "1,", "legacy.occurrence_key", "legacy.delivery_key")]
    [DataRow(ForwardSqlServer, "N'MyAtm' AS [Producer]", "CAST(1 AS int) AS [PayloadVersion]", "legacy.[OccurrenceKey] AS [CorrelationKey]", "legacy.[DeliveryKey]")]
    public void ForwardMigration_UsesTheVersionOneMyAtmIdentityAndExistingKeys(
        string file,
        string producer,
        string payloadVersion,
        string correlationKey,
        string deliveryKey)
    {
        var sql = MigrationText(file);

        StringAssert.Contains(sql, producer);
        StringAssert.Contains(sql, payloadVersion);
        StringAssert.Contains(sql, correlationKey);
        StringAssert.Contains(sql, deliveryKey);
    }

    [DataTestMethod]
    [DataRow(ForwardPostgreSql, "LEFT JOIN my_atm_alert_occurrence", "LEFT JOIN notification", "occurrence.notification_id = notification.id", "legacy.payload", "NULL, -- dead_lettered_at")]
    [DataRow(ForwardSqlServer, "LEFT JOIN [dbo].[MyAtmAlertOccurrences]", "LEFT JOIN [dbo].[Notifications]", "occurrence.[NotificationId] = notification.[Id]", "legacy.[Payload]", "NULL, -- DeadLetteredAt")]
    public void ForwardMigration_PreservesPayloadAndCopiesOnlyExistingNotifications(
        string file,
        string occurrenceJoin,
        string notificationJoin,
        string notificationPredicate,
        string payload,
        string deadLetteredAt)
    {
        var sql = MigrationText(file);

        StringAssert.Contains(sql, occurrenceJoin);
        StringAssert.Contains(sql, notificationJoin);
        StringAssert.Contains(sql, notificationPredicate);
        StringAssert.Contains(sql, payload);
        StringAssert.Contains(sql, deadLetteredAt);
    }

    [DataTestMethod]
    [DataRow(ForwardPostgreSql, "ON CONFLICT (producer, delivery_key)", "shared.attempt_count < EXCLUDED.attempt_count", "shared.status IN ('Completed', 'DeadLetter')", "EXCLUDED.status NOT IN ('Completed', 'DeadLetter')")]
    [DataRow(ForwardSqlServer, "MERGE [dbo].[MonitorDeliveryOutbox] WITH (HOLDLOCK)", "shared.[AttemptCount] < source.[AttemptCount]", "shared.[Status] IN (N'Completed', N'DeadLetter')", "source.[Status] NOT IN (N'Completed', N'DeadLetter')")]
    public void ForwardMigration_ProtectsNewerAndTerminalSharedState(
        string file,
        string conflict,
        string attempts,
        string terminal,
        string nonTerminal)
    {
        var sql = MigrationText(file);

        StringAssert.Contains(sql, conflict);
        StringAssert.Contains(sql, attempts);
        StringAssert.Contains(sql, terminal);
        StringAssert.Contains(sql, nonTerminal);
    }

    [DataTestMethod]
    [DataRow(
        ForwardPostgreSql,
        "shared.attempt_count < EXCLUDED.attempt_count",
        "shared.status = 'Completed'",
        "EXCLUDED.status = 'Completed'",
        "shared.completed_at IS NOT NULL",
        "EXCLUDED.completed_at IS NULL OR shared.completed_at > EXCLUDED.completed_at")]
    [DataRow(
        ForwardSqlServer,
        "shared.[AttemptCount] < source.[AttemptCount]",
        "shared.[Status] = N'Completed'",
        "source.[Status] = N'Completed'",
        "shared.[CompletedAt] IS NOT NULL",
        "source.[CompletedAt] IS NULL OR shared.[CompletedAt] > source.[CompletedAt]")]
    public void ForwardMigration_PreservesANewerSharedCompletionAtEqualAttempt(
        string file,
        string higherAttempt,
        string sharedCompleted,
        string sourceCompleted,
        string sharedCompletionExists,
        string sourceCompletionIsOlderOrMissing)
    {
        var sql = MigrationText(file);

        StringAssert.Contains(sql, higherAttempt);
        StringAssert.Contains(sql, sharedCompleted);
        StringAssert.Contains(sql, sourceCompleted);
        StringAssert.Contains(sql, sharedCompletionExists);
        StringAssert.Contains(sql, sourceCompletionIsOlderOrMissing);
    }

    [DataTestMethod]
    [DataRow(RollbackPostgreSql, "producer = 'MyAtm'", "payload_version = 1", "'InProgress' THEN 'Leased'", "LEFT JOIN my_atm_alert_occurrence", "ON CONFLICT (delivery_key)")]
    [DataRow(RollbackSqlServer, "[Producer] = N'MyAtm'", "[PayloadVersion] = 1", "N'InProgress' THEN N'Leased'", "LEFT JOIN [dbo].[MyAtmAlertOccurrences]", "MERGE [dbo].[MyAtmOutboxMessages] WITH (HOLDLOCK)")]
    public void RollbackMigration_IsFilteredAuthoritativeAndIdempotent(
        string file,
        string producer,
        string payloadVersion,
        string mapping,
        string occurrenceJoin,
        string conflict)
    {
        var sql = MigrationText(file);

        StringAssert.Contains(sql, producer);
        StringAssert.Contains(sql, payloadVersion);
        StringAssert.Contains(sql, mapping);
        StringAssert.Contains(sql, occurrenceJoin);
        StringAssert.Contains(sql, conflict);
        Assert.IsFalse(Regex.IsMatch(sql, @"\b(?:DELETE\s+FROM|UPDATE)\s+(?:\[dbo\]\.\[MonitorDeliveryOutbox\]|monitor_delivery_outbox)", RegexOptions.IgnoreCase));
        Assert.IsFalse(sql.Contains("DROP TABLE", StringComparison.OrdinalIgnoreCase));
    }

    [DataTestMethod]
    [DataRow(RollbackPostgreSql, "to_regclass('monitor_delivery_outbox') IS NULL", "to_regclass('my_atm_outbox_message') IS NULL", "RAISE EXCEPTION")]
    [DataRow(RollbackSqlServer, "OBJECT_ID(N'[dbo].[MonitorDeliveryOutbox]', N'U') IS NULL", "OBJECT_ID(N'[dbo].[MyAtmOutboxMessages]', N'U') IS NULL", "THROW")]
    public void RollbackMigration_GuardsBothSharedAndLegacyPrerequisites(
        string file,
        string sharedGuard,
        string legacyGuard,
        string abort)
    {
        var sql = MigrationText(file);

        StringAssert.Contains(sql, sharedGuard);
        StringAssert.Contains(sql, legacyGuard);
        StringAssert.Contains(sql, abort);
    }

    [TestMethod]
    public void SqlServerRollback_WidensLegacyLastErrorBeforeSynchronization()
    {
        var sql = MigrationText(RollbackSqlServer);
        var alterPosition = sql.IndexOf("ALTER COLUMN [LastError] nvarchar(1024) NULL", StringComparison.Ordinal);
        var mergePosition = sql.IndexOf("MERGE [dbo].[MyAtmOutboxMessages]", StringComparison.Ordinal);

        StringAssert.Contains(sql, "FROM sys.columns");
        StringAssert.Contains(sql, "[max_length] < 2048");
        StringAssert.Contains(sql, "ALTER TABLE [dbo].[MyAtmOutboxMessages]");
        Assert.IsTrue(alterPosition >= 0, "Rollback must widen LastError to the shared 1024-character capacity.");
        Assert.IsTrue(mergePosition > alterPosition, "LastError must be widened before shared rows are synchronized.");
    }

    private static string MigrationText(string file)
    {
        var path = Path.Combine(FindRepositoryRoot(), "myatmmonitor", "database", "migrations", file);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Expected migration asset '{path}' to exist.", path);
        }

        return File.ReadAllText(path);
    }

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
}
