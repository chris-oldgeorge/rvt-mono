using Omnidots.Model.Config;

namespace Omnidots.Api.UseCases;

internal static class SiteActiveDurationCalculator
{
    internal static TimeSpan Between(
        SiteTimes siteTimes,
        DateTime fromUtc,
        DateTime toUtc,
        TimeZoneInfo siteTimeZone)
    {
        ArgumentNullException.ThrowIfNull(siteTimes);
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

        DateTime firstLocalDate = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, siteTimeZone).Date;
        DateTime lastLocalDate = TimeZoneInfo.ConvertTimeFromUtc(toUtc, siteTimeZone).Date;
        DateTime scheduleDate = firstLocalDate > DateTime.MinValue.Date
            ? firstLocalDate.AddDays(-1)
            : firstLocalDate;
        TimeSpan total = TimeSpan.Zero;

        for (; scheduleDate <= lastLocalDate; scheduleDate = scheduleDate.AddDays(1))
        {
            (TimeSpan? start, TimeSpan? end) = TimesForDate(siteTimes, scheduleDate.DayOfWeek);
            if (start is null || end is null)
            {
                continue;
            }

            DateTime localStart = DateTime.SpecifyKind(scheduleDate.Add(start.Value), DateTimeKind.Unspecified);
            DateTime localEnd = DateTime.SpecifyKind(scheduleDate.Add(end.Value), DateTimeKind.Unspecified);
            if (end < start)
            {
                localEnd = localEnd.AddDays(1);
            }

            ValidateBoundary(localStart, siteTimeZone);
            ValidateBoundary(localEnd, siteTimeZone);

            DateTime activeStartUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, siteTimeZone);
            DateTime activeEndUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, siteTimeZone);
            DateTime intersectionStart = activeStartUtc > fromUtc ? activeStartUtc : fromUtc;
            DateTime intersectionEnd = activeEndUtc < toUtc ? activeEndUtc : toUtc;

            if (intersectionEnd > intersectionStart)
            {
                total += intersectionEnd - intersectionStart;
            }
        }

        return total;
    }

    private static void ValidateBoundary(DateTime localBoundary, TimeZoneInfo siteTimeZone)
    {
        if (siteTimeZone.IsInvalidTime(localBoundary) ||
            siteTimeZone.IsAmbiguousTime(localBoundary))
        {
            throw new SiteScheduleConfigurationException();
        }
    }

    private static (TimeSpan? Start, TimeSpan? End) TimesForDate(
        SiteTimes siteTimes,
        DayOfWeek dayOfWeek) => dayOfWeek switch
        {
            DayOfWeek.Saturday => (siteTimes.SaturdayStart, siteTimes.SaturdayEnd),
            DayOfWeek.Sunday => (siteTimes.SundayStart, siteTimes.SundayEnd),
            _ => (siteTimes.WeekdayStart, siteTimes.WeekdayEnd)
        };
}

internal sealed class SiteScheduleConfigurationException : Exception
{
    internal SiteScheduleConfigurationException()
        : base("Site schedule contains an invalid or ambiguous local time boundary.")
    {
    }
}
