// File summary: Configures PostgreSQL database access for repositories and EF Core contexts.
// Major updates:
// - 2026-07-26 pending Removed runtime provider selection while validating legacy provider settings.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using Microsoft.Extensions.Configuration;

namespace RVT.DataAccess.Configuration;

public sealed class RvtDatabaseOptions
{
    public const string SectionName = "Database";
    public const string DefaultConnectionStringName = "DefaultConnection";
    public const bool DefaultEnableRetryOnFailure = true;
    public const int DefaultMaxRetryCount = 6;
    public const int DefaultCommandTimeoutSeconds = 120;
    public const bool DefaultValidateSchemaOnStartup = true;

    public string ConnectionStringName { get; set; } = DefaultConnectionStringName;

    public string ConnectionString { get; set; } = string.Empty;

    public string PostgresRoutineSchema { get; set; } = "public";

    /// <summary>
    /// Retries transient faults (cloud failovers, connection drops) instead of surfacing them as errors.
    /// Requires every user-initiated transaction to run inside an execution strategy; see EfCoreUnitOfWork.
    /// </summary>
    public bool EnableRetryOnFailure { get; set; } = DefaultEnableRetryOnFailure;

    public int MaxRetryCount { get; set; } = DefaultMaxRetryCount;

    /// <summary>
    /// Command timeout for EF queries and stored routines. The default provider timeout (30s) is too short
    /// for the aggregate search views (site/noise/dust averages).
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = DefaultCommandTimeoutSeconds;

    /// <summary>
    /// Verifies at startup that every relation and column the EF model maps to exists in the database, so
    /// mapping drift fails immediately and visibly instead of on the first request that touches it.
    /// </summary>
    public bool ValidateSchemaOnStartup { get; set; } = DefaultValidateSchemaOnStartup;

    // Function summary: Handles the from configuration workflow for this module.
    public static RvtDatabaseOptions FromConfiguration(IConfiguration configuration)
    {
        ValidateLegacyProvider(
            configuration[$"{SectionName}:Provider"] ??
            configuration["RvtDatabase:Provider"]);

        RvtDatabaseOptions options = new()
        {
            ConnectionStringName = ReadValue(configuration, "ConnectionStringName", DefaultConnectionStringName),
            PostgresRoutineSchema = ReadValue(configuration, "PostgresRoutineSchema", "public"),
            EnableRetryOnFailure = ReadBool(configuration, nameof(EnableRetryOnFailure), DefaultEnableRetryOnFailure),
            MaxRetryCount = ReadInt(configuration, nameof(MaxRetryCount), DefaultMaxRetryCount),
            CommandTimeoutSeconds = ReadInt(configuration, nameof(CommandTimeoutSeconds), DefaultCommandTimeoutSeconds),
            ValidateSchemaOnStartup = ReadBool(configuration, nameof(ValidateSchemaOnStartup), DefaultValidateSchemaOnStartup)
        };

        options.ConnectionString = configuration[$"{SectionName}:ConnectionString"]
            ?? configuration["RvtDatabase:ConnectionString"]
            ?? configuration.GetConnectionString(options.ConnectionStringName)
            ?? string.Empty;

        return options;
    }

    // Function summary: Rejects legacy provider settings that would select a non-PostgreSQL database.
    public static void ValidateLegacyProvider(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string normalized = value.Trim().ToLowerInvariant();
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

    // Function summary: Evaluates validate for the current decision point.
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' not found. Configure ConnectionStrings:{ConnectionStringName} or Database:ConnectionString.");
        }

        if (MaxRetryCount < 0)
        {
            throw new InvalidOperationException($"{nameof(MaxRetryCount)} must not be negative.");
        }

        if (CommandTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException($"{nameof(CommandTimeoutSeconds)} must be greater than zero.");
        }
    }

    // Function summary: Retrieves value data for callers.
    private static string ReadValue(IConfiguration configuration, string key, string fallback)
    {
        string? value = configuration[$"{SectionName}:{key}"] ?? configuration[$"RvtDatabase:{key}"];
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    // Function summary: Reads an optional boolean setting, falling back when unset or unparsable.
    private static bool ReadBool(IConfiguration configuration, string key, bool fallback)
    {
        string? value = configuration[$"{SectionName}:{key}"] ?? configuration[$"RvtDatabase:{key}"];
        return bool.TryParse(value, out bool parsed) ? parsed : fallback;
    }

    // Function summary: Reads an optional integer setting, falling back when unset or unparsable.
    private static int ReadInt(IConfiguration configuration, string key, int fallback)
    {
        string? value = configuration[$"{SectionName}:{key}"] ?? configuration[$"RvtDatabase:{key}"];
        return int.TryParse(value, out int parsed) ? parsed : fallback;
    }
}
