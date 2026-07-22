namespace Rvt.Monitor.Common.Alerts.Persistence;

public sealed record AlertDeliveryAudit(
    Guid NotificationId,
    string Address,
    string Message,
    DateTime SentAt);
