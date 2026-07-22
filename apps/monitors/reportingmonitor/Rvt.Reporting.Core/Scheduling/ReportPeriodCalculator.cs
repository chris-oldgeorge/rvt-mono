using Rvt.Reporting.Core.Models;

namespace Rvt.Reporting.Core.Scheduling;

/// <summary>
/// Calculates report periods from legacy rule frequency settings without touching storage or rendering.
/// Major updates: 2026-06-24 extracted from legacy PdfGenerator date logic.
/// </summary>
public sealed class ReportPeriodCalculator
{
    public static IReadOnlyList<ReportPeriod> CreatePeriods(ReportRule rule, DateTimeOffset triggerUtc)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.Frequency switch
        {
            FrequencyType.WeeklyAndMonthly =>
                [.. CreatePeriods(rule with { Frequency = FrequencyType.Monthly }, triggerUtc),
                 .. CreatePeriods(rule with { Frequency = FrequencyType.Weekly }, triggerUtc)],
            _ => TryCreatePeriod(rule, rule.Frequency, triggerUtc) is { } period ? [period] : []
        };
    }

    public static ReportPeriod? TryCreatePeriod(ReportRule rule, FrequencyType frequency, DateTimeOffset triggerUtc)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var triggerDay = triggerUtc.UtcDateTime.Date;
        DateTime? start = frequency switch
        {
            FrequencyType.Daily => triggerDay.AddDays(-1),
            FrequencyType.Weekly when rule.DayOfWeek == triggerDay.DayOfWeek => triggerDay.AddDays(-7),
            FrequencyType.Monthly => GetMonthlyStart(rule, triggerDay),
            _ => null
        };

        if (start is null)
        {
            return null;
        }

        var end = new DateTimeOffset(triggerDay, TimeSpan.Zero).AddMilliseconds(-1);
        return new ReportPeriod(frequency, new DateTimeOffset(start.Value, TimeSpan.Zero), end);
    }

    private static DateTime? GetMonthlyStart(ReportRule rule, DateTime triggerDay)
    {
        if (rule.DayOfMonth is not { } configuredDay)
        {
            return null;
        }

        var daysInCurrentMonth = DateTime.DaysInMonth(triggerDay.Year, triggerDay.Month);
        if (triggerDay.Day == configuredDay)
        {
            return triggerDay.AddMonths(-1);
        }

        if (configuredDay <= daysInCurrentMonth || triggerDay.Day != daysInCurrentMonth)
        {
            return null;
        }

        var previousMonth = triggerDay.AddMonths(-1);
        var previousMonthDay = Math.Min(DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month), configuredDay);
        return new DateTime(previousMonth.Year, previousMonth.Month, previousMonthDay, 0, 0, 0, DateTimeKind.Utc);
    }
}

public sealed record ReportPeriod(FrequencyType Frequency, DateTimeOffset StartUtc, DateTimeOffset EndUtc);
