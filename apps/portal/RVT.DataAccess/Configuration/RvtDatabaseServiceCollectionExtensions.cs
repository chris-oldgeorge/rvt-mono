// File summary: Configures provider-neutral SQL Server/PostgreSQL database access for repositories and EF Core contexts.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Added SQL Server/PostgreSQL provider support.
// - 2026-07-08 pending Added shared DbConnection creation for cross-context transaction boundaries.

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace RVT.DataAccess.Configuration;

public static class RvtDatabaseServiceCollectionExtensions
{
    // Function summary: Registers RVT database provider for the current workflow.
    public static RvtDatabaseOptions AddRvtDatabaseProvider(this IServiceCollection services, IConfiguration configuration)
    {
        var options = RvtDatabaseOptions.FromConfiguration(configuration);

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

    // Function summary: Applies RVT database provider to the current configuration.
    public static DbContextOptionsBuilder UseRvtDatabaseProvider(
        this DbContextOptionsBuilder optionsBuilder,
        RvtDatabaseOptions options,
        string? migrationsHistoryTable = null)
    {
        options.Validate();

        // Guards writes of non-UTC DateTime values to PostgreSQL timestamptz columns; inert on SQL Server and on
        // timestamp-without-time-zone columns (see UtcTimestampGuardInterceptor).
        optionsBuilder.AddInterceptors(UtcTimestampGuardInterceptor.Instance);

        return options.Provider switch
        {
            RvtDatabaseProvider.SqlServer => optionsBuilder.UseSqlServer(
                options.ConnectionString,
                sql => ConfigureSqlServer(sql, options, migrationsHistoryTable)),
            RvtDatabaseProvider.Postgres => optionsBuilder.UseNpgsql(
                options.ConnectionString,
                npgsql => ConfigureNpgsql(npgsql, options, migrationsHistoryTable)),
            _ => throw new InvalidOperationException($"Unsupported database provider '{options.Provider}'.")
        };
    }

    // Function summary: Applies RVT database provider to a caller-owned connection shared across EF contexts.
    public static DbContextOptionsBuilder UseRvtDatabaseProvider(
        this DbContextOptionsBuilder optionsBuilder,
        RvtDatabaseOptions options,
        DbConnection connection,
        string? migrationsHistoryTable = null)
    {
        options.Validate();

        // Guards writes of non-UTC DateTime values to PostgreSQL timestamptz columns; inert on SQL Server and on
        // timestamp-without-time-zone columns (see UtcTimestampGuardInterceptor).
        optionsBuilder.AddInterceptors(UtcTimestampGuardInterceptor.Instance);

        return options.Provider switch
        {
            RvtDatabaseProvider.SqlServer => optionsBuilder.UseSqlServer(
                connection,
                sql => ConfigureSqlServer(sql, options, migrationsHistoryTable)),
            RvtDatabaseProvider.Postgres => optionsBuilder.UseNpgsql(
                connection,
                npgsql => ConfigureNpgsql(npgsql, options, migrationsHistoryTable)),
            _ => throw new InvalidOperationException($"Unsupported database provider '{options.Provider}'.")
        };
    }

    // Function summary: Applies shared resiliency and timeout settings to the SQL Server provider.
    private static void ConfigureSqlServer(
        SqlServerDbContextOptionsBuilder sql,
        RvtDatabaseOptions options,
        string? migrationsHistoryTable)
    {
        if (options.EnableRetryOnFailure)
        {
            sql.EnableRetryOnFailure(options.MaxRetryCount);
        }

        if (!string.IsNullOrWhiteSpace(migrationsHistoryTable))
        {
            sql.MigrationsHistoryTable(migrationsHistoryTable);
        }

        sql.CommandTimeout(options.CommandTimeoutSeconds);
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

    // Function summary: Creates the provider-specific connection used by the portal's scoped EF contexts.
    public static DbConnection CreateDbConnection(this RvtDatabaseOptions options)
    {
        options.Validate();

        return options.Provider switch
        {
            RvtDatabaseProvider.SqlServer => new SqlConnection(options.ConnectionString),
            RvtDatabaseProvider.Postgres => new NpgsqlConnection(options.ConnectionString),
            _ => throw new InvalidOperationException($"Unsupported database provider '{options.Provider}'.")
        };
    }
}
