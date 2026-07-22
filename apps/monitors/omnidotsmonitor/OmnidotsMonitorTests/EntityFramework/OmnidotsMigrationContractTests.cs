using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Npgsql;
using Rvt.Monitor.IntegrationTesting;

namespace OmnidotsMonitorTests.EntityFramework;

[TestClass]
public sealed class OmnidotsMigrationContractTests
{
    private const string ForwardScript = "2026-07-14-add-import-cursors-and-trace-order.sql";
    private const string RollbackScript = "2026-07-14-rollback-import-cursors-and-trace-order.sql";
    private const string SqlServerSeriesConstraintSql =
        "([Series] COLLATE Latin1_General_100_BIN2 = N'Peak' AND DATALENGTH([Series]) = DATALENGTH(N'Peak')) OR " +
        "([Series] COLLATE Latin1_General_100_BIN2 = N'Veff' AND DATALENGTH([Series]) = DATALENGTH(N'Veff')) OR " +
        "([Series] COLLATE Latin1_General_100_BIN2 = N'Vdv' AND DATALENGTH([Series]) = DATALENGTH(N'Vdv'))";

    [TestMethod]
    public void PostgreSqlForward_IsTransactionalAndCreatesTheRequiredSchemaInOrder()
    {
        var script = NormalizeSql(RemoveComments(ReadScript("postgres", ForwardScript)));

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
        var rawScript = ReadScript("postgres", RollbackScript);
        var script = NormalizeSql(RemoveComments(rawScript));

        Assert.IsTrue(script.StartsWith("BEGIN;", StringComparison.Ordinal));
        Assert.IsTrue(script.EndsWith("COMMIT;", StringComparison.Ordinal));
        AssertAppearsInOrder(
            script,
            "DROP CONSTRAINT IF EXISTS pk_omnidots_trace",
            "CREATE INDEX IF NOT EXISTS ix_omnidots_trace_trace_id",
            "DROP COLUMN IF EXISTS sample_index",
            "DROP TABLE IF EXISTS omnidots_import_cursor",
            "COMMIT;");
        StringAssert.Matches(
            rawScript,
            new Regex(
                @"-- WARNING: Dropping sample_index permanently discards trace sample ordering metadata\.\r?\nALTER TABLE IF EXISTS omnidots_trace\s+DROP COLUMN IF EXISTS sample_index;",
                RegexOptions.CultureInvariant));
    }

    [TestMethod]
    public void SqlServerForward_UsesAStablePhysicalTieBreakerAndRequiredSchemaOrder()
    {
        var script = NormalizeSql(RemoveComments(ReadScript("sqlserver", ForwardScript)));

        Assert.IsFalse(script.Contains("ORDER BY (SELECT NULL)", StringComparison.Ordinal));
        AssertAppearsInOrder(
            script,
            "BEGIN TRANSACTION;",
            "CREATE TABLE dbo.OmnidotsImportCursor",
            "CONSTRAINT PK_OmnidotsImportCursor PRIMARY KEY (SerialId, Series)",
            "CONSTRAINT CK_OmnidotsImportCursor_Series CHECK (" + SqlServerSeriesConstraintSql + ")",
            "ADD SampleIndex int NULL",
            "ADD MigrationSampleRowId bigint IDENTITY(1,1) NOT NULL",
            "ROW_NUMBER() OVER (PARTITION BY TraceId ORDER BY MigrationSampleRowId) - 1",
            "DROP COLUMN MigrationSampleRowId",
            "ALTER COLUMN TraceId uniqueidentifier NOT NULL",
            "ALTER COLUMN SampleIndex int NOT NULL",
            "ADD CONSTRAINT PK_OmnidotsTraces PRIMARY KEY (TraceId, SampleIndex)",
            "DROP INDEX ix_traces ON dbo.OmnidotsTraces",
            "COMMIT TRANSACTION;");
        AssertCursorHasNoDefault(script, "CREATE TABLE dbo.OmnidotsImportCursor", "IF COL_LENGTH");
    }

    [TestMethod]
    public void SqlServerForward_SeriesConstraintRejectsCaseAndPaddingVariantsStructurally()
    {
        var script = NormalizeSql(RemoveComments(ReadScript("sqlserver", ForwardScript)));
        var constraintDefinition =
            "CONSTRAINT CK_OmnidotsImportCursor_Series CHECK (" + SqlServerSeriesConstraintSql + ")";

        StringAssert.Contains(script, constraintDefinition);
        Assert.AreEqual(3, CountOccurrences(SqlServerSeriesConstraintSql, "COLLATE Latin1_General_100_BIN2"));

        foreach (var canonicalValue in new[] { "Peak", "Veff", "Vdv" })
        {
            StringAssert.Contains(
                SqlServerSeriesConstraintSql,
                $"DATALENGTH([Series]) = DATALENGTH(N'{canonicalValue}')");
        }

        foreach (var rejectedValue in new[]
                 {
                     "peak", "PEAK", "Peak ", " Peak",
                     "veff", "VEFF", "Veff ", " Veff",
                     "vdv", "VDV", "Vdv ", " Vdv"
                 })
        {
            Assert.IsFalse(
                constraintDefinition.Contains($"N'{rejectedValue}'", StringComparison.Ordinal),
                $"The constraint must not treat '{rejectedValue}' as a canonical series literal.");
        }
    }

    [TestMethod]
    public void SqlServerForward_GuardsTheCompleteTemporaryIdentityLifecycle()
    {
        var script = ReadScript("sqlserver", ForwardScript);
        var fragment = ParseValidTransactSql(script);
        var visitor = new IfStatementVisitor();
        fragment.Accept(visitor);

        var backfillGuards = visitor.Statements
            .Where(statement => NormalizeSql(GetFragmentText(script, statement.Predicate))
                .Contains("WHERE SampleIndex IS NULL", StringComparison.Ordinal))
            .ToList();
        Assert.HasCount(1, backfillGuards);

        var guardedBlock = Assert.IsInstanceOfType<BeginEndBlockStatement>(backfillGuards[0].ThenStatement);
        var guardedSql = NormalizeSql(RemoveComments(GetFragmentText(script, guardedBlock)));
        AssertAppearsInOrder(
            guardedSql,
            "IF COL_LENGTH(N'dbo.OmnidotsTraces', N'MigrationSampleRowId') IS NULL",
            "ADD MigrationSampleRowId bigint IDENTITY(1,1) NOT NULL",
            "ROW_NUMBER() OVER (PARTITION BY TraceId ORDER BY MigrationSampleRowId) - 1",
            "IF COL_LENGTH(N'dbo.OmnidotsTraces', N'MigrationSampleRowId') IS NOT NULL",
            "DROP COLUMN MigrationSampleRowId");
        Assert.AreEqual(
            CountOccurrences(script, "MigrationSampleRowId"),
            CountOccurrences(guardedSql, "MigrationSampleRowId"),
            "Completed reruns must not add, scan by, or drop the temporary identity column.");
    }

    [TestMethod]
    public void SqlServerRollback_WarnsBeforeDiscardingOrderAndRestoresLegacyShape()
    {
        var rawScript = ReadScript("sqlserver", RollbackScript);
        var script = NormalizeSql(RemoveComments(rawScript));

        AssertAppearsInOrder(
            script,
            "BEGIN TRANSACTION;",
            "DROP CONSTRAINT PK_OmnidotsTraces",
            "ALTER COLUMN TraceId uniqueidentifier NULL",
            "DROP COLUMN SampleIndex",
            "CREATE INDEX ix_traces ON dbo.OmnidotsTraces (TraceId)",
            "DROP TABLE dbo.OmnidotsImportCursor",
            "COMMIT TRANSACTION;");
        StringAssert.Matches(
            rawScript,
            new Regex(
                @"-- WARNING: Dropping SampleIndex permanently discards trace sample ordering metadata\.\r?\nIF COL_LENGTH\(N'dbo\.OmnidotsTraces', N'SampleIndex'\) IS NOT NULL",
                RegexOptions.CultureInvariant));
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

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await using var database = await PostgreSqlIntegrationDatabase.CreateAsync(
            legacySchema,
            "SELECT 1;",
            timeout.Token);

        var forward = ReadScript("postgres", ForwardScript);
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

        var rollback = ReadScript("postgres", RollbackScript);
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
        var path = Path.Combine(AppContext.BaseDirectory, provider, fileName);
        return File.ReadAllText(path);
    }

    private static string RemoveComments(string script)
    {
        var withoutBlockComments = Regex.Replace(
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
        var normalized = Regex.Replace(script, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
        normalized = Regex.Replace(normalized, @"\(\s+", "(", RegexOptions.CultureInvariant);
        return Regex.Replace(normalized, @"\s+\)", ")", RegexOptions.CultureInvariant);
    }

    private static void AssertCursorHasNoDefault(string script, string startMarker, string endMarker)
    {
        var start = script.IndexOf(startMarker, StringComparison.Ordinal);
        var end = script.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start);
        Assert.IsGreaterThan(start, end);
        Assert.IsFalse(script[start..end].Contains("DEFAULT", StringComparison.Ordinal));
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
        _ = ParseValidTransactSql(script);
    }

    private static TSqlFragment ParseValidTransactSql(string script)
    {
        var parser = new TSql180Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(script);
        var fragment = parser.Parse(reader, out var errors);

        Assert.HasCount(
            0,
            errors,
            string.Join(Environment.NewLine, errors.Select(error => $"Line {error.Line}, column {error.Column}: {error.Message}")));
        return fragment;
    }

    private static string GetFragmentText(string script, TSqlFragment fragment) =>
        script.Substring(fragment.StartOffset, fragment.FragmentLength);

    private static int CountOccurrences(string value, string search) =>
        Regex.Matches(value, Regex.Escape(search), RegexOptions.CultureInvariant).Count;

    private static async Task ExecutePostgreSqlAsync(
        PostgreSqlIntegrationDatabase database,
        string script,
        CancellationToken cancellationToken)
    {
        await using var connection = database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(script, connection) { CommandTimeout = 30 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<T> QueryScalarAsync<T>(
        PostgreSqlIntegrationDatabase database,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var connection = database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 30 };
        var result = await command.ExecuteScalarAsync(cancellationToken);
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

    private sealed class IfStatementVisitor : TSqlFragmentVisitor
    {
        public List<IfStatement> Statements { get; } = [];

        public override void ExplicitVisit(IfStatement node)
        {
            Statements.Add(node);
            base.ExplicitVisit(node);
        }
    }
}
