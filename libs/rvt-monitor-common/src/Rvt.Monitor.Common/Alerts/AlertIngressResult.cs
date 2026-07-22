namespace Rvt.Monitor.Common.Alerts;

public sealed record AlertIngressResult(
    Guid OccurrenceId,
    Guid? NotificationId,
    AlertOccurrenceOutcome Outcome,
    bool IsDuplicate);
