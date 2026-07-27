// File summary: Defines the standalone published and administrative Help use cases.
// Major updates:
// - 2026-07-28 Added the application-owned Help service boundary.

using RvtPortal.Application.Common;
using RvtPortal.Application.Identity;

namespace RvtPortal.Application.Help;

public interface IHelpApplicationService
{
    Task<UseCaseResult<HelpOverviewModel>> QueryPublishedAsync(
        PortalUserContext actor,
        string? searchText,
        CancellationToken cancellationToken);

    Task<UseCaseResult<HelpArticleModel>> GetPublishedArticleAsync(
        PortalUserContext actor,
        string slug,
        CancellationToken cancellationToken);

    Task<UseCaseResult<HelpAdminOverviewModel>> QueryAdminAsync(
        PortalUserContext actor,
        HelpAdminQuery query,
        CancellationToken cancellationToken);

    Task<UseCaseResult<HelpArticleModel>> GetAdminArticleAsync(
        PortalUserContext actor,
        Guid articleId,
        CancellationToken cancellationToken);

    Task<UseCaseResult<HelpArticleModel>> CreateAsync(
        PortalUserContext actor,
        HelpArticleMutation mutation,
        CancellationToken cancellationToken);

    Task<UseCaseResult<HelpArticleModel>> UpdateAsync(
        PortalUserContext actor,
        Guid articleId,
        HelpArticleMutation mutation,
        CancellationToken cancellationToken);

    Task<UseCaseResult<HelpArticleModel>> SetPublicationAsync(
        PortalUserContext actor,
        Guid articleId,
        bool isPublished,
        CancellationToken cancellationToken);

    Task<UseCaseResult<HelpDeleteResult>> DeleteAsync(
        PortalUserContext actor,
        Guid articleId,
        CancellationToken cancellationToken);
}
