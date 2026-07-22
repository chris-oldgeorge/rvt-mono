// File summary: Exposes API contracts for the RVT Cloud Help/FAQ mini-CMS.
// Major updates:
// - 2026-06-10 pending Expanded Help CMS contracts for admin listing, editing, and publish controls.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-08 pending Added Help CMS contracts for published browsing and admin editing.

namespace RvtPortal.Spa.Api;

public class HelpOverviewResponse
{
    public string SearchText { get; set; } = "";
    public List<HelpSectionResponse> Sections { get; set; } = [];
}

public class HelpSectionResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public int SortOrder { get; set; }
    public List<HelpArticleSummaryResponse> Articles { get; set; } = [];
}

public class HelpArticleSummaryResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Summary { get; set; }
    public string ContentType { get; set; } = "";
    public string SectionTitle { get; set; } = "";
    public string SectionSlug { get; set; } = "";
    public int SectionSortOrder { get; set; }
    public int SortOrder { get; set; }
}

public class HelpArticleResponse : HelpArticleSummaryResponse
{
    public string Body { get; set; } = "";
    public bool IsPublished { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<HelpAssetResponse> Assets { get; set; } = [];
}

public class HelpAdminOverviewResponse
{
    public string SearchText { get; set; } = "";
    public string Status { get; set; } = "All";
    public string ContentType { get; set; } = "All";
    public List<HelpSectionResponse> Sections { get; set; } = [];
    public List<HelpArticleResponse> Articles { get; set; } = [];
}

public class HelpPublishRequest
{
    public required bool IsPublished { get; set; }
}

public class HelpAssetResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string AssetType { get; set; } = "";
    public string Url { get; set; } = "";
    public string? InternalPath { get; set; }
    public int SortOrder { get; set; }
}

public class HelpArticleMutationRequest
{
    public string SectionTitle { get; set; } = "";
    public string SectionSlug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Summary { get; set; }
    public string Body { get; set; } = "";
    public string ContentType { get; set; } = "FAQ";
    public required bool IsPublished { get; set; }
    public required int SectionSortOrder { get; set; }
    public required int SortOrder { get; set; }
    public List<HelpAssetMutationRequest> Assets { get; set; } = [];
}

public class HelpAssetMutationRequest
{
    public string Title { get; set; } = "";
    public string AssetType { get; set; } = "Document";
    public string Url { get; set; } = "";
    public string? InternalPath { get; set; }
    public required int SortOrder { get; set; }
}
