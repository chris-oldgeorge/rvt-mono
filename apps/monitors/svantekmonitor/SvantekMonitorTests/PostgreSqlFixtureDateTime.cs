using System.Globalization;

namespace SvantekMonitorTests;

internal static class PostgreSqlFixtureDateTime
{
    public static DateTime ParseUtc(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture).UtcDateTime;
}
