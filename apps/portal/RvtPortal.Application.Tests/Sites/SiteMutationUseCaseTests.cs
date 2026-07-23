using RvtPortal.Application.Common;
using RvtPortal.Application.Identity;
using RvtPortal.Application.Sites;
using RvtPortal.Application.Sites.Ports;

namespace RvtPortal.Application.Tests.Sites;

public sealed class SiteMutationUseCaseTests
{
    [Fact]
    public async Task CreateAsync_StagesSiteAndSavesOnceInsideTransaction()
    {
        var fixture = SiteMutationFixture.Valid();

        var result = await fixture.Service.CreateAsync(
            fixture.Admin,
            fixture.Mutation,
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Success, result.Kind);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(1, fixture.UnitOfWork.SaveCount);
        Assert.Equal(1, fixture.Writes.CreateCount);
        Assert.Equal(1, fixture.Writes.ClaimContractCount);
        Assert.Equal(fixture.Now.UtcDateTime, fixture.Writes.CreateDateUtc);
    }

    [Fact]
    public async Task CreateAsync_InvalidTimePair_DoesNotOpenTransaction()
    {
        var fixture = SiteMutationFixture.Valid() with
        {
            Mutation = SiteMutationFixture.ValidMutation() with
            {
                StartTime = "18:00",
                EndTime = "08:00"
            }
        };

        var result = await fixture.Service.CreateAsync(
            fixture.Admin,
            fixture.Mutation,
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Validation, result.Kind);
        Assert.Equal(0, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_UserWhoCannotManage_ReturnsForbiddenBeforeBusinessReads()
    {
        var fixture = SiteMutationFixture.Valid();
        var companyUser = new PortalUserContext(
            Guid.NewGuid(),
            "company",
            fixture.Mutation.CompanyId,
            false,
            false,
            true);

        var result = await fixture.Service.CreateAsync(
            companyUser,
            fixture.Mutation,
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Forbidden, result.Kind);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(0, fixture.Reads.MutationValidationReadCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Equal(0, fixture.Writes.CreateCount);
        Assert.Equal(0, fixture.Writes.ClaimContractCount);
    }

    [Theory]
    [InlineData(false, true, true, true, "The Contract is Required")]
    [InlineData(true, false, true, true, "The Contract is already assigned to a site.")]
    [InlineData(true, true, false, true, "The Contract must belong to the selected company.")]
    public async Task CreateAsync_InvalidContract_ReturnsValidationWithoutWriting(
        bool contractExists,
        bool contractIsUnassigned,
        bool contractBelongsToCompany,
        bool companyExists,
        string expectedMessage)
    {
        var fixture = SiteMutationFixture.Valid();
        fixture.Reads.MutationData = fixture.Reads.MutationData with
        {
            ContractExists = contractExists,
            ContractIsUnassigned = contractIsUnassigned,
            ContractBelongsToCompany = contractBelongsToCompany,
            CompanyExists = companyExists
        };

        var result = await fixture.Service.CreateAsync(
            fixture.Admin,
            fixture.Mutation,
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Validation, result.Kind);
        Assert.Contains(result.Errors, error => error.Message == expectedMessage);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Equal(0, fixture.Writes.CreateCount);
    }

    [Fact]
    public async Task CreateAsync_DuplicateSiteName_ReturnsValidationWithoutWriting()
    {
        var fixture = SiteMutationFixture.Valid();
        fixture.Reads.MutationData = fixture.Reads.MutationData with
        {
            DuplicateSiteName = true
        };

        var result = await fixture.Service.CreateAsync(
            fixture.Admin,
            fixture.Mutation,
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Validation, result.Kind);
        Assert.Contains(result.Errors, error =>
            error.Field == nameof(SiteMutation.SiteName) &&
            error.Message == "The Site Name is already registered");
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Equal(0, fixture.Writes.CreateCount);
    }

    [Fact]
    public async Task CreateAsync_MissingCompany_ReturnsValidationWithoutWriting()
    {
        var fixture = SiteMutationFixture.Valid();
        fixture.Reads.MutationData = fixture.Reads.MutationData with
        {
            CompanyExists = false
        };

        var result = await fixture.Service.CreateAsync(
            fixture.Admin,
            fixture.Mutation,
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Validation, result.Kind);
        Assert.Contains(result.Errors, error =>
            error.Field == nameof(SiteMutation.CompanyId) &&
            error.Message == "The Company is required");
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Equal(0, fixture.Writes.CreateCount);
    }

    [Fact]
    public async Task CreateAsync_StaleContractClaim_ReturnsAssignedValidation()
    {
        var fixture = SiteMutationFixture.Valid();
        fixture.Writes.ClaimContractResult = false;

        var result = await fixture.Service.CreateAsync(
            fixture.Admin,
            fixture.Mutation,
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Validation, result.Kind);
        Assert.Contains(result.Errors, error =>
            error.Field == nameof(SiteMutation.ContractId) &&
            error.Message == "The Contract is already assigned to a site.");
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(1, fixture.UnitOfWork.SaveCount);
        Assert.Equal(1, fixture.Writes.CreateCount);
        Assert.Equal(1, fixture.Writes.ClaimContractCount);
    }

    [Fact]
    public async Task UpdateAsync_UserWhoCannotManage_ReturnsForbiddenBeforeBusinessReads()
    {
        var fixture = SiteMutationFixture.Valid();
        var companyUser = new PortalUserContext(
            Guid.NewGuid(),
            "company",
            fixture.Mutation.CompanyId,
            false,
            false,
            true);

        var result = await fixture.Service.UpdateAsync(
            companyUser,
            fixture.Reads.Detail.Id,
            fixture.Mutation with { ContractId = null },
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Forbidden, result.Kind);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(0, fixture.Reads.MutationValidationReadCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Equal(0, fixture.Writes.UpdateCount);
    }

    [Fact]
    public async Task UpdateAsync_MissingSite_TakesPrecedenceOverInvalidMutationFacts()
    {
        var fixture = SiteMutationFixture.Valid();
        fixture.Reads.Exists = false;
        fixture.Reads.MutationData = fixture.Reads.MutationData with
        {
            DuplicateSiteName = true,
            CompanyExists = false
        };

        var result = await fixture.Service.UpdateAsync(
            fixture.Admin,
            fixture.Reads.Detail.Id,
            fixture.Mutation with { ContractId = null },
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.NotFound, result.Kind);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(1, fixture.Reads.ExistsCallCount);
        Assert.Equal(SiteAccessScopeKind.All, fixture.Reads.LastScope?.Kind);
        Assert.Equal(fixture.Now.UtcDateTime, fixture.Reads.LastScope?.NowUtc);
        Assert.True(fixture.Reads.ExistsReadInsideTransaction);
        Assert.Equal(0, fixture.Reads.MutationValidationReadCount);
        Assert.Equal(0, fixture.Writes.UpdateCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_MissingSite_ReturnsNotFoundWithoutSaving()
    {
        var fixture = SiteMutationFixture.Valid();
        fixture.Writes.UpdateResult = false;

        var result = await fixture.Service.UpdateAsync(
            fixture.Admin,
            fixture.Reads.Detail.Id,
            fixture.Mutation with { ContractId = null },
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.NotFound, result.Kind);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Equal(1, fixture.Writes.UpdateCount);
    }

    [Fact]
    public async Task UpdateNotificationSettingAsync_CompanyUserCannotUpdateAnotherUsersSetting()
    {
        var fixture = SiteMutationFixture.Valid();
        var companyUser = new PortalUserContext(
            Guid.NewGuid(),
            "company",
            Guid.NewGuid(),
            false,
            false,
            true);
        fixture.Reads.NotificationTarget = new SiteNotificationSettingTarget(
            fixture.SiteUserId,
            fixture.Reads.Detail.Id,
            Guid.NewGuid());

        var result = await fixture.Service.UpdateNotificationSettingAsync(
            companyUser,
            fixture.Reads.Detail.Id,
            fixture.SiteUserId,
            new SiteNotificationSettingMutation(true, false, "08:00", "17:00"),
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Forbidden, result.Kind);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Equal(0, fixture.Writes.NotificationSettingCount);
    }

    [Fact]
    public async Task UpdateNotificationSettingAsync_CompanyUserCanUpdateOwnSetting()
    {
        var fixture = SiteMutationFixture.Valid();
        var userId = Guid.NewGuid();
        var companyUser = new PortalUserContext(
            userId,
            "company",
            Guid.NewGuid(),
            false,
            false,
            true);
        fixture.Reads.NotificationTarget = new SiteNotificationSettingTarget(
            fixture.SiteUserId,
            fixture.Reads.Detail.Id,
            userId);
        fixture.Reads.NotificationData = new SiteNotificationSettingsData(
            fixture.Reads.Detail.Id,
            fixture.Reads.Detail.SiteName,
            [
                new SiteNotificationAssignment(
                    fixture.SiteUserId,
                    fixture.Reads.Detail.Id,
                    userId,
                    true,
                    true,
                    false,
                    "08:00",
                    "17:00")
            ]);
        fixture.Users.Profile = new PortalUserProfile(
            userId,
            userId.ToString(),
            companyUser.CompanyId,
            false,
            "Company User",
            "company@example.test",
            null,
            null,
            true,
            [PortalRoleNames.CompanyUser]);

        var result = await fixture.Service.UpdateNotificationSettingAsync(
            companyUser,
            fixture.Reads.Detail.Id,
            fixture.SiteUserId,
            new SiteNotificationSettingMutation(true, false, "08:00", "17:00"),
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Success, result.Kind);
        Assert.Equal(fixture.SiteUserId, result.Value?.SiteUserId);
        Assert.Equal("company@example.test", result.Value?.UserEmail);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(1, fixture.UnitOfWork.SaveCount);
        Assert.Equal(1, fixture.Writes.NotificationSettingCount);
    }

    [Fact]
    public async Task UpdateNotificationSettingAsync_MissingAssignment_ReturnsNotFoundWithoutSaving()
    {
        var fixture = SiteMutationFixture.Valid();
        fixture.Reads.NotificationTarget = null;

        var result = await fixture.Service.UpdateNotificationSettingAsync(
            fixture.Admin,
            fixture.Reads.Detail.Id,
            fixture.SiteUserId,
            new SiteNotificationSettingMutation(true, false, null, null),
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.NotFound, result.Kind);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Equal(0, fixture.Writes.NotificationSettingCount);
    }

    [Theory]
    [InlineData(-2, -1)]
    [InlineData(1, 2)]
    public async Task UpdateNotificationSettingAsync_ExpiredOrFutureSelfAssignment_IsMaskedAsSiteNotFound(
        int startOffsetDays,
        int endOffsetDays)
    {
        var fixture = SiteMutationFixture.Valid();
        var userId = Guid.NewGuid();
        var companyUser = new PortalUserContext(
            userId,
            "company",
            fixture.Mutation.CompanyId,
            false,
            false,
            true);
        fixture.Reads.AssignmentWindow = new SiteAssignmentWindow(
            userId,
            fixture.Now.UtcDateTime.AddDays(startOffsetDays),
            fixture.Now.UtcDateTime.AddDays(endOffsetDays));
        fixture.Reads.NotificationTarget = new SiteNotificationSettingTarget(
            fixture.SiteUserId,
            fixture.Reads.Detail.Id,
            userId);

        var result = await fixture.Service.UpdateNotificationSettingAsync(
            companyUser,
            fixture.Reads.Detail.Id,
            fixture.SiteUserId,
            new SiteNotificationSettingMutation(true, false, null, null),
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.NotFound, result.Kind);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(1, fixture.Reads.ExistsCallCount);
        Assert.Equal(SiteAccessScopeKind.Assigned, fixture.Reads.LastScope?.Kind);
        Assert.Equal(userId, fixture.Reads.LastScope?.UserId);
        Assert.Equal(fixture.Now.UtcDateTime, fixture.Reads.LastScope?.NowUtc);
        Assert.Equal(0, fixture.Reads.NotificationTargetReadCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Equal(0, fixture.Writes.NotificationSettingCount);
    }

    [Fact]
    public async Task UpdateNotificationSettingAsync_InaccessibleSite_IsMaskedBeforeTargetOwnership()
    {
        var fixture = SiteMutationFixture.Valid();
        var userId = Guid.NewGuid();
        var companyUser = new PortalUserContext(
            userId,
            "company",
            fixture.Mutation.CompanyId,
            false,
            false,
            true);
        fixture.Reads.Exists = false;
        fixture.Reads.NotificationTarget = new SiteNotificationSettingTarget(
            fixture.SiteUserId,
            fixture.Reads.Detail.Id,
            Guid.NewGuid());

        var result = await fixture.Service.UpdateNotificationSettingAsync(
            companyUser,
            fixture.Reads.Detail.Id,
            fixture.SiteUserId,
            new SiteNotificationSettingMutation(true, false, null, null),
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.NotFound, result.Kind);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(1, fixture.Reads.ExistsCallCount);
        Assert.Equal(0, fixture.Reads.NotificationTargetReadCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Equal(0, fixture.Writes.NotificationSettingCount);
    }

    private sealed record SiteMutationFixture(
        SiteApplicationService Service,
        PortalUserContext Admin,
        SiteMutation Mutation,
        RecordingUnitOfWork UnitOfWork,
        RecordingSiteWritePort Writes,
        MutationSiteReadPort Reads,
        RecordingPortalUserDirectory Users,
        Guid SiteUserId,
        DateTimeOffset Now)
    {
        public static SiteMutationFixture Valid()
        {
            var now = new DateTimeOffset(
                2026,
                7,
                23,
                12,
                0,
                0,
                TimeSpan.Zero);
            var siteUserId = Guid.NewGuid();
            var detail = new SiteDetailModel
            {
                Id = Guid.NewGuid(),
                SiteName = "Valid Site"
            };
            var reads = new MutationSiteReadPort
            {
                Exists = true,
                MutationData = new SiteMutationValidationData(
                    DuplicateSiteName: false,
                    CompanyExists: true,
                    ContractExists: true,
                    ContractIsUnassigned: true,
                    ContractBelongsToCompany: true),
                Detail = detail,
                NotificationTarget = new SiteNotificationSettingTarget(
                    siteUserId,
                    detail.Id,
                    Guid.NewGuid()),
                NotificationData = new SiteNotificationSettingsData(
                    detail.Id,
                    detail.SiteName,
                    [])
            };
            var writes = new RecordingSiteWritePort(detail.Id);
            var unitOfWork = new RecordingUnitOfWork();
            reads.IsTransactionActive = () => unitOfWork.IsTransactionActive;
            var users = new RecordingPortalUserDirectory();
            var service = new SiteApplicationService(
                reads,
                writes,
                unitOfWork,
                users,
                new NoOpSiteArchivePort(),
                new NoOpSiteLogoPort(),
                new FixedTimeProvider(now));
            return new SiteMutationFixture(
                service,
                new PortalUserContext(
                    Guid.NewGuid(),
                    "admin",
                    null,
                    true,
                    false,
                    false),
                ValidMutation(),
                unitOfWork,
                writes,
                reads,
                users,
                siteUserId,
                now);
        }

        public static SiteMutation ValidMutation() =>
            new(
                "Valid Site",
                Guid.NewGuid(),
                Guid.NewGuid(),
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
    }

    private sealed class RecordingUnitOfWork : IApplicationUnitOfWork
    {
        public int TransactionCount { get; private set; }
        public int SaveCount { get; private set; }
        public bool IsTransactionActive { get; private set; }

        public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
            Func<CancellationToken, Task<TResponse>> operation,
            CancellationToken cancellationToken)
        {
            TransactionCount++;
            IsTransactionActive = true;
            try
            {
                return await operation(cancellationToken);
            }
            finally
            {
                IsTransactionActive = false;
            }
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class RecordingSiteWritePort(Guid createdSiteId) : ISiteWritePort
    {
        public int CreateCount { get; private set; }
        public int UpdateCount { get; private set; }
        public int NotificationSettingCount { get; private set; }
        public int ClaimContractCount { get; private set; }
        public DateTime? CreateDateUtc { get; private set; }
        public bool UpdateResult { get; set; } = true;
        public bool ClaimContractResult { get; set; } = true;

        public Task<Guid> CreateAsync(
            ValidatedSiteMutation mutation,
            DateTime createDateUtc,
            CancellationToken cancellationToken)
        {
            CreateCount++;
            CreateDateUtc = createDateUtc;
            return Task.FromResult(createdSiteId);
        }

        public Task<bool> TryClaimContractAsync(
            Guid contractId,
            Guid companyId,
            Guid siteId,
            CancellationToken cancellationToken)
        {
            ClaimContractCount++;
            return Task.FromResult(ClaimContractResult);
        }

        public Task<bool> UpdateAsync(
            Guid siteId,
            ValidatedSiteMutation mutation,
            CancellationToken cancellationToken)
        {
            UpdateCount++;
            return Task.FromResult(UpdateResult);
        }

        public Task MarkArchivedAsync(
            Guid siteId,
            string createdBy,
            string archiveUrl,
            DateTime archivedUtc,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task UpsertNotificationSettingAsync(
            Guid siteUserId,
            SiteNotificationSettingMutation request,
            TimeSpan? startTime,
            TimeSpan? endTime,
            CancellationToken cancellationToken)
        {
            NotificationSettingCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class MutationSiteReadPort : FakeSiteReadPort
    {
        public required SiteMutationValidationData MutationData { get; set; }
        public required SiteDetailModel Detail { get; init; }
        public SiteNotificationSettingTarget? NotificationTarget { get; set; }
        public SiteNotificationSettingsData? NotificationData { get; set; }
        public SiteAssignmentWindow? AssignmentWindow { get; set; }
        public Func<bool>? IsTransactionActive { get; set; }
        public bool? ExistsReadInsideTransaction { get; private set; }
        public int MutationValidationReadCount { get; private set; }
        public int NotificationTargetReadCount { get; private set; }

        public override Task<SiteMutationValidationData> GetMutationValidationDataAsync(
            SiteMutation request,
            Guid? currentSiteId,
            CancellationToken cancellationToken)
        {
            MutationValidationReadCount++;
            return Task.FromResult(MutationData);
        }

        public override Task<bool> ExistsAsync(
            Guid siteId,
            SiteAccessScope scope,
            CancellationToken cancellationToken)
        {
            ExistsReadInsideTransaction = IsTransactionActive?.Invoke();
            if (AssignmentWindow is not null)
            {
                Exists = scope.UserId.HasValue &&
                    ActiveSiteAssignment.IsActive(
                        AssignmentWindow,
                        scope.UserId.Value,
                        scope.NowUtc);
            }

            return base.ExistsAsync(siteId, scope, cancellationToken);
        }

        public override Task<SiteDetailModel?> GetAsync(
            Guid siteId,
            CancellationToken cancellationToken) =>
            Task.FromResult<SiteDetailModel?>(Detail);

        public override Task<SiteNotificationSettingTarget?> GetNotificationSettingTargetAsync(
            Guid siteId,
            Guid siteUserId,
            CancellationToken cancellationToken)
        {
            NotificationTargetReadCount++;
            return Task.FromResult(NotificationTarget);
        }

        public override Task<SiteNotificationSettingsData?> GetNotificationSettingsAsync(
            Guid siteId,
            CancellationToken cancellationToken) =>
            Task.FromResult(NotificationData);
    }

    private sealed class RecordingPortalUserDirectory : IPortalUserDirectory
    {
        public PortalUserProfile? Profile { get; set; }

        public Task<IReadOnlyList<PortalUserProfile>> ListUsersAsync(
            CancellationToken token) =>
            Task.FromResult<IReadOnlyList<PortalUserProfile>>(
                Profile is null ? [] : [Profile]);

        public Task<PortalUserProfile?> FindByIdAsync(
            Guid id,
            CancellationToken token) =>
            Task.FromResult(Profile?.UserId == id ? Profile : null);
    }
}
