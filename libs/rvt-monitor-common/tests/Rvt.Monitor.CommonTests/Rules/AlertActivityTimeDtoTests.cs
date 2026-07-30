using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Rules;

namespace Rvt.Monitor.CommonTests.Rules;

/// <summary>
/// Verifies alert activity day/time matching against UTC wall-clock windows.
/// </summary>
/// <remarks>
/// Product ruling 2026-07-30: all alert timing is UTC wall-clock. Rule
/// activity windows previously converted the evaluated moment into the
/// configured local timezone, which made quiet hours drift against the
/// contact send-windows (evaluated in UTC) whenever DST applied. Configured
/// hours are now UTC and deliberately do not track DST. These cases also
/// cover the <see cref="Rvt.Monitor.Common.Notifications.AlertActivityTimeDto"/>
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
    public void InsideTheUtcTimeWindow_IsActive()
    {
        Assert.IsTrue(WindowedRule(_weekday, startOffsetMinutes: -1, endOffsetMinutes: 1).IsActive(_weekday));
    }

    [TestMethod]
    public void BeforeTheUtcTimeWindow_IsInactive()
    {
        Assert.IsFalse(WindowedRule(_weekday, startOffsetMinutes: -2, endOffsetMinutes: -1).IsActive(_weekday));
    }

    [TestMethod]
    public void AfterTheUtcTimeWindow_IsInactive()
    {
        Assert.IsFalse(WindowedRule(_weekday, startOffsetMinutes: 1, endOffsetMinutes: 2).IsActive(_weekday));
    }

    [TestMethod]
    public void EvaluatesTheWindowInUtcWallClockTime()
    {
        // Pins the 2026-07-30 timezone ruling: the moment's UTC time of day is
        // compared to the configured hours as-is, with no local-timezone (and
        // therefore no DST) conversion. Under the old local evaluation a
        // BST-summer run shifted 07:22 UTC to 08:22 local and this window
        // missed, splitting rule windows from the UTC contact send-windows.
        DateTime utcMoment = new(2026, 7, 28, 7, 22, 16, DateTimeKind.Utc); // a Tuesday in BST
        AlertActivityTimeDto rule = new()
        {
            Weekdays = true,
            Saturdays = true,
            Sundays = true,
            StartTime = TimeSpan.FromHours(7),
            EndTime = TimeSpan.FromHours(8),
            Policy = MonitorRulePolicy.Default
        };

        Assert.IsTrue(rule.IsActive(utcMoment));                // 07:22 UTC
        Assert.IsFalse(rule.IsActive(utcMoment.AddHours(1)));   // 08:22 UTC
        Assert.IsFalse(rule.IsActive(utcMoment.AddHours(-1)));  // 06:22 UTC
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

    private static AlertActivityTimeDto WindowedRule(DateTime moment, int startOffsetMinutes, int endOffsetMinutes)
    {
        // Windows are UTC wall-clock, so they are built straight from the
        // moment's UTC time of day and hold in any host timezone.
        TimeSpan utcTime = moment.TimeOfDay;
        return new AlertActivityTimeDto
        {
            Weekdays = true,
            Saturdays = false,
            Sundays = false,
            StartTime = utcTime.Add(TimeSpan.FromMinutes(startOffsetMinutes)),
            EndTime = utcTime.Add(TimeSpan.FromMinutes(endOffsetMinutes)),
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
