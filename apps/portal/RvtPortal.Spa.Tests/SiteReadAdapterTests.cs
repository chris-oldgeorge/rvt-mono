using Microsoft.AspNetCore.Mvc.Testing;
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
    private static readonly DateTimeOffset now =
        new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetArchiveStateAsync_ReturnsMaterializedArchiveState()
    {
        using SpaTestApplicationFactory factory = new();
        Guid siteId = Guid.NewGuid();
        const string archiveUrl = "https://archive.example/canonical.zip";
        await factory.SeedDomainEntitiesAsync(
            new Site
            {
                Id = siteId,
                SiteName = "Archived Site",
                Archived = true,
                CreateDate = now.UtcDateTime,
                Contracts = []
            },
            new SiteArchived
            {
                Id = Guid.NewGuid(),
                SiteId = siteId,
                PictureLink = archiveUrl,
                CreatedBy = "admin",
                CreateDate = now.UtcDateTime
            });
        using IServiceScope scope = factory.Services.CreateScope();
        ISiteReadPort reads = scope.ServiceProvider.GetRequiredService<ISiteReadPort>();

        SiteArchiveState? state = await reads.GetArchiveStateAsync(
            siteId,
            CancellationToken.None);

        Assert.Equal(new SiteArchiveState(siteId, true, archiveUrl), state);
    }

    [Fact]
    public async Task GetArchiveStateAsync_ReturnsNullCanonicalUrlWhenMetadataIsAbsent()
    {
        using SpaTestApplicationFactory factory = new();
        Guid siteId = Guid.NewGuid();
        await factory.SeedDomainEntitiesAsync(new Site
        {
            Id = siteId,
            SiteName = "Archived Without Metadata",
            Archived = true,
            CreateDate = now.UtcDateTime,
            Contracts = []
        });
        using IServiceScope scope = factory.Services.CreateScope();
        ISiteReadPort reads = scope.ServiceProvider.GetRequiredService<ISiteReadPort>();

        SiteArchiveState? state = await reads.GetArchiveStateAsync(
            siteId,
            CancellationToken.None);

        Assert.Equal(new SiteArchiveState(siteId, true, null), state);
    }

    [Fact]
    public async Task AssignedScope_UsesActiveWindowForExistenceAndPagedQuery()
    {
        using SpaTestApplicationFactory factory = new();
        Guid companyId = Guid.NewGuid();
        Guid activeSiteId = Guid.NewGuid();
        Guid expiredSiteId = Guid.NewGuid();
        await factory.SeedUserAsync(
            "site.read.admin@rvt.test",
            null,
            RoleNames.RVTAdmin);
        ApplicationUser companyUser = await factory.SeedUserAsync(
            "site.read.company@rvt.test",
            null,
            RoleNames.CompanyUser,
            companyId: companyId);
        Guid userId = Guid.Parse(companyUser.Id);

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
                CreateDate = now.UtcDateTime.AddDays(-2),
                Contracts = []
            },
            new Site
            {
                Id = expiredSiteId,
                SiteName = "Expired Site",
                CreateDate = now.UtcDateTime.AddDays(-3),
                Contracts = []
            },
            new SiteUsers
            {
                Id = Guid.NewGuid(),
                SiteId = activeSiteId,
                UserId = userId,
                StartDate = now.UtcDateTime,
                EndDate = now.UtcDateTime
            },
            new SiteUsers
            {
                Id = Guid.NewGuid(),
                SiteId = expiredSiteId,
                UserId = userId,
                StartDate = now.UtcDateTime.AddDays(-10),
                EndDate = now.UtcDateTime.AddTicks(-1)
            });

        using WebApplicationFactory<Program> fixedTimeFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            });
        });
        using IServiceScope scope = fixedTimeFactory.Services.CreateScope();
        ISiteReadPort reads = scope.ServiceProvider.GetRequiredService<ISiteReadPort>();
        DateTime nowUtc = scope.ServiceProvider
            .GetRequiredService<TimeProvider>()
            .GetUtcNow()
            .UtcDateTime;
        SiteAccessScope assignedScope = SiteAccessScope.Assigned(userId, nowUtc);
        SiteQuery query = new(
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
