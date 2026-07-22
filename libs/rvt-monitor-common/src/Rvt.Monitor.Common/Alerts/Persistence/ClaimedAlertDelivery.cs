namespace Rvt.Monitor.Common.Alerts.Persistence;

public sealed record ClaimedAlertDelivery(
    Guid Id,
    Guid OccurrenceId,
    Guid? NotificationId,
    string DeliveryKey,
    string Kind,
    string Destination,
    string Payload,
    string Status,
    int AttemptCount,
    DateTime NextAttemptAt,
    Guid LeaseId,
    DateTime LeaseUntil,
    DateTime? CompletedAt,
    string? LastError,
    DateTime CreatedAt);
