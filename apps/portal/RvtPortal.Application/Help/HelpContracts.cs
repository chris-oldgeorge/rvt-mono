// File summary: Defines transport-neutral contracts for published and administrative Help workflows.
// Major updates:
// - 2026-07-28 Added the standalone Help application boundary contracts and immutable asset IDs.

namespace RvtPortal.Application.Help;

public sealed record HelpAdminQuery(
    string? SearchText,
    string? Status,
    string? ContentType);

public sealed record HelpAssetMutation(
    Guid? Id,
    string Title,
    string AssetType,
    string Url,
    int SortOrder);

public sealed record HelpArticleMutation(
    string SectionTitle,
    string SectionSlug,
    string Title,
    string Slug,
    string? Summary,
    string Body,
    string ContentType,
    bool IsPublished,
    int SectionSortOrder,
    int SortOrder,
    IReadOnlyList<HelpAssetMutation> Assets);

public sealed record HelpMutationValidationData(
    bool ArticleExists,
    bool SlugBelongsToAnotherArticle,
    IReadOnlySet<Guid> ExistingAssetIds);

public sealed record HelpDeleteResult(Guid ArticleId);

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
