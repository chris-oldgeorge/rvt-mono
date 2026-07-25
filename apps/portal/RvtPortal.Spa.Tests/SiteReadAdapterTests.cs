using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RVT.Entities;
using RvtPortal.Application.Common;
using RvtPortal.Application.Sites;
using RvtPortal.Application.Sites.Ports;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Tests;

public sealed class SiteReadAdapterTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetArchiveStateAsync_ReturnsMaterializedArchiveState()
    {
        using var factory = new SpaTestApplicationFactory();
        var siteId = Guid.NewGuid();
        const string archiveUrl = "https://archive.example/canonical.zip";
        await factory.SeedDomainEntitiesAsync(
            new Site
            {
                Id = siteId,
                SiteName = "Archived Site",
                Archived = true,
                CreateDate = Now.UtcDateTime,
                Contracts = []
            },
            new SiteArchived
            {
                Id = Guid.NewGuid(),
                SiteId = siteId,
                PictureLink = archiveUrl,
                CreatedBy = "admin",
                CreateDate = Now.UtcDateTime
            });
        using var scope = factory.Services.CreateScope();
        var reads = scope.ServiceProvider.GetRequiredService<ISiteReadPort>();

        var state = await reads.GetArchiveStateAsync(
            siteId,
            CancellationToken.None);

        Assert.Equal(new SiteArchiveState(siteId, true, archiveUrl), state);
    }

    [Fact]
    public async Task GetArchiveStateAsync_ReturnsNullCanonicalUrlWhenMetadataIsAbsent()
    {
        using var factory = new SpaTestApplicationFactory();
        var siteId = Guid.NewGuid();
        await factory.SeedDomainEntitiesAsync(new Site
        {
            Id = siteId,
            SiteName = "Archived Without Metadata",
            Archived = true,
            CreateDate = Now.UtcDateTime,
            Contracts = []
        });
        using var scope = factory.Services.CreateScope();
        var reads = scope.ServiceProvider.GetRequiredService<ISiteReadPort>();

        var state = await reads.GetArchiveStateAsync(
            siteId,
            CancellationToken.None);

        Assert.Equal(new SiteArchiveState(siteId, true, null), state);
    }

    [Fact]
    public async Task AssignedScope_UsesActiveWindowForExistenceAndPagedQuery()
    {
        using var factory = new SpaTestApplicationFactory();
        var companyId = Guid.NewGuid();
        var activeSiteId = Guid.NewGuid();
        var expiredSiteId = Guid.NewGuid();
        await factory.SeedUserAsync(
            "site.read.admin@rvt.test",
            null,
            RoleNames.RVTAdmin);
        var companyUser = await factory.SeedUserAsync(
            "site.read.company@rvt.test",
            null,
            RoleNames.CompanyUser,
            companyId: companyId);
        var userId = Guid.Parse(companyUser.Id);

        await factory.SeedDomainEntitiesAsync(
            new Company
            {
                Id = companyId,
                CompanyName = "Site Read Company",
                Contracts = []
            },
            new Site
            {
                Id = activeSiteId,
                SiteName = "Active Site",
                CreateDate = Now.UtcDateTime.AddDays(-2),
                Contracts = []
            },
            new Site
            {
                Id = expiredSiteId,
                SiteName = "Expired Site",
                CreateDate = Now.UtcDateTime.AddDays(-3),
                Contracts = []
            },
            new SiteUsers
            {
                Id = Guid.NewGuid(),
                SiteId = activeSiteId,
                UserId = userId,
                StartDate = Now.UtcDateTime,
                EndDate = Now.UtcDateTime
            },
            new SiteUsers
            {
                Id = Guid.NewGuid(),
                SiteId = expiredSiteId,
                UserId = userId,
                StartDate = Now.UtcDateTime.AddDays(-10),
                EndDate = Now.UtcDateTime.AddTicks(-1)
            });

        using var fixedTimeFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
            });
        });
        using var scope = fixedTimeFactory.Services.CreateScope();
        var reads = scope.ServiceProvider.GetRequiredService<ISiteReadPort>();
        var nowUtc = scope.ServiceProvider
            .GetRequiredService<TimeProvider>()
            .GetUtcNow()
            .UtcDateTime;
        var assignedScope = SiteAccessScope.Assigned(userId, nowUtc);
        var query = new SiteQuery(
            null,
            true,
            new PageRequest(
                null,
                1,
                10,
                "createDate",
                PageSortDirections.Ascending));

        Assert.True(await reads.ExistsAsync(
            activeSiteId,
            assignedScope,
            CancellationToken.None));
        Assert.False(await reads.ExistsAsync(
            expiredSiteId,
            assignedScope,
            CancellationToken.None));
        Assert.Equal(activeSiteId, Assert.Single(
            (await reads.QueryAsync(
                assignedScope,
                query,
                CancellationToken.None)).Results).Id);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
