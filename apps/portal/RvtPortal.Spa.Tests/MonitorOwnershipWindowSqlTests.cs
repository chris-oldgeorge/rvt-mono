// File summary: Verifies the ownership predicate compiles to real SQL on every supported provider.
// Major updates:
// - 2026-07-14 pending Added provider translation guards for MonitorOwnershipWindowResolver.OwnsAt.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RvtPortal.Spa.Application.Monitors;

namespace RvtPortal.Spa.Tests;

/// <summary>
/// The rest of the suite runs on the EF InMemory provider, which evaluates every predicate client-side and so
/// cannot tell a translatable expression from an untranslatable one. ToQueryString() compiles the query against
/// a real relational provider without opening a connection, so an ownership predicate that SQL Server or
/// PostgreSQL could not translate fails here instead of in production.
/// </summary>
public sealed class MonitorOwnershipWindowSqlTests
{
    private static readonly DateTime Timestamp = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    // Function summary: Verifies the ownership predicate translates to SQL Server SQL.
    public void OwnsAt_TranslatesOnSqlServer()
    {
        using var context = new RVTDbContext(
            new DbContextOptionsBuilder<RVTDbContext>()
                .UseSqlServer("Server=unused;Database=unused;Trusted_Connection=True;")
                .Options);

        var sql = OwnershipQuerySql(context);

        // The whole-day off-hire rule must survive translation rather than being silently dropped.
        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DATEADD", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    // Function summary: Verifies the ownership predicate translates to PostgreSQL SQL.
    public void OwnsAt_TranslatesOnPostgres()
    {
        using var context = new RVTDbContext(
            new DbContextOptionsBuilder<RVTDbContext>()
                .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
                .Options);

        var sql = OwnershipQuerySql(context);

        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
    }

    // Function summary: Compiles the ownership-filtered deployment query to SQL without executing it.
    private static string OwnershipQuerySql(RVTDbContext context)
    {
        // ToQueryString() throws InvalidOperationException if any part of the predicate cannot be translated.
        return context.Deployments
            .Where(MonitorOwnershipWindowResolver.OwnsAt(Timestamp))
            .ToQueryString();
    }
}
