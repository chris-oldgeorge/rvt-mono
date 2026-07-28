// File summary: Validates and canonicalizes Help article and linked-asset URL metadata.
// Major updates:
// - 2026-07-28 Added safe URL, immutable asset identity, slug, type, and persistence-limit validation.

using System.Text.RegularExpressions;
using RvtPortal.Application.Common;

namespace RvtPortal.Application.Help;

public sealed record ValidatedHelpArticleMutation(HelpArticleMutation Source);

public sealed record HelpMutationValidationResult(
    IReadOnlyList<UseCaseError> Errors,
    ValidatedHelpArticleMutation? Value)
{
    public bool IsValid => Errors.Count == 0 && Value is not null;
}

public static partial class HelpMutationValidator
{
    private static readonly IReadOnlyDictionary<string, string> contentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FAQ"] = "FAQ",
            ["Article"] = "Article",
            ["Document"] = "Document",
            ["Video"] = "Video",
            ["Definition"] = "Definition"
        };

    private static readonly IReadOnlyDictionary<string, string> assetTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Document"] = "Document",
            ["Video"] = "Video",
            ["Link"] = "Link"
        };

    public static HelpMutationValidationResult ValidateShape(
        HelpArticleMutation mutation)
    {
        var errors = new List<UseCaseError>();
        var sectionTitle = Required(
            nameof(HelpArticleMutation.SectionTitle),
            mutation.SectionTitle,
            120,
            errors);
        var sectionSlug = RequiredSlug(
            nameof(HelpArticleMutation.SectionSlug),
            mutation.SectionSlug,
            120,
            errors);
        var title = Required(
            nameof(HelpArticleMutation.Title),
            mutation.Title,
            160,
            errors);
        var slug = RequiredSlug(
            nameof(HelpArticleMutation.Slug),
            mutation.Slug,
            160,
            errors);
        var summary = Optional(
            nameof(HelpArticleMutation.Summary),
            mutation.Summary,
            512,
            errors);
        var body = Required(
            nameof(HelpArticleMutation.Body),
            mutation.Body,
            100_000,
            errors);
        var contentType = CanonicalValue(
            nameof(HelpArticleMutation.ContentType),
            mutation.ContentType,
            contentTypes,
            "Content type must be FAQ, Article, Document, Video, or Definition.",
            errors);

        ValidateNonNegative(
            nameof(HelpArticleMutation.SectionSortOrder),
            mutation.SectionSortOrder,
            errors);
        ValidateNonNegative(
            nameof(HelpArticleMutation.SortOrder),
            mutation.SortOrder,
            errors);

        var assets = new List<HelpAssetMutation>();
        var seenAssetIds = new HashSet<Guid>();
        for (var index = 0; index < mutation.Assets.Count; index++)
        {
            var asset = mutation.Assets[index];
            var prefix = $"Assets[{index}]";
            var assetTitle = Required(
                $"{prefix}.Title",
                asset.Title,
                160,
                errors);
            var assetType = CanonicalValue(
                $"{prefix}.AssetType",
                asset.AssetType,
                assetTypes,
                "Asset type must be Document, Video, or Link.",
                errors);
            var assetUrlValidation =
                HelpAssetUrlPolicy.ValidateMutationValue(asset.Url);
            var assetUrl = assetUrlValidation.CanonicalValue ?? asset.Url?.Trim() ?? "";
            if (!assetUrlValidation.IsValid)
            {
                var message = assetUrlValidation.ViolationCode switch
                {
                    "required" => $"{prefix}.Url is required.",
                    "too_long" =>
                        $"{prefix}.Url must be {HelpAssetUrlPolicy.MaximumLength} characters or fewer.",
                    _ => "Asset URL must be an absolute HTTPS URL or a /help-assets/ path."
                };
                errors.Add(new UseCaseError(
                    $"{prefix}.Url",
                    message));
            }

            ValidateNonNegative(
                $"{prefix}.SortOrder",
                asset.SortOrder,
                errors);
            if (asset.Id.HasValue &&
                (!seenAssetIds.Add(asset.Id.Value) || asset.Id.Value == Guid.Empty))
            {
                errors.Add(new UseCaseError(
                    $"{prefix}.Id",
                    "Asset IDs must be non-empty and unique."));
            }

            assets.Add(new HelpAssetMutation(
                asset.Id,
                assetTitle,
                assetType,
                assetUrl,
                asset.SortOrder));
        }

        if (errors.Count > 0)
        {
            return new HelpMutationValidationResult(errors, null);
        }

        return new HelpMutationValidationResult(
            [],
            new ValidatedHelpArticleMutation(
                new HelpArticleMutation(
                    sectionTitle,
                    sectionSlug,
                    title,
                    slug,
                    summary,
                    body,
                    contentType,
                    mutation.IsPublished,
                    mutation.SectionSortOrder,
                    mutation.SortOrder,
                    assets)));
    }

    public static HelpMutationValidationResult ValidateBusinessRules(
        HelpMutationValidationResult shape,
        HelpMutationValidationData data,
        bool requireExistingArticle)
    {
        if (!shape.IsValid)
        {
            return shape;
        }

        var errors = new List<UseCaseError>();
        if (requireExistingArticle && !data.ArticleExists)
        {
            errors.Add(new UseCaseError(
                "ArticleId",
                "The help article was not found."));
        }

        if (data.SlugBelongsToAnotherArticle)
        {
            errors.Add(new UseCaseError(
                nameof(HelpArticleMutation.Slug),
                "A help article with this slug already exists."));
        }

        var assets = shape.Value!.Source.Assets;
        for (var index = 0; index < assets.Count; index++)
        {
            var assetId = assets[index].Id;
            if (assetId.HasValue && !data.ExistingAssetIds.Contains(assetId.Value))
            {
                errors.Add(new UseCaseError(
                    $"Assets[{index}].Id",
                    "The asset does not belong to this help article."));
            }
        }

        return errors.Count == 0
            ? shape
            : new HelpMutationValidationResult(errors, null);
    }

    private static string Required(
        string field,
        string? value,
        int maximumLength,
        List<UseCaseError> errors)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            errors.Add(new UseCaseError(field, $"{field} is required."));
        }
        else if (trimmed.Length > maximumLength)
        {
            errors.Add(new UseCaseError(
                field,
                $"{field} must be {maximumLength} characters or fewer."));
        }

        return trimmed;
    }

    private static string RequiredSlug(
        string field,
        string? value,
        int maximumLength,
        List<UseCaseError> errors)
    {
        var slug = Required(field, value, maximumLength, errors);
        if (slug.Length > 0 && !SlugPattern().IsMatch(slug))
        {
            errors.Add(new UseCaseError(
                field,
                $"{field} must use lowercase letters, numbers, and single hyphens."));
        }

        return slug;
    }

    private static string? Optional(
        string field,
        string? value,
        int maximumLength,
        List<UseCaseError> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maximumLength)
        {
            errors.Add(new UseCaseError(
                field,
                $"{field} must be {maximumLength} characters or fewer."));
        }

        return trimmed;
    }

    private static string CanonicalValue(
        string field,
        string? value,
        IReadOnlyDictionary<string, string> allowed,
        string errorMessage,
        List<UseCaseError> errors)
    {
        var trimmed = value?.Trim() ?? "";
        if (allowed.TryGetValue(trimmed, out var canonical))
        {
            return canonical;
        }

        errors.Add(new UseCaseError(field, errorMessage));
        return trimmed;
    }

    private static void ValidateNonNegative(
        string field,
        int value,
        List<UseCaseError> errors)
    {
        if (value < 0)
        {
            errors.Add(new UseCaseError(field, $"{field} must be zero or greater."));
        }
    }

    [GeneratedRegex(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
