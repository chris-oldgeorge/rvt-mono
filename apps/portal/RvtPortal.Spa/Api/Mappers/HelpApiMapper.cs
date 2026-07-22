// File summary: Maps Help CMS application models to existing frontend-facing API contracts.
// Major updates:
// - 2026-07-09 pending Added Help CMS read-service mappers for controller cleanup.

using RvtPortal.Spa.Application.Help;

namespace RvtPortal.Spa.Api.Mappers;

public static class HelpApiMapper
{
    // Function summary: Maps a published Help overview model to the existing API response contract.
    public static HelpOverviewResponse ToOverviewResponse(HelpOverviewModel model)
    {
        return new HelpOverviewResponse
        {
            SearchText = model.SearchText,
            Sections = model.Sections.Select(ToSectionResponse).ToList()
        };
    }

    // Function summary: Maps an admin Help overview model to the existing API response contract.
    public static HelpAdminOverviewResponse ToAdminOverviewResponse(HelpAdminOverviewModel model)
    {
        return new HelpAdminOverviewResponse
        {
            SearchText = model.SearchText,
            Status = model.Status,
            ContentType = model.ContentType,
            Sections = model.Sections.Select(ToSectionResponse).ToList(),
            Articles = model.Articles.Select(ToArticleResponse).ToList()
        };
    }

    // Function summary: Maps a Help article detail model to the existing API response contract.
    public static HelpArticleResponse ToArticleResponse(HelpArticleModel model)
    {
        return new HelpArticleResponse
        {
            Id = model.Id,
            Title = model.Title,
            Slug = model.Slug,
            Summary = model.Summary,
            Body = model.Body,
            ContentType = model.ContentType,
            SectionTitle = model.SectionTitle,
            SectionSlug = model.SectionSlug,
            SortOrder = model.SortOrder,
            IsPublished = model.IsPublished,
            CreatedAtUtc = model.CreatedAtUtc,
            UpdatedAtUtc = model.UpdatedAtUtc,
            Assets = model.Assets.Select(ToAssetResponse).ToList()
        };
    }

    // Function summary: Maps a Help section model to the existing API section contract.
    private static HelpSectionResponse ToSectionResponse(HelpSectionModel model)
    {
        return new HelpSectionResponse
        {
            Id = model.Id,
            Title = model.Title,
            Slug = model.Slug,
            SortOrder = model.SortOrder,
            Articles = model.Articles.Select(ToArticleSummaryResponse).ToList()
        };
    }

    // Function summary: Maps a Help article summary model to the existing API summary contract.
    private static HelpArticleSummaryResponse ToArticleSummaryResponse(HelpArticleSummaryModel model)
    {
        return new HelpArticleSummaryResponse
        {
            Id = model.Id,
            Title = model.Title,
            Slug = model.Slug,
            Summary = model.Summary,
            ContentType = model.ContentType,
            SectionTitle = model.SectionTitle,
            SectionSlug = model.SectionSlug,
            SectionSortOrder = model.SectionSortOrder,
            SortOrder = model.SortOrder
        };
    }

    // Function summary: Maps a Help asset model to the existing API asset contract.
    private static HelpAssetResponse ToAssetResponse(HelpAssetModel model)
    {
        return new HelpAssetResponse
        {
            Id = model.Id,
            Title = model.Title,
            AssetType = model.AssetType,
            Url = model.Url,
            InternalPath = model.InternalPath,
            SortOrder = model.SortOrder
        };
    }
}
