using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.CommonTests.Alerts;

[TestClass]
public sealed class CautionAlertAcceptancePolicyTests
{
    private readonly CautionAlertAcceptancePolicy policy = new();

    [TestMethod]
    [DataRow(AlertType.Ignore, null, AlertOccurrenceOutcome.Ignored)]
    [DataRow(AlertType.Caution, null, AlertOccurrenceOutcome.Accepted)]
    [DataRow(AlertType.Caution, AlertType.Caution, AlertOccurrenceOutcome.Suppressed)]
    [DataRow(AlertType.Caution, AlertType.Alert, AlertOccurrenceOutcome.Suppressed)]
    [DataRow(AlertType.Alert, null, AlertOccurrenceOutcome.Accepted)]
    [DataRow(AlertType.Alert, AlertType.Caution, AlertOccurrenceOutcome.Accepted)]
    [DataRow(AlertType.Alert, AlertType.Alert, AlertOccurrenceOutcome.Suppressed)]
    public void Evaluate_ReturnsExpectedOutcome(
        AlertType incoming,
        AlertType? recent,
        AlertOccurrenceOutcome expected)
    {
        IReadOnlyCollection<AlertType> recentTypes = recent.HasValue
            ? [recent.Value]
            : [];
        Assert.AreEqual(expected, policy.Evaluate(incoming, recentTypes));
    }

    [TestMethod]
    [DataRow((AlertType)999)]
    public void Evaluate_RejectsUnsupportedIncomingAlertTypes(AlertType incoming)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => policy.Evaluate(incoming, []));
    }

    [DataRow(AlertType.Offline)]
    [DataRow(AlertType.BatteryAlert)]
    [DataRow(AlertType.BatteryCaution)]
    [TestMethod]
    public void TransitionDrivenTypes_AreAcceptedRegardlessOfRecentHistory(AlertType incoming)
    {
        // The emitting handlers only signal on a state change, so the policy
        // applies no windowed suppression to these types.
        CautionAlertAcceptancePolicy policy = new();

        Assert.AreEqual(
            AlertOccurrenceOutcome.Accepted,
            policy.Evaluate(incoming, [AlertType.Alert, AlertType.Caution]));
        Assert.AreEqual(
            AlertOccurrenceOutcome.Accepted,
            policy.Evaluate(incoming, []));
    }
}
