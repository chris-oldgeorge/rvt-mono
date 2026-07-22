using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.Common.Delivery;

public static class MonitorDeliveryProducers
{
    public const string MyAtm = "MyAtm";
    public const string Svantek = "Svantek";

    public static bool IsKnown(string producer) =>
        string.Equals(producer, MyAtm, StringComparison.Ordinal) ||
        string.Equals(producer, Svantek, StringComparison.Ordinal);
}

public enum MonitorDeliveryKind
{
    MqttDataInserted,
    MqttAlert,
    Email,
    Sms
}

public enum MonitorDeliveryFailureMode
{
    DeadLetterOnly,
    AnyDeliveryFailure
}

public sealed record MonitorDeliveryRequest(
    Guid Id,
    string Producer,
    Guid? NotificationId,
    string? CorrelationKey,
    string DeliveryKey,
    MonitorDeliveryKind Kind,
    string Destination,
    int PayloadVersion,
    string Payload,
    DateTime CreatedAt);

public sealed record MonitorDeliveryMessage(
    Guid Id,
    string Producer,
    Guid? NotificationId,
    string? CorrelationKey,
    string DeliveryKey,
    MonitorDeliveryKind Kind,
    string Destination,
    int PayloadVersion,
    string Payload,
    int AttemptCount,
    Guid LeaseId);

public sealed record MonitorDeliveryAudit(
    Guid NotificationId,
    string Address,
    string Result,
    DateTime SentAt);

public sealed record MonitorDeliveryPayloadV1(
    Guid NotificationId,
    DateTime Timestamp,
    string SerialId,
    int? CustomerId,
    string FleetNr,
    AlertType AlertType,
    string Field,
    double Level,
    string? PortalBaseUrl = null);
