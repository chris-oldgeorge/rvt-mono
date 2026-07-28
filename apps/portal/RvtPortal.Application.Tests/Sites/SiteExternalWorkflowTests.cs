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
        SiteExternalFixture fixture = SiteExternalFixture.ReadableAdmin();
        PortalUserContext companyUser = new(
            Guid.NewGuid(),
            "company",
            Guid.NewGuid(),
            false,
            false,
            true);

        UseCaseResult<SiteDetailModel> result = await fixture.Service.ArchiveAsync(
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
        SiteExternalFixture fixture = SiteExternalFixture.ReadableAdmin();
        fixture.Archive.Result = SiteArchiveExportResult.Failed(
            "The site archive could not be created, so the site was not archived. Please try again.");

        UseCaseResult<SiteDetailModel> result = await fixture.Service.ArchiveAsync(
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
        SiteExternalFixture fixture = SiteExternalFixture.ReadableAdmin();

        UseCaseResult<SiteDetailModel> result = await fixture.Service.ArchiveAsync(
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
        SiteExternalFixture fixture = SiteExternalFixture.ReadableAdmin();
        const string legacyArchiveUrl = "https://archive.example/legacy.zip";
        fixture.Reads.ArchiveState = new SiteArchiveState(
            fixture.SiteId,
            true,
            legacyArchiveUrl);
        fixture.Logos.Exists = true;

        UseCaseResult<SiteDetailModel> result = await fixture.Service.ArchiveAsync(
            fixture.Admin,
            fixture.SiteId,
            "admin",
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Success, result.Kind);
        Assert.True(result.Value?.HasCustomerLogo);
        Assert.Equal(0, fixture.Archive.ExportCount);
        Assert.Equal(0, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(0, fixture.Writes.ArchiveCount);
        Assert.Equal(
            [(fixture.SiteId, legacyArchiveUrl)],
            fixture.Archive.ReconciliationRequests);
    }

    [Fact]
    public async Task ArchiveAsync_LosingClaimCleanupFailureReportsExternalFailure()
    {
        SiteExternalFixture fixture = SiteExternalFixture.ReadableAdmin();
        fixture.Writes.ArchiveClaimResult = new SiteArchiveClaimResult(
            Claimed: false,
            DurableArchiveUrl: "https://archive.example/winner.zip");
        fixture.Archive.CleanupResult = SiteArchiveCleanupResult.Failed(
            "The duplicate archive could not be removed.");

        UseCaseResult<SiteDetailModel> result = await fixture.Service.ArchiveAsync(
            fixture.Admin,
            fixture.SiteId,
            "admin",
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.ExternalServiceUnavailable, result.Kind);
        Assert.Equal(
            "The duplicate archive could not be removed.",
            result.Message);
        Assert.Equal(1, fixture.Archive.CleanupCount);
        Assert.False(fixture.Archive.CleanupToken.CanBeCanceled);
        Assert.Equal(
            [(fixture.SiteId, "https://archive.example/winner.zip")],
            fixture.Archive.ReconciliationRequests);
    }

    [Fact]
    public async Task ArchiveAsync_UnknownCommitWithDurableSameUrlReturnsSuccessWithoutCleanup()
    {
        SiteExternalFixture fixture = SiteExternalFixture.ReadableAdmin();
        using CancellationTokenSource requestCancellation = new();
        fixture.UnitOfWork.TransactionExceptionAfterOperation =
            new IOException("connection dropped during commit");
        fixture.Reads.ArchiveStates.Enqueue(
            new SiteArchiveState(
                fixture.SiteId,
                true,
                fixture.Archive.Result.ArchiveUrl));

        UseCaseResult<SiteDetailModel> result = await fixture.Service.ArchiveAsync(
            fixture.Admin,
            fixture.SiteId,
            "admin",
            requestCancellation.Token);

        Assert.Equal(UseCaseResultKind.Success, result.Kind);
        Assert.Equal(2, fixture.Reads.ArchiveStateReadCount);
        Assert.Equal(0, fixture.Archive.DeleteCount);
        Assert.Empty(fixture.Archive.ReconciliationRequests);
        Assert.False(fixture.Reads.LastArchiveStateToken.CanBeCanceled);
    }

    [Fact]
    public async Task ArchiveAsync_FailedLoserCleanupIsRediscoveredAfterSiteIsArchived()
    {
        SiteExternalFixture fixture = SiteExternalFixture.ReadableAdmin();
        fixture.Writes.ArchiveClaimResult =
            new SiteArchiveClaimResult(false, "https://archive.example/legacy.zip");
        fixture.Archive.CleanupResults.Enqueue(
            SiteArchiveCleanupResult.Failed("cleanup failed"));
        fixture.Archive.CleanupResults.Enqueue(
            SiteArchiveCleanupResult.Success());

        UseCaseResult<SiteDetailModel> first = await fixture.Service.ArchiveAsync(
            fixture.Admin, fixture.SiteId, "admin", CancellationToken.None);
        fixture.Reads.ArchiveState = new SiteArchiveState(
            fixture.SiteId,
            true,
            "https://archive.example/legacy.zip");
        UseCaseResult<SiteDetailModel> retry = await fixture.Service.ArchiveAsync(
            fixture.Admin, fixture.SiteId, "admin", CancellationToken.None);

        Assert.Equal(UseCaseResultKind.ExternalServiceUnavailable, first.Kind);
        Assert.Equal(UseCaseResultKind.Success, retry.Kind);
        Assert.Equal(1, fixture.Archive.ExportCount);
        Assert.Equal(2, fixture.Archive.DeleteCount);
        Assert.Equal(
            [
                (fixture.SiteId, "https://archive.example/legacy.zip"),
                (fixture.SiteId, "https://archive.example/legacy.zip")
            ],
            fixture.Archive.ReconciliationRequests);
    }

    [Fact]
    public async Task ArchiveAsync_UnknownCancellationWithoutCanonicalMetadataRetainsCandidateAndRethrowsOriginal()
    {
        SiteExternalFixture fixture = SiteExternalFixture.ReadableAdmin();
        OperationCanceledException persistenceException = new("commit cancelled");
        fixture.UnitOfWork.TransactionException = persistenceException;

        OperationCanceledException actual = await Assert.ThrowsAsync<OperationCanceledException>(
            () => fixture.Service.ArchiveAsync(
                fixture.Admin,
                fixture.SiteId,
                "admin",
                CancellationToken.None));

        Assert.Same(persistenceException, actual);
        Assert.Equal(2, fixture.Reads.ArchiveStateReadCount);
        Assert.Equal(0, fixture.Archive.CleanupCount);
        Assert.False(fixture.Reads.LastArchiveStateToken.CanBeCanceled);
    }

    [Fact]
    public async Task ArchiveAsync_UnknownCancellationVerificationFailureRetainsCandidateAndRethrowsOriginal()
    {
        SiteExternalFixture fixture = SiteExternalFixture.ReadableAdmin();
        OperationCanceledException persistenceException = new("metadata cancelled");
        fixture.UnitOfWork.TransactionException = persistenceException;
        fixture.Reads.ArchiveStateExceptionAfterFirstRead =
            new IOException("verification unavailable");

        OperationCanceledException actual = await Assert.ThrowsAsync<OperationCanceledException>(
            () => fixture.Service.ArchiveAsync(
                fixture.Admin,
                fixture.SiteId,
                "admin",
                CancellationToken.None));

        Assert.Same(persistenceException, actual);
        Assert.Equal(2, fixture.Reads.ArchiveStateReadCount);
        Assert.Equal(0, fixture.Archive.CleanupCount);
    }

    [Fact]
    public async Task ArchiveAsync_UnknownCancellationWithMatchingCanonicalUrlReturnsDurableSuccess()
    {
        SiteExternalFixture fixture = SiteExternalFixture.ReadableAdmin();
        fixture.UnitOfWork.TransactionExceptionAfterOperation =
            new OperationCanceledException("commit acknowledgement cancelled");
        fixture.Reads.ArchiveStates.Enqueue(
            new SiteArchiveState(
                fixture.SiteId,
                true,
                fixture.Archive.Result.ArchiveUrl));

        UseCaseResult<SiteDetailModel> result = await fixture.Service.ArchiveAsync(
            fixture.Admin,
            fixture.SiteId,
            "admin",
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Success, result.Kind);
        Assert.Equal(0, fixture.Archive.CleanupCount);
        Assert.False(fixture.Reads.LastArchiveStateToken.CanBeCanceled);
    }

    [Fact]
    public async Task ArchiveAsync_UnknownCommitWithDifferentCanonicalUrlReconcilesLegacyWinnerAndReturnsSuccess()
    {
        SiteExternalFixture fixture = SiteExternalFixture.ReadableAdmin();
        const string legacyArchiveUrl = "https://archive.example/legacy.zip";
        fixture.UnitOfWork.TransactionExceptionAfterOperation =
            new IOException("connection dropped during commit");
        fixture.Reads.ArchiveStates.Enqueue(
            new SiteArchiveState(fixture.SiteId, true, legacyArchiveUrl));

        UseCaseResult<SiteDetailModel> result = await fixture.Service.ArchiveAsync(
            fixture.Admin,
            fixture.SiteId,
            "admin",
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Success, result.Kind);
        Assert.Equal(
            [(fixture.SiteId, legacyArchiveUrl)],
            fixture.Archive.ReconciliationRequests);
    }

    [Fact]
    public async Task ArchiveAsync_UnknownCommitWithDifferentCanonicalUrlReportsReconciliationFailure()
    {
        SiteExternalFixture fixture = SiteExternalFixture.ReadableAdmin();
        const string legacyArchiveUrl = "https://archive.example/legacy.zip";
        fixture.UnitOfWork.TransactionExceptionAfterOperation =
            new IOException("connection dropped during commit");
        fixture.Reads.ArchiveStates.Enqueue(
            new SiteArchiveState(fixture.SiteId, true, legacyArchiveUrl));
        fixture.Archive.CleanupResult =
            SiteArchiveCleanupResult.Failed("reconciliation failed");

        UseCaseResult<SiteDetailModel> result = await fixture.Service.ArchiveAsync(
            fixture.Admin,
            fixture.SiteId,
            "admin",
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.ExternalServiceUnavailable, result.Kind);
        Assert.Equal("reconciliation failed", result.Message);
        Assert.Equal(
            [(fixture.SiteId, legacyArchiveUrl)],
            fixture.Archive.ReconciliationRequests);
    }

    [Fact]
    public async Task SaveLogoAsync_UnauthorizedSiteDoesNotCallStorage()
    {
        SiteExternalFixture fixture = SiteExternalFixture.InvisibleCompanyUser();
        await using MemoryStream stream = new([1, 2, 3]);

        UseCaseResult<SiteDetailModel> result = await fixture.Service.SaveCustomerLogoAsync(
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
        SiteExternalFixture fixture = SiteExternalFixture.ReadableAdmin();
        fixture.Logos.SaveResult = new SiteLogoSaveResult(
            SiteLogoSaveOutcome.Invalid,
            "Customer logos must be PNG, JPEG, or WebP images.");
        await using MemoryStream stream = new([1, 2, 3]);

        UseCaseResult<SiteDetailModel> result = await fixture.Service.SaveCustomerLogoAsync(
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
        SiteExternalFixture fixture = SiteExternalFixture.InvisibleCompanyUser();

        UseCaseResult<SiteDetailModel> result = await fixture.Service.DeleteCustomerLogoAsync(
            fixture.User,
            fixture.SiteId,
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.NotFound, result.Kind);
        Assert.Equal(0, fixture.Logos.DeleteCount);
    }

    [Fact]
    public async Task OpenLogoAsync_UnauthorizedSiteDoesNotCallStorage()
    {
        SiteExternalFixture fixture = SiteExternalFixture.InvisibleCompanyUser();

        UseCaseResult<SiteLogoFile> result = await fixture.Service.OpenCustomerLogoAsync(
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
            Guid siteId = Guid.NewGuid();
            List<string> events = new();
            ExternalReadPort reads = new()
            {
                Exists = true,
                ArchiveState = new SiteArchiveState(siteId, false, null),
                Detail = new SiteDetailModel { Id = siteId, SiteName = "Site" }
            };
            ExternalWritePort writes = new(events);
            ExternalUnitOfWork unitOfWork = new(events);
            FakeArchivePort archive = new(events);
            FakeLogoPort logos = new();
            PortalUserContext admin = new(
                Guid.NewGuid(), "admin", null, true, false, false);
            SiteApplicationService service = new(
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
            SiteExternalFixture fixture = ReadableAdmin();
            PortalUserContext user = new(
                Guid.NewGuid(), "user", Guid.NewGuid(), false, false, true);
            fixture.Reads.Exists = false;
            return fixture with { User = user };
        }
    }

    private sealed class ExternalUnitOfWork(List<string> events) : IApplicationUnitOfWork
    {
        public int TransactionCount { get; private set; }
        public int SaveCount { get; private set; }
        public Exception? TransactionException { get; set; }
        public Exception? TransactionExceptionAfterOperation { get; set; }

        public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
            Func<CancellationToken, Task<TResponse>> operation,
            CancellationToken cancellationToken)
        {
            TransactionCount++;
            events.Add("transaction");
            if (TransactionException is not null)
            {
                throw TransactionException;
            }

            TResponse? response = await operation(cancellationToken);
            if (TransactionExceptionAfterOperation is not null)
            {
                throw TransactionExceptionAfterOperation;
            }

            return response;
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
        public SiteArchiveClaimResult ArchiveClaimResult { get; set; } =
            new(true, "https://archive.example/site.zip");

        public Task<SiteArchiveClaimResult> TryClaimArchiveAsync(
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
            return Task.FromResult(ArchiveClaimResult);
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
        public int DeleteCount { get; private set; }
        public int CleanupCount => DeleteCount;
        public CancellationToken CleanupToken { get; private set; }
        public SiteArchiveExportResult Result { get; set; } =
            SiteArchiveExportResult.Success("https://archive.example/site.zip");
        public SiteArchiveCleanupResult CleanupResult { get; set; } =
            SiteArchiveCleanupResult.Success();
        public Queue<SiteArchiveCleanupResult> CleanupResults { get; } = new();
        public List<(Guid SiteId, string DurableArchiveUrl)> ReconciliationRequests { get; } = [];

        public Task<SiteArchiveExportResult> ExportAsync(
            Guid siteId,
            CancellationToken cancellationToken)
        {
            ExportCount++;
            events.Add("export");
            return Task.FromResult(Result);
        }

        public Task<SiteArchiveCleanupResult> CleanupSupersededAsync(
            Guid siteId,
            string durableArchiveUrl,
            CancellationToken cancellationToken)
        {
            DeleteCount++;
            CleanupToken = cancellationToken;
            ReconciliationRequests.Add((siteId, durableArchiveUrl));
            return Task.FromResult(
                CleanupResults.TryDequeue(out SiteArchiveCleanupResult? result)
                    ? result
                    : CleanupResult);
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
        public Queue<SiteArchiveState> ArchiveStates { get; } = new();
        public CancellationToken LastArchiveStateToken { get; private set; }
        public Exception? ArchiveStateExceptionAfterFirstRead { get; set; }

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
            LastArchiveStateToken = cancellationToken;
            if (ArchiveStateReadCount > 1
                && ArchiveStateExceptionAfterFirstRead is not null)
            {
                throw ArchiveStateExceptionAfterFirstRead;
            }

            return Task.FromResult<SiteArchiveState?>(
                ArchiveStateReadCount > 1
                    && ArchiveStates.TryDequeue(out SiteArchiveState? state)
                    ? state
                    : ArchiveState);
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
