// File summary: Provides Help/FAQ CMS read and article lifecycle workflows for public browsing and admin management.
// Major updates:
// - 2026-07-09 pending Moved Help CMS article write orchestration out of the API controller.
// - 2026-07-09 pending Moved Help CMS read, search, filtering, and detail composition out of the API controller.

using MediatR;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;

namespace RvtPortal.Spa.Application.Help;

public interface IHelpApplicationService
{
    // Function summary: Returns published Help CMS sections and articles for user browsing.
    Task<HelpOverviewModel> QueryAsync(string? searchText, CancellationToken cancellationToken);

    // Function summary: Returns all Help CMS sections and filtered articles for admin management.
    Task<HelpAdminOverviewModel> QueryAdminAsync(
        string? searchText,
        string? status,
        string? contentType,
        CancellationToken cancellationToken);

    // Function summary: Returns one published Help CMS article by slug, or null when hidden or absent.
    Task<HelpArticleModel?> GetPublishedArticleAsync(string slug, CancellationToken cancellationToken);

    // Function summary: Returns one Help CMS article by id for admin workflows, or null when absent.
    Task<HelpArticleModel?> GetAdminArticleAsync(Guid id, CancellationToken cancellationToken);

    // Function summary: Creates a Help CMS article and returns its refreshed admin model.
    Task<HelpArticleMutationWorkflowResult> CreateArticleAsync(
        HelpArticleMutationRequest request,
        CancellationToken cancellationToken);

    // Function summary: Updates a Help CMS article and returns its refreshed admin model.
    Task<HelpArticleMutationWorkflowResult> UpdateArticleAsync(
        Guid articleId,
        HelpArticleMutationRequest request,
        CancellationToken cancellationToken);

    // Function summary: Sets Help CMS article publication state and returns its refreshed admin model.
    Task<HelpArticleMutationWorkflowResult> SetArticlePublicationAsync(
        Guid articleId,
        bool isPublished,
        CancellationToken cancellationToken);

    // Function summary: Deletes a Help CMS article.
    Task<HelpArticleMutationWorkflowResult> DeleteArticleAsync(Guid articleId, CancellationToken cancellationToken);
}

public sealed class HelpOverviewModel
{
    public string SearchText { get; init; } = "";
    public List<HelpSectionModel> Sections { get; init; } = [];
}

public sealed class HelpSectionModel
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public string Slug { get; init; } = "";
    public int SortOrder { get; init; }
    public List<HelpArticleSummaryModel> Articles { get; init; } = [];
}

public class HelpArticleSummaryModel
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public string Slug { get; init; } = "";
    public string? Summary { get; init; }
    public string ContentType { get; init; } = "";
    public string SectionTitle { get; init; } = "";
    public string SectionSlug { get; init; } = "";
    public int SectionSortOrder { get; init; }
    public int SortOrder { get; init; }
}

public sealed class HelpArticleModel : HelpArticleSummaryModel
{
    public string Body { get; init; } = "";
    public bool IsPublished { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public List<HelpAssetModel> Assets { get; init; } = [];
}

public sealed class HelpAssetModel
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public string AssetType { get; init; } = "";
    public string Url { get; init; } = "";
    public string? InternalPath { get; init; }
    public int SortOrder { get; init; }
}

public sealed class HelpAdminOverviewModel
{
    public string SearchText { get; init; } = "";
    public string Status { get; init; } = "All";
    public string ContentType { get; init; } = "All";
    public List<HelpSectionModel> Sections { get; init; } = [];
    public List<HelpArticleModel> Articles { get; init; } = [];
}

public sealed class HelpArticleMutationWorkflowResult
{
    public bool NotFound { get; init; }
    public Guid? ArticleId { get; init; }
    public string? Slug { get; init; }
    public HelpArticleModel? Article { get; init; }
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();

    // Function summary: Builds a workflow result from a command result and optional refreshed article model.
    public static HelpArticleMutationWorkflowResult FromCommand(
        HelpArticleCommandResult result,
        HelpArticleModel? article = null)
    {
        return new HelpArticleMutationWorkflowResult
        {
            NotFound = result.NotFound,
            ArticleId = result.ArticleId,
            Slug = result.Slug,
            Article = article,
            Errors = result.Errors
        };
    }
}

public sealed class HelpApplicationService : IHelpApplicationService
{
    private readonly RVTDbContext domainContext;
    private readonly IMediator mediator;

    // Function summary: Initializes Help CMS workflows with domain reads and transactional command dispatch dependencies.
    public HelpApplicationService(
        RVTDbContext domainContext,
        IMediator mediator)
    {
        this.domainContext = domainContext;
        this.mediator = mediator;
    }

    // Function summary: Returns published Help CMS sections and articles for user browsing.
    public async Task<HelpOverviewModel> QueryAsync(string? searchText, CancellationToken cancellationToken)
    {
        var sections = await domainContext.HelpSections
            .AsNoTracking()
            .Include(section => section.Articles)
            .Where(section => section.IsPublished)
            .OrderBy(section => section.SortOrder)
            .ThenBy(section => section.Title)
            .ToListAsync(cancellationToken);
        var search = searchText?.Trim();

        return new HelpOverviewModel
        {
            SearchText = search ?? "",
            Sections = sections
                .Select(section => BuildSectionModel(section, search))
                .Where(section => section.Articles.Count > 0)
                .ToList()
        };
    }

    // Function summary: Returns all Help CMS sections and filtered articles for admin management.
    public async Task<HelpAdminOverviewModel> QueryAdminAsync(
        string? searchText,
        string? status,
        string? contentType,
        CancellationToken cancellationToken)
    {
        var search = searchText?.Trim();
        var statusFilter = string.IsNullOrWhiteSpace(status) ? "All" : status.Trim();
        var contentTypeFilter = string.IsNullOrWhiteSpace(contentType) ? "All" : contentType.Trim();
        var articles = await domainContext.HelpArticles
            .AsNoTracking()
            .Include(article => article.Section)
            .Include(article => article.Assets)
            .ToListAsync(cancellationToken);
        var sections = await domainContext.HelpSections
            .AsNoTracking()
            .Include(section => section.Articles)
            .OrderBy(section => section.SortOrder)
            .ThenBy(section => section.Title)
            .ToListAsync(cancellationToken);

        var filteredArticles = articles
            .Where(article => MatchesAdminSearch(article, search))
            .Where(article => MatchesStatus(article, statusFilter))
            .Where(article => MatchesContentType(article, contentTypeFilter))
            .OrderBy(article => article.Section?.SortOrder ?? 0)
            .ThenBy(article => article.Section?.Title)
            .ThenBy(article => article.SortOrder)
            .ThenBy(article => article.Title)
            .Select(BuildArticleModel)
            .ToList();

        return new HelpAdminOverviewModel
        {
            SearchText = search ?? "",
            Status = statusFilter,
            ContentType = contentTypeFilter,
            Sections = sections.Select(section => BuildAdminSectionModel(section, search)).ToList(),
            Articles = filteredArticles
        };
    }

    // Function summary: Returns one published Help CMS article by slug, or null when hidden or absent.
    public async Task<HelpArticleModel?> GetPublishedArticleAsync(string slug, CancellationToken cancellationToken)
    {
        var article = await domainContext.HelpArticles
            .AsNoTracking()
            .Include(item => item.Section)
            .Include(item => item.Assets)
            .SingleOrDefaultAsync(
                item => item.Slug == slug && item.IsPublished && item.Section != null && item.Section.IsPublished,
                cancellationToken);
        return article == null ? null : BuildArticleModel(article);
    }

    // Function summary: Returns one Help CMS article by id for admin workflows, or null when absent.
    public async Task<HelpArticleModel?> GetAdminArticleAsync(Guid id, CancellationToken cancellationToken)
    {
        var article = await domainContext.HelpArticles
            .AsNoTracking()
            .Include(item => item.Section)
            .Include(item => item.Assets)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return article == null ? null : BuildArticleModel(article);
    }

    // Function summary: Creates a Help CMS article through the transactional command pipeline and reloads its admin model.
    public async Task<HelpArticleMutationWorkflowResult> CreateArticleAsync(
        HelpArticleMutationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateHelpArticleCommand(request), cancellationToken);
        var article = result.ArticleId.HasValue && result.Errors.Count == 0
            ? await GetAdminArticleAsync(result.ArticleId.Value, cancellationToken)
            : null;
        return HelpArticleMutationWorkflowResult.FromCommand(result, article);
    }

    // Function summary: Updates a Help CMS article through the transactional command pipeline and reloads its admin model.
    public async Task<HelpArticleMutationWorkflowResult> UpdateArticleAsync(
        Guid articleId,
        HelpArticleMutationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateHelpArticleCommand(articleId, request), cancellationToken);
        var article = !result.NotFound && result.Errors.Count == 0
            ? await GetAdminArticleAsync(articleId, cancellationToken)
            : null;
        return HelpArticleMutationWorkflowResult.FromCommand(result, article);
    }

    // Function summary: Sets Help CMS article publication through the transactional command pipeline and reloads its admin model.
    public async Task<HelpArticleMutationWorkflowResult> SetArticlePublicationAsync(
        Guid articleId,
        bool isPublished,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SetHelpArticlePublicationCommand(articleId, isPublished), cancellationToken);
        var article = !result.NotFound
            ? await GetAdminArticleAsync(articleId, cancellationToken)
            : null;
        return HelpArticleMutationWorkflowResult.FromCommand(result, article);
    }

    // Function summary: Deletes a Help CMS article through the transactional command pipeline.
    public async Task<HelpArticleMutationWorkflowResult> DeleteArticleAsync(Guid articleId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteHelpArticleCommand(articleId), cancellationToken);
        return HelpArticleMutationWorkflowResult.FromCommand(result);
    }

    // Function summary: Builds a Help CMS section model and applies optional published-search filtering.
    private static HelpSectionModel BuildSectionModel(HelpSection section, string? search)
    {
        var articles = section.Articles
            .Where(article => article.IsPublished)
            .Where(article => string.IsNullOrWhiteSpace(search) ||
                Contains(article.Title, search) ||
                Contains(article.Summary, search) ||
                Contains(article.Body, search) ||
                Contains(article.ContentType, search))
            .OrderBy(article => article.SortOrder)
            .ThenBy(article => article.Title)
            .Select(BuildArticleSummaryModel)
            .ToList();

        return new HelpSectionModel
        {
            Id = section.Id,
            Title = section.Title,
            Slug = section.Slug,
            SortOrder = section.SortOrder,
            Articles = articles
        };
    }

    // Function summary: Builds a Help CMS section model for admin management.
    private static HelpSectionModel BuildAdminSectionModel(HelpSection section, string? search)
    {
        return new HelpSectionModel
        {
            Id = section.Id,
            Title = section.Title,
            Slug = section.Slug,
            SortOrder = section.SortOrder,
            Articles = section.Articles
                .Where(article => MatchesAdminSearch(article, search))
                .OrderBy(article => article.SortOrder)
                .ThenBy(article => article.Title)
                .Select(BuildArticleSummaryModel)
                .ToList()
        };
    }

    // Function summary: Builds a Help CMS article summary model.
    private static HelpArticleSummaryModel BuildArticleSummaryModel(HelpArticle article)
    {
        return new HelpArticleSummaryModel
        {
            Id = article.Id,
            Title = article.Title,
            Slug = article.Slug,
            Summary = article.Summary,
            ContentType = article.ContentType,
            SectionTitle = article.Section?.Title ?? "",
            SectionSlug = article.Section?.Slug ?? "",
            SectionSortOrder = article.Section?.SortOrder ?? 0,
            SortOrder = article.SortOrder
        };
    }

    // Function summary: Builds a Help CMS article detail model.
    private static HelpArticleModel BuildArticleModel(HelpArticle article)
    {
        return new HelpArticleModel
        {
            Id = article.Id,
            Title = article.Title,
            Slug = article.Slug,
            Summary = article.Summary,
            Body = article.Body,
            ContentType = article.ContentType,
            SectionTitle = article.Section?.Title ?? "",
            SectionSlug = article.Section?.Slug ?? "",
            SectionSortOrder = article.Section?.SortOrder ?? 0,
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
    }

    // Function summary: Evaluates whether a value contains search text.
    private static bool Contains(string? value, string search)
    {
        return value?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    // Function summary: Evaluates Help CMS admin search matching for an article.
    private static bool MatchesAdminSearch(HelpArticle article, string? search)
    {
        return string.IsNullOrWhiteSpace(search) ||
            Contains(article.Title, search) ||
            Contains(article.Summary, search) ||
            Contains(article.Body, search) ||
            Contains(article.ContentType, search) ||
            Contains(article.Section?.Title, search);
    }

    // Function summary: Evaluates Help CMS admin publication status filtering.
    private static bool MatchesStatus(HelpArticle article, string status)
    {
        return status.Equals("All", StringComparison.OrdinalIgnoreCase) ||
            (status.Equals("Published", StringComparison.OrdinalIgnoreCase) && article.IsPublished) ||
            (status.Equals("Draft", StringComparison.OrdinalIgnoreCase) && !article.IsPublished);
    }

    // Function summary: Evaluates Help CMS admin content type filtering.
    private static bool MatchesContentType(HelpArticle article, string contentType)
    {
        return contentType.Equals("All", StringComparison.OrdinalIgnoreCase) ||
            article.ContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase);
    }
}
