// File summary: Handles transactional CQRS commands for Help CMS article mutation workflows.
// Major updates:
// - 2026-06-26 pending Moved Help CMS article writes behind MediatR transactional commands.

using MediatR;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Common;

namespace RvtPortal.Spa.Application.Help;

public sealed record CreateHelpArticleCommand(HelpArticleMutationRequest Request)
    : IRequest<HelpArticleCommandResult>, ITransactionalRequest;

public sealed record UpdateHelpArticleCommand(Guid ArticleId, HelpArticleMutationRequest Request)
    : IRequest<HelpArticleCommandResult>, ITransactionalRequest;

public sealed record SetHelpArticlePublicationCommand(Guid ArticleId, bool IsPublished)
    : IRequest<HelpArticleCommandResult>, ITransactionalRequest;

public sealed record DeleteHelpArticleCommand(Guid ArticleId)
    : IRequest<HelpArticleCommandResult>, ITransactionalRequest;

public sealed class HelpArticleCommandResult : ITransactionOutcome
{
    public bool NotFound { get; set; }
    public Guid? ArticleId { get; set; }
    public string? Slug { get; set; }
    public Dictionary<string, string[]> Errors { get; } = [];
    public bool ShouldCommit => !NotFound && Errors.Count == 0;
}

public sealed class CreateHelpArticleCommandHandler : IRequestHandler<CreateHelpArticleCommand, HelpArticleCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional Help article create command handler.
    public CreateHelpArticleCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Creates a Help article with section and assets.
    public async Task<HelpArticleCommandResult> Handle(CreateHelpArticleCommand request, CancellationToken cancellationToken)
    {
        var result = new HelpArticleCommandResult();
        await HelpArticleCommandWorkflow.ValidateArticleAsync(domainContext, request.Request, null, result.Errors, cancellationToken);
        if (result.Errors.Count > 0)
        {
            return result;
        }

        var section = await HelpArticleCommandWorkflow.GetOrCreateSectionAsync(domainContext, request.Request, cancellationToken);
        var article = HelpArticleCommandWorkflow.CreateArticle(section, request.Request);
        domainContext.HelpArticles.Add(article);
        result.ArticleId = article.Id;
        result.Slug = article.Slug;
        return result;
    }
}

public sealed class UpdateHelpArticleCommandHandler : IRequestHandler<UpdateHelpArticleCommand, HelpArticleCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional Help article update command handler.
    public UpdateHelpArticleCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Updates a Help article and replaces its assets.
    public async Task<HelpArticleCommandResult> Handle(UpdateHelpArticleCommand request, CancellationToken cancellationToken)
    {
        var result = new HelpArticleCommandResult { ArticleId = request.ArticleId };
        var article = await domainContext.HelpArticles
            .Include(item => item.Section)
            .Include(item => item.Assets)
            .SingleOrDefaultAsync(item => item.Id == request.ArticleId, cancellationToken);
        if (article == null)
        {
            result.NotFound = true;
            return result;
        }

        await HelpArticleCommandWorkflow.ValidateArticleAsync(domainContext, request.Request, request.ArticleId, result.Errors, cancellationToken);
        if (result.Errors.Count > 0)
        {
            return result;
        }

        var section = await HelpArticleCommandWorkflow.GetOrCreateSectionAsync(domainContext, request.Request, cancellationToken);
        article.Section = section;
        article.SectionId = section.Id;
        HelpArticleCommandWorkflow.ApplyArticleMutation(article, request.Request);
        domainContext.HelpAssets.RemoveRange(article.Assets);
        article.Assets = HelpArticleCommandWorkflow.BuildAssets(request.Request).ToList();
        result.Slug = article.Slug;
        return result;
    }
}

public sealed class SetHelpArticlePublicationCommandHandler
    : IRequestHandler<SetHelpArticlePublicationCommand, HelpArticleCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional Help article publication command handler.
    public SetHelpArticlePublicationCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Sets Help article publication state.
    public async Task<HelpArticleCommandResult> Handle(SetHelpArticlePublicationCommand request, CancellationToken cancellationToken)
    {
        var result = new HelpArticleCommandResult { ArticleId = request.ArticleId };
        var article = await domainContext.HelpArticles.SingleOrDefaultAsync(item => item.Id == request.ArticleId, cancellationToken);
        if (article == null)
        {
            result.NotFound = true;
            return result;
        }

        article.IsPublished = request.IsPublished;
        article.UpdatedAtUtc = DateTime.UtcNow;
        result.Slug = article.Slug;
        return result;
    }
}

public sealed class DeleteHelpArticleCommandHandler : IRequestHandler<DeleteHelpArticleCommand, HelpArticleCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional Help article delete command handler.
    public DeleteHelpArticleCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Deletes a Help article.
    public async Task<HelpArticleCommandResult> Handle(DeleteHelpArticleCommand request, CancellationToken cancellationToken)
    {
        var result = new HelpArticleCommandResult { ArticleId = request.ArticleId };
        var article = await domainContext.HelpArticles.SingleOrDefaultAsync(item => item.Id == request.ArticleId, cancellationToken);
        if (article == null)
        {
            result.NotFound = true;
            return result;
        }

        domainContext.HelpArticles.Remove(article);
        return result;
    }
}

internal static class HelpArticleCommandWorkflow
{
    private static readonly HashSet<string> AllowedAssetTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Document",
        "Video",
        "Link"
    };

    // Function summary: Validates Help CMS article mutation data.
    public static async Task ValidateArticleAsync(
        RVTDbContext domainContext,
        HelpArticleMutationRequest request,
        Guid? existingArticleId,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        ValidateRequired(nameof(HelpArticleMutationRequest.SectionTitle), request.SectionTitle, errors);
        ValidateRequired(nameof(HelpArticleMutationRequest.SectionSlug), request.SectionSlug, errors);
        ValidateRequired(nameof(HelpArticleMutationRequest.Title), request.Title, errors);
        ValidateRequired(nameof(HelpArticleMutationRequest.Slug), request.Slug, errors);
        ValidateRequired(nameof(HelpArticleMutationRequest.Body), request.Body, errors);
        ValidateRequired(nameof(HelpArticleMutationRequest.ContentType), request.ContentType, errors);
        if (!string.IsNullOrWhiteSpace(request.Slug) &&
            await domainContext.HelpArticles.AnyAsync(article => article.Slug == request.Slug.Trim() && article.Id != existingArticleId, cancellationToken))
        {
            AddError(errors, nameof(HelpArticleMutationRequest.Slug), "A help article with this slug already exists.");
        }

        foreach (var asset in request.Assets)
        {
            ValidateRequired(nameof(HelpAssetMutationRequest.Title), asset.Title, errors);
            ValidateRequired(nameof(HelpAssetMutationRequest.Url), asset.Url, errors);
            if (!AllowedAssetTypes.Contains(asset.AssetType))
            {
                AddError(errors, nameof(HelpAssetMutationRequest.AssetType), "Asset type must be Document, Video, or Link.");
            }
        }
    }

    // Function summary: Retrieves or creates a Help CMS section for article mutations.
    public static async Task<HelpSection> GetOrCreateSectionAsync(
        RVTDbContext domainContext,
        HelpArticleMutationRequest request,
        CancellationToken cancellationToken)
    {
        var sectionSlug = request.SectionSlug.Trim();
        var section = await domainContext.HelpSections.SingleOrDefaultAsync(item => item.Slug == sectionSlug, cancellationToken);
        if (section == null)
        {
            section = new HelpSection();
            domainContext.HelpSections.Add(section);
        }

        section.Title = request.SectionTitle.Trim();
        section.Slug = sectionSlug;
        section.SortOrder = request.SectionSortOrder;
        section.IsPublished = true;
        return section;
    }

    // Function summary: Creates a Help article entity from mutation data.
    public static HelpArticle CreateArticle(HelpSection section, HelpArticleMutationRequest request)
    {
        var now = DateTime.UtcNow;
        return new HelpArticle
        {
            Section = section,
            Title = request.Title.Trim(),
            Slug = request.Slug.Trim(),
            Summary = EmptyToNull(request.Summary),
            Body = request.Body.Trim(),
            ContentType = request.ContentType.Trim(),
            IsPublished = request.IsPublished,
            SortOrder = request.SortOrder,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Assets = BuildAssets(request).ToList()
        };
    }

    // Function summary: Applies request values to a Help CMS article entity.
    public static void ApplyArticleMutation(HelpArticle article, HelpArticleMutationRequest request)
    {
        var now = DateTime.UtcNow;
        article.Title = request.Title.Trim();
        article.Slug = request.Slug.Trim();
        article.Summary = EmptyToNull(request.Summary);
        article.Body = request.Body.Trim();
        article.ContentType = request.ContentType.Trim();
        article.IsPublished = request.IsPublished;
        article.SortOrder = request.SortOrder;
        if (article.CreatedAtUtc == default)
        {
            article.CreatedAtUtc = now;
        }
        article.UpdatedAtUtc = now;
    }

    // Function summary: Builds Help CMS asset entities from a mutation request.
    public static IEnumerable<HelpAsset> BuildAssets(HelpArticleMutationRequest request)
    {
        return request.Assets.Select((asset, index) => new HelpAsset
        {
            Title = asset.Title.Trim(),
            AssetType = asset.AssetType.Trim(),
            Url = asset.Url.Trim(),
            InternalPath = EmptyToNull(asset.InternalPath) ?? BuildInternalPath(asset.Url),
            SortOrder = asset.SortOrder == 0 ? index : asset.SortOrder
        });
    }

    private static void ValidateRequired(string key, string? value, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(errors, key, $"{key} is required.");
        }
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? BuildInternalPath(string url)
    {
        return url.StartsWith("/help-assets/", StringComparison.OrdinalIgnoreCase)
            ? url.TrimStart('/')
            : null;
    }

    private static void AddError(Dictionary<string, string[]> errors, string key, string message)
    {
        errors[key] = errors.TryGetValue(key, out var existing)
            ? [.. existing, message]
            : [message];
    }
}
