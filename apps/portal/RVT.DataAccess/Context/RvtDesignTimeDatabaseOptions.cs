// File summary: Resolves the required PostgreSQL connection string EF tooling uses from the environment.
// Major updates:
// - 2026-07-26 pending Removed provider selection and made RVT_EF_CONNECTION mandatory.
// - 2026-07-14 pending Extracted from RVTDbContextDesignTimeFactory so both context factories share one resolver.

using RVT.DataAccess.Configuration;

namespace RVT.DataAccess.Context;

/// <summary>
/// Both design-time factories resolve their connection the same way, and it must stay the same way: the
/// connection string is read from the environment and never from a file in the repository.
/// </summary>
public static class RvtDesignTimeDatabaseOptions
{
    // Function summary: Builds design-time PostgreSQL options from RVT_EF_CONNECTION.
    public static RvtDatabaseOptions FromEnvironment()
    {
        string? connectionString = Environment.GetEnvironmentVariable("RVT_EF_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set the RVT_EF_CONNECTION environment variable to a PostgreSQL connection string before running EF tooling.");
        }

        return new RvtDatabaseOptions
        {
            ConnectionString = connectionString
        };
    }
}
