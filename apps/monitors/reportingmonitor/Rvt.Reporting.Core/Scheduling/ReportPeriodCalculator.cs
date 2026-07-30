using Rvt.Reporting.Core.Models;

namespace Rvt.Reporting.Core.Scheduling;

/// <summary>
/// Calculates report periods from legacy rule frequency settings without touching storage or rendering.
/// Major updates: 2026-06-24 extracted from legacy PdfGenerator date logic.
/// </summary>
public sealed class ReportPeriodCalculator
{
    /// <summary>
    /// How many missed periods of one frequency a single run may regenerate.
    /// A failed or skipped run used to lose its period for good, because the
    /// period was derived from the trigger day alone and was never revisited;
    /// an unbounded backfill would instead mail a recipient one report per
    /// missed day/week/month at once. Four is a fortnight of missed daily
    /// reports, a month of weekly ones, or a quarter of monthly ones.
    /// </summary>
    public const int MaximumBackfillPeriods = 4;

    // Enough calendar days to reach MaximumBackfillPeriods monthly periods.
    private const int _maximumBackfillDays = 200;

    public static IReadOnlyList<ReportPeriod> CreatePeriods(ReportRule rule, DateTimeOffset triggerUtc)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.Frequency switch
        {
            FrequencyType.WeeklyAndMonthly =>
                [.. CreatePeriods(rule with { Frequency = FrequencyType.Monthly }, triggerUtc),
                 .. CreatePeriods(rule with { Frequency = FrequencyType.Weekly }, triggerUtc)],
            _ => CreateFrequencyPeriods(rule, rule.Frequency, triggerUtc)
        };
    }

    /// <summary>
    /// The period for <paramref name="triggerUtc"/> plus any periods missed
    /// since <see cref="ReportRule.LastGenerated"/>, oldest first. A rule that
    /// has never generated produces only the current period - no history is
    /// invented for a rule that was created after it.
    /// </summary>
    private static List<ReportPeriod> CreateFrequencyPeriods(
        ReportRule rule,
        FrequencyType frequency,
        DateTimeOffset triggerUtc)
    {
        DateTime triggerDay = triggerUtc.UtcDateTime.Date;
        DateTime firstCandidateDay = triggerDay;
        if (rule.LastGenerated is { } lastGenerated)
        {
            DateTime earliest = triggerDay.AddDays(-_maximumBackfillDays);
            DateTime resumeDay = lastGenerated.UtcDateTime.Date;
            firstCandidateDay = resumeDay > earliest ? resumeDay : earliest;
        }

        List<ReportPeriod> periods = [];
        for (DateTime day = firstCandidateDay; day <= triggerDay; day = day.AddDays(1))
        {
            if (TryCreatePeriod(rule, frequency, new DateTimeOffset(day, TimeSpan.Zero)) is { } period)
            {
                periods.Add(period);
            }
        }

        return periods.Count <= MaximumBackfillPeriods
            ? periods
            : [.. periods.Skip(periods.Count - MaximumBackfillPeriods)];
    }

    public static ReportPeriod? TryCreatePeriod(ReportRule rule, FrequencyType frequency, DateTimeOffset triggerUtc)
    {
        ArgumentNullException.ThrowIfNull(rule);

        DateTime triggerDay = triggerUtc.UtcDateTime.Date;
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

        DateTimeOffset end = new DateTimeOffset(triggerDay, TimeSpan.Zero).AddMilliseconds(-1);
        return new ReportPeriod(frequency, new DateTimeOffset(start.Value, TimeSpan.Zero), end);
    }

    private static DateTime? GetMonthlyStart(ReportRule rule, DateTime triggerDay)
    {
        if (rule.DayOfMonth is not { } configuredDay)
        {
            return null;
        }

        int daysInCurrentMonth = DateTime.DaysInMonth(triggerDay.Year, triggerDay.Month);
        if (triggerDay.Day == configuredDay)
        {
            return triggerDay.AddMonths(-1);
        }

        if (configuredDay <= daysInCurrentMonth || triggerDay.Day != daysInCurrentMonth)
        {
            return null;
        }

        DateTime previousMonth = triggerDay.AddMonths(-1);
        int previousMonthDay = Math.Min(DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month), configuredDay);
        return new DateTime(previousMonth.Year, previousMonth.Month, previousMonthDay, 0, 0, 0, DateTimeKind.Utc);
    }
}

public sealed record ReportPeriod(FrequencyType Frequency, DateTimeOffset StartUtc, DateTimeOffset EndUtc);
