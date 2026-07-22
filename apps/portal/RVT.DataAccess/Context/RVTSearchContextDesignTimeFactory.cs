// File summary: Provides an EF Core design-time factory for the search context's migration scaffolding.
// Major updates:
// - 2026-07-14 pending Added so the search context's time-series tables can be built by EF migrations.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RVT.DataAccess.Configuration;

namespace RVT.DataAccess.Context;

/// <summary>
/// Design-time factory for <see cref="RVTSearchContext"/>.
///
/// It reads the same RVT_EF_PROVIDER / RVT_EF_CONNECTION environment variables as
/// <see cref="RVTDbContextDesignTimeFactory"/> - see docs/database/ef-migrations.md - and differs from it in one
/// respect: it points the context at its own migrations-history table, because both contexts migrate disjoint
/// halves of a single database.
/// </summary>
public sealed class RVTSearchContextDesignTimeFactory : IDesignTimeDbContextFactory<RVTSearchContext>
{
    // Function summary: Creates the search context for EF tooling without relying on runtime appsettings files.
    public RVTSearchContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<RVTSearchContext>();
        optionsBuilder.UseRvtDatabaseProvider(
            RvtDesignTimeDatabaseOptions.FromEnvironment(),
            RvtDatabaseServiceCollectionExtensions.SearchMigrationsHistoryTable);

        return new RVTSearchContext(optionsBuilder.Options);
    }
}
