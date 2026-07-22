// File summary: Configures provider-neutral SQL Server/PostgreSQL database access for repositories and EF Core contexts.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Added SQL Server/PostgreSQL provider support.
// - 2026-06-04 pending Hardened PostgreSQL routine command text against unsafe identifiers.
// - 2026-06-09 pending Mapped legacy routine names to canonical PostgreSQL function names.
// - 2026-06-09 pending Mapped SQL Server stored procedure calls to canonical procedure names after the SQL Server cutover.

using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Options;

namespace RVT.DataAccess.Configuration;

public sealed class RvtStoredRoutineExecutor : IRvtStoredRoutineExecutor
{
    private readonly IRvtDatabaseConnectionFactory connectionFactory;
    private readonly RvtDatabaseOptions options;

    // Function summary: Initializes this type with the dependencies required by its workflow.
    public RvtStoredRoutineExecutor(
        IRvtDatabaseConnectionFactory connectionFactory,
        IOptions<RvtDatabaseOptions> options)
    {
        this.connectionFactory = connectionFactory;
        this.options = options.Value;
    }

    // Function summary: Runs a stored routine/function and maps its result rows for callers.
    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string routineName,
        IEnumerable<RvtRoutineParameter> parameters,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken = default)
    {
        var parameterList = parameters.ToList();
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandTimeout = options.CommandTimeoutSeconds;
        ConfigureCommand(command, routineName, parameterList);

        foreach (var parameter in parameterList)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = NormalizeParameterName(parameter.Name);
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            command.Parameters.Add(dbParameter);
        }

        var rows = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    // Function summary: Configures command text and parameters for the selected database provider.
    private void ConfigureCommand(
        DbCommand command,
        string routineName,
        IReadOnlyCollection<RvtRoutineParameter> parameters)
    {
        if (connectionFactory.Provider == RvtDatabaseProvider.SqlServer)
        {
            command.CommandText = BuildSqlServerRoutineName(routineName);
            command.CommandType = CommandType.StoredProcedure;
            return;
        }

        var argumentList = string.Join(", ", parameters.Select(parameter => NormalizeParameterName(parameter.Name)));
        var safeRoutineName = BuildPostgresRoutineName(routineName);
        command.CommandText = string.Concat("select * from ", safeRoutineName, "(", argumentList, ")");
        command.CommandType = CommandType.Text;
    }

    // Function summary: Builds a SQL Server stored-procedure identifier after validating and canonicalizing the routine name.
    private string BuildSqlServerRoutineName(string routineName)
    {
        var parts = routineName.Split('.', StringSplitOptions.None);
        if (parts.Length is < 1 or > 2 || parts.Any(part => !IsSafeIdentifier(part)))
        {
            throw new ArgumentException("Routine name must be an unqualified identifier or schema-qualified identifier.", nameof(routineName));
        }

        var canonicalRoutineName = DatabaseNamingRules.ToCanonicalRoutineName(parts[^1]);
        ValidateIdentifier(canonicalRoutineName, nameof(routineName));

        if (parts.Length == 2)
        {
            ValidateIdentifier(parts[0], nameof(routineName));
            return string.Join(
                ".",
                connectionFactory.DelimitIdentifier(parts[0]),
                connectionFactory.DelimitIdentifier(canonicalRoutineName));
        }

        return connectionFactory.DelimitIdentifier(canonicalRoutineName);
    }

    // Function summary: Builds a schema-qualified PostgreSQL routine name after validating and canonicalizing each identifier part.
    private string BuildPostgresRoutineName(string routineName)
    {
        var parts = routineName.Split('.', StringSplitOptions.None);
        if (parts.Length is < 1 or > 2 || parts.Any(part => !IsSafeIdentifier(part)))
        {
            throw new ArgumentException("Routine name must be an unqualified identifier or schema-qualified identifier.", nameof(routineName));
        }

        var canonicalRoutineName = DatabaseNamingRules.ToCanonicalRoutineName(parts[^1]);
        ValidateIdentifier(canonicalRoutineName, nameof(routineName));

        if (parts.Length == 2)
        {
            ValidateIdentifier(parts[0], nameof(routineName));
            return string.Join(
                ".",
                connectionFactory.DelimitIdentifier(parts[0]),
                connectionFactory.DelimitIdentifier(canonicalRoutineName));
        }

        ValidateIdentifier(options.PostgresRoutineSchema, nameof(options.PostgresRoutineSchema));
        return $"{connectionFactory.DelimitIdentifier(options.PostgresRoutineSchema)}.{connectionFactory.DelimitIdentifier(canonicalRoutineName)}";
    }

    // Function summary: Normalizes and validates a stored routine parameter name before it appears in SQL text.
    private static string NormalizeParameterName(string name)
    {
        var normalizedName = name.StartsWith('@') ? name[1..] : name;
        ValidateIdentifier(normalizedName, nameof(name));
        return $"@{normalizedName}";
    }

    // Function summary: Validates database identifiers allowed in provider-generated SQL text.
    private static void ValidateIdentifier(string value, string parameterName)
    {
        if (!IsSafeIdentifier(value))
        {
            throw new ArgumentException("Identifier must contain only letters, digits, or underscores and must not start with a digit.", parameterName);
        }
    }

    // Function summary: Checks whether a database identifier is safe to quote and interpolate.
    private static bool IsSafeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || char.IsDigit(value[0]))
        {
            return false;
        }

        return value.All(character => char.IsLetterOrDigit(character) || character == '_');
    }
}
