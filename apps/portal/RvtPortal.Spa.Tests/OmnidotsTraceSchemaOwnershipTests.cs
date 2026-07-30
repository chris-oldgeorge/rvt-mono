// File summary: Forces the portal and the Omnidots monitor onto one omnidots_trace shape by running the
// monitor's migration against a schema RVTSearchContext builds.
// Major updates:
// - 2026-07-30 pending Added for P1-8, which found the two owners disagreeing on the trace foreign-key column.

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

/// <summary>
/// <c>public.omnidots_trace</c> lives in one database with two writers: the portal owns its schema and reads
/// traces out of it, and the Omnidots monitor writes them in. Before this suite each side built the table from
/// its own fixtures, so nothing noticed that the monitor addressed the trace foreign key as <c>trace_id</c>
/// while the portal's canonical cutover had named it <c>omnidots_trace_index_id</c>. These tests put both
/// owners in one database: the schema comes from <c>RVTSearchContext</c> itself, and the SQL comes from the
/// monitor's shipped migration assets, so a future rename on either side fails here.
/// The ownership ruling is recorded in <c>docs/database/omnidots-trace-ownership.md</c>.
/// </summary>
public sealed partial class OmnidotsTraceSchemaOwnershipTests
{
    private const string CanonicalTraceForeignKeyColumn = "omnidots_trace_index_id";

    private static readonly string[] _monitorMigrationScripts =
    [
        "2026-07-14-add-import-cursors-and-trace-order.sql",
        "2026-07-14-rollback-import-cursors-and-trace-order.sql"
    ];

    [Fact]
    // Function summary: Pins the owning model's column name and requires the monitor's scripts to spell it the same way.
    public void OmnidotsTrace_ForeignKeyColumn_IsSpelledTheSameByBothOwners()
    {
        using RVTSearchContext search = new(TestDbContexts.ModelOnlyNpgsql<RVTSearchContext>());
        string? owned = search.Model
            .FindEntityType(typeof(OmnidotsTrace))!
            .FindProperty(nameof(OmnidotsTrace.TraceId))!
            .GetColumnName();

        Assert.Equal(CanonicalTraceForeignKeyColumn, owned);

        foreach (string script in _monitorMigrationScripts)
        {
            string sql = File.ReadAllText(MonitorMigrationPath(script));
            Assert.Contains(CanonicalTraceForeignKeyColumn, sql);

            // "trace_id" is a substring of the canonical name, so this has to match the bare identifier.
            // The forward script's one-time reconciliation block is allowed to name it; nothing else is.
            Assert.Empty(LegacyIdentifierRegex().Matches(WithoutReconciliationBlock(sql)));
        }
    }

    [RequiresPostgresFact]
    // Function summary: Applies the monitor's forward migration to the portal-built schema and reads the result back through the portal's model.
    public async Task MonitorForwardMigration_AppliesToThePortalBuiltSchemaAndStaysReadable()
    {
        string schemaName = $"omnidots_trace_owner_{Guid.NewGuid():N}";
        string connectionString = SpaTestDatabase.CreateSchema(schemaName);
        try
        {
            string forward = File.ReadAllText(MonitorMigrationPath(_monitorMigrationScripts[0]));

            // Twice: deployments re-run these assets, and a second application must be a no-op.
            await ExecuteAsync(connectionString, forward);
            await ExecuteAsync(connectionString, forward);

            Assert.Equal(
                new[] { CanonicalTraceForeignKeyColumn, "sample_index" },
                await PrimaryKeyColumnsAsync(connectionString));
            Assert.Equal(1L, await ScalarAsync<long>(
                connectionString,
                "SELECT COUNT(*) FROM information_schema.tables " +
                "WHERE table_schema = current_schema() AND table_name = 'omnidots_import_cursor';"));

            Guid traceId = Guid.NewGuid();
            await ExecuteAsync(
                connectionString,
                $"""
                INSERT INTO omnidots_trace_index (id, serial_id, start_time, end_time)
                VALUES ('{traceId}', '0001', TIMESTAMP '2026-07-14 09:00:00', TIMESTAMP '2026-07-14 09:00:01');

                INSERT INTO omnidots_trace ({CanonicalTraceForeignKeyColumn}, sample_index, x, y, z)
                VALUES ('{traceId}', 0, 1, 2, 3), ('{traceId}', 1, 4, 5, 6);
                """);

            await using RVTSearchContext search = new(TestDbContexts.Npgsql<RVTSearchContext>(connectionString));
            List<OmnidotsTrace> traces = await search.OmnidotsTraces
                .Where(trace => trace.TraceId == traceId)
                .OrderBy(trace => trace.X)
                .ToListAsync();

            Assert.Equal(2, traces.Count);
            Assert.Equal(1d, traces[0].X);
            Assert.Equal(6d, traces[1].Z);
        }
        finally
        {
            SpaTestDatabase.DropSchema(schemaName, connectionString);
        }
    }

    [RequiresPostgresFact]
    // Function summary: Verifies the monitor's rollback returns the portal-built schema to the shape the portal declares.
    public async Task MonitorRollbackMigration_RestoresThePortalDeclaredShape()
    {
        string schemaName = $"omnidots_trace_owner_{Guid.NewGuid():N}";
        string connectionString = SpaTestDatabase.CreateSchema(schemaName);
        try
        {
            await ExecuteAsync(connectionString, File.ReadAllText(MonitorMigrationPath(_monitorMigrationScripts[0])));
            string rollback = File.ReadAllText(MonitorMigrationPath(_monitorMigrationScripts[1]));
            await ExecuteAsync(connectionString, rollback);
            await ExecuteAsync(connectionString, rollback);

            Assert.Empty(await PrimaryKeyColumnsAsync(connectionString));
            Assert.Equal(0L, await ScalarAsync<long>(
                connectionString,
                "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = current_schema() " +
                "AND table_name = 'omnidots_trace' AND column_name = 'sample_index';"));

            // The portal owns the index and the column's nullability; the monitor's round trip must leave both
            // exactly as RVTSearchContext declared them.
            Assert.Equal(1L, await ScalarAsync<long>(
                connectionString,
                "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = current_schema() " +
                "AND tablename = 'omnidots_trace' AND indexname = 'ix_omnidots_trace_omnidots_trace_index_id';"));
            Assert.Equal(1L, await ScalarAsync<long>(
                connectionString,
                "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = current_schema() " +
                $"AND table_name = 'omnidots_trace' AND column_name = '{CanonicalTraceForeignKeyColumn}' " +
                "AND is_nullable = 'YES';"));
        }
        finally
        {
            SpaTestDatabase.DropSchema(schemaName, connectionString);
        }
    }

    /// <summary>
    /// Removes the forward script's documented one-time reconciliation, the only place the monitor's assets
    /// may still name the non-canonical column. An unbalanced or missing end marker fails rather than
    /// silently exempting the rest of the file.
    /// </summary>
    private static string WithoutReconciliationBlock(string sql)
    {
        const string begin = "-- BEGIN legacy-name reconciliation";
        const string end = "-- END legacy-name reconciliation";
        int start = sql.IndexOf(begin, StringComparison.Ordinal);
        if (start < 0)
        {
            return sql;
        }

        int stop = sql.IndexOf(end, start, StringComparison.Ordinal);
        Assert.True(stop > start, $"'{begin}' must be closed by '{end}'.");
        return string.Concat(sql[..start], sql[(stop + end.Length)..]);
    }

    // Function summary: Resolves a shipped Omnidots migration asset from the monorepo root.
    private static string MonitorMigrationPath(string fileName)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Rvt.Mono.slnx")))
        {
            directory = directory.Parent;
        }

        string root = directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find the repository root.");
        return Path.Combine(
            root,
            "apps",
            "monitors",
            "omnidotsmonitor",
            "OmnidotsMonitor",
            "postgres",
            fileName);
    }

    // Function summary: Runs one SQL batch on the schema-scoped connection.
    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    // Function summary: Reads one scalar from the schema-scoped connection.
    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object? value = await command.ExecuteScalarAsync();
        Assert.NotNull(value);
        return (T)value;
    }

    // Function summary: Returns omnidots_trace's primary-key columns in key order, empty when it has no primary key.
    private static async Task<IReadOnlyList<string>> PrimaryKeyColumnsAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT attribute.attname
            FROM pg_constraint constraint_row
            JOIN LATERAL unnest(constraint_row.conkey) WITH ORDINALITY AS key_column(attnum, ordinality) ON TRUE
            JOIN pg_attribute attribute
                ON attribute.attrelid = constraint_row.conrelid
                AND attribute.attnum = key_column.attnum
            WHERE constraint_row.conrelid = 'omnidots_trace'::regclass
              AND constraint_row.contype = 'p'
            ORDER BY key_column.ordinality;
            """;
        List<string> columns = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    [GeneratedRegex(@"(?<![A-Za-z0-9_])trace_id(?![A-Za-z0-9_])", RegexOptions.CultureInvariant)]
    private static partial Regex LegacyIdentifierRegex();
}
