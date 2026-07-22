using System.Text.Json;

namespace Rvt.Monitor.Common.Delivery;

public static class MonitorDeliveryPayloadCodec
{
    public static MonitorDeliveryPayloadV1 Decode(MonitorDeliveryMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.PayloadVersion != 1)
        {
            throw new InvalidDataException($"Unsupported delivery payload version '{message.PayloadVersion}'.");
        }

        MonitorDeliveryPayloadV1 payload;
        try
        {
            payload = JsonSerializer.Deserialize<MonitorDeliveryPayloadV1>(message.Payload)
                ?? throw new InvalidDataException("Delivery payload is required.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Delivery payload is not valid version 1 JSON.", exception);
        }

        if (payload.Timestamp.Kind != DateTimeKind.Utc)
        {
            throw new InvalidDataException("Delivery payload timestamp must be UTC.");
        }

        if (string.IsNullOrWhiteSpace(payload.SerialId))
        {
            throw new InvalidDataException("Delivery payload serial ID is required.");
        }

        var requiresNotification = message.Kind is
            MonitorDeliveryKind.MqttAlert or
            MonitorDeliveryKind.Email or
            MonitorDeliveryKind.Sms;
        if (message.Kind != MonitorDeliveryKind.MqttDataInserted && payload.NotificationId == Guid.Empty)
        {
            throw new InvalidDataException("Delivery payload notification ID is required for alert and contact delivery.");
        }

        if (requiresNotification &&
            message.NotificationId.HasValue &&
            message.NotificationId.Value != payload.NotificationId)
        {
            throw new InvalidDataException("Delivery row notification ID does not match the payload.");
        }

        return payload;
    }
}
