namespace RvtPortal.Application.Sites.Ports;

public sealed record SiteArchiveClaimResult(
    bool Claimed,
    string? DurableArchiveUrl);

public interface ISiteWritePort
{
    Task<Guid> CreateAsync(
        ValidatedSiteMutation mutation,
        DateTime createDateUtc,
        CancellationToken cancellationToken);

    Task<bool> TryClaimContractAsync(
        Guid contractId,
        Guid companyId,
        Guid siteId,
        CancellationToken cancellationToken);

    Task<bool> UpdateAsync(
        Guid siteId,
        ValidatedSiteMutation mutation,
        CancellationToken cancellationToken);

    Task<SiteArchiveClaimResult> TryClaimArchiveAsync(
        Guid siteId,
        string createdBy,
        string archiveUrl,
        DateTime archivedUtc,
        CancellationToken cancellationToken);

    Task UpsertNotificationSettingAsync(
        Guid siteUserId,
        SiteNotificationSettingMutation request,
        TimeSpan? startTime,
        TimeSpan? endTime,
        CancellationToken cancellationToken);
}
