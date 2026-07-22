namespace Rvt.Monitor.Common.Alerts.Persistence;

public sealed record AlertCommitRequest(
    AlertSignal Signal,
    byte[] SourceKeyHash,
    Guid NotificationId,
    DateTime CreatedAt);
