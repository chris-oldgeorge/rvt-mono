using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Application.Sites;
using RvtPortal.Application.Sites.Ports;
using RvtPortal.Spa.Adapters.Sites;
using RvtPortal.Spa.Application.Common;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Tests;

public sealed class SiteWriteAdapterTests
{
    [Fact]
    public async Task TryClaimArchiveAsync_PersistsUtcArchiveMetadata()
    {
        await using SiteWriteAdapterFixture fixture = await SiteWriteAdapterFixture.CreateAsync();
        Guid siteId = Guid.NewGuid();
        fixture.DomainContext.Sites.Add(new Site
        {
            Id = siteId,
            SiteName = "Archive Site",
            CreateDate = DateTime.UtcNow,
            Contracts = []
        });
        await fixture.DomainContext.SaveChangesAsync();
        DateTime archivedUtc = new DateTime(
            2026,
            7,
            23,
            12,
            0,
            0,
            DateTimeKind.Utc);

        await fixture.UnitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                SiteArchiveClaimResult claim = await fixture.Adapter.TryClaimArchiveAsync(
                    siteId,
                    "admin",
                    "https://archive.example/site.zip",
                    archivedUtc,
                    token);
                Assert.True(claim.Claimed);
                await fixture.UnitOfWork.SaveChangesAsync(token);
                return true;
            },
            CancellationToken.None);

        await using RVTDbContext context = fixture.CreateDomainContext();
        Assert.True(await context.Sites
            .Where(site => site.Id == siteId)
            .Select(site => site.Archived)
            .SingleAsync());
        SiteArchived archive = await context.SiteArchived
            .SingleAsync(entry => entry.SiteId == siteId);
        Assert.Equal("admin", archive.CreatedBy);
        Assert.Equal("https://archive.example/site.zip", archive.PictureLink);
        Assert.Equal(archivedUtc, archive.CreateDate);
    }

    [Fact]
    public async Task CreateAsync_CommitsSiteContractLinkAndSevenOperatingHourRows()
    {
        await using SiteWriteAdapterFixture fixture = await SiteWriteAdapterFixture.CreateAsync();
        Guid companyId = Guid.NewGuid();
        Guid contractId = Guid.NewGuid();
        await fixture.SeedContractAsync(companyId, contractId);
        ValidatedSiteMutation mutation = ValidatedMutation(companyId, contractId);
        DateTime createDateUtc = new DateTime(
            2026,
            7,
            23,
            12,
            0,
            0,
            DateTimeKind.Utc);

        Guid siteId = await fixture.UnitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                Guid createdSiteId = await fixture.Adapter.CreateAsync(
                    mutation,
                    createDateUtc,
                    token);
                await fixture.UnitOfWork.SaveChangesAsync(token);
                Assert.True(await fixture.Adapter.TryClaimContractAsync(
                    contractId,
                    companyId,
                    createdSiteId,
                    token));
                return createdSiteId;
            },
            CancellationToken.None);

        await using RVTDbContext context = fixture.CreateDomainContext();
        Assert.Equal(1, await context.Sites.CountAsync());
        Assert.Equal(siteId, await context.Contracts
            .Where(contract => contract.Id == contractId)
            .Select(contract => contract.SiteiD)
            .SingleAsync());
        Assert.Equal(7, await context.SiteOperatingHours
            .CountAsync(hours => hours.SiteId == siteId));
        Assert.Equal(createDateUtc, await context.Sites
            .Where(site => site.Id == siteId)
            .Select(site => site.CreateDate)
            .SingleAsync());
    }

    [Fact]
    public async Task CreateAsync_WhenOperationThrows_RollsBackSiteAndContractLink()
    {
        await using SiteWriteAdapterFixture fixture = await SiteWriteAdapterFixture.CreateAsync();
        Guid companyId = Guid.NewGuid();
        Guid contractId = Guid.NewGuid();
        await fixture.SeedContractAsync(companyId, contractId);
        ValidatedSiteMutation mutation = ValidatedMutation(companyId, contractId);

        await Assert.ThrowsAsync<ExpectedFailureException>(
            () => fixture.UnitOfWork.ExecuteInTransactionAsync<bool>(
                async token =>
                {
                    Guid siteId = await fixture.Adapter.CreateAsync(
                        mutation,
                        DateTime.UtcNow,
                        token);
                    await fixture.UnitOfWork.SaveChangesAsync(token);
                    Assert.True(await fixture.Adapter.TryClaimContractAsync(
                        contractId,
                        companyId,
                        siteId,
                        token));
                    throw new ExpectedFailureException();
                },
                CancellationToken.None));

        await using RVTDbContext context = fixture.CreateDomainContext();
        Assert.Equal(0, await context.Sites.CountAsync());
        Assert.Null(await context.Contracts
            .Where(contract => contract.Id == contractId)
            .Select(contract => contract.SiteiD)
            .SingleAsync());
        Assert.Equal(0, await context.SiteOperatingHours.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_WhenContractClaimIsStale_RollsBackTheUncommittedSite()
    {
        await using SiteWriteAdapterFixture fixture = await SiteWriteAdapterFixture.CreateAsync();
        Guid companyId = Guid.NewGuid();
        Guid contractId = Guid.NewGuid();
        await fixture.SeedContractAsync(companyId, contractId);
        ValidatedSiteMutation firstMutation = ValidatedMutation(companyId, contractId);
        ValidatedSiteMutation secondMutation = firstMutation with
        {
            Source = firstMutation.Source with
            {
                SiteName = "Stale Claim Site"
            }
        };

        Guid claimedSiteId = await fixture.UnitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                Guid siteId = await fixture.Adapter.CreateAsync(
                    firstMutation,
                    DateTime.UtcNow,
                    token);
                await fixture.UnitOfWork.SaveChangesAsync(token);
                Assert.True(await fixture.Adapter.TryClaimContractAsync(
                    contractId,
                    companyId,
                    siteId,
                    token));
                return siteId;
            },
            CancellationToken.None);

        await Assert.ThrowsAsync<StaleContractClaimException>(
            () => fixture.UnitOfWork.ExecuteInTransactionAsync<bool>(
                async token =>
                {
                    Guid siteId = await fixture.Adapter.CreateAsync(
                        secondMutation,
                        DateTime.UtcNow,
                        token);
                    await fixture.UnitOfWork.SaveChangesAsync(token);
                    if (!await fixture.Adapter.TryClaimContractAsync(
                        contractId,
                        companyId,
                        siteId,
                        token))
                    {
                        throw new StaleContractClaimException();
                    }

                    return true;
                },
                CancellationToken.None));

        await using RVTDbContext context = fixture.CreateDomainContext();
        Assert.Equal(1, await context.Sites.CountAsync());
        Assert.Equal(claimedSiteId, await context.Contracts
            .Where(contract => contract.Id == contractId)
            .Select(contract => contract.SiteiD)
            .SingleAsync());
        Assert.Equal(7, await context.SiteOperatingHours.CountAsync());
    }

    private static ValidatedSiteMutation ValidatedMutation(
        Guid companyId,
        Guid contractId)
    {
        SiteMutation request = new SiteMutation(
            "Adapter Site",
            companyId,
            contractId,
            "Unit 1",
            null,
            "AB1 2CD",
            "London",
            null,
            "08:00",
            "17:00",
            null,
            null,
            null,
            null,
            []);
        SiteMutationValidationResult shape = SiteMutationValidator.ValidateShape(request);
        SiteMutationValidationResult validation = SiteMutationValidator.ValidateBusinessRules(
            shape,
            new SiteMutationValidationData(
                DuplicateSiteName: false,
                CompanyExists: true,
                ContractExists: true,
                ContractIsUnassigned: true,
                ContractBelongsToCompany: true),
            requireContract: true);
        return Assert.IsType<ValidatedSiteMutation>(validation.Value);
    }

    private sealed class ExpectedFailureException : Exception;
    private sealed class StaleContractClaimException : Exception;

    private sealed class SiteWriteAdapterFixture : IAsyncDisposable
    {
        private readonly DbContextOptions<RVTDbContext> domainOptions;
        private readonly SqliteConnection connection;

        private SiteWriteAdapterFixture(
            SqliteConnection connection,
            DbContextOptions<RVTDbContext> domainOptions,
            RVTDbContext domainContext,
            RVTSearchContext searchContext,
            ApplicationDbContext applicationContext)
        {
            this.connection = connection;
            this.domainOptions = domainOptions;
            DomainContext = domainContext;
            SearchContext = searchContext;
            ApplicationContext = applicationContext;
            UnitOfWork = new EfCoreUnitOfWork(
                domainContext,
                searchContext,
                applicationContext);
            Adapter = new EfSiteWriteAdapter(domainContext);
        }

        public RVTDbContext DomainContext { get; }
        public RVTSearchContext SearchContext { get; }
        public ApplicationDbContext ApplicationContext { get; }
        public EfCoreUnitOfWork UnitOfWork { get; }
        public EfSiteWriteAdapter Adapter { get; }

        public static async Task<SiteWriteAdapterFixture> CreateAsync()
        {
            SqliteConnection connection = new SqliteConnection(
                "Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();

            DbContextOptions<RVTDbContext> domainOptions = new DbContextOptionsBuilder<RVTDbContext>()
                .UseSqlite(connection)
                .Options;
            DbContextOptions<RVTSearchContext> searchOptions = new DbContextOptionsBuilder<RVTSearchContext>()
                .UseSqlite(connection)
                .Options;
            DbContextOptions<ApplicationDbContext> applicationOptions =
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlite(connection)
                    .Options;
            await CreateTablesAsync(new RVTDbContext(domainOptions));
            await CreateTablesAsync(new RVTSearchContext(searchOptions));
            await CreateTablesAsync(
                new ApplicationDbContext(applicationOptions));

            return new SiteWriteAdapterFixture(
                connection,
                domainOptions,
                new RVTDbContext(domainOptions),
                new RVTSearchContext(searchOptions),
                new ApplicationDbContext(applicationOptions));
        }

        public RVTDbContext CreateDomainContext() =>
            new(domainOptions);

        public async Task SeedContractAsync(Guid companyId, Guid contractId)
        {
            DomainContext.Companies.Add(new Company
            {
                Id = companyId,
                CompanyName = "Adapter Company"
            });
            DomainContext.Contracts.Add(new Contract
            {
                Id = contractId,
                ContractNumber = "ADAPTER-1",
                CompanyId = companyId,
                OnHireDate = new DateTime(
                    2026,
                    7,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc)
            });
            await DomainContext.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await DomainContext.DisposeAsync();
            await SearchContext.DisposeAsync();
            await ApplicationContext.DisposeAsync();
            await connection.DisposeAsync();
        }

        private static async Task CreateTablesAsync(DbContext context)
        {
            await using (context)
            {
                await context.Database
                    .GetService<IRelationalDatabaseCreator>()
                    .CreateTablesAsync();
            }
        }
    }
}
