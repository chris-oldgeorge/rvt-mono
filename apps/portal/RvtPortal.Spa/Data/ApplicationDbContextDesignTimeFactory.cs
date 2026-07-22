// File summary: Provides an EF Core design-time factory for the Identity context's migration scaffolding.
// Major updates:
// - 2026-07-14 pending Added so the ASP.NET Identity tables can be built by EF migrations.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RVT.DataAccess.Configuration;
using RVT.DataAccess.Context;

namespace RvtPortal.Spa.Data;

/// <summary>
/// Design-time factory for <see cref="ApplicationDbContext"/>.
///
/// It resolves the provider and connection from RVT_EF_PROVIDER / RVT_EF_CONNECTION, exactly as the two
/// RVT.DataAccess factories do - see docs/database/portal/ef-migrations.md - and points the context at its own
/// migrations-history table, because all three contexts migrate disjoint halves of a single database.
/// </summary>
public sealed class ApplicationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    // Function summary: Creates the Identity context for EF tooling without relying on runtime appsettings files.
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseRvtDatabaseProvider(
            RvtDesignTimeDatabaseOptions.FromEnvironment(),
            RvtDatabaseServiceCollectionExtensions.IdentityMigrationsHistoryTable);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
