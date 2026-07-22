namespace Rvt.Monitor.Common.Communications;

public interface INotificationDeliveryService
{
    Task SendAsync(
        NotificationDeliveryRequest request,
        CancellationToken cancellationToken = default);
}
