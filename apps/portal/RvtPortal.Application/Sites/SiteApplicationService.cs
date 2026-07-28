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
    private readonly ISiteArchivePort archive;
    private readonly ISiteLogoPort logos;
    private readonly TimeProvider timeProvider;

    public SiteApplicationService(
        ISiteReadPort reads,
        ISiteWritePort writes,
        IApplicationUnitOfWork unitOfWork,
        IPortalUserDirectory userDirectory,
        ISiteArchivePort archive,
        ISiteLogoPort logos,
        TimeProvider timeProvider)
    {
        this.reads = reads;
        this.writes = writes;
        this.unitOfWork = unitOfWork;
        this.userDirectory = userDirectory;
        this.archive = archive;
        this.logos = logos;
        this.timeProvider = timeProvider;
    }

    public async Task<UseCaseResult<PagedResult<SiteListModel>>> QueryAsync(
        PortalUserContext user,
        SiteQuery request,
        CancellationToken cancellationToken)
    {
        SiteAccessScope scope = SiteAuthorizationPolicy.ReadScope(
            user,
            timeProvider.GetUtcNow().UtcDateTime);
        PagedResult<SiteListModel> result = await reads.QueryAsync(scope, request, cancellationToken);
        return UseCaseResult<PagedResult<SiteListModel>>.Success(result);
    }

    public async Task<UseCaseResult<SiteOptionsModel>> OptionsAsync(
        Guid? companyId,
        CancellationToken cancellationToken)
    {
        SiteOptionsModel result = await reads.OptionsAsync(companyId, cancellationToken);
        return UseCaseResult<SiteOptionsModel>.Success(result);
    }

    public async Task<UseCaseResult<SiteDetailModel>> GetAsync(
        PortalUserContext user,
        Guid id,
        CancellationToken cancellationToken)
    {
        SiteAccessScope scope = SiteAuthorizationPolicy.ReadScope(
            user,
            timeProvider.GetUtcNow().UtcDateTime);
        if (!await reads.ExistsAsync(id, scope, cancellationToken))
        {
            return SiteNotFound<SiteDetailModel>(id);
        }

        SiteDetailModel? site = await ReadDetailAsync(user, id, cancellationToken);
        if (site == null)
        {
            return SiteNotFound<SiteDetailModel>(id);
        }

        return UseCaseResult<SiteDetailModel>.Success(site);
    }

    public async Task<UseCaseResult<PagedResult<SiteMonitorModel>>> QueryMonitorsAsync(
        PortalUserContext user,
        Guid siteId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        SiteAccessScope scope = SiteAuthorizationPolicy.ReadScope(
            user,
            timeProvider.GetUtcNow().UtcDateTime);
        if (!await reads.ExistsAsync(siteId, scope, cancellationToken))
        {
            return SiteNotFound<PagedResult<SiteMonitorModel>>(siteId);
        }

        PagedResult<SiteMonitorModel> result = await reads.QueryMonitorsAsync(siteId, page, cancellationToken);
        return UseCaseResult<PagedResult<SiteMonitorModel>>.Success(result);
    }

    public async Task<UseCaseResult<PagedResult<SiteNotificationModel>>> QueryOpenNotificationsAsync(
        PortalUserContext user,
        Guid siteId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        SiteAccessScope scope = SiteAuthorizationPolicy.ReadScope(
            user,
            timeProvider.GetUtcNow().UtcDateTime);
        if (!await reads.ExistsAsync(siteId, scope, cancellationToken))
        {
            return SiteNotFound<PagedResult<SiteNotificationModel>>(siteId);
        }

        PagedResult<SiteNotificationModel> result = await reads.QueryOpenNotificationsAsync(siteId, page, cancellationToken);
        return UseCaseResult<PagedResult<SiteNotificationModel>>.Success(result);
    }

    public async Task<bool> CanReadSiteAsync(
        PortalUserContext user,
        Guid id,
        CancellationToken cancellationToken)
    {
        SiteAccessScope scope = SiteAuthorizationPolicy.ReadScope(
            user,
            timeProvider.GetUtcNow().UtcDateTime);
        return await reads.ExistsAsync(id, scope, cancellationToken);
    }

    public async Task<bool> CanManageSiteAsync(
        PortalUserContext user,
        Guid id,
        CancellationToken cancellationToken)
    {
        SiteAccessScope scope = SiteAuthorizationPolicy.ReadScope(
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
        SiteAccessScope scope = SiteAuthorizationPolicy.ReadScope(
            user,
            timeProvider.GetUtcNow().UtcDateTime);
        if (!await reads.ExistsAsync(siteId, scope, cancellationToken))
        {
            return SiteNotFound<SiteNotificationSettingsModel>(siteId);
        }

        SiteNotificationSettingsData? data = await reads.GetNotificationSettingsAsync(siteId, cancellationToken);
        if (data == null)
        {
            return SiteNotFound<SiteNotificationSettingsModel>(siteId);
        }

        SiteNotificationSettingsModel result = await BuildNotificationSettingsAsync(
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
        SiteMutationValidationResult shape = SiteMutationValidator.ValidateShape(request);
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

                    SiteMutationValidationData data = await reads.GetMutationValidationDataAsync(
                        request,
                        currentSiteId: null,
                        token);
                    SiteMutationValidationResult validation = SiteMutationValidator.ValidateBusinessRules(
                        shape,
                        data,
                        requireContract: true);
                    if (!validation.IsValid)
                    {
                        return UseCaseResult<SiteDetailModel>.Validation(
                            [.. validation.Errors]);
                    }

                    Guid siteId = await writes.CreateAsync(
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

                    SiteDetailModel detail = await ReadDetailAsync(user, siteId, token)
                        ?? throw new InvalidOperationException(
                            $"Site '{siteId}' was not readable after a successful create.");
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
        SiteMutationValidationResult shape = SiteMutationValidator.ValidateShape(request);

        return unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                if (!SiteAuthorizationPolicy.CanManage(user))
                {
                    return UseCaseResult<SiteDetailModel>.Forbidden();
                }

                SiteAccessScope scope = SiteAuthorizationPolicy.ReadScope(
                    user,
                    timeProvider.GetUtcNow().UtcDateTime);
                if (!await reads.ExistsAsync(id, scope, token))
                {
                    return SiteNotFound<SiteDetailModel>(id);
                }

                if (!shape.IsValid)
                {
                    return UseCaseResult<SiteDetailModel>.Validation(
                        [.. shape.Errors]);
                }

                SiteMutationValidationData data = await reads.GetMutationValidationDataAsync(
                    request,
                    id,
                    token);
                SiteMutationValidationResult validation = SiteMutationValidator.ValidateBusinessRules(
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
                SiteDetailModel detail = await ReadDetailAsync(user, id, token)
                    ?? throw new InvalidOperationException(
                        $"Site '{id}' was not readable after a successful update.");
                return UseCaseResult<SiteDetailModel>.Success(detail);
            },
            cancellationToken);
    }

    public async Task<UseCaseResult<SiteDetailModel>> ArchiveAsync(
        PortalUserContext user,
        Guid id,
        string createdBy,
        CancellationToken cancellationToken)
    {
        if (!SiteAuthorizationPolicy.CanManage(user))
        {
            return UseCaseResult<SiteDetailModel>.Forbidden();
        }

        SiteArchiveState? state = await reads.GetArchiveStateAsync(id, cancellationToken);
        if (state is null)
        {
            return SiteNotFound<SiteDetailModel>(id);
        }

        CancellationToken detailCancellationToken = cancellationToken;
        if (state.Archived && state.ArchiveUrl is not null)
        {
            SiteArchiveCleanupResult cleanup = await CleanupArchiveAsync(
                id,
                state.ArchiveUrl,
                cancellationToken);
            if (!cleanup.Succeeded)
            {
                return ArchiveCleanupFailure(cleanup);
            }
        }

        if (!state.Archived)
        {
            SiteArchiveExportResult export = await archive.ExportAsync(id, cancellationToken);
            if (!export.Succeeded || string.IsNullOrWhiteSpace(export.ArchiveUrl))
            {
                return UseCaseResult<SiteDetailModel>.ExternalServiceUnavailable(
                    export.ErrorMessage
                        ?? "The site archive could not be created, so the site was not archived. Please try again.");
            }

            SiteArchiveClaimResult? claim = null;
            try
            {
                claim = await unitOfWork.ExecuteInTransactionAsync(
                    async token =>
                    {
                        SiteArchiveClaimResult result = await writes.TryClaimArchiveAsync(
                            id,
                            createdBy,
                            export.ArchiveUrl,
                            timeProvider.GetUtcNow().UtcDateTime,
                            token);
                        if (result.Claimed)
                        {
                            await unitOfWork.SaveChangesAsync(token);
                        }

                        return result;
                    },
                    cancellationToken);
            }
            catch (Exception persistenceException)
            {
                SiteArchiveState? durableState = null;
                try
                {
                    durableState = await reads.GetArchiveStateAsync(
                        id,
                        CancellationToken.None);
                }
                catch
                {
                    Rethrow(persistenceException);
                }

                if (string.IsNullOrWhiteSpace(durableState?.ArchiveUrl))
                {
                    Rethrow(persistenceException);
                }

                if (!string.Equals(
                        durableState.ArchiveUrl,
                        export.ArchiveUrl,
                        StringComparison.Ordinal))
                {
                    SiteArchiveCleanupResult cleanup = await CleanupArchiveAsync(
                        id,
                        durableState.ArchiveUrl,
                        CancellationToken.None);
                    if (!cleanup.Succeeded)
                    {
                        return ArchiveCleanupFailure(cleanup);
                    }
                }

                detailCancellationToken = CancellationToken.None;
            }

            if (claim?.DurableArchiveUrl is not null
                && !string.Equals(
                    claim.DurableArchiveUrl,
                    export.ArchiveUrl,
                    StringComparison.Ordinal))
            {
                SiteArchiveCleanupResult cleanup = await CleanupArchiveAsync(
                    id,
                    claim.DurableArchiveUrl,
                    CancellationToken.None);
                if (!cleanup.Succeeded)
                {
                    return ArchiveCleanupFailure(cleanup);
                }
            }
        }

        SiteDetailModel? detail = await ReadDetailAsync(user, id, detailCancellationToken);
        return detail is null
            ? SiteNotFound<SiteDetailModel>(id)
            : UseCaseResult<SiteDetailModel>.Success(detail);
    }

    private async Task<SiteArchiveCleanupResult> CleanupArchiveAsync(
        Guid siteId,
        string durableArchiveUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            return await archive.CleanupSupersededAsync(
                siteId,
                durableArchiveUrl,
                cancellationToken);
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return SiteArchiveCleanupResult.Failed(exception.Message);
        }
    }

    private static UseCaseResult<SiteDetailModel> ArchiveCleanupFailure(
        SiteArchiveCleanupResult cleanup) =>
        UseCaseResult<SiteDetailModel>.ExternalServiceUnavailable(
            cleanup.ErrorMessage
                ?? "The site archive candidate could not be reconciled with durable archive metadata. Contact support.");

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Rethrow(Exception exception)
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo
            .Capture(exception)
            .Throw();
    }

    public async Task<UseCaseResult<SiteDetailModel>> SaveCustomerLogoAsync(
        PortalUserContext user,
        Guid id,
        SiteLogoUpload upload,
        CancellationToken cancellationToken)
    {
        if (!await CanManageSiteAsync(user, id, cancellationToken))
        {
            return SiteNotFound<SiteDetailModel>(id);
        }

        SiteLogoSaveResult save = await logos.SaveAsync(id, upload, cancellationToken);
        if (save.Outcome == SiteLogoSaveOutcome.Invalid)
        {
            return UseCaseResult<SiteDetailModel>.Validation(
                new UseCaseError(
                    "logo",
                    save.Message ?? "The customer logo is invalid."));
        }

        SiteDetailModel? detail = await ReadDetailAsync(user, id, cancellationToken);
        return detail is null
            ? SiteNotFound<SiteDetailModel>(id)
            : UseCaseResult<SiteDetailModel>.Success(detail);
    }

    public async Task<UseCaseResult<SiteDetailModel>> DeleteCustomerLogoAsync(
        PortalUserContext user,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!await CanManageSiteAsync(user, id, cancellationToken))
        {
            return SiteNotFound<SiteDetailModel>(id);
        }

        await logos.DeleteAsync(id, cancellationToken);
        SiteDetailModel? detail = await ReadDetailAsync(user, id, cancellationToken);
        return detail is null
            ? SiteNotFound<SiteDetailModel>(id)
            : UseCaseResult<SiteDetailModel>.Success(detail);
    }

    public async Task<UseCaseResult<SiteLogoFile>> OpenCustomerLogoAsync(
        PortalUserContext user,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!await CanReadSiteAsync(user, id, cancellationToken))
        {
            return SiteNotFound<SiteLogoFile>(id);
        }

        SiteLogoFile? logo = await logos.OpenReadAsync(id, cancellationToken);
        return logo is null
            ? SiteNotFound<SiteLogoFile>(id)
            : UseCaseResult<SiteLogoFile>.Success(logo);
    }

    public Task<UseCaseResult<SiteNotificationSettingModel>> UpdateNotificationSettingAsync(
        PortalUserContext user,
        Guid siteId,
        Guid siteUserId,
        SiteNotificationSettingMutation request,
        CancellationToken cancellationToken)
    {
        SiteTimePairValidationResult timePair = SiteMutationValidator.ValidateTimePair(
            request.StartTime,
            request.EndTime,
            nameof(SiteNotificationSettingMutation.StartTime),
            nameof(SiteNotificationSettingMutation.EndTime));

        return unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                SiteAccessScope scope = SiteAuthorizationPolicy.ReadScope(
                    user,
                    timeProvider.GetUtcNow().UtcDateTime);
                if (!await reads.ExistsAsync(siteId, scope, token))
                {
                    return SiteNotFound<SiteNotificationSettingModel>(siteId);
                }

                SiteNotificationSettingTarget? target = await reads.GetNotificationSettingTargetAsync(
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

                if (!timePair.IsValid)
                {
                    return UseCaseResult<SiteNotificationSettingModel>.Validation(
                        [.. timePair.Errors]);
                }

                await writes.UpsertNotificationSettingAsync(
                    siteUserId,
                    request,
                    timePair.StartTime,
                    timePair.EndTime,
                    token);
                await unitOfWork.SaveChangesAsync(token);

                SiteNotificationSettingsData data = await reads.GetNotificationSettingsAsync(siteId, token)
                    ?? throw new InvalidOperationException(
                        $"Site '{siteId}' notification settings were not readable after a successful update.");
                SiteNotificationSettingsModel settings = await BuildNotificationSettingsAsync(
                    user,
                    data,
                    token);
                SiteNotificationSettingModel updated = settings.Settings.SingleOrDefault(
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
        Dictionary<Guid, PortalUserProfile> profiles = (await userDirectory.ListUsersAsync(cancellationToken))
            .ToDictionary(profile => profile.UserId);
        IEnumerable<SiteNotificationAssignment> assignments = user.IsCompanyUser && !user.IsAdmin
            ? data.Assignments.Where(assignment => assignment.UserId == user.UserId)
            : data.Assignments;

        return new SiteNotificationSettingsModel
        {
            SiteId = data.SiteId,
            SiteName = data.SiteName,
            Settings = assignments.Select(assignment =>
            {
                profiles.TryGetValue(assignment.UserId, out PortalUserProfile? profile);
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

    private async Task<SiteDetailModel?> ReadDetailAsync(
        PortalUserContext user,
        Guid siteId,
        CancellationToken cancellationToken)
    {
        SiteDetailModel? detail = await reads.GetAsync(siteId, cancellationToken);
        if (detail is null)
        {
            return null;
        }

        detail.CanManage = SiteAuthorizationPolicy.CanManage(user);
        detail.HasCustomerLogo = await logos.ExistsAsync(
            detail.Id,
            cancellationToken);
        return detail;
    }

    private static UseCaseResult<T> SiteNotFound<T>(Guid siteId) =>
        UseCaseResult<T>.NotFound($"Site '{siteId}' was not found.");

    private sealed class ContractClaimFailedException : Exception;
}
