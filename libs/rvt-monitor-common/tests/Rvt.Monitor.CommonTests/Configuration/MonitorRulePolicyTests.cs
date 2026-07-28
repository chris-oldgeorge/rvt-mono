using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Rules;

namespace Rvt.Monitor.CommonTests.Configuration;

/// <summary>
/// These shared rules used to branch on static configuration that inferred the
/// monitor from the entry assembly name, so the same type evaluated differently
/// depending on which executable loaded it and could not be exercised without
/// global state. The behaviour is now an explicit policy value.
/// </summary>
[TestClass]
public sealed class MonitorRulePolicyTests
{
    private static readonly DateTime _saturdayOutsideWindow =
        new(2026, 3, 7, 22, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void ForMonitorKind_MyAtmDoesNotApplyTheActivityTimeWindow()
    {
        var policy = MonitorRulePolicy.ForMonitorKind("myatm");

        Assert.IsFalse(policy.AppliesActivityTimeWindow);
    }

    [TestMethod]
    [DataRow("airq")]
    [DataRow("svantek")]
    [DataRow("omnidots")]
    [DataRow(null)]
    public void ForMonitorKind_EveryOtherMonitorAppliesTheActivityTimeWindow(string? monitorKind)
    {
        var policy = MonitorRulePolicy.ForMonitorKind(monitorKind);

        Assert.IsTrue(policy.AppliesActivityTimeWindow);
    }

    [TestMethod]
    [DataRow("airq", MonitorNotificationStyle.Noise)]
    [DataRow("svantek", MonitorNotificationStyle.Noise)]
    [DataRow("omnidots", MonitorNotificationStyle.Vibration)]
    [DataRow("myatm", MonitorNotificationStyle.Generic)]
    [DataRow(null, MonitorNotificationStyle.Generic)]
    public void ForMonitorKind_SelectsTheNotificationStyle(
        string? monitorKind,
        MonitorNotificationStyle expected)
    {
        Assert.AreEqual(expected, MonitorRulePolicy.ForMonitorKind(monitorKind).NotificationStyle);
    }

    [TestMethod]
    public void IsActive_WithoutATimeWindowPolicy_ConsidersOnlyTheDay()
    {
        AlertActivityTimeDto rule = CreateSaturdayRule();

        // The time is outside the configured window, but this policy ignores it.
        Assert.IsTrue(rule.IsActive(_saturdayOutsideWindow, MonitorRulePolicy.ForMonitorKind("myatm")));
    }

    [TestMethod]
    public void IsActive_WithATimeWindowPolicy_ConsidersTheDayAndTheWindow()
    {
        AlertActivityTimeDto rule = CreateSaturdayRule();

        Assert.IsFalse(rule.IsActive(_saturdayOutsideWindow, MonitorRulePolicy.ForMonitorKind("airq")));
    }

    [TestMethod]
    public void IsActive_WithADayThatDoesNotApply_IsInactiveUnderEveryPolicy()
    {
        var rule = new AlertActivityTimeDto
        {
            Weekdays = true,
            Saturdays = false,
            Sundays = false,
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(18),
        };

        Assert.IsFalse(rule.IsActive(_saturdayOutsideWindow, MonitorRulePolicy.ForMonitorKind("myatm")));
        Assert.IsFalse(rule.IsActive(_saturdayOutsideWindow, MonitorRulePolicy.ForMonitorKind("airq")));
    }

    [TestMethod]
    public void IsActive_UsesTheRulesOwnPolicyWhenNoneIsSupplied()
    {
        var rule = new AlertActivityTimeDto
        {
            Weekdays = true,
            Saturdays = true,
            Sundays = true,
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(18),
            Policy = MonitorRulePolicy.ForMonitorKind("myatm"),
        };

        Assert.IsTrue(rule.IsActive(_saturdayOutsideWindow));
    }

    [TestMethod]
    public void IsActive_WithANullPolicy_ThrowsArgumentNullException()
    {
        AlertActivityTimeDto rule = CreateSaturdayRule();

        Assert.ThrowsExactly<ArgumentNullException>(() => rule.IsActive(_saturdayOutsideWindow, null!));
    }

    private static AlertActivityTimeDto CreateSaturdayRule() => new()
    {
        Weekdays = true,
        Saturdays = true,
        Sundays = true,
        StartTime = TimeSpan.FromHours(8),
        EndTime = TimeSpan.FromHours(18),
    };
}
