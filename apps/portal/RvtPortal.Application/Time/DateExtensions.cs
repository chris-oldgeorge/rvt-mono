namespace RvtPortal.Application.Time;

public static class DateExtensions
{
    // Function summary: Removes seconds from a DateTime while preserving its existing kind.
    public static DateTime TruncateSeconds(this DateTime dt)
    {
        return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, dt.Kind);
    }

    // Function summary: Converts UTC time to the configured local time zone through the injected provider.
    public static DateTime UtcToLocal(this DateTime dt, IRvtDateTimeProvider dateTimeProvider)
    {
        return dateTimeProvider.UtcToLocal(dt);
    }

    // Function summary: Converts configured local time to UTC through the injected provider.
    public static DateTime LocalToUtc(this DateTime dt, IRvtDateTimeProvider dateTimeProvider)
    {
        return dateTimeProvider.LocalToUtc(dt);
    }
}
