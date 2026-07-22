using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.Common.Alerts;

public interface IAlertAcceptancePolicy
{
    AlertOccurrenceOutcome Evaluate(
        AlertType incoming,
        IReadOnlyCollection<AlertType> recentAlertTypes);
}
