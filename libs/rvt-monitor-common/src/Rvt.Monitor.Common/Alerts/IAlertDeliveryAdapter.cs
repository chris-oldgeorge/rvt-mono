using System.Text.Json;
using Rvt.Monitor.Common.Alerts.Persistence;

namespace Rvt.Monitor.Common.Alerts;

public interface IAlertDeliveryAdapter
{
    string Kind { get; }

    Task<AlertDeliveryAudit?> DeliverAsync(
        ClaimedAlertDelivery delivery,
        CancellationToken cancellationToken);
}

internal static class AlertDeliveryAdapterValidation
{
    internal const string MqttKind = "MqttAlert";
    internal const string EmailKind = "Email";
    internal const string SmsKind = "Sms";

    internal static AlertDeliveryEnvelope ReadEnvelope(
        ClaimedAlertDelivery delivery,
        string expectedKind,
        Func<string, bool> isValidDestination)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        if (!string.Equals(delivery.Kind, expectedKind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Alert delivery kind does not match the selected adapter.");
        }

        if (!isValidDestination(delivery.Destination))
        {
            throw new InvalidOperationException("Alert delivery destination is invalid.");
        }

        if (delivery.NotificationId is not { } authoritativeNotificationId ||
            authoritativeNotificationId == Guid.Empty)
        {
            throw new InvalidOperationException("Alert delivery notification ID is invalid.");
        }

        var envelope = JsonSerializer.Deserialize<AlertDeliveryEnvelope>(delivery.Payload)
            ?? throw new InvalidOperationException("Alert delivery envelope is missing.");
        if (envelope.Version != 1)
        {
            throw new InvalidOperationException("Alert delivery envelope version is unsupported.");
        }

        if (envelope.NotificationId == Guid.Empty)
        {
            throw new InvalidOperationException("Alert delivery notification ID is invalid.");
        }

        if (envelope.NotificationId != authoritativeNotificationId)
        {
            throw new InvalidOperationException("Alert delivery notification ID does not match its occurrence.");
        }

        return envelope;
    }
}
