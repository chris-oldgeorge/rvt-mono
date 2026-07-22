using System.Text.RegularExpressions;

namespace Rvt.Monitor.CommonTests.Data.EntityFramework;

[TestClass]
public sealed class MonitorDeliveryMigrationContractTests
{
    private const string PostgreSqlMigration = "2026-07-15-add-monitor-delivery-outbox.postgres.sql";
    private const string SqlServerMigration = "2026-07-15-add-monitor-delivery-outbox.sqlserver.sql";

    [TestMethod]
    public void PostgreSqlMigration_CreatesSharedDeliveryOutboxContract()
    {
        var sql = ReadMigration(PostgreSqlMigration);

        StringAssert.Contains(sql, "CREATE TABLE IF NOT EXISTS monitor_delivery_outbox");
        StringAssert.Contains(sql, "UNIQUE (producer, delivery_key)");
        StringAssert.Contains(sql, "CHECK (status IN ('Pending', 'InProgress', 'Completed', 'DeadLetter'))");
        StringAssert.Contains(sql, "REFERENCES notification (id) ON DELETE SET NULL");
        StringAssert.Contains(sql, "dead_lettered_at timestamp with time zone NULL");
        StringAssert.Contains(sql, "CREATE INDEX IF NOT EXISTS ix_monitor_delivery_outbox_due");
        StringAssert.Contains(sql, "ON monitor_delivery_outbox (producer, status, next_attempt_at)");
        StringAssert.Contains(sql, "DO $$");
        Assert.AreEqual(1, Regex.Matches(sql, @"\bCREATE\s+TABLE\b", RegexOptions.IgnoreCase).Count);
        Assert.DoesNotContain("ALTER TABLE notification ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE notification_sent", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE notification ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE notification_sent", sql, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void SqlServerMigration_CreatesSharedDeliveryOutboxContract()
    {
        var sql = ReadMigration(SqlServerMigration);

        StringAssert.Contains(sql, "IF OBJECT_ID(N'[dbo].[MonitorDeliveryOutbox]', N'U') IS NULL");
        StringAssert.Contains(sql, "UNIQUE ([Producer], [DeliveryKey])");
        StringAssert.Contains(sql, "CHECK ([Status] IN (N'Pending', N'InProgress', N'Completed', N'DeadLetter'))");
        StringAssert.Contains(sql, "REFERENCES [dbo].[Notifications] ([Id]) ON DELETE SET NULL");
        StringAssert.Contains(sql, "[DeadLetteredAt] datetime2 NULL");
        StringAssert.Contains(sql, "[LastError] nvarchar(1024) NULL");
        StringAssert.Contains(sql, "[DeliveryKey] nvarchar(450) COLLATE Latin1_General_100_BIN2 NOT NULL");
        StringAssert.Contains(sql, "IF NOT EXISTS");
        StringAssert.Contains(sql, "[IX_MonitorDeliveryOutbox_Due]");
        StringAssert.Contains(sql, "([Producer], [Status], [NextAttemptAt])");
        Assert.AreEqual(1, Regex.Matches(sql, @"\bCREATE\s+TABLE\b", RegexOptions.IgnoreCase).Count);
        Assert.DoesNotContain("ALTER TABLE [dbo].[Notifications]", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE [dbo].[NotificationsSent]", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE [dbo].[Notifications]", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE [dbo].[NotificationsSent]", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadMigration(string fileName)
    {
        var repositoryRoot = FindRepositoryRoot();
        var path = Path.Combine(repositoryRoot, "database", "migrations", fileName);

        Assert.IsTrue(File.Exists(path), $"Expected migration file '{path}' to exist.");
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

        Assert.Fail("Could not find repository root from test output directory.");
        return string.Empty;
    }
}
