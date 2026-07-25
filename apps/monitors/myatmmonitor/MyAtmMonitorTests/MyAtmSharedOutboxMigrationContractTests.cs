using System.Text.RegularExpressions;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmSharedOutboxMigrationContractTests
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
        var migrationFiles = Directory
            .GetFiles(MigrationDirectory(), "*.sql", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(SupportedMigrations, migrationFiles);
    }

    [TestMethod]
    public void MigrationDirectory_RejectsRetiredProviderFilenames()
    {
        var retiredProviderFiles = Directory
            .GetFiles(MigrationDirectory(), "*.sql", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(file => file!.Contains(".sqlserver.", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.HasCount(0, retiredProviderFiles);
    }

    [DataTestMethod]
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
        var sql = MigrationText(file);

        StringAssert.Contains(sql, firstRerunGuard);
        StringAssert.Contains(sql, secondRerunGuard);
    }

    [TestMethod]
    public void HardeningMigrations_AreForwardRollbackPairs()
    {
        var forward = MigrationText(AddHardening);
        var rollback = MigrationText(RemoveHardening);

        StringAssert.Contains(forward, "ADD COLUMN IF NOT EXISTS lease_id uuid NULL");
        StringAssert.Contains(rollback, "DROP COLUMN IF EXISTS lease_id");
        StringAssert.Contains(forward, "CREATE INDEX IF NOT EXISTS ix_my_atm_alert_occurrence_recent_lookup");
        StringAssert.Contains(rollback, "DROP INDEX IF EXISTS ix_my_atm_alert_occurrence_recent_lookup");
    }

    [TestMethod]
    public void ForwardMigration_MapsTheLegacyLeaseState()
    {
        var sql = MigrationText(ForwardSharedOutbox);

        StringAssert.Contains(sql, "'Leased' THEN 'InProgress'");
        StringAssert.Contains(sql, "MyAtm");
        StringAssert.Contains(sql, "PayloadVersion");
        Assert.IsFalse(sql.Contains("DROP TABLE", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ForwardMigration_GuardsPrerequisitesAndLegacyValues()
    {
        var sql = MigrationText(ForwardSharedOutbox);

        StringAssert.Contains(sql, "to_regclass('monitor_delivery_outbox') IS NULL");
        StringAssert.Contains(sql, "RAISE EXCEPTION");
        StringAssert.Contains(sql, "status NOT IN ('Pending', 'Leased', 'Completed', 'DeadLetter')");
        StringAssert.Contains(sql, "kind NOT IN ('MqttDataInserted', 'MqttAlert', 'Email', 'Sms')");
    }

    [TestMethod]
    public void ForwardMigration_UsesTheVersionOneMyAtmIdentityAndExistingKeys()
    {
        var sql = MigrationText(ForwardSharedOutbox);

        StringAssert.Contains(sql, "'MyAtm',");
        StringAssert.Contains(sql, "1,");
        StringAssert.Contains(sql, "legacy.occurrence_key");
        StringAssert.Contains(sql, "legacy.delivery_key");
    }

    [TestMethod]
    public void ForwardMigration_PreservesPayloadAndCopiesOnlyExistingNotifications()
    {
        var sql = MigrationText(ForwardSharedOutbox);

        StringAssert.Contains(sql, "LEFT JOIN my_atm_alert_occurrence");
        StringAssert.Contains(sql, "LEFT JOIN notification");
        StringAssert.Contains(sql, "occurrence.notification_id = notification.id");
        StringAssert.Contains(sql, "legacy.payload");
        StringAssert.Contains(sql, "NULL, -- dead_lettered_at");
    }

    [TestMethod]
    public void ForwardMigration_ProtectsNewerAndTerminalSharedState()
    {
        var sql = MigrationText(ForwardSharedOutbox);

        StringAssert.Contains(sql, "ON CONFLICT (producer, delivery_key)");
        StringAssert.Contains(sql, "shared.attempt_count < EXCLUDED.attempt_count");
        StringAssert.Contains(sql, "shared.status IN ('Completed', 'DeadLetter')");
        StringAssert.Contains(sql, "EXCLUDED.status NOT IN ('Completed', 'DeadLetter')");
    }

    [TestMethod]
    public void ForwardMigration_PreservesANewerSharedCompletionAtEqualAttempt()
    {
        var sql = MigrationText(ForwardSharedOutbox);

        StringAssert.Contains(sql, "shared.attempt_count < EXCLUDED.attempt_count");
        StringAssert.Contains(sql, "shared.status = 'Completed'");
        StringAssert.Contains(sql, "EXCLUDED.status = 'Completed'");
        StringAssert.Contains(sql, "shared.completed_at IS NOT NULL");
        StringAssert.Contains(sql, "EXCLUDED.completed_at IS NULL OR shared.completed_at > EXCLUDED.completed_at");
    }

    [TestMethod]
    public void RollbackMigration_IsFilteredAuthoritativeAndIdempotent()
    {
        var sql = MigrationText(RollbackSharedOutbox);

        StringAssert.Contains(sql, "producer = 'MyAtm'");
        StringAssert.Contains(sql, "payload_version = 1");
        StringAssert.Contains(sql, "'InProgress' THEN 'Leased'");
        StringAssert.Contains(sql, "LEFT JOIN my_atm_alert_occurrence");
        StringAssert.Contains(sql, "ON CONFLICT (delivery_key)");
        Assert.IsFalse(Regex.IsMatch(
            sql,
            @"\b(?:DELETE\s+FROM|UPDATE)\s+monitor_delivery_outbox",
            RegexOptions.IgnoreCase));
        Assert.IsFalse(sql.Contains("DROP TABLE", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void RollbackMigration_GuardsBothSharedAndLegacyPrerequisites()
    {
        var sql = MigrationText(RollbackSharedOutbox);

        StringAssert.Contains(sql, "to_regclass('monitor_delivery_outbox') IS NULL");
        StringAssert.Contains(sql, "to_regclass('my_atm_outbox_message') IS NULL");
        StringAssert.Contains(sql, "RAISE EXCEPTION");
    }

    private static string MigrationText(string file)
    {
        var path = Path.Combine(MigrationDirectory(), file);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Expected migration asset '{path}' to exist.", path);
        }

        return File.ReadAllText(path);
    }

    private static string MigrationDirectory() =>
        Path.Combine(
            FindRepositoryRoot(),
            "apps",
            "monitors",
            "myatmmonitor",
            "database",
            "migrations");

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
