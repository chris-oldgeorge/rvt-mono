using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.Common.Alerts;

public sealed record AlertDeliveryEnvelope(
    int Version,
    Guid NotificationId,
    DateTime Timestamp,
    AlertType AlertType,
    string SerialId,
    int? CustomerId,
    string FleetNr,
    string Message);
