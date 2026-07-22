using System.Text.Json;
using Rvt.Monitor.Common.Rules;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;

namespace Rvt.Monitor.Common.Delivery;

public sealed record RuleAlertDeliveryPlan(
    NotificationDto Notification,
    IReadOnlyList<MonitorDeliveryRequest> Deliveries);

public sealed class RuleAlertDeliveryPlanner
{
    public RuleAlertDeliveryPlan Plan(
        RuleNotificationRequest request,
        IReadOnlyList<RvtContactDto> contacts,
        string producer,
        int? customerId,
        string correlationKey,
        DateTime createdAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(contacts);
        if (!MonitorDeliveryProducers.IsKnown(producer))
        {
            throw new ArgumentException("Unknown monitor delivery producer.", nameof(producer));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(correlationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SerialId);
        if (request.AlertTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Alert time must be UTC.", nameof(request));
        }

        if (createdAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Creation time must be UTC.", nameof(createdAt));
        }

        var notificationId = MonitorDeliveryIdentity.CreateGuid($"notification:{correlationKey}");
        var notification = new NotificationDto(
            notificationId,
            request.AlertTime,
            request.LimitOn,
            request.AveragingPeriod,
            request.Level,
            closedTime: null,
            closedByUser: null,
            request.AlertType,
            request.Field,
            request.MonitorId);
        var payload = JsonSerializer.Serialize(new MonitorDeliveryPayloadV1(
            notificationId,
            request.AlertTime,
            request.SerialId,
            customerId,
            request.FleetNr,
            request.AlertType,
            request.Field,
            request.Level));
        var deliveries = new List<MonitorDeliveryRequest>
        {
            CreateDelivery(
                producer,
                notificationId,
                correlationKey,
                MonitorDeliveryKind.MqttAlert,
                "alert",
                payload,
                createdAt)
        };

        var emailDestinations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var contact in contacts)
        {
            if (!contact.Email ||
                !contact.ShouldSendAtTime(request.AlertTime) ||
                string.IsNullOrWhiteSpace(contact.EmailAddress) ||
                !emailDestinations.Add(contact.EmailAddress))
            {
                continue;
            }

            deliveries.Add(CreateDelivery(
                producer,
                notificationId,
                correlationKey,
                MonitorDeliveryKind.Email,
                contact.EmailAddress,
                payload,
                createdAt));
        }

        var smsDestinations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var contact in contacts)
        {
            if (!contact.SMS ||
                !contact.ShouldSendAtTime(request.AlertTime) ||
                string.IsNullOrWhiteSpace(contact.PhoneNumber) ||
                !smsDestinations.Add(contact.PhoneNumber))
            {
                continue;
            }

            deliveries.Add(CreateDelivery(
                producer,
                notificationId,
                correlationKey,
                MonitorDeliveryKind.Sms,
                contact.PhoneNumber!,
                payload,
                createdAt));
        }

        return new RuleAlertDeliveryPlan(notification, deliveries);
    }

    private static MonitorDeliveryRequest CreateDelivery(
        string producer,
        Guid notificationId,
        string correlationKey,
        MonitorDeliveryKind kind,
        string destination,
        string payload,
        DateTime createdAt)
    {
        var deliveryKey = $"{correlationKey}:{kind}:{destination}";
        return new MonitorDeliveryRequest(
            MonitorDeliveryIdentity.CreateGuid($"outbox:{deliveryKey}"),
            producer,
            notificationId,
            correlationKey,
            deliveryKey,
            kind,
            destination,
            PayloadVersion: 1,
            payload,
            createdAt);
    }
}
