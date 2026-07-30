// The namespace is retained from the shared-kernel folder this file moved out
// of, so its consumers keep compiling; IDE0130 would force a rename ripple.
#pragma warning disable IDE0130
namespace Rvt.Monitor.Common.Delivery;

public interface IMonitorDeliveryOutboxQueries
{
    Task<MonitorDeliveryMessage?> ClaimNextDueAsync(
        string producer,
        DateTime utcNow,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);
}
