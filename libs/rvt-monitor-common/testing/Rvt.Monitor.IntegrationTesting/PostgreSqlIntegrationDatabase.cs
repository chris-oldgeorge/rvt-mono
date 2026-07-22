using System.Text.Json;
using Npgsql;

namespace Rvt.Monitor.IntegrationTesting;

public sealed class PostgreSqlIntegrationDatabase : IAsyncDisposable
{
    public const string ConnectionStringEnvironmentVariable = "RVT__POSTGRES_INTEGRATION_CONNECTION";
    private const string DevelopmentSettingsFileName = "rvt-integration.appsettings.Development.json";
    private readonly string adminConnectionString;
    private readonly Func<CancellationToken, Task>? dropSchema;
    private bool disposed;

    private PostgreSqlIntegrationDatabase(string adminConnectionString, string connectionString, string schemaName)
    {
        this.adminConnectionString = adminConnectionString;
        ConnectionString = connectionString;
        SchemaName = schemaName;
    }

    internal PostgreSqlIntegrationDatabase(
        string adminConnectionString,
        string connectionString,
        string schemaName,
        Func<CancellationToken, Task> dropSchema)
        : this(adminConnectionString, connectionString, schemaName)
    {
        this.dropSchema = dropSchema;
    }

    public string ConnectionString { get; }
    public string SchemaName { get; }

    public static async Task<PostgreSqlIntegrationDatabase> CreateAsync(
        string setupSql, string resetSql, CancellationToken cancellationToken = default)
    {
        var adminConnectionString = GetAdminConnectionString();
        if (string.IsNullOrWhiteSpace(adminConnectionString))
        {
            throw new InvalidOperationException(
                $"Set {ConnectionStringEnvironmentVariable} to run PostgreSQL integration tests.");
        }

        var schemaName = $"rvt_integration_{Guid.NewGuid():N}";
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString) { SearchPath = schemaName };
        var database = new PostgreSqlIntegrationDatabase(adminConnectionString, builder.ConnectionString, schemaName);

        try
        {
            await database.ExecuteAdminAsync($"CREATE SCHEMA {QuoteIdentifier(schemaName)};", cancellationToken);
            await database.ExecuteScopedAsync(setupSql, cancellationToken);
            await database.ExecuteScopedAsync(resetSql, cancellationToken);
            return database;
        }
        catch
        {
            await database.DropSchemaAsync(CancellationToken.None);
            throw;
        }
    }

    public NpgsqlConnection OpenConnection() => new(ConnectionString);

    internal static string? GetAdminConnectionString() =>
        ResolveAdminConnectionString(
            Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable),
            ReadDevelopmentConnectionString());

    internal static string? ResolveAdminConnectionString(string? environmentValue, string? developmentValue) =>
        !string.IsNullOrWhiteSpace(environmentValue)
            ? environmentValue
            : string.IsNullOrWhiteSpace(developmentValue)
                ? null
                : developmentValue;

    public Task ResetAsync(string resetSql, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(resetSql, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        try
        {
            await (dropSchema?.Invoke(CancellationToken.None) ?? DropSchemaAsync(CancellationToken.None));
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Failed to drop PostgreSQL integration schema '{SchemaName}'. DisposeAsync can be retried.",
                exception);
        }

        disposed = true;
    }

    private async Task ExecuteScopedAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task ExecuteAdminAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private Task DropSchemaAsync(CancellationToken cancellationToken) =>
        ExecuteAdminAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(SchemaName)} CASCADE;", cancellationToken);

    private static string? ReadDevelopmentConnectionString()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, DevelopmentSettingsFileName);
        if (!File.Exists(settingsPath))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        return document.RootElement.TryGetProperty(ConnectionStringEnvironmentVariable, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";
}
