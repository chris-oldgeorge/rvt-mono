// File summary: Provides pure date helpers and provider-backed UTC/local conversion extension methods.
// Major updates:
// - 2026-07-09 pending Removed static appsettings reads; UTC/local conversion now uses an injected date-time provider.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

namespace RVT.BusinessLogic
{
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

        // Function summary: Formats UTC time as configured local time through the injected provider.
        public static string DisplayUtcAsLocal(this DateTime dt, string format, IRvtDateTimeProvider dateTimeProvider)
        {
            return dateTimeProvider.DisplayUtcAsLocal(dt, format);
        }

        // Function summary: Formats nullable UTC time as configured local time through the injected provider.
        public static string DisplayUtcAsLocal(this DateTime? dt, string format, IRvtDateTimeProvider dateTimeProvider, string nullValue = "")
        {
            return dt == null ? nullValue : dateTimeProvider.DisplayUtcAsLocal(dt.Value, format);
        }
    }
}
