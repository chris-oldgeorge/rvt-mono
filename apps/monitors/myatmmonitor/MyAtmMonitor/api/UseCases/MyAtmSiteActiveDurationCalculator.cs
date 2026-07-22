using MyAtm.Model.Config;

namespace MyAtm.Api.UseCases;

public static class MyAtmSiteActiveDurationCalculator
{
    public static TimeSpan Between(
        MyAtmSiteSchedule schedule,
        DateTime fromUtc,
        DateTime toUtc,
        TimeZoneInfo siteTimeZone)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(siteTimeZone);

        if (fromUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("fromUtc must be UTC.", nameof(fromUtc));
        }

        if (toUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("toUtc must be UTC.", nameof(toUtc));
        }

        if (toUtc <= fromUtc)
        {
            return TimeSpan.Zero;
        }

        var firstLocalDate = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, siteTimeZone).Date;
        var lastLocalDate = TimeZoneInfo.ConvertTimeFromUtc(toUtc, siteTimeZone).Date;
        var scheduleDate = firstLocalDate > DateTime.MinValue.Date
            ? firstLocalDate.AddDays(-1)
            : firstLocalDate;
        var total = TimeSpan.Zero;

        for (; scheduleDate <= lastLocalDate; scheduleDate = scheduleDate.AddDays(1))
        {
            var (start, end) = TimesForDate(schedule, scheduleDate.DayOfWeek);
            if (start.HasValue != end.HasValue)
            {
                throw new MyAtmSiteScheduleConfigurationException();
            }

            if (start is null || end is null)
            {
                continue;
            }

            var localStart = DateTime.SpecifyKind(scheduleDate.Add(start.Value), DateTimeKind.Unspecified);
            var localEnd = DateTime.SpecifyKind(scheduleDate.Add(end.Value), DateTimeKind.Unspecified);
            if (end < start)
            {
                localEnd = localEnd.AddDays(1);
            }

            ValidateBoundary(localStart, siteTimeZone);
            ValidateBoundary(localEnd, siteTimeZone);

            var activeStartUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, siteTimeZone);
            var activeEndUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, siteTimeZone);
            var intersectionStart = activeStartUtc > fromUtc ? activeStartUtc : fromUtc;
            var intersectionEnd = activeEndUtc < toUtc ? activeEndUtc : toUtc;
            if (intersectionEnd > intersectionStart)
            {
                total += intersectionEnd - intersectionStart;
            }
        }

        return total;
    }

    private static void ValidateBoundary(DateTime localBoundary, TimeZoneInfo siteTimeZone)
    {
        if (siteTimeZone.IsInvalidTime(localBoundary) || siteTimeZone.IsAmbiguousTime(localBoundary))
        {
            throw new MyAtmSiteScheduleConfigurationException();
        }
    }

    private static (TimeSpan? Start, TimeSpan? End) TimesForDate(
        MyAtmSiteSchedule schedule,
        DayOfWeek dayOfWeek) => dayOfWeek switch
        {
            DayOfWeek.Saturday => (schedule.SaturdayStart, schedule.SaturdayEnd),
            DayOfWeek.Sunday => (schedule.SundayStart, schedule.SundayEnd),
            _ => (schedule.WeekdayStart, schedule.WeekdayEnd)
        };
}

public sealed class MyAtmSiteScheduleConfigurationException : Exception
{
    public MyAtmSiteScheduleConfigurationException()
        : base("Site schedule contains an invalid or ambiguous local time boundary.")
    {
    }
}
