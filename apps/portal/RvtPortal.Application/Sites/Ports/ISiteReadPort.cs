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
}
