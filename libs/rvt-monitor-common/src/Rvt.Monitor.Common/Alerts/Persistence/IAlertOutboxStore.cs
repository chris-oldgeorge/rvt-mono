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

    /// <summary>
    /// Returns a claimed delivery to Pending, due at
    /// <paramref name="nextAttemptAt"/>, without consuming a delivery attempt.
    /// </summary>
    /// <remarks>
    /// A delivery whose recipient's quiet-hours window is currently closed is
    /// deferred, not dropped and not dead-lettered. Claiming increments
    /// attempt_count, so the defer undoes that increment: waiting for a
    /// window is not a failed attempt and must not erode the retry budget.
    /// </remarks>
    Task<bool> DeferAsync(
        Guid id,
        Guid leaseId,
        DateTime nextAttemptAt,
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
