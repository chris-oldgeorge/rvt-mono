using RvtPortal.Application.Common;

namespace RvtPortal.Application.Sites.Ports;

public interface ISiteReadPort
{
    Task<bool> ExistsAsync(
        Guid siteId,
        SiteAccessScope scope,
        CancellationToken cancellationToken);

    Task<PagedResult<SiteListModel>> QueryAsync(
        SiteAccessScope scope,
        SiteQuery query,
        CancellationToken cancellationToken);

    Task<SiteOptionsModel> OptionsAsync(
        Guid? companyId,
        CancellationToken cancellationToken);

    Task<SiteDetailModel?> GetAsync(
        Guid siteId,
        CancellationToken cancellationToken);

    Task<SiteArchiveState?> GetArchiveStateAsync(
        Guid siteId,
        CancellationToken cancellationToken);

    Task<PagedResult<SiteMonitorModel>> QueryMonitorsAsync(
        Guid siteId,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<PagedResult<SiteNotificationModel>> QueryOpenNotificationsAsync(
        Guid siteId,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<SiteNotificationSettingsData?> GetNotificationSettingsAsync(
        Guid siteId,
        CancellationToken cancellationToken);

    Task<SiteMutationValidationData> GetMutationValidationDataAsync(
        SiteMutation request,
        Guid? currentSiteId,
        CancellationToken cancellationToken);

    Task<SiteNotificationSettingTarget?> GetNotificationSettingTargetAsync(
        Guid siteId,
        Guid siteUserId,
        CancellationToken cancellationToken);
}

public sealed record SiteArchiveState(
    Guid SiteId,
    bool Archived,
    string? ArchiveUrl);
