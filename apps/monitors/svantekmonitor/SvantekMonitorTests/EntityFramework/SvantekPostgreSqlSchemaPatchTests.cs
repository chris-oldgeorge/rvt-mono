using System.Text.RegularExpressions;
using Rvt.Monitor.IntegrationTesting;

namespace SvantekMonitorTests.EntityFramework;

[TestClass]
public sealed class SvantekPostgreSqlSchemaPatchTests
{
    [TestMethod]
    public void IntegrationFixture_UsesCanonicalGeneratedSchemaObjects()
    {
        string fixturePath = RepositoryLayout.GetPath(
            "apps",
            "monitors",
            "svantekmonitor",
            "SvantekMonitorTests",
            "testdata",
            "create.postgres.sql");

        Assert.IsTrue(File.Exists(fixturePath), $"Missing PostgreSQL fixture: {fixturePath}");

        string sql = File.ReadAllText(fixturePath);
        string[] expectedTables =
        [
            "monitor",
            "svantek_monitor_status",
            "svantek_noise_level",
            "svantek_noise_8_hour_average",
            "svantek_error_message",
            "rvt_alert_rule",
            "deployment",
            "contract",
            "site_user",
            "notification_setting",
            "notification_sent",
            "notification",
            "site",
            "site_average",
            "error_log"
        ];

        foreach (string? table in expectedTables)
        {
            Assert.Contains($"CREATE TABLE {table}", sql);
        }

        Assert.Contains("CREATE TABLE \"AspNetUsers\"", sql);
        Assert.Contains("what_3_words", sql);
        Assert.Contains("recording_link", sql);
        Assert.IsFalse(sql.Contains("public.", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("dbo.", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("CREATE EXTENSION", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("gen_random_uuid", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("uuid_generate", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void IntegrationFixture_ResetCoversCanonicalTablesAndSeedsOfflineRule()
    {
        string testDataPath = RepositoryLayout.GetPath(
            "apps",
            "monitors",
            "svantekmonitor",
            "SvantekMonitorTests",
            "testdata");
        string resetPath = Path.Combine(testDataPath, "reset.postgres.sql");

        Assert.IsTrue(File.Exists(resetPath), $"Missing PostgreSQL reset script: {resetPath}");
        Assert.IsFalse(File.Exists(Path.Combine(testDataPath, "create.sql")));

        string sql = File.ReadAllText(resetPath);

        Assert.Contains("svantek_noise_8_hour_average", sql);
        Assert.Contains("svantek_error_message", sql);
        Assert.Contains("error_log", sql);
        MatchCollection offlineRuleInserts = Regex.Matches(
            sql,
            @"(?is)\bINSERT\s+INTO\s+rvt_alert_rule\b.*?;");

        Assert.HasCount(1, offlineRuleInserts, "The reset script must seed exactly one offline rule.");

        string offlineRuleInsert = offlineRuleInserts[0].Value;
        Assert.Contains("'00000000-0000-0000-0000-000000000001'", offlineRuleInsert);
        Assert.Contains("'offline-rule'", offlineRuleInsert);
        Assert.IsTrue(Regex.IsMatch(offlineRuleInsert, @"(?<!\d)86400(?!\d)"));
        Assert.IsFalse(sql.Contains("gen_random_uuid", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("uuid_generate", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("public.", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void StatusTelemetryPatch_IncludesMissingPostgreSqlColumns()
    {
        string patchPath = RepositoryLayout.GetPath(
            "apps",
            "monitors",
            "svantekmonitor",
            "SvantekMonitor",
            "postgres",
            "2026-06-30-add-status-telemetry-columns.sql");

        Assert.IsTrue(File.Exists(patchPath), $"Missing schema patch: {patchPath}");

        string sql = File.ReadAllText(patchPath);

        Assert.Contains("svantek_monitor_status", sql);
        Assert.Contains("update_time", sql);
        Assert.Contains("status", sql);
        Assert.Contains("battery_voltage", sql);
        Assert.Contains("calibration_date", sql);
        Assert.Contains("filter_change_date", sql);
        Assert.Contains("pump_hours", sql);
        Assert.Contains("svantek_error_message", sql);
        Assert.Contains("ON public.svantek_error_message (error_time);", sql);
    }

    [TestMethod]
    public void DemoMonitorResetPatch_IncludesTargetNoiseMonitor()
    {
        string patchPath = RepositoryLayout.GetPath(
            "apps",
            "monitors",
            "svantekmonitor",
            "SvantekMonitor",
            "postgres",
            "2026-06-30-reset-demo-monitor-157206.sql");

        Assert.IsTrue(File.Exists(patchPath), $"Missing demo reset patch: {patchPath}");

        string sql = File.ReadAllText(patchPath);

        Assert.Contains("public.monitor", sql);
        Assert.Contains("public.deployment", sql);
        Assert.Contains("157206", sql);
        Assert.Contains("E125V", sql);
        Assert.Contains("last_data_time_15_min", sql);
        Assert.Contains("2026-03-15 20:00:00", sql);
    }

}
