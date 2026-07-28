// File summary: Covers the transport-neutral Help application policy, validation, and use-case orchestration.
// Major updates:
// - 2026-07-28 Added RED coverage for the standalone Help application boundary.

using RvtPortal.Application.Common;
using RvtPortal.Application.Help;
using RvtPortal.Application.Help.Ports;
using RvtPortal.Application.Identity;

namespace RvtPortal.Spa.Tests;

public sealed class HelpApplicationServiceTests
{
    [Fact]
    public void AuthorizationPolicy_PreservesPublishedAndAdminRoleContracts()
    {
        var admin = Actor(isAdmin: true);
        var companyUser = Actor(isCompanyUser: true);
        var installer = Actor(isInstaller: true);

        Assert.True(HelpAuthorizationPolicy.CanReadPublished(admin));
        Assert.True(HelpAuthorizationPolicy.CanReadPublished(companyUser));
        Assert.False(HelpAuthorizationPolicy.CanReadPublished(installer));
        Assert.True(HelpAuthorizationPolicy.CanManage(admin));
        Assert.False(HelpAuthorizationPolicy.CanManage(companyUser));
        Assert.False(HelpAuthorizationPolicy.CanManage(installer));
    }

    [Theory]
    [InlineData("https://docs.rvt.test/guide.pdf")]
    [InlineData("https://docs.rvt.test")]
    [InlineData("/help-assets/guides/guide.pdf")]
    public void MutationValidator_AcceptsSafeAssetUrls(string url)
    {
        var result = HelpMutationValidator.ValidateShape(
            ValidMutation() with
            {
                Assets =
                [
                    new HelpAssetMutation(null, "Guide", "Document", url, 0)
                ]
            });

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("http://docs.rvt.test/guide.pdf")]
    [InlineData("//docs.rvt.test/guide.pdf")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,test")]
    [InlineData("file:///tmp/guide.pdf")]
    [InlineData("/other/path.pdf")]
    [InlineData("/help-assets\\guide.pdf")]
    [InlineData("https://user:password@docs.rvt.test/guide.pdf")]
    [InlineData("https://docs.rvt.test/guide\u0001.pdf")]
    public void MutationValidator_RejectsUnsafeAssetUrls(string url)
    {
        var result = HelpMutationValidator.ValidateShape(
            ValidMutation() with
            {
                Assets =
                [
                    new HelpAssetMutation(null, "Guide", "Document", url, 0)
                ]
            });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Field == "Assets[0].Url");
    }

    [Fact]
    public void MutationValidator_CanonicalizesValuesAndPreservesAssetIds()
    {
        var assetId = Guid.NewGuid();
        var result = HelpMutationValidator.ValidateShape(
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
        var mutation = result.Value!.Source;
        Assert.Equal("Guides", mutation.SectionTitle);
        Assert.Equal("guides", mutation.SectionSlug);
        Assert.Equal("Dust guide", mutation.Title);
        Assert.Equal("dust-guide", mutation.Slug);
        Assert.Equal("Summary", mutation.Summary);
        Assert.Equal("Body", mutation.Body);
        Assert.Equal("FAQ", mutation.ContentType);
        var asset = Assert.Single(mutation.Assets);
        Assert.Equal(assetId, asset.Id);
        Assert.Equal("Document", asset.AssetType);
        Assert.Equal("Guide", asset.Title);
    }

    [Fact]
    public void MutationValidator_RejectsRequiredFormatAndRangeViolations()
    {
        var result = HelpMutationValidator.ValidateShape(
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
        var result = HelpMutationValidator.ValidateShape(
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
        var existingAssetId = Guid.NewGuid();
        var foreignAssetId = Guid.NewGuid();
        var shape = HelpMutationValidator.ValidateShape(
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

        var result = HelpMutationValidator.ValidateBusinessRules(
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
        var shape = HelpMutationValidator.ValidateShape(ValidMutation());

        var result = HelpMutationValidator.ValidateBusinessRules(
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
        using var cancellation = new CancellationTokenSource();
        var reads = new RecordingHelpReadPort
        {
            PublishedOverview = new HelpOverviewModel { SearchText = "dust" }
        };
        var service = CreateService(reads: reads);

        var allowed = await service.QueryPublishedAsync(
            Actor(isCompanyUser: true),
            " dust ",
            cancellation.Token);
        var forbidden = await service.QueryPublishedAsync(
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
        var reads = new RecordingHelpReadPort();
        var service = CreateService(reads: reads);

        var result = await service.QueryAdminAsync(
            Actor(isCompanyUser: true),
            new HelpAdminQuery(null, null, null),
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Forbidden, result.Kind);
        Assert.Equal(0, reads.AdminQueryCount);
    }

    [Fact]
    public async Task CreateAsync_UsesOneTransactionOneSaveAndInjectedUtc()
    {
        var articleId = Guid.NewGuid();
        var expectedTime = new DateTimeOffset(
            2026,
            7,
            28,
            9,
            30,
            0,
            TimeSpan.Zero);
        var reads = new RecordingHelpReadPort
        {
            ValidationData = ValidCreateData(),
            AdminArticle = Article(articleId)
        };
        var writes = new RecordingHelpWritePort { CreatedArticleId = articleId };
        var unitOfWork = new RecordingUnitOfWork();
        var service = CreateService(
            reads,
            writes,
            unitOfWork,
            new FixedTimeProvider(expectedTime));

        var result = await service.CreateAsync(
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
        var writes = new RecordingHelpWritePort();
        var unitOfWork = new RecordingUnitOfWork();
        var service = CreateService(
            writes: writes,
            unitOfWork: unitOfWork);

        var result = await service.CreateAsync(
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
        var reads = new RecordingHelpReadPort
        {
            ValidationData = new HelpMutationValidationData(
                ArticleExists: false,
                SlugBelongsToAnotherArticle: false,
                ExistingAssetIds: new HashSet<Guid>())
        };
        var writes = new RecordingHelpWritePort();
        var unitOfWork = new RecordingUnitOfWork();
        var service = CreateService(reads, writes, unitOfWork);

        var result = await service.UpdateAsync(
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
        var foreignAssetId = Guid.NewGuid();
        var reads = new RecordingHelpReadPort
        {
            ValidationData = new HelpMutationValidationData(
                ArticleExists: true,
                SlugBelongsToAnotherArticle: false,
                ExistingAssetIds: new HashSet<Guid>())
        };
        var writes = new RecordingHelpWritePort();
        var unitOfWork = new RecordingUnitOfWork();
        var service = CreateService(reads, writes, unitOfWork);

        var result = await service.UpdateAsync(
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
        var writes = new RecordingHelpWritePort
        {
            PublicationResult = false,
            DeleteResult = false
        };
        var unitOfWork = new RecordingUnitOfWork();
        var service = CreateService(
            writes: writes,
            unitOfWork: unitOfWork);
        var actor = Actor(isAdmin: true);
        var articleId = Guid.NewGuid();

        var publication = await service.SetPublicationAsync(
            actor,
            articleId,
            true,
            CancellationToken.None);
        var deletion = await service.DeleteAsync(
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
        var articleId = Guid.NewGuid();
        var reads = new RecordingHelpReadPort
        {
            ValidationData = new HelpMutationValidationData(
                ArticleExists: true,
                SlugBelongsToAnotherArticle: false,
                ExistingAssetIds: new HashSet<Guid>()),
            AdminArticle = Article(articleId)
        };
        var writes = new RecordingHelpWritePort
        {
            UpdateResult = true,
            PublicationResult = true,
            DeleteResult = true
        };
        var unitOfWork = new RecordingUnitOfWork();
        var service = CreateService(reads, writes, unitOfWork);
        var actor = Actor(isAdmin: true);

        var update = await service.UpdateAsync(
            actor,
            articleId,
            ValidMutation(),
            CancellationToken.None);
        var publication = await service.SetPublicationAsync(
            actor,
            articleId,
            true,
            CancellationToken.None);
        var deletion = await service.DeleteAsync(
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
        var writes = new RecordingHelpWritePort();
        var unitOfWork = new RecordingUnitOfWork();
        var service = CreateService(
            writes: writes,
            unitOfWork: unitOfWork);
        var actor = Actor(isCompanyUser: true);

        var create = await service.CreateAsync(
            actor,
            ValidMutation(),
            CancellationToken.None);
        var update = await service.UpdateAsync(
            actor,
            Guid.NewGuid(),
            ValidMutation(),
            CancellationToken.None);
        var publication = await service.SetPublicationAsync(
            actor,
            Guid.NewGuid(),
            true,
            CancellationToken.None);
        var deletion = await service.DeleteAsync(
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
        using var cancellation = new CancellationTokenSource();
        var articleId = Guid.NewGuid();
        var reads = new RecordingHelpReadPort
        {
            ValidationData = ValidCreateData(),
            AdminArticle = Article(articleId)
        };
        var writes = new RecordingHelpWritePort { CreatedArticleId = articleId };
        var unitOfWork = new RecordingUnitOfWork();
        var service = CreateService(reads, writes, unitOfWork);

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
        var fields = result.Errors
            .Select(error => error.Field)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var field in expectedFields)
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
