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
        SiteMutationFixture fixture = SiteMutationFixture.Valid();

        UseCaseResult<SiteDetailModel> result = await fixture.Service.CreateAsync(
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
        SiteMutationFixture fixture = SiteMutationFixture.Valid() with
        {
            Mutation = SiteMutationFixture.ValidMutation() with
            {
                StartTime = "18:00",
                EndTime = "08:00"
            }
        };

        UseCaseResult<SiteDetailModel> result = await fixture.Service.CreateAsync(
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
        SiteMutationFixture fixture = SiteMutationFixture.Valid();
        PortalUserContext companyUser = new(
            Guid.NewGuid(),
            "company",
            fixture.Mutation.CompanyId,
            false,
            false,
            true);

        UseCaseResult<SiteDetailModel> result = await fixture.Service.CreateAsync(
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
        SiteMutationFixture fixture = SiteMutationFixture.Valid();
        fixture.Reads.MutationData = fixture.Reads.MutationData with
        {
            ContractExists = contractExists,
            ContractIsUnassigned = contractIsUnassigned,
            ContractBelongsToCompany = contractBelongsToCompany,
            CompanyExists = companyExists
        };

        UseCaseResult<SiteDetailModel> result = await fixture.Service.CreateAsync(
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
        SiteMutationFixture fixture = SiteMutationFixture.Valid();
        fixture.Reads.MutationData = fixture.Reads.MutationData with
        {
            DuplicateSiteName = true
        };

        UseCaseResult<SiteDetailModel> result = await fixture.Service.CreateAsync(
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
        SiteMutationFixture fixture = SiteMutationFixture.Valid();
        fixture.Reads.MutationData = fixture.Reads.MutationData with
        {
            CompanyExists = false
        };

        UseCaseResult<SiteDetailModel> result = await fixture.Service.CreateAsync(
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
        SiteMutationFixture fixture = SiteMutationFixture.Valid();
        fixture.Writes.ClaimContractResult = false;

        UseCaseResult<SiteDetailModel> result = await fixture.Service.CreateAsync(
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
    public async Task UpdateAsync_MalformedRequest_UserWhoCannotManage_ReturnsForbiddenBeforeValidation()
    {
        SiteMutationFixture fixture = SiteMutationFixture.Valid();
        PortalUserContext companyUser = new(
            Guid.NewGuid(),
            "company",
            fixture.Mutation.CompanyId,
            false,
            false,
            true);

        UseCaseResult<SiteDetailModel> result = await fixture.Service.UpdateAsync(
            companyUser,
            fixture.Reads.Detail.Id,
            fixture.Mutation with
            {
                SiteName = "",
                EndTime = "not-a-time",
                ContractId = null
            },
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Forbidden, result.Kind);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(0, fixture.Reads.ExistsCallCount);
        Assert.Equal(0, fixture.Reads.MutationValidationReadCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Equal(0, fixture.Writes.UpdateCount);
    }

    [Fact]
    public async Task UpdateAsync_MalformedRequest_MissingSiteReturnsMaskedNotFound()
    {
        SiteMutationFixture fixture = SiteMutationFixture.Valid();
        fixture.Reads.Exists = false;

        UseCaseResult<SiteDetailModel> result = await fixture.Service.UpdateAsync(
            fixture.Admin,
            fixture.Reads.Detail.Id,
            fixture.Mutation with
            {
                SiteName = "",
                EndTime = "not-a-time",
                ContractId = null
            },
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.NotFound, result.Kind);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(1, fixture.Reads.ExistsCallCount);
        Assert.True(fixture.Reads.ExistsReadInsideTransaction);
        Assert.Equal(0, fixture.Reads.MutationValidationReadCount);
        Assert.Equal(0, fixture.Writes.UpdateCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_MalformedRequest_ExistingSiteReturnsValidationBeforeBusinessReads()
    {
        SiteMutationFixture fixture = SiteMutationFixture.Valid();

        UseCaseResult<SiteDetailModel> result = await fixture.Service.UpdateAsync(
            fixture.Admin,
            fixture.Reads.Detail.Id,
            fixture.Mutation with
            {
                StartTime = "08:00",
                EndTime = "not-a-time",
                ContractId = null
            },
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Validation, result.Kind);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(1, fixture.Reads.ExistsCallCount);
        Assert.True(fixture.Reads.ExistsReadInsideTransaction);
        Assert.Equal(0, fixture.Reads.MutationValidationReadCount);
        Assert.Equal(0, fixture.Writes.UpdateCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_MissingSite_TakesPrecedenceOverInvalidMutationFacts()
    {
        SiteMutationFixture fixture = SiteMutationFixture.Valid();
        fixture.Reads.Exists = false;
        fixture.Reads.MutationData = fixture.Reads.MutationData with
        {
            DuplicateSiteName = true,
            CompanyExists = false
        };

        UseCaseResult<SiteDetailModel> result = await fixture.Service.UpdateAsync(
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
        SiteMutationFixture fixture = SiteMutationFixture.Valid();
        fixture.Writes.UpdateResult = false;

        UseCaseResult<SiteDetailModel> result = await fixture.Service.UpdateAsync(
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
    public async Task UpdateNotificationSettingAsync_InvalidTime_CompanyUserCannotUpdateAnotherUsersSetting()
    {
        SiteMutationFixture fixture = SiteMutationFixture.Valid();
        PortalUserContext companyUser = new(
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

        UseCaseResult<SiteNotificationSettingModel> result = await fixture.Service.UpdateNotificationSettingAsync(
            companyUser,
            fixture.Reads.Detail.Id,
            fixture.SiteUserId,
            new SiteNotificationSettingMutation(true, false, "08:00", "not-a-time"),
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Forbidden, result.Kind);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(1, fixture.Reads.ExistsCallCount);
        Assert.Equal(1, fixture.Reads.NotificationTargetReadCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Equal(0, fixture.Writes.NotificationSettingCount);
    }

    [Fact]
    public async Task UpdateNotificationSettingAsync_CompanyUserCanUpdateOwnSetting()
    {
        SiteMutationFixture fixture = SiteMutationFixture.Valid();
        Guid userId = Guid.NewGuid();
        PortalUserContext companyUser = new(
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

        UseCaseResult<SiteNotificationSettingModel> result = await fixture.Service.UpdateNotificationSettingAsync(
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
    public async Task UpdateNotificationSettingAsync_MalformedEndTimeReportsExactLegacyFieldsWithoutWriting()
    {
        SiteMutationFixture fixture = SiteMutationFixture.Valid();

        UseCaseResult<SiteNotificationSettingModel> result = await fixture.Service.UpdateNotificationSettingAsync(
            fixture.Admin,
            fixture.Reads.Detail.Id,
            fixture.SiteUserId,
            new SiteNotificationSettingMutation(
                true,
                false,
                "08:00",
                "not-a-time"),
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Validation, result.Kind);
        Assert.Collection(
            result.Errors,
            error =>
            {
                Assert.Equal(
                    nameof(SiteNotificationSettingMutation.EndTime),
                    error.Field);
                Assert.Equal(
                    "Time values must use HH:mm format.",
                    error.Message);
            },
            error =>
            {
                Assert.Equal(
                    nameof(SiteNotificationSettingMutation.StartTime),
                    error.Field);
                Assert.Equal(
                    "You need to set both start and end time",
                    error.Message);
            });
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(1, fixture.Reads.ExistsCallCount);
        Assert.Equal(1, fixture.Reads.NotificationTargetReadCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Equal(0, fixture.Writes.NotificationSettingCount);
    }

    [Fact]
    public async Task UpdateNotificationSettingAsync_InvalidTime_MissingTargetReturnsNotFoundWithoutSaving()
    {
        SiteMutationFixture fixture = SiteMutationFixture.Valid();
        fixture.Reads.NotificationTarget = null;

        UseCaseResult<SiteNotificationSettingModel> result = await fixture.Service.UpdateNotificationSettingAsync(
            fixture.Admin,
            fixture.Reads.Detail.Id,
            fixture.SiteUserId,
            new SiteNotificationSettingMutation(true, false, "08:00", "not-a-time"),
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.NotFound, result.Kind);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(1, fixture.Reads.ExistsCallCount);
        Assert.Equal(1, fixture.Reads.NotificationTargetReadCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Equal(0, fixture.Writes.NotificationSettingCount);
    }

    [Fact]
    public async Task UpdateNotificationSettingAsync_InvalidTime_MissingSiteReturnsMaskedNotFound()
    {
        SiteMutationFixture fixture = SiteMutationFixture.Valid();
        fixture.Reads.Exists = false;

        UseCaseResult<SiteNotificationSettingModel> result = await fixture.Service.UpdateNotificationSettingAsync(
            fixture.Admin,
            fixture.Reads.Detail.Id,
            fixture.SiteUserId,
            new SiteNotificationSettingMutation(true, false, "08:00", "not-a-time"),
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.NotFound, result.Kind);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(1, fixture.Reads.ExistsCallCount);
        Assert.Equal(0, fixture.Reads.NotificationTargetReadCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Equal(0, fixture.Writes.NotificationSettingCount);
    }

    [Theory]
    [InlineData(-2, -1)]
    [InlineData(1, 2)]
    public async Task UpdateNotificationSettingAsync_InvalidTime_ExpiredOrFutureSelfAssignmentIsMaskedAsSiteNotFound(
        int startOffsetDays,
        int endOffsetDays)
    {
        SiteMutationFixture fixture = SiteMutationFixture.Valid();
        Guid userId = Guid.NewGuid();
        PortalUserContext companyUser = new(
            userId,
            "company",
            fixture.Mutation.CompanyId,
            false,
            false,
            true);
        fixture.Reads.AssignmentWindow = new AssignmentWindow(
            userId,
            fixture.Now.UtcDateTime.AddDays(startOffsetDays),
            fixture.Now.UtcDateTime.AddDays(endOffsetDays));
        fixture.Reads.NotificationTarget = new SiteNotificationSettingTarget(
            fixture.SiteUserId,
            fixture.Reads.Detail.Id,
            userId);

        UseCaseResult<SiteNotificationSettingModel> result = await fixture.Service.UpdateNotificationSettingAsync(
            companyUser,
            fixture.Reads.Detail.Id,
            fixture.SiteUserId,
            new SiteNotificationSettingMutation(true, false, "08:00", "not-a-time"),
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
    public async Task UpdateNotificationSettingAsync_InvalidTime_InaccessibleSiteIsMaskedBeforeTargetOwnership()
    {
        SiteMutationFixture fixture = SiteMutationFixture.Valid();
        Guid userId = Guid.NewGuid();
        PortalUserContext companyUser = new(
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

        UseCaseResult<SiteNotificationSettingModel> result = await fixture.Service.UpdateNotificationSettingAsync(
            companyUser,
            fixture.Reads.Detail.Id,
            fixture.SiteUserId,
            new SiteNotificationSettingMutation(true, false, "08:00", "not-a-time"),
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
            DateTimeOffset now = new(
                2026,
                7,
                23,
                12,
                0,
                0,
                TimeSpan.Zero);
            Guid siteUserId = Guid.NewGuid();
            SiteDetailModel detail = new()
            {
                Id = Guid.NewGuid(),
                SiteName = "Valid Site"
            };
            MutationSiteReadPort reads = new()
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
            RecordingSiteWritePort writes = new(detail.Id);
            RecordingUnitOfWork unitOfWork = new();
            reads.IsTransactionActive = () => unitOfWork.IsTransactionActive;
            RecordingPortalUserDirectory users = new();
            SiteApplicationService service = new(
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

        public Task<SiteArchiveClaimResult> TryClaimArchiveAsync(
            Guid siteId,
            string createdBy,
            string archiveUrl,
            DateTime archivedUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SiteArchiveClaimResult(true, archiveUrl));

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

    private sealed record AssignmentWindow(
        Guid UserId,
        DateTime StartDateUtc,
        DateTime? EndDateUtc);

    private sealed class MutationSiteReadPort : FakeSiteReadPort
    {
        public required SiteMutationValidationData MutationData { get; set; }
        public required SiteDetailModel Detail { get; init; }
        public SiteNotificationSettingTarget? NotificationTarget { get; set; }
        public SiteNotificationSettingsData? NotificationData { get; set; }
        public AssignmentWindow? AssignmentWindow { get; set; }
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
                // Mirrors the production adapter's inclusive assignment-window filter (the Spa EF expression).
                Exists = scope.UserId.HasValue &&
                    AssignmentWindow.UserId == scope.UserId.Value &&
                    AssignmentWindow.StartDateUtc <= scope.NowUtc &&
                    (!AssignmentWindow.EndDateUtc.HasValue || AssignmentWindow.EndDateUtc.Value >= scope.NowUtc);
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
