// File summary: Configures provider-neutral SQL Server/PostgreSQL database access for repositories and EF Core contexts.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Added SQL Server/PostgreSQL provider support.

using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Npgsql;

namespace RVT.DataAccess.Configuration;

public sealed class RvtDatabaseConnectionFactory : IRvtDatabaseConnectionFactory
{
    private readonly RvtDatabaseOptions options;

    // Function summary: Initializes this type with the dependencies required by its workflow.
    public RvtDatabaseConnectionFactory(IOptions<RvtDatabaseOptions> options)
        : this(options.Value)
    {
    }

    // Function summary: Initializes this type with the dependencies required by its workflow.
    public RvtDatabaseConnectionFactory(RvtDatabaseOptions options)
    {
        options.Validate();
        this.options = options;
    }

    public RvtDatabaseProvider Provider => options.Provider;

    // Function summary: Creates connection data for the current workflow.
    public DbConnection CreateConnection()
    {
        return options.Provider switch
        {
            RvtDatabaseProvider.SqlServer => new SqlConnection(options.ConnectionString),
            RvtDatabaseProvider.Postgres => new NpgsqlConnection(options.ConnectionString),
            _ => throw new InvalidOperationException($"Unsupported database provider '{options.Provider}'.")
        };
    }

    // Function summary: Handles the delimit identifier workflow for this module.
    public string DelimitIdentifier(string identifier)
    {
        var escaped = identifier.Replace("\"", "\"\"", StringComparison.Ordinal);

        return options.Provider switch
        {
            RvtDatabaseProvider.SqlServer => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]",
            RvtDatabaseProvider.Postgres => $"\"{escaped}\"",
            _ => throw new InvalidOperationException($"Unsupported database provider '{options.Provider}'.")
        };
    }
}
