// File summary: Creates PostgreSQL connections and safely quoted identifiers for repository data access.
// Major updates:
// - 2026-07-26 pending Collapsed connection creation and identifier quoting to PostgreSQL.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System.Data.Common;
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

    // Function summary: Creates connection data for the current workflow.
    public DbConnection CreateConnection()
    {
        return new NpgsqlConnection(options.ConnectionString);
    }

    // Function summary: Handles the delimit identifier workflow for this module.
    public string DelimitIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
