using Microsoft.Extensions.Options;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.Common.Alerts;

public sealed class SmsAlertDeliveryAdapter(
    INotificationDeliveryService notificationDelivery,
    IOptions<DurableAlertOptions> options,
    TimeProvider timeProvider) : IAlertDeliveryAdapter
{
    public string Kind => AlertDeliveryAdapterValidation.SmsKind;

    public async Task<AlertDeliveryAudit?> DeliverAsync(
        ClaimedAlertDelivery delivery,
        CancellationToken cancellationToken)
    {
        var envelope = AlertDeliveryAdapterValidation.ReadEnvelope(
            delivery,
            Kind,
            destination => !string.IsNullOrWhiteSpace(destination));
        await notificationDelivery.SendAsync(new NotificationDeliveryRequest(
            EmailAlertDeliveryAdapter.ToMessage(envelope.AlertType),
            NotificationChannel.Sms,
            delivery.Destination,
            envelope.FleetNr,
            EmailAlertDeliveryAdapter.NotificationUrl(
                options.Value.PortalBaseUrl,
                delivery.NotificationId!.Value)),
            cancellationToken);
        return new AlertDeliveryAudit(
            delivery.NotificationId.Value,
            delivery.Destination,
            NotificationConstants.SENT_OK,
            timeProvider.GetUtcNow().UtcDateTime);
    }
}
