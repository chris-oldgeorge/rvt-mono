using System.Globalization;
using System.Text.RegularExpressions;
using Npgsql;
using Rvt.Monitor.IntegrationTesting;

namespace OmnidotsAdapterTests;

[TestClass]
public sealed class OmnidotsMigrationContractTests
{
    private const string _forwardScript = "2026-07-14-add-import-cursors-and-trace-order.sql";
    private const string _rollbackScript = "2026-07-14-rollback-import-cursors-and-trace-order.sql";

    private static readonly string[] _supportedMigrations =
    [
        _forwardScript,
        _rollbackScript,
        "2026-07-15-add-common-durable-alerts.sql",
        "2026-07-15-rollback-common-durable-alerts.sql"
    ];

    [TestMethod]
    public void MigrationAssets_ContainOnlyTheSupportedPostgreSqlScripts()
    {
        string?[] migrationFiles = [.. Directory
            .GetFiles(Path.Combine(MonitorProjectDirectory(), "postgres"), "*.sql", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(file => file, StringComparer.Ordinal)];

        CollectionAssert.AreEqual(_supportedMigrations, migrationFiles);
        Assert.IsFalse(
            Directory.Exists(Path.Combine(MonitorProjectDirectory(), "sql" + "server")),
            "The retired database-engine migration directory must not remain.");
    }

    [TestMethod]
    public void PostgreSqlForward_IsTransactionalAndCreatesTheRequiredSchemaInOrder()
    {
        string script = NormalizeSql(RemoveComments(ReadScript("postgres", _forwardScript)));

        Assert.IsTrue(script.StartsWith("BEGIN;", StringComparison.Ordinal));
        Assert.IsTrue(script.EndsWith("COMMIT;", StringComparison.Ordinal));
        AssertAppearsInOrder(
            script,
            "CREATE TABLE IF NOT EXISTS omnidots_import_cursor",
            "CONSTRAINT pk_omnidots_import_cursor PRIMARY KEY (serial_id, series)",
            "CONSTRAINT ck_omnidots_import_cursor_series CHECK (series IN ('Peak', 'Veff', 'Vdv'))",
            "ALTER TABLE omnidots_trace ADD COLUMN IF NOT EXISTS sample_index integer",
            "row_number() OVER (PARTITION BY trace_id ORDER BY ctid) - 1",
            "ALTER TABLE omnidots_trace ALTER COLUMN sample_index SET NOT NULL",
            "ADD CONSTRAINT pk_omnidots_trace PRIMARY KEY (trace_id, sample_index)",
            "DROP INDEX IF EXISTS ix_omnidots_trace_trace_id",
            "COMMIT;");
        AssertCursorHasNoDefault(script, "CREATE TABLE IF NOT EXISTS omnidots_import_cursor", "ALTER TABLE omnidots_trace");
    }

    [TestMethod]
    public void PostgreSqlRollback_IsTransactionalAndWarnsBeforeDiscardingOrder()
    {
        string rawScript = ReadScript("postgres", _rollbackScript);
        string script = NormalizeSql(RemoveComments(rawScript));

        Assert.IsTrue(script.StartsWith("BEGIN;", StringComparison.Ordinal));
        Assert.IsTrue(script.EndsWith("COMMIT;", StringComparison.Ordinal));
        AssertAppearsInOrder(
            script,
            "DROP CONSTRAINT IF EXISTS pk_omnidots_trace",
            "CREATE INDEX IF NOT EXISTS ix_omnidots_trace_trace_id",
            "DROP COLUMN IF EXISTS sample_index",
            "DROP TABLE IF EXISTS omnidots_import_cursor",
            "COMMIT;");
        Assert.MatchesRegex(
            new Regex(
                @"-- WARNING: Dropping sample_index permanently discards trace sample ordering metadata\.\r?\nALTER TABLE IF EXISTS omnidots_trace\s+DROP COLUMN IF EXISTS sample_index;",
                RegexOptions.CultureInvariant),
            rawScript);
    }

    [TestMethod]
    [TestCategory("PostgreSqlIntegration")]
    public async Task PostgreSqlScripts_ExecuteForwardAndRollbackIdempotently()
    {
        const string legacySchema = """
            CREATE TABLE omnidots_trace_index
            (
                id uuid PRIMARY KEY,
                serial_id text,
                start_time timestamp with time zone NOT NULL,
                end_time timestamp with time zone NOT NULL
            );

            CREATE TABLE omnidots_trace
            (
                trace_id uuid NOT NULL REFERENCES omnidots_trace_index (id) ON DELETE CASCADE,
                x double precision,
                y double precision,
                z double precision
            );

            CREATE INDEX ix_omnidots_trace_trace_id ON omnidots_trace (trace_id);

            INSERT INTO omnidots_trace_index (id, start_time, end_time)
            VALUES ('11111111-1111-1111-1111-111111111111', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);

            INSERT INTO omnidots_trace (trace_id, x, y, z)
            VALUES
                ('11111111-1111-1111-1111-111111111111', 1, 2, 3),
                ('11111111-1111-1111-1111-111111111111', 1, 2, 3),
                ('11111111-1111-1111-1111-111111111111', 4, 5, 6);
            """;

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(45));
        await using PostgreSqlIntegrationDatabase database = await PostgreSqlIntegrationDatabase.CreateAsync(
            legacySchema,
            "SELECT 1;",
            timeout.Token);

        string forward = ReadScript("postgres", _forwardScript);
        await ExecutePostgreSqlAsync(database, forward, timeout.Token);
        await ExecutePostgreSqlAsync(database, forward, timeout.Token);

        await AssertLegacyRowsPreservedAsync(database, timeout.Token);
        Assert.AreEqual(3L, await QueryScalarAsync<long>(
            database,
            "SELECT COUNT(DISTINCT sample_index) FROM omnidots_trace;",
            timeout.Token));
        Assert.AreEqual(0L, await QueryScalarAsync<long>(
            database,
            "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'omnidots_import_cursor' AND column_default IS NOT NULL;",
            timeout.Token));

        string rollback = ReadScript("postgres", _rollbackScript);
        await ExecutePostgreSqlAsync(database, rollback, timeout.Token);
        await AssertLegacyRowsPreservedAsync(database, timeout.Token);
        await ExecutePostgreSqlAsync(database, rollback, timeout.Token);
        await AssertLegacyRowsPreservedAsync(database, timeout.Token);

        Assert.AreEqual(0L, await QueryScalarAsync<long>(
            database,
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'omnidots_import_cursor';",
            timeout.Token));
        Assert.AreEqual(0L, await QueryScalarAsync<long>(
            database,
            "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'omnidots_trace' AND column_name = 'sample_index';",
            timeout.Token));
        Assert.AreEqual(1L, await QueryScalarAsync<long>(
            database,
            "SELECT COUNT(*) FROM pg_indexes WHERE tablename = 'omnidots_trace' AND indexname = 'ix_omnidots_trace_trace_id';",
            timeout.Token));
    }

    private static string ReadScript(string provider, string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, provider, fileName);
        return File.ReadAllText(path);
    }

    private static string RemoveComments(string script)
    {
        string withoutBlockComments = Regex.Replace(
            script,
            @"/\*.*?\*/",
            string.Empty,
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        return Regex.Replace(
            withoutBlockComments,
            @"--[^\r\n]*",
            string.Empty,
            RegexOptions.CultureInvariant);
    }

    private static string NormalizeSql(string script)
    {
        string normalized = Regex.Replace(script, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
        normalized = Regex.Replace(normalized, @"\(\s+", "(", RegexOptions.CultureInvariant);
        return Regex.Replace(normalized, @"\s+\)", ")", RegexOptions.CultureInvariant);
    }

    private static void AssertCursorHasNoDefault(string script, string startMarker, string endMarker)
    {
        int start = script.IndexOf(startMarker, StringComparison.Ordinal);
        int end = script.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start);
        Assert.IsGreaterThan(start, end);
        Assert.IsFalse(script[start..end].Contains("DEFAULT", StringComparison.Ordinal));
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

    private static async Task ExecutePostgreSqlAsync(
        PostgreSqlIntegrationDatabase database,
        string script,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = new(script, connection) { CommandTimeout = 30 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<T> QueryScalarAsync<T>(
        PostgreSqlIntegrationDatabase database,
        string sql,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = new(sql, connection) { CommandTimeout = 30 };
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        Assert.IsNotNull(result);
        return (T)Convert.ChangeType(result, typeof(T), CultureInfo.InvariantCulture);
    }

    private static async Task AssertLegacyRowsPreservedAsync(
        PostgreSqlIntegrationDatabase database,
        CancellationToken cancellationToken)
    {
        Assert.AreEqual(3L, await QueryScalarAsync<long>(
            database,
            "SELECT COUNT(*) FROM omnidots_trace;",
            cancellationToken));
        Assert.AreEqual(2L, await QueryScalarAsync<long>(
            database,
            "SELECT COUNT(*) FROM omnidots_trace WHERE x = 1 AND y = 2 AND z = 3;",
            cancellationToken));
    }

    private static string MonitorProjectDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string gitPath = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return Path.Combine(
                    directory.FullName,
                    "apps",
                    "monitors",
                    "omnidotsmonitor",
                    "OmnidotsMonitor");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the repository root from the test output directory.");
    }
}
