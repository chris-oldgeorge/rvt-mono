namespace MyAtm.Delivery;

public interface IMonitorDeliveryOutboxCommands
{
    Task<bool> CompleteAsync(
        Guid id,
        Guid leaseId,
        DateTime completedAt,
        MonitorDeliveryAudit? audit,
        CancellationToken cancellationToken = default);

    Task<bool> RetryAsync(
        Guid id,
        Guid leaseId,
        DateTime nextAttemptAt,
        string error,
        CancellationToken cancellationToken = default);

    Task<bool> DeadLetterAsync(
        Guid id,
        Guid leaseId,
        DateTime failedAt,
        string error,
        MonitorDeliveryAudit? audit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes completed deliveries older than <paramref name="cutoff"/> for one
    /// producer. Without it the outbox grows without bound and
    /// <c>ClaimNextDueAsync</c> orders over that table every minute; the shared
    /// durable-alert stack purges the same way (<c>DeleteCompletedBeforeAsync</c>).
    /// </summary>
    Task<int> DeleteCompletedBeforeAsync(
        string producer,
        DateTime cutoff,
        CancellationToken cancellationToken = default);
}
