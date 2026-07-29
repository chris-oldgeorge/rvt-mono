using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;

namespace Rvt.Monitor.CommonTests.Rules;

/// <summary>
/// Verifies alert activity day/time matching against local-time rule windows.
/// </summary>
/// <remarks>
/// All four monitor test projects carried a copy of these cases, each pinning
/// the behaviour under its own monitor's ambient <c>RvtConfig.RulePolicy</c>:
/// AirQ and Svantek held the local-time-hardened variant, MyAtm's copy encoded
/// its day-only policy implicitly (the same window case asserted the opposite
/// result), and Omnidots padded its windows by an hour to absorb the timezone
/// conversion it did not perform. This single copy makes each policy explicit
/// instead of depending on which process the test happens to run in, and also
/// covers the <see cref="Rvt.Monitor.Common.Notifications.AlertActivityTimeDto"/>
/// legacy twin, which inherits the behaviour.
/// </remarks>
[TestClass]
public sealed class AlertActivityTimeDtoTests
{
    private static readonly DateTime _weekday = DateTime.Parse("Tue, 3 Oct 2023 07:22:16 GMT");
    private static readonly DateTime _sunday = DateTime.Parse("Sun, 1 Oct 2023 07:22:16 GMT");
    private static readonly DateTime _saturday = DateTime.Parse("Sat, 30 Sep 2023 07:22:16 GMT");

    [TestInitialize]
    public void InitializeLogger() =>
        RvtLogger.CreateLogger(
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None)),
            nameof(AlertActivityTimeDtoTests));

    [TestMethod]
    public void InsideTheLocalTimeWindow_IsActive()
    {
        Assert.IsTrue(WindowedRule(_weekday, startOffsetMinutes: -1, endOffsetMinutes: 1).IsActive(_weekday));
    }

    [TestMethod]
    public void BeforeTheLocalTimeWindow_IsInactive()
    {
        Assert.IsFalse(WindowedRule(_weekday, startOffsetMinutes: -2, endOffsetMinutes: -1).IsActive(_weekday));
    }

    [TestMethod]
    public void AfterTheLocalTimeWindow_IsInactive()
    {
        Assert.IsFalse(WindowedRule(_weekday, startOffsetMinutes: 1, endOffsetMinutes: 2).IsActive(_weekday));
    }

    [TestMethod]
    public void WithoutAnyWindow_TheDayAloneDecides()
    {
        AlertActivityTimeDto rule = new()
        {
            Weekdays = true,
            Saturdays = false,
            Sundays = false,
            Policy = MonitorRulePolicy.Default
        };
        Assert.IsTrue(rule.IsActive(_weekday));
    }

    [TestMethod]
    public void WithOnlyAStartTime_TheDayAloneDecides()
    {
        AlertActivityTimeDto rule = new()
        {
            Weekdays = true,
            Saturdays = false,
            Sundays = false,
            StartTime = _weekday.TimeOfDay,
            Policy = MonitorRulePolicy.Default
        };
        Assert.IsTrue(rule.IsActive(_weekday));
    }

    [TestMethod]
    public void WithOnlyAnEndTime_TheDayAloneDecides()
    {
        AlertActivityTimeDto rule = new()
        {
            Weekdays = true,
            Saturdays = false,
            Sundays = false,
            EndTime = _weekday.TimeOfDay,
            Policy = MonitorRulePolicy.Default
        };
        Assert.IsTrue(rule.IsActive(_weekday));
    }

    [TestMethod]
    public void OutsideTheConfiguredDays_IsInactive()
    {
        Assert.IsFalse(DayRule(weekdays: false, saturdays: true, sundays: true).IsActive(_weekday));
        Assert.IsFalse(DayRule(weekdays: true, saturdays: true, sundays: false).IsActive(_sunday));
        Assert.IsFalse(DayRule(weekdays: true, saturdays: false, sundays: true).IsActive(_saturday));
    }

    [TestMethod]
    public void DayOnlyPolicy_IgnoresTheTimeWindow()
    {
        // MyAtm's rules carry no time window; under its policy the same
        // out-of-window rule that is inactive elsewhere stays active.
        MonitorRulePolicy dayOnly = MonitorRulePolicy.ForMonitorKind("myatm");
        Assert.IsFalse(dayOnly.AppliesActivityTimeWindow);

        AlertActivityTimeDto rule = new()
        {
            Weekdays = true,
            Saturdays = false,
            Sundays = false,
            StartTime = _weekday.AddMinutes(-2).TimeOfDay,
            EndTime = _weekday.AddMinutes(-1).TimeOfDay,
            Policy = dayOnly
        };
        Assert.IsTrue(rule.IsActive(_weekday));
    }

    [TestMethod]
    public void NotificationsTwin_InheritsTheSameBehaviour()
    {
        TimeSpan localTime = DateTimeUtil.UtcToLocal(_weekday.TimeOfDay);
        Rvt.Monitor.Common.Notifications.AlertActivityTimeDto twin = new()
        {
            Weekdays = true,
            Saturdays = false,
            Sundays = false,
            StartTime = localTime.Add(TimeSpan.FromMinutes(-1)),
            EndTime = localTime.Add(TimeSpan.FromMinutes(1)),
            Policy = MonitorRulePolicy.Default
        };

        Assert.IsTrue(twin.IsActive(_weekday));
        Assert.IsInstanceOfType<AlertActivityTimeDto>(twin);
    }

    private static AlertActivityTimeDto WindowedRule(DateTime moment, int startOffsetMinutes, int endOffsetMinutes)
    {
        // Built through the same local-time conversion the rule itself applies,
        // so the expectation holds in any host timezone.
        TimeSpan localTime = DateTimeUtil.UtcToLocal(moment.TimeOfDay);
        return new AlertActivityTimeDto
        {
            Weekdays = true,
            Saturdays = false,
            Sundays = false,
            StartTime = localTime.Add(TimeSpan.FromMinutes(startOffsetMinutes)),
            EndTime = localTime.Add(TimeSpan.FromMinutes(endOffsetMinutes)),
            Policy = MonitorRulePolicy.Default
        };
    }

    private static AlertActivityTimeDto DayRule(bool weekdays, bool saturdays, bool sundays)
    {
        return new AlertActivityTimeDto
        {
            Weekdays = weekdays,
            Saturdays = saturdays,
            Sundays = sundays,
            StartTime = TimeSpan.Zero,
            EndTime = TimeSpan.FromHours(24),
            Policy = MonitorRulePolicy.Default
        };
    }
}
