using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.Common.Alerts;

/// <summary>
/// The self-contained payload of one outbox row.
/// </summary>
/// <param name="SendWindowStart">
/// The recipient's quiet-hours window, carried per row so the dispatcher can
/// evaluate it against the send clock. Null on channels with no recipient
/// (MQTT), on contacts with no window, and on rows written before the window
/// moved out of planning — all of which send immediately.
/// </param>
public sealed record AlertDeliveryEnvelope(
    int Version,
    Guid NotificationId,
    DateTime Timestamp,
    AlertType AlertType,
    string SerialId,
    int? CustomerId,
    string FleetNr,
    string Message,
    TimeSpan? SendWindowStart = null,
    TimeSpan? SendWindowEnd = null);
