// The namespace is retained from the shared-kernel folder this file moved out
// of, so its consumers keep compiling; IDE0130 would force a rename ripple.
#pragma warning disable IDE0130
namespace Rvt.Monitor.Common.Delivery;

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
}
