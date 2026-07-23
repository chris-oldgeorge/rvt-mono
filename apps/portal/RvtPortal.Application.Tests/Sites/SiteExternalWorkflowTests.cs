using RvtPortal.Application.Common;
using RvtPortal.Application.Identity;
using RvtPortal.Application.Sites;
using RvtPortal.Application.Sites.Ports;

namespace RvtPortal.Application.Tests.Sites;

public sealed class SiteExternalWorkflowTests
{
    [Fact]
    public async Task ArchiveAsync_UserWhoCannotManage_ReturnsForbiddenBeforeExternalOrDatabaseWork()
    {
        var fixture = SiteExternalFixture.ReadableAdmin();
        var companyUser = new PortalUserContext(
            Guid.NewGuid(),
            "company",
            Guid.NewGuid(),
            false,
            false,
            true);

        var result = await fixture.Service.ArchiveAsync(
            companyUser,
            fixture.SiteId,
            "company",
            CancellationToken.None);

        Assert.Multiple(
            () => Assert.Equal(UseCaseResultKind.Forbidden, result.Kind),
            () => Assert.Equal(0, fixture.Reads.ArchiveStateReadCount),
            () => Assert.Equal(0, fixture.Reads.DetailReadCount),
            () => Assert.Equal(0, fixture.Logos.ExistsReadCount),
            () => Assert.Equal(0, fixture.Archive.ExportCount),
            () => Assert.Equal(0, fixture.UnitOfWork.TransactionCount),
            () => Assert.Equal(0, fixture.Writes.ArchiveCount),
            () => Assert.Equal(0, fixture.UnitOfWork.SaveCount),
            () => Assert.Empty(fixture.Events));
    }

    [Fact]
    public async Task ArchiveAsync_ExportFailureDoesNotOpenDatabaseTransaction()
    {
        var fixture = SiteExternalFixture.ReadableAdmin();
        fixture.Archive.Result = SiteArchiveExportResult.Failed(
            "The site archive could not be created, so the site was not archived. Please try again.");

        var result = await fixture.Service.ArchiveAsync(
            fixture.Admin,
            fixture.SiteId,
            "admin",
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.ExternalServiceUnavailable, result.Kind);
        Assert.Equal(0, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(0, fixture.Writes.ArchiveCount);
    }

    [Fact]
    public async Task ArchiveAsync_SuccessExportsBeforeOpeningDatabaseTransaction()
    {
        var fixture = SiteExternalFixture.ReadableAdmin();

        var result = await fixture.Service.ArchiveAsync(
            fixture.Admin,
            fixture.SiteId,
            "admin",
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Success, result.Kind);
        Assert.Equal(["export", "transaction", "archive"], fixture.Events);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(1, fixture.Writes.ArchiveCount);
        Assert.Equal("admin", fixture.Writes.CreatedBy);
        Assert.Equal("https://archive.example/site.zip", fixture.Writes.ArchiveUrl);
        Assert.Equal(
            new DateTime(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc),
            fixture.Writes.ArchivedUtc);
    }

    [Fact]
    public async Task ArchiveAsync_AlreadyArchivedSkipsExportAndReturnsEnrichedDetail()
    {
        var fixture = SiteExternalFixture.ReadableAdmin();
        fixture.Reads.ArchiveState = new SiteArchiveState(fixture.SiteId, true);
        fixture.Logos.Exists = true;

        var result = await fixture.Service.ArchiveAsync(
            fixture.Admin,
            fixture.SiteId,
            "admin",
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Success, result.Kind);
        Assert.True(result.Value?.HasCustomerLogo);
        Assert.Equal(0, fixture.Archive.ExportCount);
        Assert.Equal(0, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(0, fixture.Writes.ArchiveCount);
    }

    [Fact]
    public async Task SaveLogoAsync_UnauthorizedSiteDoesNotCallStorage()
    {
        var fixture = SiteExternalFixture.InvisibleCompanyUser();
        await using var stream = new MemoryStream([1, 2, 3]);

        var result = await fixture.Service.SaveCustomerLogoAsync(
            fixture.User,
            fixture.SiteId,
            new SiteLogoUpload(stream, 3, "image/png", "logo.png"),
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.NotFound, result.Kind);
        Assert.Equal(0, fixture.Logos.SaveCount);
    }

    [Fact]
    public async Task SaveLogoAsync_InvalidLogoReturnsExistingValidationMessage()
    {
        var fixture = SiteExternalFixture.ReadableAdmin();
        fixture.Logos.SaveResult = new SiteLogoSaveResult(
            SiteLogoSaveOutcome.Invalid,
            "Customer logos must be PNG, JPEG, or WebP images.");
        await using var stream = new MemoryStream([1, 2, 3]);

        var result = await fixture.Service.SaveCustomerLogoAsync(
            fixture.Admin,
            fixture.SiteId,
            new SiteLogoUpload(stream, 3, "image/png", "logo.png"),
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Validation, result.Kind);
        Assert.Contains(
            result.Errors,
            error => error.Message == "Customer logos must be PNG, JPEG, or WebP images.");
        Assert.Equal(1, fixture.Logos.SaveCount);
    }

    [Fact]
    public async Task DeleteLogoAsync_UnauthorizedSiteDoesNotCallStorage()
    {
        var fixture = SiteExternalFixture.InvisibleCompanyUser();

        var result = await fixture.Service.DeleteCustomerLogoAsync(
            fixture.User,
            fixture.SiteId,
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.NotFound, result.Kind);
        Assert.Equal(0, fixture.Logos.DeleteCount);
    }

    [Fact]
    public async Task OpenLogoAsync_UnauthorizedSiteDoesNotCallStorage()
    {
        var fixture = SiteExternalFixture.InvisibleCompanyUser();

        var result = await fixture.Service.OpenCustomerLogoAsync(
            fixture.User,
            fixture.SiteId,
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.NotFound, result.Kind);
        Assert.Equal(0, fixture.Logos.OpenReadCount);
    }

    private sealed record SiteExternalFixture(
        Guid SiteId,
        PortalUserContext User,
        PortalUserContext Admin,
        SiteApplicationService Service,
        ExternalUnitOfWork UnitOfWork,
        ExternalWritePort Writes,
        FakeArchivePort Archive,
        FakeLogoPort Logos,
        ExternalReadPort Reads,
        List<string> Events)
    {
        public static SiteExternalFixture ReadableAdmin()
        {
            var siteId = Guid.NewGuid();
            var events = new List<string>();
            var reads = new ExternalReadPort
            {
                Exists = true,
                ArchiveState = new SiteArchiveState(siteId, false),
                Detail = new SiteDetailModel { Id = siteId, SiteName = "Site" }
            };
            var writes = new ExternalWritePort(events);
            var unitOfWork = new ExternalUnitOfWork(events);
            var archive = new FakeArchivePort(events);
            var logos = new FakeLogoPort();
            var admin = new PortalUserContext(
                Guid.NewGuid(), "admin", null, true, false, false);
            var service = new SiteApplicationService(
                reads,
                writes,
                unitOfWork,
                new EmptyPortalUserDirectory(),
                archive,
                logos,
                new FixedTimeProvider(
                    new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero)));
            return new(
                siteId,
                admin,
                admin,
                service,
                unitOfWork,
                writes,
                archive,
                logos,
                reads,
                events);
        }

        public static SiteExternalFixture InvisibleCompanyUser()
        {
            var fixture = ReadableAdmin();
            var user = new PortalUserContext(
                Guid.NewGuid(), "user", Guid.NewGuid(), false, false, true);
            fixture.Reads.Exists = false;
            return fixture with { User = user };
        }
    }

    private sealed class ExternalUnitOfWork(List<string> events) : IApplicationUnitOfWork
    {
        public int TransactionCount { get; private set; }
        public int SaveCount { get; private set; }

        public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
            Func<CancellationToken, Task<TResponse>> operation,
            CancellationToken cancellationToken)
        {
            TransactionCount++;
            events.Add("transaction");
            return await operation(cancellationToken);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class ExternalWritePort(List<string> events) : ISiteWritePort
    {
        public int ArchiveCount { get; private set; }
        public string? CreatedBy { get; private set; }
        public string? ArchiveUrl { get; private set; }
        public DateTime? ArchivedUtc { get; private set; }

        public Task MarkArchivedAsync(
            Guid siteId,
            string createdBy,
            string archiveUrl,
            DateTime archivedUtc,
            CancellationToken cancellationToken)
        {
            ArchiveCount++;
            CreatedBy = createdBy;
            ArchiveUrl = archiveUrl;
            ArchivedUtc = archivedUtc;
            events.Add("archive");
            return Task.CompletedTask;
        }

        public Task<Guid> CreateAsync(
            ValidatedSiteMutation mutation,
            DateTime createDateUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(Guid.NewGuid());

        public Task<bool> TryClaimContractAsync(
            Guid contractId,
            Guid companyId,
            Guid siteId,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> UpdateAsync(
            Guid siteId,
            ValidatedSiteMutation mutation,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task UpsertNotificationSettingAsync(
            Guid siteUserId,
            SiteNotificationSettingMutation request,
            TimeSpan? startTime,
            TimeSpan? endTime,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeArchivePort(List<string> events) : ISiteArchivePort
    {
        public int ExportCount { get; private set; }
        public SiteArchiveExportResult Result { get; set; } =
            SiteArchiveExportResult.Success("https://archive.example/site.zip");

        public Task<SiteArchiveExportResult> ExportAsync(
            Guid siteId,
            CancellationToken cancellationToken)
        {
            ExportCount++;
            events.Add("export");
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeLogoPort : ISiteLogoPort
    {
        public int ExistsReadCount { get; private set; }
        public int SaveCount { get; private set; }
        public int DeleteCount { get; private set; }
        public int OpenReadCount { get; private set; }
        public bool Exists { get; set; }
        public SiteLogoSaveResult SaveResult { get; set; } =
            new(SiteLogoSaveOutcome.Saved, null);

        public Task<bool> ExistsAsync(
            Guid siteId,
            CancellationToken cancellationToken)
        {
            ExistsReadCount++;
            return Task.FromResult(Exists);
        }

        public Task<SiteLogoSaveResult> SaveAsync(
            Guid siteId,
            SiteLogoUpload upload,
            CancellationToken cancellationToken)
        {
            SaveCount++;
            if (SaveResult.Outcome == SiteLogoSaveOutcome.Saved)
            {
                Exists = true;
            }

            return Task.FromResult(SaveResult);
        }

        public Task DeleteAsync(
            Guid siteId,
            CancellationToken cancellationToken)
        {
            DeleteCount++;
            Exists = false;
            return Task.CompletedTask;
        }

        public Task<SiteLogoFile?> OpenReadAsync(
            Guid siteId,
            CancellationToken cancellationToken)
        {
            OpenReadCount++;
            return Task.FromResult<SiteLogoFile?>(null);
        }
    }

    private sealed class ExternalReadPort : FakeSiteReadPort
    {
        public new bool Exists { get; set; }
        public required SiteArchiveState ArchiveState { get; set; }
        public required SiteDetailModel Detail { get; init; }
        public int ArchiveStateReadCount { get; private set; }
        public int DetailReadCount { get; private set; }

        public override Task<bool> ExistsAsync(
            Guid siteId,
            SiteAccessScope scope,
            CancellationToken cancellationToken) =>
            Task.FromResult(Exists);

        public override Task<SiteArchiveState?> GetArchiveStateAsync(
            Guid siteId,
            CancellationToken cancellationToken)
        {
            ArchiveStateReadCount++;
            return Task.FromResult<SiteArchiveState?>(ArchiveState);
        }

        public override Task<SiteDetailModel?> GetAsync(
            Guid siteId,
            CancellationToken cancellationToken)
        {
            DetailReadCount++;
            return Task.FromResult<SiteDetailModel?>(Detail);
        }
    }
}
