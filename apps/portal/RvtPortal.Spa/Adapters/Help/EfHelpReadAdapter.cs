// File summary: Adapts PostgreSQL-backed Help content reads to transport-neutral application models.
// Major updates:
// - 2026-07-28 Extracted published/admin filtering, ordering, projection, and mutation validation behind IHelpReadPort.

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Application.Help;
using RvtPortal.Application.Help.Ports;

namespace RvtPortal.Spa.Adapters.Help;

public sealed class EfHelpReadAdapter(RVTDbContext domainContext) : IHelpReadPort
{
    public async Task<HelpOverviewModel> QueryPublishedAsync(
        string? searchText,
        CancellationToken cancellationToken)
    {
        var search = NormalizeSearch(searchText);
        var articleQuery = domainContext.HelpArticles
            .AsNoTracking()
            .Where(article =>
                article.IsPublished &&
                article.Section != null &&
                article.Section.IsPublished);
        articleQuery = ApplySearch(
            articleQuery,
            search,
            includeSectionTitle: false);

        var articles = await articleQuery
            .OrderBy(article => article.Section!.SortOrder)
            .ThenBy(article => article.Section!.Title)
            .ThenBy(article => article.SortOrder)
            .ThenBy(article => article.Title)
            .Select(articleSummaryProjection)
            .ToListAsync(cancellationToken);
        var sectionIds = articles
            .Select(article => article.SectionId)
            .Distinct()
            .ToList();
        var sections = await domainContext.HelpSections
            .AsNoTracking()
            .Where(section => section.IsPublished && sectionIds.Contains(section.Id))
            .OrderBy(section => section.SortOrder)
            .ThenBy(section => section.Title)
            .Select(section => new HelpSectionRow(
                section.Id,
                section.Title,
                section.Slug,
                section.SortOrder))
            .ToListAsync(cancellationToken);

        return new HelpOverviewModel
        {
            SearchText = searchText?.Trim() ?? "",
            Sections = BuildSections(sections, articles)
        };
    }

    public async Task<HelpArticleModel?> GetPublishedArticleAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        return await domainContext.HelpArticles
            .AsNoTracking()
            .Where(article =>
                article.Slug == slug &&
                article.IsPublished &&
                article.Section != null &&
                article.Section.IsPublished)
            .Select(articleProjection)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<HelpAdminOverviewModel> QueryAdminAsync(
        HelpAdminQuery query,
        CancellationToken cancellationToken)
    {
        var search = NormalizeSearch(query.SearchText);
        var status = string.IsNullOrWhiteSpace(query.Status)
            ? "All"
            : query.Status.Trim();
        var contentType = string.IsNullOrWhiteSpace(query.ContentType)
            ? "All"
            : query.ContentType.Trim();
        var articleQuery = domainContext.HelpArticles.AsNoTracking();
        articleQuery = ApplySearch(
            articleQuery,
            search,
            includeSectionTitle: true);
        if (status.Equals("Published", StringComparison.OrdinalIgnoreCase))
        {
            articleQuery = articleQuery.Where(article => article.IsPublished);
        }
        else if (status.Equals("Draft", StringComparison.OrdinalIgnoreCase))
        {
            articleQuery = articleQuery.Where(article => !article.IsPublished);
        }

        if (!contentType.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (IsInMemory)
            {
                articleQuery = articleQuery.Where(article =>
                    article.ContentType.Equals(
                        contentType,
                        StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                var pattern = EscapeLikePattern(contentType);
                articleQuery = articleQuery.Where(article =>
                    EF.Functions.ILike(article.ContentType, pattern, "\\"));
            }
        }

        var articles = await articleQuery
            .OrderBy(article => article.Section == null ? 0 : article.Section.SortOrder)
            .ThenBy(article => article.Section == null ? "" : article.Section.Title)
            .ThenBy(article => article.SortOrder)
            .ThenBy(article => article.Title)
            .Select(articleProjection)
            .ToListAsync(cancellationToken);

        var sectionArticleQuery = ApplySearch(
            domainContext.HelpArticles.AsNoTracking(),
            search,
            includeSectionTitle: true);
        var sectionArticles = await sectionArticleQuery
            .OrderBy(article => article.SortOrder)
            .ThenBy(article => article.Title)
            .Select(articleSummaryProjection)
            .ToListAsync(cancellationToken);
        var sections = await domainContext.HelpSections
            .AsNoTracking()
            .OrderBy(section => section.SortOrder)
            .ThenBy(section => section.Title)
            .Select(section => new HelpSectionRow(
                section.Id,
                section.Title,
                section.Slug,
                section.SortOrder))
            .ToListAsync(cancellationToken);

        return new HelpAdminOverviewModel
        {
            SearchText = query.SearchText?.Trim() ?? "",
            Status = status,
            ContentType = contentType,
            Sections = BuildSections(sections, sectionArticles),
            Articles = articles
        };
    }

    public async Task<HelpArticleModel?> GetAdminArticleAsync(
        Guid articleId,
        CancellationToken cancellationToken)
    {
        return await domainContext.HelpArticles
            .AsNoTracking()
            .Where(article => article.Id == articleId)
            .Select(articleProjection)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<HelpMutationValidationData> GetMutationValidationDataAsync(
        string slug,
        Guid? articleId,
        CancellationToken cancellationToken)
    {
        var articleExists = articleId.HasValue &&
            await domainContext.HelpArticles
                .AsNoTracking()
                .AnyAsync(article => article.Id == articleId.Value, cancellationToken);
        var slugBelongsToAnotherArticle = await domainContext.HelpArticles
            .AsNoTracking()
            .AnyAsync(
                article => article.Slug == slug && article.Id != articleId,
                cancellationToken);
        IReadOnlySet<Guid> assetIds = articleId.HasValue
            ? [.. await domainContext.HelpAssets
                .AsNoTracking()
                .Where(asset => asset.HelpArticleId == articleId.Value)
                .Select(asset => asset.Id)
                .ToListAsync(cancellationToken)]
            : new HashSet<Guid>();

        return new HelpMutationValidationData(
            articleExists,
            slugBelongsToAnotherArticle,
            assetIds);
    }

    private IQueryable<HelpArticle> ApplySearch(
        IQueryable<HelpArticle> query,
        string? search,
        bool includeSectionTitle)
    {
        if (search is null)
        {
            return query;
        }

        if (IsInMemory)
        {
            return includeSectionTitle
                ? query.Where(article =>
                    article.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (article.Summary != null &&
                        article.Summary.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    article.Body.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    article.ContentType.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (article.Section != null &&
                        article.Section.Title.Contains(search, StringComparison.OrdinalIgnoreCase)))
                : query.Where(article =>
                    article.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (article.Summary != null &&
                        article.Summary.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    article.Body.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    article.ContentType.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var pattern = $"%{EscapeLikePattern(search)}%";
        return includeSectionTitle
            ? query.Where(article =>
                EF.Functions.ILike(article.Title, pattern, "\\") ||
                (article.Summary != null &&
                    EF.Functions.ILike(article.Summary, pattern, "\\")) ||
                EF.Functions.ILike(article.Body, pattern, "\\") ||
                EF.Functions.ILike(article.ContentType, pattern, "\\") ||
                (article.Section != null &&
                    EF.Functions.ILike(article.Section.Title, pattern, "\\")))
            : query.Where(article =>
                EF.Functions.ILike(article.Title, pattern, "\\") ||
                (article.Summary != null &&
                    EF.Functions.ILike(article.Summary, pattern, "\\")) ||
                EF.Functions.ILike(article.Body, pattern, "\\") ||
                EF.Functions.ILike(article.ContentType, pattern, "\\"));
    }

    private bool IsInMemory =>
        string.Equals(
            domainContext.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }

    private static List<HelpSectionModel> BuildSections(
        IReadOnlyList<HelpSectionRow> sections,
        IReadOnlyList<HelpArticleSummaryRow> articles)
    {
        var articlesBySection = articles
            .GroupBy(article => article.SectionId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(article => article.Model).ToList());
        return [.. sections
            .Select(section => new HelpSectionModel
            {
                Id = section.Id,
                Title = section.Title,
                Slug = section.Slug,
                SortOrder = section.SortOrder,
                Articles = articlesBySection.GetValueOrDefault(section.Id) ?? []
            })];
    }

    private static string? NormalizeSearch(string? searchText)
    {
        var trimmed = searchText?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static readonly Expression<Func<HelpArticle, HelpArticleSummaryRow>>
        articleSummaryProjection =
            article => new HelpArticleSummaryRow(
                article.SectionId,
                new HelpArticleSummaryModel
                {
                    Id = article.Id,
                    Title = article.Title,
                    Slug = article.Slug,
                    Summary = article.Summary,
                    ContentType = article.ContentType,
                    SectionTitle = article.Section == null ? "" : article.Section.Title,
                    SectionSlug = article.Section == null ? "" : article.Section.Slug,
                    SectionSortOrder = article.Section == null ? 0 : article.Section.SortOrder,
                    SortOrder = article.SortOrder
                });

    private static readonly Expression<Func<HelpArticle, HelpArticleModel>>
        articleProjection =
            article => new HelpArticleModel
            {
                Id = article.Id,
                Title = article.Title,
                Slug = article.Slug,
                Summary = article.Summary,
                Body = article.Body,
                ContentType = article.ContentType,
                SectionTitle = article.Section == null ? "" : article.Section.Title,
                SectionSlug = article.Section == null ? "" : article.Section.Slug,
                SectionSortOrder = article.Section == null ? 0 : article.Section.SortOrder,
                SortOrder = article.SortOrder,
                IsPublished = article.IsPublished,
                CreatedAtUtc = article.CreatedAtUtc,
                UpdatedAtUtc = article.UpdatedAtUtc,
                Assets = article.Assets
                    .OrderBy(asset => asset.SortOrder)
                    .ThenBy(asset => asset.Title)
                    .Select(asset => new HelpAssetModel
                    {
                        Id = asset.Id,
                        Title = asset.Title,
                        AssetType = asset.AssetType,
                        Url = asset.Url,
                        InternalPath = asset.InternalPath,
                        SortOrder = asset.SortOrder
                    })
                    .ToList()
            };

    private sealed record HelpSectionRow(
        Guid Id,
        string Title,
        string Slug,
        int SortOrder);

    private sealed record HelpArticleSummaryRow(
        Guid SectionId,
        HelpArticleSummaryModel Model);
}
