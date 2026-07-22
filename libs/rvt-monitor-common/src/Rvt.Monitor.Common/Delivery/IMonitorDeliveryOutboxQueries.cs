namespace Rvt.Monitor.Common.Delivery;

public interface IMonitorDeliveryOutboxQueries
{
    Task<MonitorDeliveryMessage?> ClaimNextDueAsync(
        string producer,
        DateTime utcNow,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);
}
