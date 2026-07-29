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

            // Transition-driven types: the emitting handlers only signal on a
            // state change (offline flag, battery status), so no windowed
            // suppression applies here.
            AlertType.Offline => AlertOccurrenceOutcome.Accepted,
            AlertType.BatteryAlert => AlertOccurrenceOutcome.Accepted,
            AlertType.BatteryCaution => AlertOccurrenceOutcome.Accepted,
            _ => throw new ArgumentOutOfRangeException(nameof(incoming), incoming, "Unsupported alert type.")
        };
}
