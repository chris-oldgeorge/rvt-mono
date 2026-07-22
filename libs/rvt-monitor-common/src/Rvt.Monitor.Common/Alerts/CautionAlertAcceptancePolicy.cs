using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.Common.Alerts;

public sealed class CautionAlertAcceptancePolicy : IAlertAcceptancePolicy
{
    public AlertOccurrenceOutcome Evaluate(
        AlertType incoming,
        IReadOnlyCollection<AlertType> recentAlertTypes) =>
        incoming switch
        {
            AlertType.Ignore => AlertOccurrenceOutcome.Ignored,
            AlertType.Caution when recentAlertTypes.Contains(AlertType.Caution)
                || recentAlertTypes.Contains(AlertType.Alert) => AlertOccurrenceOutcome.Suppressed,
            AlertType.Caution => AlertOccurrenceOutcome.Accepted,
            AlertType.Alert when recentAlertTypes.Contains(AlertType.Alert) => AlertOccurrenceOutcome.Suppressed,
            AlertType.Alert => AlertOccurrenceOutcome.Accepted,
            _ => throw new ArgumentOutOfRangeException(nameof(incoming), incoming, "Unsupported alert type.")
        };
}
