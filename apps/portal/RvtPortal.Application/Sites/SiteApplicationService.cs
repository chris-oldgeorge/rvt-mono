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
    private readonly ISiteWritePort writes;
    private readonly IApplicationUnitOfWork unitOfWork;
    private readonly IPortalUserDirectory userDirectory;
    private readonly TimeProvider timeProvider;

    public SiteApplicationService(
        ISiteReadPort reads,
        ISiteWritePort writes,
        IApplicationUnitOfWork unitOfWork,
        IPortalUserDirectory userDirectory,
        TimeProvider timeProvider)
    {
        this.reads = reads;
        this.writes = writes;
        this.unitOfWork = unitOfWork;
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

        var result = await BuildNotificationSettingsAsync(
            user,
            data,
            cancellationToken);
        return UseCaseResult<SiteNotificationSettingsModel>.Success(result);
    }

    public async Task<UseCaseResult<SiteDetailModel>> CreateAsync(
        PortalUserContext user,
        SiteMutation request,
        CancellationToken cancellationToken)
    {
        var shape = SiteMutationValidator.ValidateShape(request);
        if (!shape.IsValid)
        {
            return UseCaseResult<SiteDetailModel>.Validation([.. shape.Errors]);
        }

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                async token =>
                {
                    if (!SiteAuthorizationPolicy.CanManage(user))
                    {
                        return UseCaseResult<SiteDetailModel>.Forbidden();
                    }

                    var data = await reads.GetMutationValidationDataAsync(
                        request,
                        currentSiteId: null,
                        token);
                    var validation = SiteMutationValidator.ValidateBusinessRules(
                        shape,
                        data,
                        requireContract: true);
                    if (!validation.IsValid)
                    {
                        return UseCaseResult<SiteDetailModel>.Validation(
                            [.. validation.Errors]);
                    }

                    var siteId = await writes.CreateAsync(
                        validation.Value!,
                        timeProvider.GetUtcNow().UtcDateTime,
                        token);
                    await unitOfWork.SaveChangesAsync(token);
                    if (!await writes.TryClaimContractAsync(
                        request.ContractId!.Value,
                        request.CompanyId,
                        siteId,
                        token))
                    {
                        throw new ContractClaimFailedException();
                    }

                    var detail = await reads.GetAsync(siteId, token)
                        ?? throw new InvalidOperationException(
                            $"Site '{siteId}' was not readable after a successful create.");
                    detail.CanManage = SiteAuthorizationPolicy.CanManage(user);
                    return UseCaseResult<SiteDetailModel>.Success(detail);
                },
                cancellationToken);
        }
        catch (ContractClaimFailedException)
        {
            return UseCaseResult<SiteDetailModel>.Validation(
                SiteMutationValidator.ContractAlreadyAssignedError());
        }
    }

    public Task<UseCaseResult<SiteDetailModel>> UpdateAsync(
        PortalUserContext user,
        Guid id,
        SiteMutation request,
        CancellationToken cancellationToken)
    {
        var shape = SiteMutationValidator.ValidateShape(request);
        if (!shape.IsValid)
        {
            return Task.FromResult(
                UseCaseResult<SiteDetailModel>.Validation([.. shape.Errors]));
        }

        return unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                if (!SiteAuthorizationPolicy.CanManage(user))
                {
                    return UseCaseResult<SiteDetailModel>.Forbidden();
                }

                var scope = SiteAuthorizationPolicy.ReadScope(
                    user,
                    timeProvider.GetUtcNow().UtcDateTime);
                if (!await reads.ExistsAsync(id, scope, token))
                {
                    return SiteNotFound<SiteDetailModel>(id);
                }

                var data = await reads.GetMutationValidationDataAsync(
                    request,
                    id,
                    token);
                var validation = SiteMutationValidator.ValidateBusinessRules(
                    shape,
                    data,
                    requireContract: false);
                if (!validation.IsValid)
                {
                    return UseCaseResult<SiteDetailModel>.Validation(
                        [.. validation.Errors]);
                }

                if (!await writes.UpdateAsync(
                    id,
                    validation.Value!,
                    token))
                {
                    return SiteNotFound<SiteDetailModel>(id);
                }

                await unitOfWork.SaveChangesAsync(token);
                var detail = await reads.GetAsync(id, token)
                    ?? throw new InvalidOperationException(
                        $"Site '{id}' was not readable after a successful update.");
                detail.CanManage = SiteAuthorizationPolicy.CanManage(user);
                return UseCaseResult<SiteDetailModel>.Success(detail);
            },
            cancellationToken);
    }

    public Task<UseCaseResult<SiteNotificationSettingModel>> UpdateNotificationSettingAsync(
        PortalUserContext user,
        Guid siteId,
        Guid siteUserId,
        SiteNotificationSettingMutation request,
        CancellationToken cancellationToken)
    {
        var timePair = SiteMutationValidator.ValidateTimePair(
            request.StartTime,
            request.EndTime,
            nameof(SiteNotificationSettingMutation.StartTime));
        if (!timePair.IsValid)
        {
            return Task.FromResult(
                UseCaseResult<SiteNotificationSettingModel>.Validation(
                    [.. timePair.Errors]));
        }

        return unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var scope = SiteAuthorizationPolicy.ReadScope(
                    user,
                    timeProvider.GetUtcNow().UtcDateTime);
                if (!await reads.ExistsAsync(siteId, scope, token))
                {
                    return SiteNotFound<SiteNotificationSettingModel>(siteId);
                }

                var target = await reads.GetNotificationSettingTargetAsync(
                    siteId,
                    siteUserId,
                    token);
                if (target is null)
                {
                    return SiteNotFound<SiteNotificationSettingModel>(siteId);
                }

                if (!SiteAuthorizationPolicy.CanUpdateNotificationSetting(
                    user,
                    target.UserId))
                {
                    return UseCaseResult<SiteNotificationSettingModel>.Forbidden();
                }

                await writes.UpsertNotificationSettingAsync(
                    siteUserId,
                    request,
                    timePair.StartTime,
                    timePair.EndTime,
                    token);
                await unitOfWork.SaveChangesAsync(token);

                var data = await reads.GetNotificationSettingsAsync(siteId, token)
                    ?? throw new InvalidOperationException(
                        $"Site '{siteId}' notification settings were not readable after a successful update.");
                var settings = await BuildNotificationSettingsAsync(
                    user,
                    data,
                    token);
                var updated = settings.Settings.SingleOrDefault(
                    item => item.SiteUserId == siteUserId)
                    ?? throw new InvalidOperationException(
                        $"Site user '{siteUserId}' notification settings were not readable after a successful update.");
                return UseCaseResult<SiteNotificationSettingModel>.Success(updated);
            },
            cancellationToken);
    }

    private async Task<SiteNotificationSettingsModel> BuildNotificationSettingsAsync(
        PortalUserContext user,
        SiteNotificationSettingsData data,
        CancellationToken cancellationToken)
    {
        var profiles = (await userDirectory.ListUsersAsync(cancellationToken))
            .ToDictionary(profile => profile.UserId);
        var assignments = user.IsCompanyUser && !user.IsAdmin
            ? data.Assignments.Where(assignment => assignment.UserId == user.UserId)
            : data.Assignments;

        return new SiteNotificationSettingsModel
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
    }

    private static UseCaseResult<T> SiteNotFound<T>(Guid siteId) =>
        UseCaseResult<T>.NotFound($"Site '{siteId}' was not found.");

    private sealed class ContractClaimFailedException : Exception;
}
