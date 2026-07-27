// File summary: Covers EF Core adapters for published and administrative Help persistence.
// Major updates:
// - 2026-07-28 Added RED coverage for the Help read adapter.

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
