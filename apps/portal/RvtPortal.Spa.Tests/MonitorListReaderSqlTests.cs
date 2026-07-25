// File summary: Verifies the monitor list projection compiles to canonical PostgreSQL SQL.
// Major updates:
// - 2026-07-25 pending Removed the retired SQL Server translation case.
// - 2026-07-14 pending Added provider translation guards after collapsing the repeated latest-deployment subquery.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RvtPortal.Spa.Application.Monitors;

namespace RvtPortal.Spa.Tests;

/// <summary>
/// The rest of the suite runs on the EF InMemory provider, which evaluates projections client-side and so
/// cannot tell a translatable query from an untranslatable one. ToQueryString() compiles the query against a
/// real relational provider without opening a connection, so a monitor list projection that PostgreSQL could
/// not translate fails here instead of in production.
/// </summary>
public sealed class MonitorListReaderSqlTests
{
    [Fact]
    // Function summary: Verifies the monitor list projection translates to PostgreSQL SQL.
    public void BuildBaseRows_TranslatesOnPostgres()
    {
        var sql = BaseRowsSql();

        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    // Function summary: Verifies the current deployment is resolved once rather than once per projected column.
    public void BuildBaseRows_ResolvesCurrentDeploymentOnce()
    {
        var sql = BaseRowsSql();

        // Every projected deployment column used to repeat this ORDER BY, once per column. Collapsing the
        // subquery should leave at most one ordering over the deployment start date in the generated SQL.
        var orderings = CountOccurrences(sql, "start_date\" DESC");

        Assert.True(
            orderings <= 1,
            $"Expected the current deployment to be resolved once, but the SQL orders by start_date {orderings} times:\n{sql}");
    }

    // Function summary: Compiles the monitor list projection to SQL without executing it.
    private static string BaseRowsSql()
    {
        var builder = new DbContextOptionsBuilder<RVTDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused");

        using var context = new RVTDbContext(builder.Options);
        var reader = new MonitorListReader(context);

        // ToQueryString() throws InvalidOperationException if any part of the projection cannot be translated.
        return reader.BuildBaseRows(null).ToQueryString();
    }

    // Function summary: Counts non-overlapping occurrences of a marker in the generated SQL.
    private static int CountOccurrences(string text, string marker)
    {
        var count = 0;
        var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(marker, index + marker.Length, StringComparison.OrdinalIgnoreCase);
        }

        return count;
    }
}
