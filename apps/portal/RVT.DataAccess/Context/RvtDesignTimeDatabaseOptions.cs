// File summary: Resolves the provider and connection string EF tooling uses, from the environment only.
// Major updates:
// - 2026-07-14 pending Extracted from RVTDbContextDesignTimeFactory so both context factories share one resolver.

using RVT.DataAccess.Configuration;

namespace RVT.DataAccess.Context;

/// <summary>
/// Both design-time factories resolve their connection the same way, and it must stay the same way: the
/// connection string is read from the environment and never from a file in the repository.
/// </summary>
public static class RvtDesignTimeDatabaseOptions
{
    // Function summary: Builds design-time database options from RVT_EF_PROVIDER and RVT_EF_CONNECTION.
    public static RvtDatabaseOptions FromEnvironment()
    {
        var provider = ResolveProvider();
        return new RvtDatabaseOptions
        {
            Provider = provider,
            ConnectionString = Environment.GetEnvironmentVariable("RVT_EF_CONNECTION")
                ?? DefaultConnectionString(provider)
        };
    }

    // Function summary: Supplies a credential-free local default, or demands an explicit connection string.
    private static string DefaultConnectionString(RvtDatabaseProvider provider)
    {
        // SQL Server can authenticate with the current Windows identity, so a local default carries no secret.
        if (provider == RvtDatabaseProvider.SqlServer)
        {
            return "Server=localhost;Database=rvt_design_time;Trusted_Connection=True;TrustServerCertificate=True";
        }

        // PostgreSQL has no equivalent, and the previous default hardcoded postgres/postgres in source. EF
        // tooling now asks for the connection string explicitly rather than shipping credentials in the repo.
        throw new InvalidOperationException(
            "Set the RVT_EF_CONNECTION environment variable to a PostgreSQL connection string before running EF " +
            "tooling with RVT_EF_PROVIDER=postgres. Example: " +
            "RVT_EF_CONNECTION=\"Host=localhost;Database=rvt_design_time;Username=<user>;Password=<value>\"");
    }

    // Function summary: Resolves the design-time provider while defaulting to SQL Server for EF migrations.
    private static RvtDatabaseProvider ResolveProvider()
    {
        var configuredProvider = Environment.GetEnvironmentVariable("RVT_EF_PROVIDER");
        return string.Equals(configuredProvider, "postgres", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(configuredProvider, "postgresql", StringComparison.OrdinalIgnoreCase)
                ? RvtDatabaseProvider.Postgres
                : RvtDatabaseProvider.SqlServer;
    }
}
