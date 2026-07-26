using RvtPortal.Application.Common;
using RvtPortal.Application.Identity;
using RvtPortal.Application.Sites.Ports;

namespace RvtPortal.Application.Sites;

public interface ISiteApplicationService
{
    Task<UseCaseResult<PagedResult<SiteListModel>>> QueryAsync(
        PortalUserContext user,
        SiteQuery request,
        CancellationToken cancellationToken);

    Task<UseCaseResult<SiteOptionsModel>> OptionsAsync(
        Guid? companyId,
        CancellationToken cancellationToken);

    Task<UseCaseResult<SiteDetailModel>> GetAsync(
        PortalUserContext user,
        Guid id,
        CancellationToken cancellationToken);

    Task<UseCaseResult<PagedResult<SiteMonitorModel>>> QueryMonitorsAsync(
        PortalUserContext user,
        Guid siteId,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<UseCaseResult<PagedResult<SiteNotificationModel>>> QueryOpenNotificationsAsync(
        PortalUserContext user,
        Guid siteId,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<bool> CanReadSiteAsync(
        PortalUserContext user,
        Guid id,
        CancellationToken cancellationToken);

    Task<bool> CanManageSiteAsync(
        PortalUserContext user,
        Guid id,
        CancellationToken cancellationToken);

    Task<UseCaseResult<SiteNotificationSettingsModel>> GetNotificationSettingsAsync(
        PortalUserContext user,
        Guid siteId,
        CancellationToken cancellationToken);

    Task<UseCaseResult<SiteDetailModel>> CreateAsync(
        PortalUserContext user,
        SiteMutation request,
        CancellationToken cancellationToken);

    Task<UseCaseResult<SiteDetailModel>> UpdateAsync(
        PortalUserContext user,
        Guid id,
        SiteMutation request,
        CancellationToken cancellationToken);

    Task<UseCaseResult<SiteDetailModel>> ArchiveAsync(
        PortalUserContext user,
        Guid id,
        string createdBy,
        CancellationToken cancellationToken);

    Task<UseCaseResult<SiteDetailModel>> SaveCustomerLogoAsync(
        PortalUserContext user,
        Guid id,
        SiteLogoUpload upload,
        CancellationToken cancellationToken);

    Task<UseCaseResult<SiteDetailModel>> DeleteCustomerLogoAsync(
        PortalUserContext user,
        Guid id,
        CancellationToken cancellationToken);

    Task<UseCaseResult<SiteLogoFile>> OpenCustomerLogoAsync(
        PortalUserContext user,
        Guid id,
        CancellationToken cancellationToken);

    Task<UseCaseResult<SiteNotificationSettingModel>> UpdateNotificationSettingAsync(
        PortalUserContext user,
        Guid siteId,
        Guid siteUserId,
        SiteNotificationSettingMutation request,
        CancellationToken cancellationToken);
}
