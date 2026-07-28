// File summary: Configures PostgreSQL database access for repositories and EF Core contexts.
// Major updates:
// - 2026-07-26 pending Collapsed Portal registration and shared connections to Npgsql.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-07-08 pending Added shared DbConnection creation for cross-context transaction boundaries.

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace RVT.DataAccess.Configuration;

public static class RvtDatabaseServiceCollectionExtensions
{
    // Function summary: Registers PostgreSQL database options and supporting services.
    public static RvtDatabaseOptions AddRvtDatabaseProvider(this IServiceCollection services, IConfiguration configuration)
    {
        RvtDatabaseOptions options = RvtDatabaseOptions.FromConfiguration(configuration);

        services.TryAddSingleton<IOptions<RvtDatabaseOptions>>(Options.Create(options));
        services.TryAddSingleton<IRvtDatabaseConnectionFactory, RvtDatabaseConnectionFactory>();
        services.TryAddSingleton<IRvtStoredRoutineExecutor, RvtStoredRoutineExecutor>();

        return options;
    }

    /// <summary>
    /// The migrations-history table for <c>RVTSearchContext</c>.
    ///
    /// The domain context and the search context map disjoint halves of the SAME database, so each needs its own
    /// history table. Sharing the default <c>__EFMigrationsHistory</c> would make each context believe the other
    /// context's migrations were its own: <c>database update --context RVTSearchContext</c> would see
    /// <c>CanonicalBaseline</c> recorded, conclude nothing is pending, and never create the time-series tables.
    /// </summary>
    public const string SearchMigrationsHistoryTable = "__EFMigrationsHistorySearch";

    /// <summary>
    /// The migrations-history table for <c>ApplicationDbContext</c> (ASP.NET Identity), for the same reason as
    /// <see cref="SearchMigrationsHistoryTable"/>: three contexts, one database, three independent chains.
    /// </summary>
    public const string IdentityMigrationsHistoryTable = "__EFMigrationsHistoryIdentity";

    // Function summary: Applies PostgreSQL using the connection string in RVT database options.
    public static DbContextOptionsBuilder UseRvtDatabaseProvider(
        this DbContextOptionsBuilder optionsBuilder,
        RvtDatabaseOptions options,
        string? migrationsHistoryTable = null)
    {
        options.Validate();

        // Guards writes of non-UTC DateTime values to PostgreSQL timestamptz columns and is inert on
        // timestamp-without-time-zone columns (see UtcTimestampGuardInterceptor).
        optionsBuilder.AddInterceptors(UtcTimestampGuardInterceptor.Instance);

        return optionsBuilder.UseNpgsql(
            options.ConnectionString,
            npgsql => ConfigureNpgsql(npgsql, options, migrationsHistoryTable));
    }

    // Function summary: Applies PostgreSQL to a caller-owned connection shared across EF contexts.
    public static DbContextOptionsBuilder UseRvtDatabaseProvider(
        this DbContextOptionsBuilder optionsBuilder,
        RvtDatabaseOptions options,
        DbConnection connection,
        string? migrationsHistoryTable = null)
    {
        options.Validate();

        // Guards writes of non-UTC DateTime values to PostgreSQL timestamptz columns and is inert on
        // timestamp-without-time-zone columns (see UtcTimestampGuardInterceptor).
        optionsBuilder.AddInterceptors(UtcTimestampGuardInterceptor.Instance);

        return optionsBuilder.UseNpgsql(
            connection,
            npgsql => ConfigureNpgsql(npgsql, options, migrationsHistoryTable));
    }

    // Function summary: Applies shared resiliency and timeout settings to the PostgreSQL provider.
    private static void ConfigureNpgsql(
        NpgsqlDbContextOptionsBuilder npgsql,
        RvtDatabaseOptions options,
        string? migrationsHistoryTable)
    {
        if (options.EnableRetryOnFailure)
        {
            npgsql.EnableRetryOnFailure(options.MaxRetryCount);
        }

        if (!string.IsNullOrWhiteSpace(migrationsHistoryTable))
        {
            npgsql.MigrationsHistoryTable(migrationsHistoryTable);
        }

        npgsql.CommandTimeout(options.CommandTimeoutSeconds);
    }

    // Function summary: Creates the Npgsql connection used by the portal's scoped EF contexts.
    public static DbConnection CreateDbConnection(this RvtDatabaseOptions options)
    {
        options.Validate();

        return new NpgsqlConnection(options.ConnectionString);
    }
}
