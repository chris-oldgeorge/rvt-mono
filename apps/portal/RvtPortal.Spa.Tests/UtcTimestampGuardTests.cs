// File summary: Verifies the timestamptz UTC guard rejects non-UTC DateTime writes to the domain context.
// Major updates:
// - 2026-07-15 pending Added alongside UtcTimestampGuardInterceptor after DateTime.Now writes to timestamptz columns.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Configuration;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

/// <summary>
/// The domain context's DateTime columns are all PostgreSQL <c>timestamp with time zone</c>, which Npgsql writes
/// only from <c>Kind=Utc</c> values. These tests exercise <see cref="UtcTimestampGuardInterceptor.Guard"/>
/// against a real Npgsql-built model - so the store type is genuinely timestamptz - without opening a connection:
/// the guard reads only the change tracker.
/// </summary>
public sealed class UtcTimestampGuardTests
{
    // Function summary: Builds a domain context on the PostgreSQL provider without connecting to a database.
    private static RVTDbContext NpgsqlContext()
    {
        var options = new DbContextOptionsBuilder<RVTDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;
        return new RVTDbContext(options);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    // Function summary: Verifies a non-UTC DateTime bound for a timestamptz column is rejected before save.
    public void Guard_RejectsNonUtcTimestamptzWrite(DateTimeKind kind)
    {
        using var context = NpgsqlContext();
        context.Sites.Add(new Site { CreateDate = DateTime.SpecifyKind(new DateTime(2026, 7, 15, 9, 0, 0), kind) });

        var error = Assert.Throws<InvalidOperationException>(() => UtcTimestampGuardInterceptor.Guard(context));

        Assert.Contains("Site.CreateDate", error.Message, StringComparison.Ordinal);
        Assert.Contains(kind.ToString(), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies a UTC DateTime bound for a timestamptz column passes the guard.
    public void Guard_AllowsUtcTimestamptzWrite()
    {
        using var context = NpgsqlContext();
        context.Sites.Add(new Site { CreateDate = DateTime.UtcNow });

        UtcTimestampGuardInterceptor.Guard(context);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies end-to-end that the guard fires on a real save and a UTC site actually inserts.
    public async Task SiteInsert_IsGuardedForLocalAndSucceedsForUtc()
    {
        // Opt-in against a real database: the interceptor is wired the way production wires it (AddInterceptors),
        // and both halves are proven - DateTime.Now is stopped at SaveChanges, and DateTime.UtcNow reaches
        // PostgreSQL and inserts. Both cases roll back, leaving no row behind.
        var connectionString = Environment.GetEnvironmentVariable(RequiresPostgresFactAttribute.ConnectionVariable);
        var options = new DbContextOptionsBuilder<RVTDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(UtcTimestampGuardInterceptor.Instance)
            .Options;
        await using var context = new RVTDbContext(options);
        await using var transaction = await context.Database.BeginTransactionAsync();

        // DateTime.Now (Kind=Local) - the guard must stop this before it reaches the database.
        context.Sites.Add(new Site { SiteName = "TEST-GUARD-LOCAL", CreateDate = DateTime.Now, Archived = false });
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        // DateTime.UtcNow (Kind=Utc) - Npgsql accepts this for a timestamptz column and the row inserts.
        context.Sites.Add(new Site { SiteName = "TEST-GUARD-UTC", CreateDate = DateTime.UtcNow, Archived = false });
        var written = await context.SaveChangesAsync();
        Assert.Equal(1, written);

        await transaction.RollbackAsync();
    }
}
