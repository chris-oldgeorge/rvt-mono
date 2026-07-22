// File summary: Provides a stable EF Core design-time factory for migration scaffolding.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-09 pending Added canonical-baseline migration scaffolding support independent of appsettings.
// - 2026-07-14 pending Moved environment resolution to RvtDesignTimeDatabaseOptions, shared with the search factory.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RVT.DataAccess.Configuration;

namespace RVT.DataAccess.Context;

public sealed class RVTDbContextDesignTimeFactory : IDesignTimeDbContextFactory<RVTDbContext>
{
    // Function summary: Creates the domain context for EF tooling without relying on runtime appsettings files.
    public RVTDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<RVTDbContext>();
        optionsBuilder.UseRvtDatabaseProvider(RvtDesignTimeDatabaseOptions.FromEnvironment());

        return new RVTDbContext(optionsBuilder.Options);
    }
}
