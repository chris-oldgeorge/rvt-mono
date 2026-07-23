using RvtPortal.Application.Common;
using RvtPortal.Application.Identity;
using RvtPortal.Application.Sites;
using RvtPortal.Application.Sites.Ports;

namespace RvtPortal.Application.Tests.Sites;

public sealed class SiteReadUseCaseTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAsync_UsesAssignedScopeAndMasksInvisibleSite()
    {
        var userId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var reads = new FakeSiteReadPort { Exists = false };
        var service = CreateService(reads);
        var user = new PortalUserContext(userId, "user", Guid.NewGuid(), false, false, true);

        var result = await service.GetAsync(user, siteId, CancellationToken.None);

        Assert.Equal(UseCaseResultKind.NotFound, result.Kind);
        Assert.Equal(SiteAccessScopeKind.Assigned, reads.LastScope?.Kind);
        Assert.Equal(userId, reads.LastScope?.UserId);
        Assert.Equal(Now.UtcDateTime, reads.LastScope?.NowUtc);
    }

    [Fact]
    public async Task QueryAsync_ForwardsMaterializedPagingRequest()
    {
        var reads = new FakeSiteReadPort
        {
            QueryResult = new PagedResult<SiteListModel>
            {
                Results = [new SiteListModel { Id = Guid.NewGuid(), SiteName = "A" }],
                Total = 1,
                Page = 2,
                PageSize = 10,
                Sort = "siteName",
                SortDir = PageSortDirections.Ascending
            }
        };
        var service = CreateService(reads);
        var request = new SiteQuery(
            null,
            false,
            new PageRequest(
                null,
                2,
                10,
                "siteName",
                PageSortDirections.Ascending));

        var result = await service.QueryAsync(
            new PortalUserContext(Guid.NewGuid(), "admin", null, true, false, false),
            request,
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Success, result.Kind);
        Assert.Equal(1, result.Value?.Total);
        Assert.Same(request, reads.LastQuery);
    }

    private static SiteApplicationService CreateService(ISiteReadPort reads) =>
        new(reads, new EmptyPortalUserDirectory(), new FixedTimeProvider(Now));
}
