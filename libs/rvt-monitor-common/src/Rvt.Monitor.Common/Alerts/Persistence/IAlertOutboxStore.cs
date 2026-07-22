namespace Rvt.Monitor.Common.Alerts.Persistence;

public interface IAlertOutboxStore
{
    Task<ClaimedAlertDelivery?> ClaimNextDueAsync(
        DateTime utcNow,
        TimeSpan lease,
        CancellationToken cancellationToken = default);

    Task<bool> CompleteAsync(
        Guid id,
        Guid leaseId,
        DateTime completedAt,
        AlertDeliveryAudit? audit,
        CancellationToken cancellationToken = default);

    Task<bool> RetryAsync(
        Guid id,
        Guid leaseId,
        DateTime nextAttemptAt,
        string error,
        bool deadLetter,
        AlertDeliveryAudit? audit,
        CancellationToken cancellationToken = default);

    Task<int> DeleteCompletedBeforeAsync(
        DateTime cutoff,
        CancellationToken cancellationToken = default);
}
