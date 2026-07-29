// File summary: Provides a stable EF Core design-time factory for migration scaffolding.
// Major updates:
// - 2026-07-26 pending Made design-time domain migrations PostgreSQL-only through shared environment options.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-09 pending Added canonical-baseline migration scaffolding support independent of appsettings.
// - 2026-07-14 pending Moved environment resolution to RvtDesignTimeDatabaseOptions, shared with the search factory.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RVT.DataAccess.Configuration;

namespace RVT.DataAccess.Context;

[SuppressMessage("Naming", "S101:Types should be named in PascalCase", Justification = "Legacy EF design-time factory name matches the established context and migration tooling contract.")]
public sealed class RVTDbContextDesignTimeFactory : IDesignTimeDbContextFactory<RVTDbContext>
{
    // Function summary: Creates the domain context for EF tooling without relying on runtime appsettings files.
    public RVTDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<RVTDbContext> optionsBuilder = new();
        optionsBuilder.UseRvtDatabaseProvider(RvtDesignTimeDatabaseOptions.FromEnvironment());

        return new RVTDbContext(optionsBuilder.Options);
    }
}
