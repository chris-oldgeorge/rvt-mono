namespace Rvt.Monitor.Common.Alerts.Persistence;

public sealed record AlertCommitResult(
    Guid OccurrenceId,
    Guid? NotificationId,
    AlertOccurrenceOutcome Outcome,
    bool IsDuplicate);
