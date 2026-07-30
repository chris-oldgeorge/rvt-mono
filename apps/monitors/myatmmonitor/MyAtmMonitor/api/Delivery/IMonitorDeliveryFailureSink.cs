namespace MyAtm.Delivery;

public interface IMonitorDeliveryFailureSink
{
    Task RecordFailureAsync(
        MonitorDeliveryMessage message,
        string error,
        bool terminal,
        CancellationToken cancellationToken = default);
}
