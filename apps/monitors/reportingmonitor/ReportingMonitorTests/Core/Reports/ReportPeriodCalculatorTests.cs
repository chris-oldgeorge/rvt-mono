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
        ReportRule rule = new() { Frequency = FrequencyType.Daily };
        IReadOnlyList<ReportPeriod> periods = ReportPeriodCalculator.CreatePeriods(rule, new DateTimeOffset(2026, 6, 24, 4, 0, 0, TimeSpan.Zero));

        Assert.Single(periods);
        Assert.Equal(new DateTimeOffset(2026, 6, 23, 0, 0, 0, TimeSpan.Zero), periods[0].StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 6, 23, 23, 59, 59, 999, TimeSpan.Zero), periods[0].EndUtc);
    }

    [Fact]
    public void CreatePeriods_Weekly_ReturnsNothingWhenTriggerDayDoesNotMatch()
    {
        ReportRule rule = new() { Frequency = FrequencyType.Weekly, DayOfWeek = DayOfWeek.Monday };
        IReadOnlyList<ReportPeriod> periods = ReportPeriodCalculator.CreatePeriods(rule, new DateTimeOffset(2026, 6, 24, 4, 0, 0, TimeSpan.Zero));

        Assert.Empty(periods);
    }

    [Fact]
    public void CreatePeriods_Weekly_ReturnsPreviousSevenDaysWhenTriggerDayMatches()
    {
        ReportRule rule = new() { Frequency = FrequencyType.Weekly, DayOfWeek = DayOfWeek.Wednesday };
        IReadOnlyList<ReportPeriod> periods = ReportPeriodCalculator.CreatePeriods(rule, new DateTimeOffset(2026, 6, 24, 4, 0, 0, TimeSpan.Zero));

        Assert.Single(periods);
        Assert.Equal(new DateTimeOffset(2026, 6, 17, 0, 0, 0, TimeSpan.Zero), periods[0].StartUtc);
        Assert.Equal(FrequencyType.Weekly, periods[0].Frequency);
    }

    [Fact]
    public void CreatePeriods_Monthly_UsesLastDayForShortMonth()
    {
        ReportRule rule = new() { Frequency = FrequencyType.Monthly, DayOfMonth = 31 };
        IReadOnlyList<ReportPeriod> periods = ReportPeriodCalculator.CreatePeriods(rule, new DateTimeOffset(2026, 4, 30, 4, 0, 0, TimeSpan.Zero));

        Assert.Single(periods);
        Assert.Equal(new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero), periods[0].StartUtc);
    }

    [Fact]
    public void CreatePeriods_WeeklyAndMonthly_CanCreateBothCandidatePeriods()
    {
        ReportRule rule = new()
        {
            Frequency = FrequencyType.WeeklyAndMonthly,
            DayOfWeek = DayOfWeek.Wednesday,
            DayOfMonth = 24
        };

        IReadOnlyList<ReportPeriod> periods = ReportPeriodCalculator.CreatePeriods(rule, new DateTimeOffset(2026, 6, 24, 4, 0, 0, TimeSpan.Zero));

        Assert.Equal(2, periods.Count);
        Assert.Contains(periods, period => period.Frequency == FrequencyType.Monthly);
        Assert.Contains(periods, period => period.Frequency == FrequencyType.Weekly);
    }
}
