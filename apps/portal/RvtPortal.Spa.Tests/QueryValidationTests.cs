// File summary: Verifies unknown filter/sort fields fail loudly instead of silently widening the result set.
// Major updates:
// - 2026-07-14 pending Added coverage for filter/sort validation in the shared query builders.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess;
using RVT.DataAccess.Context;
using RVT.Entities;
using RVT.Entities.Querying;

namespace RvtPortal.Spa.Tests;

public sealed class QueryValidationTests
{
    private static readonly OrderByProperty[] ByName =
    [
        new() { OrderByDirection = OrderByDirectionEnum.Ascending, OrderByColumn = "CompanyName" }
    ];

    [Fact]
    // Function summary: Verifies a filter naming a property the entity does not have is rejected.
    public async Task ReadFilteredAsync_UnknownFilterField_Throws()
    {
        await using var context = CreateContext();
        var repository = new CompanyRepository(context);

        var error = await Assert.ThrowsAsync<QueryValidationException>(() => repository.ReadFilteredAsync(
            [new SingleFilter { Operation = Op.Equals, PropertyName = "NotAField", Value = "x" }],
            ByName,
            maximumRecords: 10,
            new Paging { paged = false },
            CancellationToken.None));

        Assert.Contains("NotAField", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies a misspelled filter no longer degrades into a match-everything query.
    public async Task ReadFilteredAsync_UnknownFilterField_DoesNotReturnEveryRow()
    {
        await using var context = CreateContext();
        context.Companies.Add(new Company { Id = Guid.NewGuid(), CompanyName = "Alpha" });
        context.Companies.Add(new Company { Id = Guid.NewGuid(), CompanyName = "Beta" });
        await context.SaveChangesAsync();
        var repository = new CompanyRepository(context);

        // The whole point: an entirely invalid filter used to build "WHERE true" and hand back the table.
        await Assert.ThrowsAsync<QueryValidationException>(() => repository.ReadFilteredAsync(
            [new SingleFilter { Operation = Op.Equals, PropertyName = "Nonsense", Value = "x" }],
            ByName,
            maximumRecords: 10,
            new Paging { paged = false },
            CancellationToken.None));
    }

    [Fact]
    // Function summary: Verifies a sort naming a property the entity does not have is rejected.
    public async Task ReadFilteredAsync_UnknownSortField_Throws()
    {
        await using var context = CreateContext();
        var repository = new CompanyRepository(context);

        var error = await Assert.ThrowsAsync<QueryValidationException>(() => repository.ReadFilteredAsync(
            [],
            [new OrderByProperty { OrderByColumn = "NotASortField" }],
            maximumRecords: 10,
            new Paging { paged = false },
            CancellationToken.None));

        Assert.Contains("NotASortField", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies supplying no filters still legitimately matches every row.
    public async Task ReadFilteredAsync_NoFilters_StillMatchesEveryRow()
    {
        await using var context = CreateContext();
        context.Companies.Add(new Company { Id = Guid.NewGuid(), CompanyName = "Alpha" });
        context.Companies.Add(new Company { Id = Guid.NewGuid(), CompanyName = "Beta" });
        await context.SaveChangesAsync();
        var repository = new CompanyRepository(context);

        var result = await repository.ReadFilteredAsync(
            [],
            ByName,
            maximumRecords: 10,
            new Paging { paged = false },
            CancellationToken.None);

        Assert.Equal(2, result.RecordCount);
    }

    // Function summary: Creates an isolated in-memory domain context for query-builder tests.
    private static RVTDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RVTDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new RVTDbContext(options);
    }
}
