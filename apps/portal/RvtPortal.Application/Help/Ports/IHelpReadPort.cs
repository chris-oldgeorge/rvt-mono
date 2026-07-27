// File summary: Defines persistence-neutral reads required by published and administrative Help use cases.
// Major updates:
// - 2026-07-28 Added the application-owned Help read port.

namespace RvtPortal.Application.Help.Ports;

public interface IHelpReadPort
{
    Task<HelpOverviewModel> QueryPublishedAsync(
        string? searchText,
        CancellationToken cancellationToken);

    Task<HelpArticleModel?> GetPublishedArticleAsync(
        string slug,
        CancellationToken cancellationToken);

    Task<HelpAdminOverviewModel> QueryAdminAsync(
        HelpAdminQuery query,
        CancellationToken cancellationToken);

    Task<HelpArticleModel?> GetAdminArticleAsync(
        Guid articleId,
        CancellationToken cancellationToken);

    Task<HelpMutationValidationData> GetMutationValidationDataAsync(
        string slug,
        Guid? articleId,
        CancellationToken cancellationToken);
}
