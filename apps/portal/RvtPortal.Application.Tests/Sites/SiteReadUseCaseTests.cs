using RvtPortal.Application.Common;
using RvtPortal.Application.Identity;
using RvtPortal.Application.Sites;
using RvtPortal.Application.Sites.Ports;

namespace RvtPortal.Application.Tests.Sites;

public sealed class SiteReadUseCaseTests
{
    private static readonly DateTimeOffset now =
        new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAsync_UsesAssignedScopeAndMasksInvisibleSite()
    {
        Guid userId = Guid.NewGuid();
        Guid siteId = Guid.NewGuid();
        FakeSiteReadPort reads = new() { Exists = false };
        SiteApplicationService service = CreateService(reads);
        PortalUserContext user = new(userId, "user", Guid.NewGuid(), false, false, true);

        UseCaseResult<SiteDetailModel> result = await service.GetAsync(user, siteId, CancellationToken.None);

        Assert.Equal(UseCaseResultKind.NotFound, result.Kind);
        Assert.Equal(SiteAccessScopeKind.Assigned, reads.LastScope?.Kind);
        Assert.Equal(userId, reads.LastScope?.UserId);
        Assert.Equal(now.UtcDateTime, reads.LastScope?.NowUtc);
    }

    [Fact]
    public async Task QueryAsync_ForwardsMaterializedPagingRequest()
    {
        FakeSiteReadPort reads = new()
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
        SiteApplicationService service = CreateService(reads);
        SiteQuery request = new(
            null,
            false,
            new PageRequest(
                null,
                2,
                10,
                "siteName",
                PageSortDirections.Ascending));

        UseCaseResult<PagedResult<SiteListModel>> result = await service.QueryAsync(
            new PortalUserContext(Guid.NewGuid(), "admin", null, true, false, false),
            request,
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Success, result.Kind);
        Assert.Equal(1, result.Value?.Total);
        Assert.Same(request, reads.LastQuery);
    }

    private static SiteApplicationService CreateService(ISiteReadPort reads) =>
        new(
            reads,
            new NoOpSiteWritePort(),
            new InlineUnitOfWork(),
            new EmptyPortalUserDirectory(),
            new NoOpSiteArchivePort(),
            new NoOpSiteLogoPort(),
            new FixedTimeProvider(now));
}
