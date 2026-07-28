// File summary: Orchestrates authorized published and administrative Help use cases through application-owned ports.
// Major updates:
// - 2026-07-28 Added transactional Help workflows with injected UTC time and defense-in-depth authorization.

using RvtPortal.Application.Common;
using RvtPortal.Application.Help.Ports;
using RvtPortal.Application.Identity;

namespace RvtPortal.Application.Help;

public sealed class HelpApplicationService(
    IHelpReadPort reads,
    IHelpWritePort writes,
    IApplicationUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IHelpApplicationService
{
    public async Task<UseCaseResult<HelpOverviewModel>> QueryPublishedAsync(
        PortalUserContext actor,
        string? searchText,
        CancellationToken cancellationToken)
    {
        if (!HelpAuthorizationPolicy.CanReadPublished(actor))
        {
            return UseCaseResult<HelpOverviewModel>.Forbidden();
        }

        HelpOverviewModel result = await reads.QueryPublishedAsync(
            EmptyToNull(searchText),
            cancellationToken);
        return UseCaseResult<HelpOverviewModel>.Success(result);
    }

    public async Task<UseCaseResult<HelpArticleModel>> GetPublishedArticleAsync(
        PortalUserContext actor,
        string slug,
        CancellationToken cancellationToken)
    {
        if (!HelpAuthorizationPolicy.CanReadPublished(actor))
        {
            return UseCaseResult<HelpArticleModel>.Forbidden();
        }

        HelpArticleModel? article = await reads.GetPublishedArticleAsync(
            slug.Trim(),
            cancellationToken);
        return article is null
            ? ArticleNotFound<HelpArticleModel>(slug)
            : UseCaseResult<HelpArticleModel>.Success(article);
    }

    public async Task<UseCaseResult<HelpAdminOverviewModel>> QueryAdminAsync(
        PortalUserContext actor,
        HelpAdminQuery query,
        CancellationToken cancellationToken)
    {
        if (!HelpAuthorizationPolicy.CanManage(actor))
        {
            return UseCaseResult<HelpAdminOverviewModel>.Forbidden();
        }

        HelpAdminQuery normalized = new HelpAdminQuery(
            EmptyToNull(query.SearchText),
            DefaultFilter(query.Status),
            DefaultFilter(query.ContentType));
        HelpAdminOverviewModel result = await reads.QueryAdminAsync(normalized, cancellationToken);
        return UseCaseResult<HelpAdminOverviewModel>.Success(result);
    }

    public async Task<UseCaseResult<HelpArticleModel>> GetAdminArticleAsync(
        PortalUserContext actor,
        Guid articleId,
        CancellationToken cancellationToken)
    {
        if (!HelpAuthorizationPolicy.CanManage(actor))
        {
            return UseCaseResult<HelpArticleModel>.Forbidden();
        }

        HelpArticleModel? article = await reads.GetAdminArticleAsync(
            articleId,
            cancellationToken);
        return article is null
            ? ArticleNotFound<HelpArticleModel>(articleId)
            : UseCaseResult<HelpArticleModel>.Success(article);
    }

    public Task<UseCaseResult<HelpArticleModel>> CreateAsync(
        PortalUserContext actor,
        HelpArticleMutation mutation,
        CancellationToken cancellationToken)
    {
        if (!HelpAuthorizationPolicy.CanManage(actor))
        {
            return Task.FromResult(UseCaseResult<HelpArticleModel>.Forbidden());
        }

        HelpMutationValidationResult shape = HelpMutationValidator.ValidateShape(mutation);
        if (!shape.IsValid)
        {
            return Task.FromResult(Validation<HelpArticleModel>(shape));
        }

        return unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                HelpMutationValidationData data = await reads.GetMutationValidationDataAsync(
                    shape.Value!.Source.Slug,
                    articleId: null,
                    token);
                HelpMutationValidationResult validation = HelpMutationValidator.ValidateBusinessRules(
                    shape,
                    data,
                    requireExistingArticle: false);
                if (!validation.IsValid)
                {
                    return Validation<HelpArticleModel>(validation);
                }

                Guid articleId = await writes.CreateAsync(
                    validation.Value!,
                    UtcNow(),
                    token);
                await unitOfWork.SaveChangesAsync(token);
                HelpArticleModel article = await reads.GetAdminArticleAsync(articleId, token)
                    ?? throw new InvalidOperationException(
                        $"Help article '{articleId}' was not readable after creation.");
                return UseCaseResult<HelpArticleModel>.Success(article);
            },
            cancellationToken);
    }

    public Task<UseCaseResult<HelpArticleModel>> UpdateAsync(
        PortalUserContext actor,
        Guid articleId,
        HelpArticleMutation mutation,
        CancellationToken cancellationToken)
    {
        if (!HelpAuthorizationPolicy.CanManage(actor))
        {
            return Task.FromResult(UseCaseResult<HelpArticleModel>.Forbidden());
        }

        HelpMutationValidationResult shape = HelpMutationValidator.ValidateShape(mutation);
        if (!shape.IsValid)
        {
            return Task.FromResult(Validation<HelpArticleModel>(shape));
        }

        return unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                HelpMutationValidationData data = await reads.GetMutationValidationDataAsync(
                    shape.Value!.Source.Slug,
                    articleId,
                    token);
                if (!data.ArticleExists)
                {
                    return ArticleNotFound<HelpArticleModel>(articleId);
                }

                HelpMutationValidationResult validation = HelpMutationValidator.ValidateBusinessRules(
                    shape,
                    data,
                    requireExistingArticle: true);
                if (!validation.IsValid)
                {
                    return Validation<HelpArticleModel>(validation);
                }

                if (!await writes.UpdateAsync(
                    articleId,
                    validation.Value!,
                    UtcNow(),
                    token))
                {
                    return ArticleNotFound<HelpArticleModel>(articleId);
                }

                await unitOfWork.SaveChangesAsync(token);
                HelpArticleModel article = await reads.GetAdminArticleAsync(articleId, token)
                    ?? throw new InvalidOperationException(
                        $"Help article '{articleId}' was not readable after update.");
                return UseCaseResult<HelpArticleModel>.Success(article);
            },
            cancellationToken);
    }

    public Task<UseCaseResult<HelpArticleModel>> SetPublicationAsync(
        PortalUserContext actor,
        Guid articleId,
        bool isPublished,
        CancellationToken cancellationToken)
    {
        if (!HelpAuthorizationPolicy.CanManage(actor))
        {
            return Task.FromResult(UseCaseResult<HelpArticleModel>.Forbidden());
        }

        return unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                if (!await writes.SetPublicationAsync(
                    articleId,
                    isPublished,
                    UtcNow(),
                    token))
                {
                    return ArticleNotFound<HelpArticleModel>(articleId);
                }

                await unitOfWork.SaveChangesAsync(token);
                HelpArticleModel article = await reads.GetAdminArticleAsync(articleId, token)
                    ?? throw new InvalidOperationException(
                        $"Help article '{articleId}' was not readable after publication changed.");
                return UseCaseResult<HelpArticleModel>.Success(article);
            },
            cancellationToken);
    }

    public Task<UseCaseResult<HelpDeleteResult>> DeleteAsync(
        PortalUserContext actor,
        Guid articleId,
        CancellationToken cancellationToken)
    {
        if (!HelpAuthorizationPolicy.CanManage(actor))
        {
            return Task.FromResult(UseCaseResult<HelpDeleteResult>.Forbidden());
        }

        return unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                if (!await writes.DeleteAsync(articleId, token))
                {
                    return ArticleNotFound<HelpDeleteResult>(articleId);
                }

                await unitOfWork.SaveChangesAsync(token);
                return UseCaseResult<HelpDeleteResult>.Success(
                    new HelpDeleteResult(articleId));
            },
            cancellationToken);
    }

    private DateTime UtcNow() =>
        timeProvider.GetUtcNow().UtcDateTime;

    private static UseCaseResult<T> Validation<T>(
        HelpMutationValidationResult validation) =>
        UseCaseResult<T>.Validation([.. validation.Errors]);

    private static UseCaseResult<T> ArticleNotFound<T>(object articleId) =>
        UseCaseResult<T>.NotFound(
            $"Help article '{articleId}' was not found.");

    private static string? EmptyToNull(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string DefaultFilter(string? value) =>
        EmptyToNull(value) ?? "All";
}
