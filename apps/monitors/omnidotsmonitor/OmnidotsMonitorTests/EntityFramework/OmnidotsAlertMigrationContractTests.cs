using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Npgsql;
using Rvt.Monitor.IntegrationTesting;

namespace OmnidotsMonitorTests.EntityFramework;

[TestClass]
public sealed class OmnidotsAlertMigrationContractTests
{
    private const string ForwardScript = "2026-07-15-add-common-durable-alerts.sql";
    private const string RollbackScript = "2026-07-15-rollback-common-durable-alerts.sql";

    [TestMethod]
    public void PostgreSqlForward_IsTransactionalIdempotentAndDefinesDurableAlertConstraints()
    {
        var script = NormalizeSql(RemoveComments(ReadScript("postgres", ForwardScript)));

        Assert.IsTrue(script.StartsWith("BEGIN;", StringComparison.Ordinal));
        Assert.IsTrue(script.EndsWith("COMMIT;", StringComparison.Ordinal));
        AssertAppearsInOrder(
            script,
            "CREATE TABLE IF NOT EXISTS alert_occurrence",
            "CONSTRAINT uq_alert_occurrence_source_key UNIQUE (source, source_key_hash)",
            "CREATE TABLE IF NOT EXISTS alert_delivery_outbox",
            "CONSTRAINT uq_alert_delivery_outbox_delivery_key UNIQUE (delivery_key)",
            "CREATE INDEX IF NOT EXISTS ix_alert_delivery_outbox_due ON alert_delivery_outbox (status, next_attempt_at, lease_until, created_at)",
            "COMMIT;");
        StringAssert.Contains(script, "CHECK (octet_length(source_key_hash) = 32)");
        StringAssert.Contains(script, "CHECK (outcome IN ('Accepted','Ignored','Suppressed'))");
        StringAssert.Contains(script, "CHECK (kind IN ('MqttAlert','Email','Sms'))");
        StringAssert.Contains(script, "CHECK (status IN ('Pending','Leased','Completed','DeadLetter'))");
        StringAssert.Contains(script, "ON DELETE RESTRICT");
        StringAssert.Contains(script, "ON DELETE CASCADE");
        StringAssert.Contains(script, "IF NOT EXISTS");
    }

    [TestMethod]
    public void PostgreSqlRollback_IsTransactionalIdempotentAndDropsDependentsFirst()
    {
        var rawScript = ReadScript("postgres", RollbackScript);
        var script = NormalizeSql(RemoveComments(rawScript));

        Assert.IsTrue(script.StartsWith("BEGIN;", StringComparison.Ordinal));
        Assert.IsTrue(script.EndsWith("COMMIT;", StringComparison.Ordinal));
        AssertAppearsInOrder(
            script,
            "DROP TABLE IF EXISTS alert_delivery_outbox",
            "DROP TABLE IF EXISTS alert_occurrence",
            "COMMIT;");
        StringAssert.Contains(rawScript, "WARNING: Dropping alert_occurrence removes permanent webhook replay protection.");
    }

    [TestMethod]
    public void SqlServerForward_IsTransactionalIdempotentAndCaseExact()
    {
        var script = NormalizeSql(RemoveComments(ReadScript("sqlserver", ForwardScript)));

        StringAssert.Contains(script, "SET XACT_ABORT ON;");
        AssertAppearsInOrder(script, "BEGIN TRY", "BEGIN TRANSACTION;", "CREATE TABLE dbo.AlertOccurrences", "CREATE TABLE dbo.AlertDeliveryOutbox", "COMMIT TRANSACTION;", "END TRY", "BEGIN CATCH");
        StringAssert.Contains(script, "CONSTRAINT UQ_AlertOccurrences_SourceKey UNIQUE (Source, SourceKeyHash)");
        StringAssert.Contains(script, "CONSTRAINT UQ_AlertDeliveryOutbox_DeliveryKey UNIQUE (DeliveryKey)");
        StringAssert.Contains(script, "CREATE INDEX IX_AlertDeliveryOutbox_Due ON dbo.AlertDeliveryOutbox (Status, NextAttemptAt, LeaseUntil, CreatedAt)");
        StringAssert.Contains(script, "SourceKeyHash binary(32) NOT NULL");

        foreach (var literal in new[] { "Accepted", "Ignored", "Suppressed", "MqttAlert", "Email", "Sms", "Pending", "Leased", "Completed", "DeadLetter" })
        {
            StringAssert.Contains(script, $"COLLATE Latin1_General_100_BIN2 = N'{literal}'");
            StringAssert.Contains(script, $"DATALENGTH(N'{literal}')");
        }
    }

    [TestMethod]
    public void SqlServerRollback_IsTransactionalIdempotentAndDropsDependentsFirst()
    {
        var rawScript = ReadScript("sqlserver", RollbackScript);
        var script = NormalizeSql(RemoveComments(rawScript));

        StringAssert.Contains(script, "SET XACT_ABORT ON;");
        AssertAppearsInOrder(script, "BEGIN TRY", "BEGIN TRANSACTION;", "DROP TABLE dbo.AlertDeliveryOutbox", "DROP TABLE dbo.AlertOccurrences", "COMMIT TRANSACTION;", "END TRY", "BEGIN CATCH");
        StringAssert.Contains(rawScript, "WARNING: Dropping AlertOccurrences removes permanent webhook replay protection.");
    }

    [TestMethod]
    public void SqlServerScripts_ParseWithMicrosoftScriptDom()
    {
        AssertValidTransactSql(ReadScript("sqlserver", ForwardScript));
        AssertValidTransactSql(ReadScript("sqlserver", RollbackScript));
    }

    [TestMethod]
    [TestCategory("PostgreSqlIntegration")]
    public async Task PostgreSqlScripts_ExecuteForwardAndRollbackIdempotently()
    {
        const string prerequisiteSchema = """
            CREATE TABLE monitor (id uuid PRIMARY KEY);
            CREATE TABLE notification
            (
                id uuid PRIMARY KEY,
                monitor_id uuid NOT NULL REFERENCES monitor(id) ON DELETE CASCADE
            );
            """;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await using var database = await PostgreSqlIntegrationDatabase.CreateAsync(prerequisiteSchema, "SELECT 1;", timeout.Token);

        var forward = ReadScript("postgres", ForwardScript);
        await ExecutePostgreSqlAsync(database, forward, timeout.Token);
        await ExecutePostgreSqlAsync(database, forward, timeout.Token);

        Assert.AreEqual(2L, await QueryScalarAsync<long>(database, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = current_schema() AND table_name IN ('alert_occurrence', 'alert_delivery_outbox');", timeout.Token));
        Assert.AreEqual(1L, await QueryScalarAsync<long>(database, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = current_schema() AND tablename = 'alert_occurrence' AND indexname = 'uq_alert_occurrence_source_key';", timeout.Token));
        Assert.AreEqual(1L, await QueryScalarAsync<long>(database, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = current_schema() AND tablename = 'alert_delivery_outbox' AND indexname = 'uq_alert_delivery_outbox_delivery_key';", timeout.Token));
        Assert.AreEqual(1L, await QueryScalarAsync<long>(database, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = current_schema() AND tablename = 'alert_delivery_outbox' AND indexname = 'ix_alert_delivery_outbox_due';", timeout.Token));

        var rollback = ReadScript("postgres", RollbackScript);
        await ExecutePostgreSqlAsync(database, rollback, timeout.Token);
        await ExecutePostgreSqlAsync(database, rollback, timeout.Token);
        Assert.AreEqual(0L, await QueryScalarAsync<long>(database, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = current_schema() AND table_name IN ('alert_occurrence', 'alert_delivery_outbox');", timeout.Token));

        await ExecutePostgreSqlAsync(database, forward, timeout.Token);
        Assert.AreEqual(2L, await QueryScalarAsync<long>(database, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = current_schema() AND table_name IN ('alert_occurrence', 'alert_delivery_outbox');", timeout.Token));
    }

    private static string ReadScript(string provider, string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, provider, fileName));

    private static string RemoveComments(string script)
    {
        var withoutBlockComments = Regex.Replace(script, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline | RegexOptions.CultureInvariant);
        return Regex.Replace(withoutBlockComments, @"--[^\r\n]*", string.Empty, RegexOptions.CultureInvariant);
    }

    private static string NormalizeSql(string script)
    {
        var normalized = Regex.Replace(script, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
        normalized = Regex.Replace(normalized, @"\(\s+", "(", RegexOptions.CultureInvariant);
        return Regex.Replace(normalized, @"\s+\)", ")", RegexOptions.CultureInvariant);
    }

    private static void AssertAppearsInOrder(string script, params string[] statements)
    {
        var lastIndex = -1;
        foreach (var statement in statements)
        {
            var index = script.IndexOf(statement, StringComparison.Ordinal);
            Assert.IsGreaterThan(lastIndex, index, $"Expected '{statement}' after the preceding migration operation.");
            lastIndex = index;
        }
    }

    private static void AssertValidTransactSql(string script)
    {
        var parser = new TSql180Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(script);
        _ = parser.Parse(reader, out var errors);
        Assert.HasCount(0, errors, string.Join(Environment.NewLine, errors.Select(error => $"Line {error.Line}, column {error.Column}: {error.Message}")));
    }

    private static async Task ExecutePostgreSqlAsync(PostgreSqlIntegrationDatabase database, string script, CancellationToken cancellationToken)
    {
        await using var connection = database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(script, connection) { CommandTimeout = 30 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<T> QueryScalarAsync<T>(PostgreSqlIntegrationDatabase database, string sql, CancellationToken cancellationToken)
    {
        await using var connection = database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 30 };
        var result = await command.ExecuteScalarAsync(cancellationToken);
        Assert.IsNotNull(result);
        return (T)Convert.ChangeType(result, typeof(T), CultureInfo.InvariantCulture);
    }
}
