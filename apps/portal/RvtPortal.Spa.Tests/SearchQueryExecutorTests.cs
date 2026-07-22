// File summary: Verifies the shared filtered/ordered/paged read path used by every search repository.
// Major updates:
// - 2026-07-14 pending Added coverage for unpaged record counts and the truncation flag after the async rewrite.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess;
using RVT.DataAccess.Context;
using RVT.Entities;
using RVT.Entities.Querying;

namespace RvtPortal.Spa.Tests;

public sealed class SearchQueryExecutorTests
{
    private static readonly OrderByProperty[] ByName =
    [
        new() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = "CompanyName" }
    ];

    [Fact]
    // Function summary: Verifies an unpaged read reports how many rows it actually returned.
    public async Task ReadFilteredAsync_Unpaged_ReportsReturnedRowCount()
    {
        const int seededCompanies = 3;
        await using var context = CreateContext();
        SeedCompanies(context, seededCompanies);
        await context.SaveChangesAsync();
        var repository = new CompanyRepository(context);

        var result = await repository.ReadFilteredAsync(
            [],
            ByName,
            maximumRecords: 10,
            new Paging { paged = false },
            CancellationToken.None);

        // RecordCount used to be hard-coded to 0 on the unpaged path, which silently broke every caller
        // that gated on "RecordCount > 0" (the GetLatest* readings).
        Assert.Equal(seededCompanies, result.RecordCount);
        Assert.Equal(seededCompanies, result.Value.Count);
        Assert.False(result.HasMore);
    }

    [Fact]
    // Function summary: Verifies an unpaged read that hits its bound reports the result as truncated.
    public async Task ReadFilteredAsync_Unpaged_FlagsTruncationWhenBoundIsHit()
    {
        const int maximumRecords = 2;
        await using var context = CreateContext();
        SeedCompanies(context, 5);
        await context.SaveChangesAsync();
        var repository = new CompanyRepository(context);

        var result = await repository.ReadFilteredAsync(
            [],
            ByName,
            maximumRecords,
            new Paging { paged = false },
            CancellationToken.None);

        Assert.Equal(maximumRecords, result.Value.Count);
        Assert.True(result.HasMore);
    }

    [Fact]
    // Function summary: Verifies a paged read returns one page while reporting the full matching total.
    public async Task ReadFilteredAsync_Paged_ReturnsPageWithFullTotal()
    {
        const int seededCompanies = 5;
        const int pageSize = 2;
        await using var context = CreateContext();
        SeedCompanies(context, seededCompanies);
        await context.SaveChangesAsync();
        var repository = new CompanyRepository(context);

        var result = await repository.ReadFilteredAsync(
            [],
            ByName,
            maximumRecords: 0,
            new Paging { paged = true, page = 2, pageSize = pageSize },
            CancellationToken.None);

        Assert.Equal(seededCompanies, result.RecordCount);
        Assert.Equal(pageSize, result.Value.Count);
        Assert.Equal("Company 3", result.Value[0].CompanyName);
        Assert.False(result.HasMore);
    }

    [Fact]
    // Function summary: Verifies a filter is applied in the query rather than after materialization.
    public async Task ReadFilteredAsync_AppliesFilter()
    {
        await using var context = CreateContext();
        SeedCompanies(context, 4);
        await context.SaveChangesAsync();
        var repository = new CompanyRepository(context);

        var result = await repository.ReadFilteredAsync(
            [new SingleFilter { Operation = Op.Equals, PropertyName = "CompanyName", Value = "Company 2" }],
            ByName,
            maximumRecords: 10,
            new Paging { paged = false },
            CancellationToken.None);

        Assert.Equal("Company 2", Assert.Single(result.Value).CompanyName);
        Assert.Equal(1, result.RecordCount);
    }

    // Function summary: Seeds sequentially named companies so ordering assertions are deterministic.
    private static void SeedCompanies(RVTDbContext context, int count)
    {
        for (var index = 1; index <= count; index++)
        {
            context.Companies.Add(new Company { Id = Guid.NewGuid(), CompanyName = $"Company {index}" });
        }
    }

    // Function summary: Creates an isolated in-memory domain context for read-path tests.
    private static RVTDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RVTDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new RVTDbContext(options);
    }
}
