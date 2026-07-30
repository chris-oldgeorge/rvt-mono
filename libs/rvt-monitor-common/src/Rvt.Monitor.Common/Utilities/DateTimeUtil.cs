using Rvt.Monitor.Common.Diagnostics;

namespace Rvt.Monitor.Common.Utilities;


public sealed class DateTimeUtil
{
    public static readonly DateTime JAN1_1970 = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

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

    public static DateTime GetNearestPeriodBlock(DateTime time, int period)
    {
        int seconds = (int)Math.Floor(time.TimeOfDay.TotalSeconds);
        int nearestMultipleSecs = (seconds + period / 2) / period * period;
        return time.Date.AddSeconds(nearestMultipleSecs);
    }
}
