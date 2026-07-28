// File summary: Covers the transport-neutral Help application policy, validation, and use-case orchestration.
// Major updates:
// - 2026-07-28 Added RED coverage for the standalone Help application boundary.

using RvtPortal.Application.Common;
using RvtPortal.Application.Help;
using RvtPortal.Application.Help.Ports;
using RvtPortal.Application.Identity;
using RvtPortal.Testing.Help;

namespace RvtPortal.Spa.Tests;

public sealed class HelpApplicationServiceTests
{
    [Fact]
    public void AuthorizationPolicy_PreservesPublishedAndAdminRoleContracts()
    {
        PortalUserContext admin = Actor(isAdmin: true);
        PortalUserContext companyUser = Actor(isCompanyUser: true);
        PortalUserContext installer = Actor(isInstaller: true);

        Assert.True(HelpAuthorizationPolicy.CanReadPublished(admin));
        Assert.True(HelpAuthorizationPolicy.CanReadPublished(companyUser));
        Assert.False(HelpAuthorizationPolicy.CanReadPublished(installer));
        Assert.True(HelpAuthorizationPolicy.CanManage(admin));
        Assert.False(HelpAuthorizationPolicy.CanManage(companyUser));
        Assert.False(HelpAuthorizationPolicy.CanManage(installer));
    }

    public static TheoryData<string, string?, string?, string?> MutationAssetUrlCases
    {
        get
        {
            TheoryData<string, string?, string?, string?> cases = new TheoryData<string, string?, string?, string?>();
            foreach (HelpAssetUrlCase @case in HelpAssetUrlPolicyCases.All)
            {
                cases.Add(
                    @case.Name,
                    @case.Input,
                    @case.MutationCanonicalValue,
                    @case.MutationViolation);
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(MutationAssetUrlCases))]
    public void MutationValidator_ValidatesAssetUrlsFromSharedCorpus(
        string name,
        string? input,
        string? mutationCanonicalValue,
        string? mutationViolation)
    {
        Assert.False(string.IsNullOrWhiteSpace(name));
        HelpMutationValidationResult result = HelpMutationValidator.ValidateShape(
            ValidMutation() with
            {
                Assets =
                [
                    new HelpAssetMutation(null, "Guide", "Document", input!, 0)
                ]
            });

        if (mutationViolation is null)
        {
            Assert.True(result.IsValid);
            HelpAssetMutation asset = Assert.Single(result.Value!.Source.Assets);
            Assert.Equal(mutationCanonicalValue, asset.Url);
            return;
        }

        Assert.False(result.IsValid);
        string expectedMessage = mutationViolation switch
        {
            "required" => "Assets[0].Url is required.",
            "too_long" => "Assets[0].Url must be 512 characters or fewer.",
            _ => "Asset URL must be an absolute HTTPS URL or a /help-assets/ path."
        };
        Assert.Contains(
            result.Errors,
            error => error.Field == "Assets[0].Url" &&
                error.Message == expectedMessage);
    }

    [Fact]
    public void MutationValidator_CanonicalizesValuesAndPreservesAssetIds()
    {
        Guid assetId = Guid.NewGuid();
        HelpMutationValidationResult result = HelpMutationValidator.ValidateShape(
            ValidMutation() with
            {
                SectionTitle = " Guides ",
                SectionSlug = " guides ",
                Title = " Dust guide ",
                Slug = " dust-guide ",
                Summary = " Summary ",
                Body = " Body ",
                ContentType = "faq",
                Assets =
                [
                    new HelpAssetMutation(
                        assetId,
                        " Guide ",
                        "document",
                        "https://docs.rvt.test/guide.pdf",
                        2)
                ]
            });

        Assert.True(result.IsValid);
        HelpArticleMutation mutation = result.Value!.Source;
        Assert.Equal("Guides", mutation.SectionTitle);
        Assert.Equal("guides", mutation.SectionSlug);
        Assert.Equal("Dust guide", mutation.Title);
        Assert.Equal("dust-guide", mutation.Slug);
        Assert.Equal("Summary", mutation.Summary);
        Assert.Equal("Body", mutation.Body);
        Assert.Equal("FAQ", mutation.ContentType);
        HelpAssetMutation asset = Assert.Single(mutation.Assets);
        Assert.Equal(assetId, asset.Id);
        Assert.Equal("Document", asset.AssetType);
        Assert.Equal("Guide", asset.Title);
    }

    [Fact]
    public void MutationValidator_RejectsRequiredFormatAndRangeViolations()
    {
        HelpMutationValidationResult result = HelpMutationValidator.ValidateShape(
            new HelpArticleMutation(
                "",
                "Not a slug",
                "",
                "also_not_a_slug",
                null,
                "",
                "Unknown",
                false,
                -1,
                -2,
                [
                    new HelpAssetMutation(
                        null,
                        "",
                        "Unknown",
                        "",
                        -1)
                ]));

        Assert.False(result.IsValid);
        AssertErrors(
            result,
            nameof(HelpArticleMutation.SectionTitle),
            nameof(HelpArticleMutation.SectionSlug),
            nameof(HelpArticleMutation.Title),
            nameof(HelpArticleMutation.Slug),
            nameof(HelpArticleMutation.Body),
            nameof(HelpArticleMutation.ContentType),
            nameof(HelpArticleMutation.SectionSortOrder),
            nameof(HelpArticleMutation.SortOrder),
            "Assets[0].Title",
            "Assets[0].AssetType",
            "Assets[0].Url",
            "Assets[0].SortOrder");
    }

    [Fact]
    public void MutationValidator_RejectsAllPersistedLengthOverflows()
    {
        HelpMutationValidationResult result = HelpMutationValidator.ValidateShape(
            ValidMutation() with
            {
                SectionTitle = new string('a', 121),
                SectionSlug = new string('a', 121),
                Title = new string('a', 161),
                Slug = new string('a', 161),
                Summary = new string('a', 513),
                Body = new string('a', 100_001),
                Assets =
                [
                    new HelpAssetMutation(
                        null,
                        new string('a', 161),
                        "Document",
                        $"https://docs.rvt.test/{new string('a', 500)}",
                        0)
                ]
            });

        Assert.False(result.IsValid);
        AssertErrors(
            result,
            nameof(HelpArticleMutation.SectionTitle),
            nameof(HelpArticleMutation.SectionSlug),
            nameof(HelpArticleMutation.Title),
            nameof(HelpArticleMutation.Slug),
            nameof(HelpArticleMutation.Summary),
            nameof(HelpArticleMutation.Body),
            "Assets[0].Title",
            "Assets[0].Url");
    }

    [Fact]
    public void MutationValidator_RejectsDuplicateSlugAndForeignAssetIds()
    {
        Guid existingAssetId = Guid.NewGuid();
        Guid foreignAssetId = Guid.NewGuid();
        HelpMutationValidationResult shape = HelpMutationValidator.ValidateShape(
            ValidMutation() with
            {
                Assets =
                [
                    new HelpAssetMutation(
                        existingAssetId,
                        "Existing",
                        "Document",
                        "https://docs.rvt.test/existing.pdf",
                        0),
                    new HelpAssetMutation(
                        foreignAssetId,
                        "Foreign",
                        "Link",
                        "https://docs.rvt.test/foreign",
                        1)
                ]
            });

        HelpMutationValidationResult result = HelpMutationValidator.ValidateBusinessRules(
            shape,
            new HelpMutationValidationData(
                ArticleExists: true,
                SlugBelongsToAnotherArticle: true,
                ExistingAssetIds: new HashSet<Guid> { existingAssetId }),
            requireExistingArticle: true);

        Assert.False(result.IsValid);
        AssertErrors(
            result,
            nameof(HelpArticleMutation.Slug),
            "Assets[1].Id");
    }

    [Fact]
    public void MutationValidator_RejectsMissingUpdateTarget()
    {
        HelpMutationValidationResult shape = HelpMutationValidator.ValidateShape(ValidMutation());

        HelpMutationValidationResult result = HelpMutationValidator.ValidateBusinessRules(
            shape,
            new HelpMutationValidationData(
                ArticleExists: false,
                SlugBelongsToAnotherArticle: false,
                ExistingAssetIds: new HashSet<Guid>()),
            requireExistingArticle: true);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Field == "ArticleId");
    }

    [Fact]
    public async Task PublishedReads_EnforceApplicationAuthorizationAndPreserveCancellation()
    {
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        RecordingHelpReadPort reads = new RecordingHelpReadPort
        {
            PublishedOverview = new HelpOverviewModel { SearchText = "dust" }
        };
        HelpApplicationService service = CreateService(reads: reads);

        UseCaseResult<HelpOverviewModel> allowed = await service.QueryPublishedAsync(
            Actor(isCompanyUser: true),
            " dust ",
            cancellation.Token);
        UseCaseResult<HelpOverviewModel> forbidden = await service.QueryPublishedAsync(
            Actor(isInstaller: true),
            "dust",
            cancellation.Token);

        Assert.Equal(UseCaseResultKind.Success, allowed.Kind);
        Assert.Same(reads.PublishedOverview, allowed.Value);
        Assert.Equal("dust", reads.PublishedSearchText);
        Assert.Equal(cancellation.Token, reads.LastCancellationToken);
        Assert.Equal(UseCaseResultKind.Forbidden, forbidden.Kind);
        Assert.Equal(1, reads.PublishedQueryCount);
    }

    [Fact]
    public async Task AdminReads_RejectNonAdminsBeforeCallingThePort()
    {
        RecordingHelpReadPort reads = new RecordingHelpReadPort();
        HelpApplicationService service = CreateService(reads: reads);

        UseCaseResult<HelpAdminOverviewModel> result = await service.QueryAdminAsync(
            Actor(isCompanyUser: true),
            new HelpAdminQuery(null, null, null),
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Forbidden, result.Kind);
        Assert.Equal(0, reads.AdminQueryCount);
    }

    [Fact]
    public async Task CreateAsync_UsesOneTransactionOneSaveAndInjectedUtc()
    {
        Guid articleId = Guid.NewGuid();
        DateTimeOffset expectedTime = new DateTimeOffset(
            2026,
            7,
            28,
            9,
            30,
            0,
            TimeSpan.Zero);
        RecordingHelpReadPort reads = new RecordingHelpReadPort
        {
            ValidationData = ValidCreateData(),
            AdminArticle = Article(articleId)
        };
        RecordingHelpWritePort writes = new RecordingHelpWritePort { CreatedArticleId = articleId };
        RecordingUnitOfWork unitOfWork = new RecordingUnitOfWork();
        HelpApplicationService service = CreateService(
            reads,
            writes,
            unitOfWork,
            new FixedTimeProvider(expectedTime));

        UseCaseResult<HelpArticleModel> result = await service.CreateAsync(
            Actor(isAdmin: true),
            ValidMutation(),
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Success, result.Kind);
        Assert.Equal(articleId, result.Value?.Id);
        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(1, writes.CreateCount);
        Assert.Equal(expectedTime.UtcDateTime, writes.CreateTimestampUtc);
        Assert.Equal(DateTimeKind.Utc, writes.CreateTimestampUtc.Kind);
        Assert.Equal(articleId, reads.AdminArticleId);
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidInputBeforeStartingATransaction()
    {
        RecordingHelpWritePort writes = new RecordingHelpWritePort();
        RecordingUnitOfWork unitOfWork = new RecordingUnitOfWork();
        HelpApplicationService service = CreateService(
            writes: writes,
            unitOfWork: unitOfWork);

        UseCaseResult<HelpArticleModel> result = await service.CreateAsync(
            Actor(isAdmin: true),
            ValidMutation() with { Slug = "INVALID SLUG" },
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Validation, result.Kind);
        Assert.Equal(0, unitOfWork.TransactionCount);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Equal(0, writes.CreateCount);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFoundWithoutWritingOrSaving()
    {
        RecordingHelpReadPort reads = new RecordingHelpReadPort
        {
            ValidationData = new HelpMutationValidationData(
                ArticleExists: false,
                SlugBelongsToAnotherArticle: false,
                ExistingAssetIds: new HashSet<Guid>())
        };
        RecordingHelpWritePort writes = new RecordingHelpWritePort();
        RecordingUnitOfWork unitOfWork = new RecordingUnitOfWork();
        HelpApplicationService service = CreateService(reads, writes, unitOfWork);

        UseCaseResult<HelpArticleModel> result = await service.UpdateAsync(
            Actor(isAdmin: true),
            Guid.NewGuid(),
            ValidMutation(),
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.NotFound, result.Kind);
        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Equal(0, writes.UpdateCount);
    }

    [Fact]
    public async Task UpdateAsync_RejectsForeignAssetIdsInsideTheTransaction()
    {
        Guid foreignAssetId = Guid.NewGuid();
        RecordingHelpReadPort reads = new RecordingHelpReadPort
        {
            ValidationData = new HelpMutationValidationData(
                ArticleExists: true,
                SlugBelongsToAnotherArticle: false,
                ExistingAssetIds: new HashSet<Guid>())
        };
        RecordingHelpWritePort writes = new RecordingHelpWritePort();
        RecordingUnitOfWork unitOfWork = new RecordingUnitOfWork();
        HelpApplicationService service = CreateService(reads, writes, unitOfWork);

        UseCaseResult<HelpArticleModel> result = await service.UpdateAsync(
            Actor(isAdmin: true),
            Guid.NewGuid(),
            ValidMutation() with
            {
                Assets =
                [
                    new HelpAssetMutation(
                        foreignAssetId,
                        "Foreign",
                        "Link",
                        "https://docs.rvt.test/foreign",
                        0)
                ]
            },
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Validation, result.Kind);
        Assert.Contains(
            result.Errors,
            error => error.Field == "Assets[0].Id");
        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Equal(0, writes.UpdateCount);
    }

    [Fact]
    public async Task PublicationAndDelete_ReturnNotFoundWithoutSaving()
    {
        RecordingHelpWritePort writes = new RecordingHelpWritePort
        {
            PublicationResult = false,
            DeleteResult = false
        };
        RecordingUnitOfWork unitOfWork = new RecordingUnitOfWork();
        HelpApplicationService service = CreateService(
            writes: writes,
            unitOfWork: unitOfWork);
        PortalUserContext actor = Actor(isAdmin: true);
        Guid articleId = Guid.NewGuid();

        UseCaseResult<HelpArticleModel> publication = await service.SetPublicationAsync(
            actor,
            articleId,
            true,
            CancellationToken.None);
        UseCaseResult<HelpDeleteResult> deletion = await service.DeleteAsync(
            actor,
            articleId,
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.NotFound, publication.Kind);
        Assert.Equal(UseCaseResultKind.NotFound, deletion.Kind);
        Assert.Equal(2, unitOfWork.TransactionCount);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task SuccessfulMutations_SaveAndReturnRefreshedModels()
    {
        Guid articleId = Guid.NewGuid();
        RecordingHelpReadPort reads = new RecordingHelpReadPort
        {
            ValidationData = new HelpMutationValidationData(
                ArticleExists: true,
                SlugBelongsToAnotherArticle: false,
                ExistingAssetIds: new HashSet<Guid>()),
            AdminArticle = Article(articleId)
        };
        RecordingHelpWritePort writes = new RecordingHelpWritePort
        {
            UpdateResult = true,
            PublicationResult = true,
            DeleteResult = true
        };
        RecordingUnitOfWork unitOfWork = new RecordingUnitOfWork();
        HelpApplicationService service = CreateService(reads, writes, unitOfWork);
        PortalUserContext actor = Actor(isAdmin: true);

        UseCaseResult<HelpArticleModel> update = await service.UpdateAsync(
            actor,
            articleId,
            ValidMutation(),
            CancellationToken.None);
        UseCaseResult<HelpArticleModel> publication = await service.SetPublicationAsync(
            actor,
            articleId,
            true,
            CancellationToken.None);
        UseCaseResult<HelpDeleteResult> deletion = await service.DeleteAsync(
            actor,
            articleId,
            CancellationToken.None);

        Assert.Equal(articleId, update.Value?.Id);
        Assert.Equal(articleId, publication.Value?.Id);
        Assert.Equal(articleId, deletion.Value?.ArticleId);
        Assert.Equal(3, unitOfWork.TransactionCount);
        Assert.Equal(3, unitOfWork.SaveCount);
        Assert.Equal(2, reads.AdminArticleQueryCount);
    }

    [Fact]
    public async Task AdminMutations_RejectNonAdminsBeforeStartingATransaction()
    {
        RecordingHelpWritePort writes = new RecordingHelpWritePort();
        RecordingUnitOfWork unitOfWork = new RecordingUnitOfWork();
        HelpApplicationService service = CreateService(
            writes: writes,
            unitOfWork: unitOfWork);
        PortalUserContext actor = Actor(isCompanyUser: true);

        UseCaseResult<HelpArticleModel> create = await service.CreateAsync(
            actor,
            ValidMutation(),
            CancellationToken.None);
        UseCaseResult<HelpArticleModel> update = await service.UpdateAsync(
            actor,
            Guid.NewGuid(),
            ValidMutation(),
            CancellationToken.None);
        UseCaseResult<HelpArticleModel> publication = await service.SetPublicationAsync(
            actor,
            Guid.NewGuid(),
            true,
            CancellationToken.None);
        UseCaseResult<HelpDeleteResult> deletion = await service.DeleteAsync(
            actor,
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.All(
            new[] { create.Kind, update.Kind, publication.Kind, deletion.Kind },
            kind => Assert.Equal(UseCaseResultKind.Forbidden, kind));
        Assert.Equal(0, unitOfWork.TransactionCount);
        Assert.Equal(0, writes.TotalCalls);
    }

    [Fact]
    public async Task CreateAsync_PropagatesCancellationToPortsAndUnitOfWork()
    {
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        Guid articleId = Guid.NewGuid();
        RecordingHelpReadPort reads = new RecordingHelpReadPort
        {
            ValidationData = ValidCreateData(),
            AdminArticle = Article(articleId)
        };
        RecordingHelpWritePort writes = new RecordingHelpWritePort { CreatedArticleId = articleId };
        RecordingUnitOfWork unitOfWork = new RecordingUnitOfWork();
        HelpApplicationService service = CreateService(reads, writes, unitOfWork);

        await service.CreateAsync(
            Actor(isAdmin: true),
            ValidMutation(),
            cancellation.Token);

        Assert.Equal(cancellation.Token, unitOfWork.LastCancellationToken);
        Assert.Equal(cancellation.Token, reads.LastCancellationToken);
        Assert.Equal(cancellation.Token, writes.LastCancellationToken);
    }

    private static PortalUserContext Actor(
        bool isAdmin = false,
        bool isInstaller = false,
        bool isCompanyUser = false) =>
        new(
            Guid.NewGuid(),
            "help.user@rvt.test",
            Guid.NewGuid(),
            isAdmin,
            isInstaller,
            isCompanyUser);

    private static HelpArticleMutation ValidMutation() =>
        new(
            "Guides",
            "guides",
            "Dust guide",
            "dust-guide",
            "Dust summary",
            "Dust body",
            "FAQ",
            false,
            0,
            0,
            []);

    private static HelpMutationValidationData ValidCreateData() =>
        new(
            ArticleExists: false,
            SlugBelongsToAnotherArticle: false,
            ExistingAssetIds: new HashSet<Guid>());

    private static HelpArticleModel Article(Guid id) =>
        new()
        {
            Id = id,
            Title = "Dust guide",
            Slug = "dust-guide",
            Body = "Dust body",
            ContentType = "FAQ",
            SectionTitle = "Guides",
            SectionSlug = "guides",
            CreatedAtUtc = new DateTime(2026, 7, 28, 9, 30, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 7, 28, 9, 30, 0, DateTimeKind.Utc)
        };

    private static HelpApplicationService CreateService(
        RecordingHelpReadPort? reads = null,
        RecordingHelpWritePort? writes = null,
        RecordingUnitOfWork? unitOfWork = null,
        TimeProvider? clock = null) =>
        new(
            reads ?? new RecordingHelpReadPort(),
            writes ?? new RecordingHelpWritePort(),
            unitOfWork ?? new RecordingUnitOfWork(),
            clock ?? new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 28, 9, 30, 0, TimeSpan.Zero)));

    private static void AssertErrors(
        HelpMutationValidationResult result,
        params string[] expectedFields)
    {
        HashSet<string> fields = result.Errors
            .Select(error => error.Field)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string field in expectedFields)
        {
            Assert.Contains(field, fields);
        }
    }

    private sealed class RecordingHelpReadPort : IHelpReadPort
    {
        public HelpOverviewModel PublishedOverview { get; set; } = new();
        public HelpAdminOverviewModel AdminOverview { get; set; } = new();
        public HelpArticleModel? PublishedArticle { get; set; }
        public HelpArticleModel? AdminArticle { get; set; }
        public HelpMutationValidationData ValidationData { get; set; } =
            ValidCreateData();
        public int PublishedQueryCount { get; private set; }
        public int AdminQueryCount { get; private set; }
        public int AdminArticleQueryCount { get; private set; }
        public string? PublishedSearchText { get; private set; }
        public Guid? AdminArticleId { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }

        public Task<HelpOverviewModel> QueryPublishedAsync(
            string? searchText,
            CancellationToken cancellationToken)
        {
            PublishedQueryCount++;
            PublishedSearchText = searchText;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(PublishedOverview);
        }

        public Task<HelpArticleModel?> GetPublishedArticleAsync(
            string slug,
            CancellationToken cancellationToken)
        {
            LastCancellationToken = cancellationToken;
            return Task.FromResult(PublishedArticle);
        }

        public Task<HelpAdminOverviewModel> QueryAdminAsync(
            HelpAdminQuery query,
            CancellationToken cancellationToken)
        {
            AdminQueryCount++;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(AdminOverview);
        }

        public Task<HelpArticleModel?> GetAdminArticleAsync(
            Guid articleId,
            CancellationToken cancellationToken)
        {
            AdminArticleQueryCount++;
            AdminArticleId = articleId;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(AdminArticle);
        }

        public Task<HelpMutationValidationData> GetMutationValidationDataAsync(
            string slug,
            Guid? articleId,
            CancellationToken cancellationToken)
        {
            LastCancellationToken = cancellationToken;
            return Task.FromResult(ValidationData);
        }
    }

    private sealed class RecordingHelpWritePort : IHelpWritePort
    {
        public Guid CreatedArticleId { get; set; } = Guid.NewGuid();
        public bool UpdateResult { get; set; } = true;
        public bool PublicationResult { get; set; } = true;
        public bool DeleteResult { get; set; } = true;
        public int CreateCount { get; private set; }
        public int UpdateCount { get; private set; }
        public int PublicationCount { get; private set; }
        public int DeleteCount { get; private set; }
        public int TotalCalls =>
            CreateCount + UpdateCount + PublicationCount + DeleteCount;
        public DateTime CreateTimestampUtc { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }

        public Task<Guid> CreateAsync(
            ValidatedHelpArticleMutation mutation,
            DateTime nowUtc,
            CancellationToken cancellationToken)
        {
            CreateCount++;
            CreateTimestampUtc = nowUtc;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(CreatedArticleId);
        }

        public Task<bool> UpdateAsync(
            Guid articleId,
            ValidatedHelpArticleMutation mutation,
            DateTime nowUtc,
            CancellationToken cancellationToken)
        {
            UpdateCount++;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(UpdateResult);
        }

        public Task<bool> SetPublicationAsync(
            Guid articleId,
            bool isPublished,
            DateTime nowUtc,
            CancellationToken cancellationToken)
        {
            PublicationCount++;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(PublicationResult);
        }

        public Task<bool> DeleteAsync(
            Guid articleId,
            CancellationToken cancellationToken)
        {
            DeleteCount++;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(DeleteResult);
        }
    }

    private sealed class RecordingUnitOfWork : IApplicationUnitOfWork
    {
        public int TransactionCount { get; private set; }
        public int SaveCount { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }

        public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
            Func<CancellationToken, Task<TResponse>> operation,
            CancellationToken cancellationToken)
        {
            TransactionCount++;
            LastCancellationToken = cancellationToken;
            return await operation(cancellationToken);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(1);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
