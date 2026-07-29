using System.Text.RegularExpressions;

namespace Rvt.Monitor.CommonTests.Data.EntityFramework;

[TestClass]
public sealed partial class MonitorDeliveryMigrationContractTests
{
    private const string PostgreSqlMigration = "2026-07-15-add-monitor-delivery-outbox.postgres.sql";

    [TestMethod]
    public void PostgreSqlMigration_CreatesSharedDeliveryOutboxContract()
    {
        string sql = ReadMigration(PostgreSqlMigration);

        Assert.Contains("CREATE TABLE IF NOT EXISTS monitor_delivery_outbox", sql);
        Assert.Contains("UNIQUE (producer, delivery_key)", sql);
        Assert.Contains("CHECK (status IN ('Pending', 'InProgress', 'Completed', 'DeadLetter'))", sql);
        Assert.Contains("REFERENCES notification (id) ON DELETE SET NULL", sql);
        Assert.Contains("dead_lettered_at timestamp with time zone NULL", sql);
        Assert.Contains("CREATE INDEX IF NOT EXISTS ix_monitor_delivery_outbox_due", sql);
        Assert.Contains("ON monitor_delivery_outbox (producer, status, next_attempt_at)", sql);
        Assert.Contains("DO $$", sql);
        Assert.HasCount(1, CreateTablePattern().Matches(sql));
        Assert.DoesNotContain("ALTER TABLE notification ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE notification_sent", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE notification ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE notification_sent", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadMigration(string fileName)
    {
        string repositoryRoot = FindRepositoryRoot();
        string path = Path.Combine(repositoryRoot, "database", "migrations", fileName);

        Assert.IsTrue(File.Exists(path), $"Expected migration file '{path}' to exist.");
        return File.ReadAllText(path);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string gitPath = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return Path.Combine(directory.FullName, "libs", "rvt-monitor-common");
            }

            directory = directory.Parent;
        }

        Assert.Fail("Could not find repository root from test output directory.");
        return string.Empty;
    }

    [GeneratedRegex(@"\bCREATE\s+TABLE\b", RegexOptions.IgnoreCase)]
    private static partial Regex CreateTablePattern();
}
