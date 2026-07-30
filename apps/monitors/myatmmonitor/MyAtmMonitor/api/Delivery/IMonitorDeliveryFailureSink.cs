// The namespace is retained from the shared-kernel folder this file moved out
// of, so its consumers keep compiling; IDE0130 would force a rename ripple.
#pragma warning disable IDE0130
namespace Rvt.Monitor.Common.Delivery;

public interface IMonitorDeliveryFailureSink
{
    Task RecordFailureAsync(
        MonitorDeliveryMessage message,
        string error,
        bool terminal,
        CancellationToken cancellationToken = default);
}
