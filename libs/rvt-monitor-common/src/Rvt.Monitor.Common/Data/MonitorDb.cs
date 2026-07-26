using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Npgsql;

namespace Rvt.Monitor.Common.Data;

// Summary: Centralizes PostgreSQL operations shared by monitor apps.
public static class MonitorDb
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly Regex SafeSqlIdentifierRegex = new(
        "^(?:\"[A-Za-z_][A-Za-z0-9_]*\"|[A-Za-z_][A-Za-z0-9_]*)" +
        "(?:\\.(?:\"[A-Za-z_][A-Za-z0-9_]*\"|[A-Za-z_][A-Za-z0-9_]*))*$",
        RegexOptions.Compiled,
        RegexTimeout);

    public static void ValidateLegacyProvider(string? primaryProvider, string? fallbackProvider)
    {
        var provider = !string.IsNullOrWhiteSpace(primaryProvider)
            ? primaryProvider
            : fallbackProvider;
        if (string.IsNullOrWhiteSpace(provider))
        {
            return;
        }

        var normalized = provider.Trim().ToLowerInvariant();
        if (normalized is
            "postgres" or
            "postgresql" or
            "npgsql" or
            "timescale" or
            "timescaledb")
        {
            return;
        }

        throw new InvalidOperationException("PostgreSQL is the only supported database provider");
    }

    public static DbConnection OpenConnection(string connectionString)
    {
        DbConnection connection = new NpgsqlConnection(connectionString);
        connection.Open();
        return connection;
    }

    public static DbCommand CreateCommand(string sql, DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        return command;
    }

    public static void BulkInsert(
        string connectionString,
        string tableName,
        DataTable table,
        MonitorDbOptions options)
    {
        var columns = table.Columns
            .Cast<DataColumn>()
            .Select(column => RequireSafeSqlIdentifier(column.ColumnName, "bulk insert column"))
            .ToArray();
        var mappedTable = options.IdentifierMap.TryGetValue(tableName, out var mapped)
            ? mapped
            : tableName;
        var targetTable = RequireSafeSqlIdentifier(mappedTable, "bulk insert table");

        using var connection = (NpgsqlConnection)OpenConnection(connectionString);
        using var writer = connection.BeginBinaryImport(
            $"COPY {targetTable} ({string.Join(", ", columns)}) FROM STDIN (FORMAT BINARY)");
        foreach (DataRow row in table.Rows)
        {
            writer.StartRow();
            foreach (DataColumn column in table.Columns)
            {
                writer.Write(row[column] == DBNull.Value ? null : row[column]);
            }
        }

        writer.Complete();
    }

    public static void WriteException(
        string connectionString,
        string tag,
        Exception exception,
        string serviceName,
        string serviceVersion)
    {
        const string sql = """
            INSERT INTO error_log (host, source, message, level, stack_trace, variables, logged_at)
            VALUES (@Host, @Source, @Message, @Level, @StackTrace, @Variables, @LogTime);
            """;

        using var connection = OpenConnection(connectionString);
        using var command = CreateCommand(sql, connection);
        command.Parameters.AddWithValue("@Host", HostName());
        command.Parameters.AddWithValue("@Source", serviceName + " " + serviceVersion);
        command.Parameters.AddWithValue("@Message", exception.Message);
        command.Parameters.AddWithValue("@Level", "Exception");
        command.Parameters.AddWithValue("@StackTrace", exception.StackTrace ?? "");
        command.Parameters.AddWithValue("@Variables", tag);
        command.Parameters.AddWithValue("@LogTime", DateTime.UtcNow);
        command.ExecuteNonQuery();
    }

    public static string RequireMappedSqlIdentifier(
        string identifier,
        IReadOnlyDictionary<string, string> allowedIdentifiers,
        string context)
    {
        if (!allowedIdentifiers.TryGetValue(identifier, out var mappedIdentifier))
        {
            throw new NotSupportedException($"Unsupported SQL identifier '{identifier}' for {context}.");
        }

        return RequireSafeSqlIdentifier(mappedIdentifier, context);
    }

    public static string RequireSafeSqlIdentifier(string identifier, string context)
    {
        if (!SafeSqlIdentifierRegex.IsMatch(identifier))
        {
            throw new InvalidOperationException($"Unsafe SQL identifier '{identifier}' for {context}.");
        }

        return identifier;
    }

    private static string HostName()
    {
        var hostName = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME") ?? Environment.MachineName;
        return hostName.Length > 100 ? hostName[..100] : hostName;
    }
}
