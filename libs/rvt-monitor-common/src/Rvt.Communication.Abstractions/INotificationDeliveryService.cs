namespace Rvt.Communication.Abstractions;

public interface INotificationDeliveryService
{
    Task SendAsync(NotificationDeliveryRequest request, CancellationToken cancellationToken = default);
}
