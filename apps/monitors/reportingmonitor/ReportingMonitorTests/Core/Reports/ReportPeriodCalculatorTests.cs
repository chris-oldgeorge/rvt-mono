using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Scheduling;

namespace Rvt.Reporting.Core.Tests.Reports;

/// <summary>
/// Verifies legacy report frequency behavior after extraction from the Azure Function.
/// Major updates: 2026-06-24 initial scheduling coverage.
/// </summary>
public sealed class ReportPeriodCalculatorTests
{
    [Fact]
    public void CreatePeriods_Daily_UsesPreviousDay()
    {
        var rule = new ReportRule { Frequency = FrequencyType.Daily };
        var periods = ReportPeriodCalculator.CreatePeriods(rule, new DateTimeOffset(2026, 6, 24, 4, 0, 0, TimeSpan.Zero));

        Assert.Single(periods);
        Assert.Equal(new DateTimeOffset(2026, 6, 23, 0, 0, 0, TimeSpan.Zero), periods[0].StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 6, 23, 23, 59, 59, 999, TimeSpan.Zero), periods[0].EndUtc);
    }

    [Fact]
    public void CreatePeriods_Weekly_ReturnsNothingWhenTriggerDayDoesNotMatch()
    {
        var rule = new ReportRule { Frequency = FrequencyType.Weekly, DayOfWeek = DayOfWeek.Monday };
        var periods = ReportPeriodCalculator.CreatePeriods(rule, new DateTimeOffset(2026, 6, 24, 4, 0, 0, TimeSpan.Zero));

        Assert.Empty(periods);
    }

    [Fact]
    public void CreatePeriods_Weekly_ReturnsPreviousSevenDaysWhenTriggerDayMatches()
    {
        var rule = new ReportRule { Frequency = FrequencyType.Weekly, DayOfWeek = DayOfWeek.Wednesday };
        var periods = ReportPeriodCalculator.CreatePeriods(rule, new DateTimeOffset(2026, 6, 24, 4, 0, 0, TimeSpan.Zero));

        Assert.Single(periods);
        Assert.Equal(new DateTimeOffset(2026, 6, 17, 0, 0, 0, TimeSpan.Zero), periods[0].StartUtc);
        Assert.Equal(FrequencyType.Weekly, periods[0].Frequency);
    }

    [Fact]
    public void CreatePeriods_Monthly_UsesLastDayForShortMonth()
    {
        var rule = new ReportRule { Frequency = FrequencyType.Monthly, DayOfMonth = 31 };
        var periods = ReportPeriodCalculator.CreatePeriods(rule, new DateTimeOffset(2026, 4, 30, 4, 0, 0, TimeSpan.Zero));

        Assert.Single(periods);
        Assert.Equal(new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero), periods[0].StartUtc);
    }

    [Fact]
    public void CreatePeriods_WeeklyAndMonthly_CanCreateBothCandidatePeriods()
    {
        var rule = new ReportRule
        {
            Frequency = FrequencyType.WeeklyAndMonthly,
            DayOfWeek = DayOfWeek.Wednesday,
            DayOfMonth = 24
        };

        var periods = ReportPeriodCalculator.CreatePeriods(rule, new DateTimeOffset(2026, 6, 24, 4, 0, 0, TimeSpan.Zero));

        Assert.Equal(2, periods.Count);
        Assert.Contains(periods, period => period.Frequency == FrequencyType.Monthly);
        Assert.Contains(periods, period => period.Frequency == FrequencyType.Weekly);
    }
}
