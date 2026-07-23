using RvtPortal.Application.Common;
using RvtPortal.Application.Identity;
using RvtPortal.Application.Sites;
using RvtPortal.Application.Sites.Ports;

namespace RvtPortal.Application.Tests.Sites;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class EmptyPortalUserDirectory : IPortalUserDirectory
{
    public Task<IReadOnlyList<PortalUserProfile>> ListUsersAsync(CancellationToken token) =>
        Task.FromResult<IReadOnlyList<PortalUserProfile>>([]);

    public Task<PortalUserProfile?> FindByIdAsync(Guid id, CancellationToken token) =>
        Task.FromResult<PortalUserProfile?>(null);
}

internal class FakeSiteReadPort : ISiteReadPort
{
    public bool Exists { get; set; }
    public int ExistsCallCount { get; private set; }
    public SiteAccessScope? LastScope { get; set; }
    public SiteQuery? LastQuery { get; set; }
    public PagedResult<SiteListModel> QueryResult { get; set; } = new();

    public virtual Task<bool> ExistsAsync(
        Guid siteId,
        SiteAccessScope scope,
        CancellationToken token)
    {
        ExistsCallCount++;
        LastScope = scope;
        return Task.FromResult(Exists);
    }

    public virtual Task<PagedResult<SiteListModel>> QueryAsync(
        SiteAccessScope scope,
        SiteQuery query,
        CancellationToken token)
    {
        LastScope = scope;
        LastQuery = query;
        return Task.FromResult(QueryResult);
    }

    public virtual Task<SiteOptionsModel> OptionsAsync(
        Guid? companyId,
        CancellationToken token) =>
        Task.FromResult(new SiteOptionsModel());

    public virtual Task<SiteDetailModel?> GetAsync(
        Guid siteId,
        CancellationToken token) =>
        Task.FromResult<SiteDetailModel?>(null);

    public virtual Task<PagedResult<SiteMonitorModel>> QueryMonitorsAsync(
        Guid siteId,
        PageRequest page,
        CancellationToken token) =>
        Task.FromResult(new PagedResult<SiteMonitorModel>());

    public virtual Task<PagedResult<SiteNotificationModel>> QueryOpenNotificationsAsync(
        Guid siteId,
        PageRequest page,
        CancellationToken token) =>
        Task.FromResult(new PagedResult<SiteNotificationModel>());

    public virtual Task<SiteNotificationSettingsData?> GetNotificationSettingsAsync(
        Guid siteId,
        CancellationToken token) =>
        Task.FromResult<SiteNotificationSettingsData?>(null);

    public virtual Task<SiteMutationValidationData> GetMutationValidationDataAsync(
        SiteMutation request,
        Guid? currentSiteId,
        CancellationToken token) =>
        Task.FromResult(new SiteMutationValidationData(
            DuplicateSiteName: false,
            CompanyExists: true,
            ContractExists: true,
            ContractIsUnassigned: true,
            ContractBelongsToCompany: true));

    public virtual Task<SiteNotificationSettingTarget?> GetNotificationSettingTargetAsync(
        Guid siteId,
        Guid siteUserId,
        CancellationToken token) =>
        Task.FromResult<SiteNotificationSettingTarget?>(null);
}

internal sealed class InlineUnitOfWork : IApplicationUnitOfWork
{
    public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
        Func<CancellationToken, Task<TResponse>> operation,
        CancellationToken cancellationToken) =>
        await operation(cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(1);
}

internal sealed class NoOpSiteWritePort : ISiteWritePort
{
    public Task<Guid> CreateAsync(
        ValidatedSiteMutation mutation,
        DateTime createDateUtc,
        CancellationToken cancellationToken) =>
        Task.FromResult(Guid.NewGuid());

    public Task<bool> UpdateAsync(
        Guid siteId,
        ValidatedSiteMutation mutation,
        CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task<bool> TryClaimContractAsync(
        Guid contractId,
        Guid companyId,
        Guid siteId,
        CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task UpsertNotificationSettingAsync(
        Guid siteUserId,
        SiteNotificationSettingMutation request,
        TimeSpan? startTime,
        TimeSpan? endTime,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
