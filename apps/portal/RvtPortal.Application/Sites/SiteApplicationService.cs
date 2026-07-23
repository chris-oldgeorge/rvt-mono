using RvtPortal.Application.Common;
using RvtPortal.Application.Identity;
using RvtPortal.Application.Sites.Ports;

namespace RvtPortal.Application.Sites;

public sealed class SiteApplicationService : ISiteApplicationService
{
    public const string DefaultSort = "createDate";
    public const string MonitorSort = "fleetNumber";
    public const string NotificationSort = "notificationTime";

    public static readonly IReadOnlySet<string> SortFields =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "siteName",
            "companyName",
            "contracts",
            "createDate",
            "siteAddress"
        };

    public static readonly IReadOnlySet<string> MonitorSortFields =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            MonitorSort
        };

    public static readonly IReadOnlySet<string> NotificationSortFields =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NotificationSort
        };

    private readonly ISiteReadPort reads;
    private readonly IPortalUserDirectory userDirectory;
    private readonly TimeProvider timeProvider;

    public SiteApplicationService(
        ISiteReadPort reads,
        IPortalUserDirectory userDirectory,
        TimeProvider timeProvider)
    {
        this.reads = reads;
        this.userDirectory = userDirectory;
        this.timeProvider = timeProvider;
    }

    public async Task<UseCaseResult<PagedResult<SiteListModel>>> QueryAsync(
        PortalUserContext user,
        SiteQuery request,
        CancellationToken cancellationToken)
    {
        var scope = SiteAuthorizationPolicy.ReadScope(
            user,
            timeProvider.GetUtcNow().UtcDateTime);
        var result = await reads.QueryAsync(scope, request, cancellationToken);
        return UseCaseResult<PagedResult<SiteListModel>>.Success(result);
    }

    public async Task<UseCaseResult<SiteOptionsModel>> OptionsAsync(
        Guid? companyId,
        CancellationToken cancellationToken)
    {
        var result = await reads.OptionsAsync(companyId, cancellationToken);
        return UseCaseResult<SiteOptionsModel>.Success(result);
    }

    public async Task<UseCaseResult<SiteDetailModel>> GetAsync(
        PortalUserContext user,
        Guid id,
        CancellationToken cancellationToken)
    {
        var scope = SiteAuthorizationPolicy.ReadScope(
            user,
            timeProvider.GetUtcNow().UtcDateTime);
        if (!await reads.ExistsAsync(id, scope, cancellationToken))
        {
            return SiteNotFound<SiteDetailModel>(id);
        }

        var site = await reads.GetAsync(id, cancellationToken);
        if (site == null)
        {
            return SiteNotFound<SiteDetailModel>(id);
        }

        site.CanManage = SiteAuthorizationPolicy.CanManage(user);
        return UseCaseResult<SiteDetailModel>.Success(site);
    }

    public async Task<UseCaseResult<PagedResult<SiteMonitorModel>>> QueryMonitorsAsync(
        PortalUserContext user,
        Guid siteId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var scope = SiteAuthorizationPolicy.ReadScope(
            user,
            timeProvider.GetUtcNow().UtcDateTime);
        if (!await reads.ExistsAsync(siteId, scope, cancellationToken))
        {
            return SiteNotFound<PagedResult<SiteMonitorModel>>(siteId);
        }

        var result = await reads.QueryMonitorsAsync(siteId, page, cancellationToken);
        return UseCaseResult<PagedResult<SiteMonitorModel>>.Success(result);
    }

    public async Task<UseCaseResult<PagedResult<SiteNotificationModel>>> QueryOpenNotificationsAsync(
        PortalUserContext user,
        Guid siteId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var scope = SiteAuthorizationPolicy.ReadScope(
            user,
            timeProvider.GetUtcNow().UtcDateTime);
        if (!await reads.ExistsAsync(siteId, scope, cancellationToken))
        {
            return SiteNotFound<PagedResult<SiteNotificationModel>>(siteId);
        }

        var result = await reads.QueryOpenNotificationsAsync(siteId, page, cancellationToken);
        return UseCaseResult<PagedResult<SiteNotificationModel>>.Success(result);
    }

    public async Task<bool> CanReadSiteAsync(
        PortalUserContext user,
        Guid id,
        CancellationToken cancellationToken)
    {
        var scope = SiteAuthorizationPolicy.ReadScope(
            user,
            timeProvider.GetUtcNow().UtcDateTime);
        return await reads.ExistsAsync(id, scope, cancellationToken);
    }

    public async Task<bool> CanManageSiteAsync(
        PortalUserContext user,
        Guid id,
        CancellationToken cancellationToken)
    {
        var scope = SiteAuthorizationPolicy.ReadScope(
            user,
            timeProvider.GetUtcNow().UtcDateTime);
        return SiteAuthorizationPolicy.CanManage(user)
            && await reads.ExistsAsync(id, scope, cancellationToken);
    }

    public async Task<UseCaseResult<SiteNotificationSettingsModel>> GetNotificationSettingsAsync(
        PortalUserContext user,
        Guid siteId,
        CancellationToken cancellationToken)
    {
        var scope = SiteAuthorizationPolicy.ReadScope(
            user,
            timeProvider.GetUtcNow().UtcDateTime);
        if (!await reads.ExistsAsync(siteId, scope, cancellationToken))
        {
            return SiteNotFound<SiteNotificationSettingsModel>(siteId);
        }

        var data = await reads.GetNotificationSettingsAsync(siteId, cancellationToken);
        if (data == null)
        {
            return SiteNotFound<SiteNotificationSettingsModel>(siteId);
        }

        var profiles = (await userDirectory.ListUsersAsync(cancellationToken))
            .ToDictionary(profile => profile.UserId);
        var assignments = user.IsCompanyUser && !user.IsAdmin
            ? data.Assignments.Where(assignment => assignment.UserId == user.UserId)
            : data.Assignments;

        var result = new SiteNotificationSettingsModel
        {
            SiteId = data.SiteId,
            SiteName = data.SiteName,
            Settings = assignments.Select(assignment =>
            {
                profiles.TryGetValue(assignment.UserId, out var profile);
                return new SiteNotificationSettingModel(
                    assignment.SiteUserId,
                    assignment.SiteId,
                    assignment.UserId,
                    profile?.Email ?? "",
                    profile?.Name,
                    assignment.SiteContact,
                    assignment.Email,
                    assignment.Sms,
                    assignment.StartTime,
                    assignment.EndTime);
            }).ToList()
        };
        return UseCaseResult<SiteNotificationSettingsModel>.Success(result);
    }

    private static UseCaseResult<T> SiteNotFound<T>(Guid siteId) =>
        UseCaseResult<T>.NotFound($"Site '{siteId}' was not found.");
}
