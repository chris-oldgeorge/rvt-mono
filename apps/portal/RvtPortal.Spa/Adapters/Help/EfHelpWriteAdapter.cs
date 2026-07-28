// File summary: Stages PostgreSQL-backed Help content mutations without owning transaction commits.
// Major updates:
// - 2026-07-28 Added section reuse, immutable asset reconciliation, publication, and deletion staging.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Application.Help;
using RvtPortal.Application.Help.Ports;

namespace RvtPortal.Spa.Adapters.Help;

public sealed class EfHelpWriteAdapter(RVTDbContext domainContext) : IHelpWritePort
{
    public async Task<Guid> CreateAsync(
        ValidatedHelpArticleMutation mutation,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        HelpArticleMutation source = mutation.Source;
        HelpSection section = await GetOrCreateSectionAsync(source, cancellationToken);
        HelpArticle article = new()
        {
            Id = Guid.NewGuid(),
            SectionId = section.Id,
            Section = section,
            Title = source.Title,
            Slug = source.Slug,
            Summary = source.Summary,
            Body = source.Body,
            ContentType = source.ContentType,
            IsPublished = source.IsPublished,
            SortOrder = source.SortOrder,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
        article.Assets = [.. source.Assets.Select(asset => CreateAsset(article, asset))];
        domainContext.HelpArticles.Add(article);

        return article.Id;
    }

    public async Task<bool> UpdateAsync(
        Guid articleId,
        ValidatedHelpArticleMutation mutation,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        HelpArticle? article = await domainContext.HelpArticles
            .Include(item => item.Section)
            .Include(item => item.Assets)
            .SingleOrDefaultAsync(item => item.Id == articleId, cancellationToken);
        if (article is null)
        {
            return false;
        }

        Dictionary<Guid, HelpAsset> existingAssets = article.Assets.ToDictionary(asset => asset.Id);
        if (mutation.Source.Assets.Any(asset =>
            asset.Id.HasValue && !existingAssets.ContainsKey(asset.Id.Value)))
        {
            return false;
        }

        HelpArticleMutation source = mutation.Source;
        HelpSection section = await GetOrCreateSectionAsync(source, cancellationToken);
        article.SectionId = section.Id;
        article.Section = section;
        article.Title = source.Title;
        article.Slug = source.Slug;
        article.Summary = source.Summary;
        article.Body = source.Body;
        article.ContentType = source.ContentType;
        article.IsPublished = source.IsPublished;
        article.SortOrder = source.SortOrder;
        if (article.CreatedAtUtc == default)
        {
            article.CreatedAtUtc = nowUtc;
        }

        article.UpdatedAtUtc = nowUtc;
        ReconcileAssets(article, source.Assets, existingAssets);
        return true;
    }

    public async Task<bool> SetPublicationAsync(
        Guid articleId,
        bool isPublished,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        HelpArticle? article = await domainContext.HelpArticles
            .SingleOrDefaultAsync(item => item.Id == articleId, cancellationToken);
        if (article is null)
        {
            return false;
        }

        article.IsPublished = isPublished;
        article.UpdatedAtUtc = nowUtc;
        return true;
    }

    public async Task<bool> DeleteAsync(
        Guid articleId,
        CancellationToken cancellationToken)
    {
        HelpArticle? article = await domainContext.HelpArticles
            .SingleOrDefaultAsync(item => item.Id == articleId, cancellationToken);
        if (article is null)
        {
            return false;
        }

        domainContext.HelpArticles.Remove(article);
        return true;
    }

    private async Task<HelpSection> GetOrCreateSectionAsync(
        HelpArticleMutation source,
        CancellationToken cancellationToken)
    {
        HelpSection? section = await domainContext.HelpSections
            .SingleOrDefaultAsync(
                item => item.Slug == source.SectionSlug,
                cancellationToken);
        if (section is null)
        {
            section = new HelpSection
            {
                Id = Guid.NewGuid()
            };
            domainContext.HelpSections.Add(section);
        }

        section.Title = source.SectionTitle;
        section.Slug = source.SectionSlug;
        section.SortOrder = source.SectionSortOrder;
        section.IsPublished = true;
        return section;
    }

    private void ReconcileAssets(
        HelpArticle article,
        IReadOnlyList<HelpAssetMutation> mutations,
        IReadOnlyDictionary<Guid, HelpAsset> existingAssets)
    {
        HashSet<Guid> retainedIds = new();
        foreach (HelpAssetMutation mutation in mutations)
        {
            if (mutation.Id.HasValue)
            {
                HelpAsset asset = existingAssets[mutation.Id.Value];
                ApplyAssetMutation(asset, mutation);
                retainedIds.Add(asset.Id);
            }
            else
            {
                HelpAsset asset = CreateAsset(article, mutation);
                article.Assets.Add(asset);
                domainContext.HelpAssets.Add(asset);
                retainedIds.Add(asset.Id);
            }
        }

        List<HelpAsset> removedAssets = [.. existingAssets.Values.Where(asset => !retainedIds.Contains(asset.Id))];
        domainContext.HelpAssets.RemoveRange(removedAssets);
    }

    private static HelpAsset CreateAsset(
        HelpArticle article,
        HelpAssetMutation mutation)
    {
        HelpAsset asset = new()
        {
            Id = Guid.NewGuid(),
            HelpArticleId = article.Id,
            HelpArticle = article
        };
        ApplyAssetMutation(asset, mutation);
        return asset;
    }

    private static void ApplyAssetMutation(
        HelpAsset asset,
        HelpAssetMutation mutation)
    {
        asset.Title = mutation.Title;
        asset.AssetType = mutation.AssetType;
        asset.Url = mutation.Url;
        asset.InternalPath = InternalPath(mutation.Url);
        asset.SortOrder = mutation.SortOrder;
    }

    private static string? InternalPath(string url) =>
        url.StartsWith("/help-assets/", StringComparison.Ordinal)
            ? url
            : null;
}
