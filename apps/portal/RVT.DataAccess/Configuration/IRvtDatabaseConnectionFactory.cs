// File summary: Provides PostgreSQL connections and identifier quoting for repository data access.
// Major updates:
// - 2026-07-26 pending Removed the provider-selection contract from the connection factory.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System.Data.Common;

namespace RVT.DataAccess.Configuration;

public interface IRvtDatabaseConnectionFactory
{
    DbConnection CreateConnection();

    string DelimitIdentifier(string identifier);
}
