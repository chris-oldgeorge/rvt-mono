// File summary: Covers EF Core adapters for published and administrative Help persistence.
// Major updates:
// - 2026-07-28 Added RED coverage for the Help read adapter.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Application.Help;
using RvtPortal.Spa.Adapters.Help;

namespace RvtPortal.Spa.Tests;

public sealed class HelpAdapterTests
{
    [Fact]
    public async Task ReadAdapter_PublishedQueryFiltersSearchesAndOrders()
    {
        var seeded = await SeedAsync();
        using var factory = seeded.Factory;
        using var scope = seeded.Factory.Services.CreateScope();
        var adapter = new EfHelpReadAdapter(
            scope.ServiceProvider.GetRequiredService<RVTDbContext>());

        var overview = await adapter.QueryPublishedAsync(
            searchText: null,
            CancellationToken.None);
        var searched = await adapter.QueryPublishedAsync(
            "DUST",
            CancellationToken.None);

        Assert.Equal(
            ["Alerts", "Data"],
            overview.Sections.Select(section => section.Title));
        Assert.Equal(
            ["Alert response", "Dust guide"],
            overview.Sections.SelectMany(section => section.Articles)
                .Select(article => article.Title));
        var searchedSection = Assert.Single(searched.Sections);
        Assert.Equal("Data", searchedSection.Title);
        Assert.Equal("Dust guide", Assert.Single(searchedSection.Articles).Title);
        Assert.DoesNotContain(
            overview.Sections.SelectMany(section => section.Articles),
            article => article.Title is "Draft FAQ" or "Hidden guide");
    }

    [Fact]
    public async Task ReadAdapter_AdminQueryAppliesStatusTypeAndSearchFilters()
    {
        var seeded = await SeedAsync();
        using var factory = seeded.Factory;
        using var scope = seeded.Factory.Services.CreateScope();
        var adapter = new EfHelpReadAdapter(
            scope.ServiceProvider.GetRequiredService<RVTDbContext>());

        var overview = await adapter.QueryAdminAsync(
            new HelpAdminQuery("draft", "Draft", "faq"),
            CancellationToken.None);

        var article = Assert.Single(overview.Articles);
        Assert.Equal(seeded.DraftArticleId, article.Id);
        Assert.Equal("Draft FAQ", article.Title);
        Assert.Equal("Draft", overview.Status);
        Assert.Equal("faq", overview.ContentType);
        Assert.Contains(
            overview.Sections,
            section => section.Id == seeded.DataSectionId &&
                section.Articles.Single().Id == seeded.DraftArticleId);
    }

    [Fact]
    public async Task ReadAdapter_DetailHonorsPublicationAndOrdersAssets()
    {
        var seeded = await SeedAsync();
        using var factory = seeded.Factory;
        using var scope = seeded.Factory.Services.CreateScope();
        var adapter = new EfHelpReadAdapter(
            scope.ServiceProvider.GetRequiredService<RVTDbContext>());

        var published = await adapter.GetPublishedArticleAsync(
            "dust-guide",
            CancellationToken.None);
        var hiddenDraft = await adapter.GetPublishedArticleAsync(
            "draft-faq",
            CancellationToken.None);
        var adminDraft = await adapter.GetAdminArticleAsync(
            seeded.DraftArticleId,
            CancellationToken.None);

        Assert.NotNull(published);
        Assert.Equal(
            ["Alpha asset", "Zeta asset"],
            published.Assets.Select(asset => asset.Title));
        Assert.Null(hiddenDraft);
        Assert.Equal("Draft FAQ", adminDraft?.Title);
    }

    [Fact]
    public async Task ReadAdapter_MutationValidationReportsSlugAndAssetOwnership()
    {
        var seeded = await SeedAsync();
        using var factory = seeded.Factory;
        using var scope = seeded.Factory.Services.CreateScope();
        var adapter = new EfHelpReadAdapter(
            scope.ServiceProvider.GetRequiredService<RVTDbContext>());

        var sameArticle = await adapter.GetMutationValidationDataAsync(
            "dust-guide",
            seeded.DustArticleId,
            CancellationToken.None);
        var newArticle = await adapter.GetMutationValidationDataAsync(
            "dust-guide",
            articleId: null,
            CancellationToken.None);

        Assert.True(sameArticle.ArticleExists);
        Assert.False(sameArticle.SlugBelongsToAnotherArticle);
        Assert.Equal(
            seeded.DustAssetIds.Order(),
            sameArticle.ExistingAssetIds.Order());
        Assert.False(newArticle.ArticleExists);
        Assert.True(newArticle.SlugBelongsToAnotherArticle);
        Assert.Empty(newArticle.ExistingAssetIds);
    }

    [Fact]
    public async Task WriteAdapter_CreateReusesSectionAndStagesCanonicalArticle()
    {
        var seeded = await SeedAsync();
        using var factory = seeded.Factory;
        using var scope = seeded.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RVTDbContext>();
        var adapter = new EfHelpWriteAdapter(context);
        var now = new DateTime(2026, 7, 28, 10, 30, 0, DateTimeKind.Utc);
        var mutation = ValidatedMutation(
            sectionTitle: "Data centre",
            sectionSlug: "data",
            assets:
            [
                new HelpAssetMutation(
                    null,
                    "Internal guide",
                    "Document",
                    "/help-assets/internal.pdf",
                    1),
                new HelpAssetMutation(
                    null,
                    "External guide",
                    "Link",
                    "https://docs.rvt.test/external",
                    2)
            ]);

        var articleId = await adapter.CreateAsync(
            mutation,
            now,
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, articleId);
        Assert.Single(
            context.ChangeTracker.Entries<HelpArticle>(),
            entry => entry.State == EntityState.Added);
        Assert.Equal(
            2,
            context.ChangeTracker.Entries<HelpAsset>()
                .Count(entry => entry.State == EntityState.Added));

        await context.SaveChangesAsync();
        var article = await context.HelpArticles
            .Include(item => item.Section)
            .Include(item => item.Assets)
            .SingleAsync(item => item.Id == articleId);

        Assert.Equal(seeded.DataSectionId, article.SectionId);
        Assert.Equal("Data centre", article.Section!.Title);
        Assert.Equal(now, article.CreatedAtUtc);
        Assert.Equal(now, article.UpdatedAtUtc);
        Assert.All(article.Assets, asset => Assert.NotEqual(Guid.Empty, asset.Id));
        Assert.Equal(
            "/help-assets/internal.pdf",
            article.Assets.Single(asset => asset.Title == "Internal guide").InternalPath);
        Assert.Null(
            article.Assets.Single(asset => asset.Title == "External guide").InternalPath);
    }

    [Fact]
    public async Task WriteAdapter_CreateAddsCanonicalPublishedSectionWhenMissing()
    {
        using var factory = new SpaTestApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RVTDbContext>();
        var adapter = new EfHelpWriteAdapter(context);
        var mutation = ValidatedMutation(
            sectionTitle: "Getting started",
            sectionSlug: "getting-started");

        var articleId = await adapter.CreateAsync(
            mutation,
            new DateTime(2026, 7, 28, 11, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var section = await context.HelpSections
            .Include(item => item.Articles)
            .SingleAsync(item => item.Slug == "getting-started");
        Assert.NotEqual(Guid.Empty, section.Id);
        Assert.True(section.IsPublished);
        Assert.Equal(articleId, Assert.Single(section.Articles).Id);
    }

    [Fact]
    public async Task WriteAdapter_UpdateMovesArticleAndReconcilesAssetsById()
    {
        var seeded = await SeedAsync();
        using var factory = seeded.Factory;
        using var scope = seeded.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RVTDbContext>();
        var adapter = new EfHelpWriteAdapter(context);
        var retainedAssetId = seeded.DustAssetIds[0];
        var omittedAssetId = seeded.DustAssetIds[1];
        var now = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
        var mutation = ValidatedMutation(
            sectionTitle: "Alerts",
            sectionSlug: "alerts",
            title: "Updated dust guide",
            assets:
            [
                new HelpAssetMutation(
                    retainedAssetId,
                    "Updated internal guide",
                    "Document",
                    "https://docs.rvt.test/updated",
                    3),
                new HelpAssetMutation(
                    null,
                    "New internal guide",
                    "Document",
                    "/help-assets/new.pdf",
                    4)
            ]);

        var updated = await adapter.UpdateAsync(
            seeded.DustArticleId,
            mutation,
            now,
            CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.True(updated);
        var article = await context.HelpArticles
            .Include(item => item.Section)
            .Include(item => item.Assets)
            .SingleAsync(item => item.Id == seeded.DustArticleId);
        Assert.Equal("alerts", article.Section!.Slug);
        Assert.Equal("Updated dust guide", article.Title);
        Assert.Equal(now, article.UpdatedAtUtc);
        Assert.Contains(article.Assets, asset => asset.Id == retainedAssetId);
        Assert.DoesNotContain(article.Assets, asset => asset.Id == omittedAssetId);
        var newAsset = Assert.Single(
            article.Assets,
            asset => asset.Id != retainedAssetId);
        Assert.NotEqual(Guid.Empty, newAsset.Id);
        Assert.Equal("/help-assets/new.pdf", newAsset.InternalPath);
        Assert.Null(
            article.Assets.Single(asset => asset.Id == retainedAssetId).InternalPath);
    }

    [Fact]
    public async Task WriteAdapter_UpdateRejectsForeignAssetWithoutStagingChanges()
    {
        var seeded = await SeedAsync();
        using var factory = seeded.Factory;
        using var scope = seeded.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RVTDbContext>();
        var adapter = new EfHelpWriteAdapter(context);
        var mutation = ValidatedMutation(
            title: "Must not be staged",
            assets:
            [
                new HelpAssetMutation(
                    Guid.NewGuid(),
                    "Foreign",
                    "Link",
                    "https://docs.rvt.test/foreign",
                    0)
            ]);

        var updated = await adapter.UpdateAsync(
            seeded.DustArticleId,
            mutation,
            new DateTime(2026, 7, 28, 13, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        Assert.False(updated);
        Assert.All(
            context.ChangeTracker.Entries(),
            entry => Assert.Equal(EntityState.Unchanged, entry.State));
        Assert.Equal(
            "Dust guide",
            (await context.HelpArticles.SingleAsync(
                article => article.Id == seeded.DustArticleId)).Title);
    }

    [Fact]
    public async Task WriteAdapter_PublicationAndDeletionReportMissingAndUseSuppliedUtc()
    {
        var seeded = await SeedAsync();
        using var factory = seeded.Factory;
        using var scope = seeded.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RVTDbContext>();
        var adapter = new EfHelpWriteAdapter(context);
        var now = new DateTime(2026, 7, 28, 14, 0, 0, DateTimeKind.Utc);

        var published = await adapter.SetPublicationAsync(
            seeded.DraftArticleId,
            isPublished: true,
            now,
            CancellationToken.None);
        var missingPublication = await adapter.SetPublicationAsync(
            Guid.NewGuid(),
            isPublished: true,
            now,
            CancellationToken.None);
        var deleted = await adapter.DeleteAsync(
            seeded.DustArticleId,
            CancellationToken.None);
        var missingDelete = await adapter.DeleteAsync(
            Guid.NewGuid(),
            CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.True(published);
        Assert.False(missingPublication);
        Assert.True(deleted);
        Assert.False(missingDelete);
        var draft = await context.HelpArticles.SingleAsync(
            article => article.Id == seeded.DraftArticleId);
        Assert.True(draft.IsPublished);
        Assert.Equal(now, draft.UpdatedAtUtc);
        Assert.False(await context.HelpArticles.AnyAsync(
            article => article.Id == seeded.DustArticleId));
    }

    private static ValidatedHelpArticleMutation ValidatedMutation(
        string sectionTitle = "Data",
        string sectionSlug = "data",
        string title = "New guide",
        IReadOnlyList<HelpAssetMutation>? assets = null)
    {
        var result = HelpMutationValidator.ValidateShape(
            new HelpArticleMutation(
                sectionTitle,
                sectionSlug,
                title,
                "new-guide",
                "Summary",
                "Body",
                "FAQ",
                true,
                2,
                1,
                assets ?? []));

        Assert.True(result.IsValid);
        return result.Value!;
    }

    private static async Task<SeededHelpData> SeedAsync()
    {
        var factory = new SpaTestApplicationFactory();
        var alertsSectionId = Guid.NewGuid();
        var dataSectionId = Guid.NewGuid();
        var hiddenSectionId = Guid.NewGuid();
        var alertArticleId = Guid.NewGuid();
        var dustArticleId = Guid.NewGuid();
        var draftArticleId = Guid.NewGuid();
        var hiddenArticleId = Guid.NewGuid();
        var dustAssetIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var now = new DateTime(2026, 7, 28, 8, 0, 0, DateTimeKind.Utc);

        await factory.SeedDomainEntitiesAsync(
            new HelpSection
            {
                Id = alertsSectionId,
                Title = "Alerts",
                Slug = "alerts",
                SortOrder = 1,
                IsPublished = true,
                Articles =
                [
                    new HelpArticle
                    {
                        Id = alertArticleId,
                        SectionId = alertsSectionId,
                        Title = "Alert response",
                        Slug = "alert-response",
                        Summary = "Respond to alerts",
                        Body = "Alert body",
                        ContentType = "Article",
                        IsPublished = true,
                        SortOrder = 1,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    }
                ]
            },
            new HelpSection
            {
                Id = dataSectionId,
                Title = "Data",
                Slug = "data",
                SortOrder = 2,
                IsPublished = true,
                Articles =
                [
                    new HelpArticle
                    {
                        Id = draftArticleId,
                        SectionId = dataSectionId,
                        Title = "Draft FAQ",
                        Slug = "draft-faq",
                        Summary = "Draft summary",
                        Body = "Draft body",
                        ContentType = "FAQ",
                        IsPublished = false,
                        SortOrder = 1,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    },
                    new HelpArticle
                    {
                        Id = dustArticleId,
                        SectionId = dataSectionId,
                        Title = "Dust guide",
                        Slug = "dust-guide",
                        Summary = "Dust readings",
                        Body = "Understand DUST values",
                        ContentType = "FAQ",
                        IsPublished = true,
                        SortOrder = 2,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now,
                        Assets =
                        [
                            new HelpAsset
                            {
                                Id = dustAssetIds[1],
                                HelpArticleId = dustArticleId,
                                Title = "Zeta asset",
                                AssetType = "Link",
                                Url = "https://docs.rvt.test/zeta",
                                SortOrder = 2
                            },
                            new HelpAsset
                            {
                                Id = dustAssetIds[0],
                                HelpArticleId = dustArticleId,
                                Title = "Alpha asset",
                                AssetType = "Document",
                                Url = "/help-assets/alpha.pdf",
                                InternalPath = "/help-assets/alpha.pdf",
                                SortOrder = 1
                            }
                        ]
                    }
                ]
            },
            new HelpSection
            {
                Id = hiddenSectionId,
                Title = "Hidden",
                Slug = "hidden",
                SortOrder = 0,
                IsPublished = false,
                Articles =
                [
                    new HelpArticle
                    {
                        Id = hiddenArticleId,
                        SectionId = hiddenSectionId,
                        Title = "Hidden guide",
                        Slug = "hidden-guide",
                        Body = "Hidden body",
                        ContentType = "FAQ",
                        IsPublished = true,
                        SortOrder = 0,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    }
                ]
            });

        return new SeededHelpData(
            factory,
            dataSectionId,
            dustArticleId,
            draftArticleId,
            dustAssetIds);
    }

    private sealed record SeededHelpData(
        SpaTestApplicationFactory Factory,
        Guid DataSectionId,
        Guid DustArticleId,
        Guid DraftArticleId,
        IReadOnlyList<Guid> DustAssetIds);
}
