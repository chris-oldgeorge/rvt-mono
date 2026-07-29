using System.Globalization;
using System.Text.RegularExpressions;
using Npgsql;
using Rvt.Monitor.IntegrationTesting;

namespace OmnidotsAdapterTests;

[TestClass]
public sealed partial class OmnidotsAlertMigrationContractTests
{
    private const string _forwardScript = "2026-07-15-add-common-durable-alerts.sql";
    private const string _rollbackScript = "2026-07-15-rollback-common-durable-alerts.sql";

    [TestMethod]
    public void PostgreSqlForward_IsTransactionalIdempotentAndDefinesDurableAlertConstraints()
    {
        string script = NormalizeSql(RemoveComments(ReadScript("postgres", _forwardScript)));

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
        Assert.Contains("CHECK (octet_length(source_key_hash) = 32)", script);
        Assert.Contains("CHECK (outcome IN ('Accepted','Ignored','Suppressed'))", script);
        Assert.Contains("CHECK (kind IN ('MqttAlert','Email','Sms'))", script);
        Assert.Contains("CHECK (status IN ('Pending','Leased','Completed','DeadLetter'))", script);
        Assert.Contains("ON DELETE RESTRICT", script);
        Assert.Contains("ON DELETE CASCADE", script);
        Assert.Contains("IF NOT EXISTS", script);
    }

    [TestMethod]
    public void PostgreSqlRollback_IsTransactionalIdempotentAndDropsDependentsFirst()
    {
        string rawScript = ReadScript("postgres", _rollbackScript);
        string script = NormalizeSql(RemoveComments(rawScript));

        Assert.IsTrue(script.StartsWith("BEGIN;", StringComparison.Ordinal));
        Assert.IsTrue(script.EndsWith("COMMIT;", StringComparison.Ordinal));
        AssertAppearsInOrder(
            script,
            "DROP TABLE IF EXISTS alert_delivery_outbox",
            "DROP TABLE IF EXISTS alert_occurrence",
            "COMMIT;");
        Assert.Contains("WARNING: Dropping alert_occurrence removes permanent webhook replay protection.", rawScript);
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

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(45));
        await using PostgreSqlIntegrationDatabase database = await PostgreSqlIntegrationDatabase.CreateAsync(prerequisiteSchema, "SELECT 1;", timeout.Token);

        string forward = ReadScript("postgres", _forwardScript);
        await ExecutePostgreSqlAsync(database, forward, timeout.Token);
        await ExecutePostgreSqlAsync(database, forward, timeout.Token);

        Assert.AreEqual(2L, await QueryScalarAsync<long>(database, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = current_schema() AND table_name IN ('alert_occurrence', 'alert_delivery_outbox');", timeout.Token));
        Assert.AreEqual(1L, await QueryScalarAsync<long>(database, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = current_schema() AND tablename = 'alert_occurrence' AND indexname = 'uq_alert_occurrence_source_key';", timeout.Token));
        Assert.AreEqual(1L, await QueryScalarAsync<long>(database, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = current_schema() AND tablename = 'alert_delivery_outbox' AND indexname = 'uq_alert_delivery_outbox_delivery_key';", timeout.Token));
        Assert.AreEqual(1L, await QueryScalarAsync<long>(database, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = current_schema() AND tablename = 'alert_delivery_outbox' AND indexname = 'ix_alert_delivery_outbox_due';", timeout.Token));

        string rollback = ReadScript("postgres", _rollbackScript);
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
        string withoutBlockComments = BlockCommentPattern().Replace(script, string.Empty);
        return LineCommentPattern().Replace(withoutBlockComments, string.Empty);
    }

    private static string NormalizeSql(string script)
    {
        string normalized = WhitespacePattern().Replace(script, " ").Trim();
        normalized = OpeningParenthesisWhitespacePattern().Replace(normalized, "(");
        return ClosingParenthesisWhitespacePattern().Replace(normalized, ")");
    }

    private static void AssertAppearsInOrder(string script, params string[] statements)
    {
        int lastIndex = -1;
        foreach (string statement in statements)
        {
            int index = script.IndexOf(statement, StringComparison.Ordinal);
            Assert.IsGreaterThan(lastIndex, index, $"Expected '{statement}' after the preceding migration operation.");
            lastIndex = index;
        }
    }

    private static async Task ExecutePostgreSqlAsync(PostgreSqlIntegrationDatabase database, string script, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = new(script, connection) { CommandTimeout = 30 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<T> QueryScalarAsync<T>(PostgreSqlIntegrationDatabase database, string sql, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = new(sql, connection) { CommandTimeout = 30 };
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        Assert.IsNotNull(result);
        return (T)Convert.ChangeType(result, typeof(T), CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex BlockCommentPattern();

    [GeneratedRegex(@"--[^\r\n]*", RegexOptions.CultureInvariant)]
    private static partial Regex LineCommentPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex(@"\(\s+", RegexOptions.CultureInvariant)]
    private static partial Regex OpeningParenthesisWhitespacePattern();

    [GeneratedRegex(@"\s+\)", RegexOptions.CultureInvariant)]
    private static partial Regex ClosingParenthesisWhitespacePattern();
}
