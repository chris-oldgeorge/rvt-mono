using System.Text.RegularExpressions;
using Rvt.Monitor.IntegrationTesting;

namespace MyAtmMonitorTests;

[TestClass]
public sealed partial class MyAtmSharedOutboxMigrationContractTests
{
    private const string AddDurableOutbox = "2026-07-14-add-durable-outbox.postgres.sql";
    private const string AddHardening = "2026-07-14-add-myatm-hardening.postgres.sql";
    private const string RemoveHardening = "2026-07-14-remove-myatm-hardening.postgres.sql";
    private const string ForwardSharedOutbox = "2026-07-15-migrate-myatm-outbox-to-shared.postgres.sql";
    private const string RollbackSharedOutbox = "2026-07-15-rollback-myatm-outbox-to-local.postgres.sql";

    private static readonly string[] SupportedMigrations =
    [
        AddDurableOutbox,
        AddHardening,
        RemoveHardening,
        ForwardSharedOutbox,
        RollbackSharedOutbox
    ];

    [TestMethod]
    public void MigrationDirectory_ContainsOnlyOrderedPostgreSqlAssets()
    {
        string?[] migrationFiles = [.. Directory
            .GetFiles(MigrationDirectory(), "*.sql", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(file => file, StringComparer.Ordinal)];

        CollectionAssert.AreEqual(SupportedMigrations, migrationFiles);
    }

    [TestMethod]
    public void MigrationDirectory_RejectsRetiredProviderFilenames()
    {
        string?[] retiredProviderFiles = [.. Directory
            .GetFiles(MigrationDirectory(), "*.sql", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(file => file!.Contains(".sql" + "server.", StringComparison.OrdinalIgnoreCase))];

        Assert.HasCount(0, retiredProviderFiles);
    }

    [TestMethod]
    [DataRow(AddDurableOutbox, "CREATE TABLE IF NOT EXISTS", "CREATE INDEX IF NOT EXISTS")]
    [DataRow(AddHardening, "ADD COLUMN IF NOT EXISTS", "CREATE INDEX IF NOT EXISTS")]
    [DataRow(RemoveHardening, "DROP INDEX IF EXISTS", "DROP COLUMN IF EXISTS")]
    [DataRow(ForwardSharedOutbox, "ON CONFLICT (producer, delivery_key) DO UPDATE", "BEGIN;")]
    [DataRow(RollbackSharedOutbox, "ON CONFLICT (delivery_key) DO UPDATE", "BEGIN;")]
    public void PostgreSqlMigrations_AreRerunnable(
        string file,
        string firstRerunGuard,
        string secondRerunGuard)
    {
        string sql = MigrationText(file);

        Assert.Contains(firstRerunGuard, sql);
        Assert.Contains(secondRerunGuard, sql);
    }

    [TestMethod]
    public void HardeningMigrations_AreForwardRollbackPairs()
    {
        string forward = MigrationText(AddHardening);
        string rollback = MigrationText(RemoveHardening);

        Assert.Contains("ADD COLUMN IF NOT EXISTS lease_id uuid NULL", forward);
        Assert.Contains("DROP COLUMN IF EXISTS lease_id", rollback);
        Assert.Contains("CREATE INDEX IF NOT EXISTS ix_my_atm_alert_occurrence_recent_lookup", forward);
        Assert.Contains("DROP INDEX IF EXISTS ix_my_atm_alert_occurrence_recent_lookup", rollback);
    }

    [TestMethod]
    public void ForwardMigration_MapsTheLegacyLeaseState()
    {
        string sql = MigrationText(ForwardSharedOutbox);

        Assert.Contains("'Leased' THEN 'InProgress'", sql);
        Assert.Contains("MyAtm", sql);
        Assert.Contains("PayloadVersion", sql);
        Assert.IsFalse(sql.Contains("DROP TABLE", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ForwardMigration_GuardsPrerequisitesAndLegacyValues()
    {
        string sql = MigrationText(ForwardSharedOutbox);

        Assert.Contains("to_regclass('monitor_delivery_outbox') IS NULL", sql);
        Assert.Contains("RAISE EXCEPTION", sql);
        Assert.Contains("status NOT IN ('Pending', 'Leased', 'Completed', 'DeadLetter')", sql);
        Assert.Contains("kind NOT IN ('MqttDataInserted', 'MqttAlert', 'Email', 'Sms')", sql);
    }

    [TestMethod]
    public void ForwardMigration_UsesTheVersionOneMyAtmIdentityAndExistingKeys()
    {
        string sql = MigrationText(ForwardSharedOutbox);

        Assert.Contains("'MyAtm',", sql);
        Assert.Contains("1,", sql);
        Assert.Contains("legacy.occurrence_key", sql);
        Assert.Contains("legacy.delivery_key", sql);
    }

    [TestMethod]
    public void ForwardMigration_PreservesPayloadAndCopiesOnlyExistingNotifications()
    {
        string sql = MigrationText(ForwardSharedOutbox);

        Assert.Contains("LEFT JOIN my_atm_alert_occurrence", sql);
        Assert.Contains("LEFT JOIN notification", sql);
        Assert.Contains("occurrence.notification_id = notification.id", sql);
        Assert.Contains("legacy.payload", sql);
        Assert.Contains("NULL, -- dead_lettered_at", sql);
    }

    [TestMethod]
    public void ForwardMigration_ProtectsNewerAndTerminalSharedState()
    {
        string sql = MigrationText(ForwardSharedOutbox);

        Assert.Contains("ON CONFLICT (producer, delivery_key)", sql);
        Assert.Contains("shared.attempt_count < EXCLUDED.attempt_count", sql);
        Assert.Contains("shared.status IN ('Completed', 'DeadLetter')", sql);
        Assert.Contains("EXCLUDED.status NOT IN ('Completed', 'DeadLetter')", sql);
    }

    [TestMethod]
    public void ForwardMigration_PreservesANewerSharedCompletionAtEqualAttempt()
    {
        string sql = MigrationText(ForwardSharedOutbox);

        Assert.Contains("shared.attempt_count < EXCLUDED.attempt_count", sql);
        Assert.Contains("shared.status = 'Completed'", sql);
        Assert.Contains("EXCLUDED.status = 'Completed'", sql);
        Assert.Contains("shared.completed_at IS NOT NULL", sql);
        Assert.Contains("EXCLUDED.completed_at IS NULL OR shared.completed_at > EXCLUDED.completed_at", sql);
    }

    [TestMethod]
    public void RollbackMigration_IsFilteredAuthoritativeAndIdempotent()
    {
        string sql = MigrationText(RollbackSharedOutbox);

        Assert.Contains("producer = 'MyAtm'", sql);
        Assert.Contains("payload_version = 1", sql);
        Assert.Contains("'InProgress' THEN 'Leased'", sql);
        Assert.Contains("LEFT JOIN my_atm_alert_occurrence", sql);
        Assert.Contains("ON CONFLICT (delivery_key)", sql);
        Assert.IsFalse(DestructiveOutboxMutationPattern().IsMatch(sql));
        Assert.IsFalse(sql.Contains("DROP TABLE", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void RollbackMigration_GuardsBothSharedAndLegacyPrerequisites()
    {
        string sql = MigrationText(RollbackSharedOutbox);

        Assert.Contains("to_regclass('monitor_delivery_outbox') IS NULL", sql);
        Assert.Contains("to_regclass('my_atm_outbox_message') IS NULL", sql);
        Assert.Contains("RAISE EXCEPTION", sql);
    }

    private static string MigrationText(string file)
    {
        string path = Path.Combine(MigrationDirectory(), file);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Expected migration asset '{path}' to exist.", path);
        }

        return File.ReadAllText(path);
    }

    private static string MigrationDirectory() =>
        RepositoryLayout.GetPath(
            "apps",
            "monitors",
            "myatmmonitor",
            "database",
            "migrations");
    [GeneratedRegex(
        @"\b(?:DELETE\s+FROM|UPDATE)\s+monitor_delivery_outbox",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DestructiveOutboxMutationPattern();
}
