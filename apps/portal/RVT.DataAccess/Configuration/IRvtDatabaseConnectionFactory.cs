// File summary: Configures provider-neutral SQL Server/PostgreSQL database access for repositories and EF Core contexts.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Added SQL Server/PostgreSQL provider support.

using System.Data.Common;

namespace RVT.DataAccess.Configuration;

public interface IRvtDatabaseConnectionFactory
{
    RvtDatabaseProvider Provider { get; }

    DbConnection CreateConnection();

    string DelimitIdentifier(string identifier);
}
