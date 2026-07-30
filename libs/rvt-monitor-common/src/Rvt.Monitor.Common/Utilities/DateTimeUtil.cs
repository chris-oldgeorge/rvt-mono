using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;

namespace Rvt.Monitor.Common.Utilities;


public sealed class DateTimeUtil
{
    public static readonly DateTime JAN1_1970 = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    static readonly TimeZoneInfo _tzi = TimeZoneInfo.FindSystemTimeZoneById(RvtConfig.LOCAL_TIME_ZONE);

    public static long GetMillis(DateTime dateTime)
    {
        return (long)(dateTime - JAN1_1970).TotalMilliseconds;
    }

    public static DateTime FromMillis(long millis)
    {
        return DateTimeUtil.JAN1_1970.Add(TimeSpan.FromMilliseconds(millis)).ToUniversalTime();
    }

    public static DateTime TruncateMillis(DateTime dateTime)
    {
        return dateTime.AddTicks(-(dateTime.Ticks % TimeSpan.TicksPerSecond));
    }

    public static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public static DateTime? AsUtc(DateTime? value) => value.HasValue
        ? AsUtc(value.Value)
        : null;

    public static TimeSpan UtcToLocal(TimeSpan utc)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(DateTime.Today + utc, DateTimeKind.Utc), _tzi).TimeOfDay;
    }

    public static DateTime UtcToLocal(DateTime utc)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind((DateTime)utc!, DateTimeKind.Utc), _tzi);
    }

    public static DateTime? UtcToLocal(DateTime? utc)
    {
        if (utc == null)
        {
            return null;
        }

        return UtcToLocal((DateTime)utc);
    }

    public static DateTime LocalToUtc(DateTime local)
    {
        return TimeZoneInfo.ConvertTimeToUtc(new DateTime(local.Ticks, DateTimeKind.Unspecified), _tzi);
    }

    public static DateTime GetNearestPeriodBlock(DateTime time, int period)
    {
        int seconds = (int)Math.Floor(time.TimeOfDay.TotalSeconds);
        int nearestMultipleSecs = (seconds + period / 2) / period * period;
        return time.Date.AddSeconds(nearestMultipleSecs);
    }
}
