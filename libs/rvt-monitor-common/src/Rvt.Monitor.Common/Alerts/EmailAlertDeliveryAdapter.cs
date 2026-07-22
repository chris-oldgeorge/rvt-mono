using Microsoft.Extensions.Options;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.Common.Alerts;

public sealed class EmailAlertDeliveryAdapter(
    INotificationDeliveryService notificationDelivery,
    IOptions<DurableAlertOptions> options,
    TimeProvider timeProvider) : IAlertDeliveryAdapter
{
    public string Kind => AlertDeliveryAdapterValidation.EmailKind;

    public async Task<AlertDeliveryAudit?> DeliverAsync(
        ClaimedAlertDelivery delivery,
        CancellationToken cancellationToken)
    {
        var envelope = AlertDeliveryAdapterValidation.ReadEnvelope(
            delivery,
            Kind,
            destination => !string.IsNullOrWhiteSpace(destination));
        await notificationDelivery.SendAsync(new NotificationDeliveryRequest(
            ToMessage(envelope.AlertType),
            NotificationChannel.Email,
            delivery.Destination,
            envelope.FleetNr,
            NotificationUrl(options.Value.PortalBaseUrl, delivery.NotificationId!.Value)),
            cancellationToken);
        return new AlertDeliveryAudit(
            delivery.NotificationId.Value,
            delivery.Destination,
            NotificationConstants.SENT_OK,
            timeProvider.GetUtcNow().UtcDateTime);
    }

    internal static NotificationMessageKind ToMessage(AlertType alertType) =>
        alertType switch
        {
            AlertType.Alert => NotificationMessageKind.Alert,
            AlertType.Caution => NotificationMessageKind.Caution,
            AlertType.BatteryAlert => NotificationMessageKind.BatteryAlert,
            AlertType.BatteryCaution => NotificationMessageKind.BatteryCaution,
            _ => NotificationMessageKind.Offline
        };

    internal static string NotificationUrl(string portalBaseUrl, Guid notificationId) =>
        $"{portalBaseUrl.TrimEnd('/')}/Notification/View/{notificationId}";
}
