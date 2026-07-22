// File summary: Provides injectable clock and configured time-zone conversion services for business workflows.
// Major updates:
// - 2026-07-09 pending Added DI/options-based clock and time-zone provider to replace static DateExtensions configuration reads.

using System.Globalization;
using Microsoft.Extensions.Options;

namespace RVT.BusinessLogic;

public sealed class RvtTimeZoneOptions
{
    public string? Local { get; set; }
}

public interface IRvtDateTimeProvider
{
    DateTime UtcNow { get; }

    TimeZoneInfo LocalTimeZone { get; }

    DateTime UtcToLocal(DateTime utcDateTime);

    DateTime LocalToUtc(DateTime localDateTime);

    string DisplayUtcAsLocal(DateTime utcDateTime, string format);
}

public sealed class RvtDateTimeProvider : IRvtDateTimeProvider
{
    private readonly TimeZoneInfo localTimeZone;

    // Function summary: Initializes the provider with an options-backed local time-zone identifier.
    public RvtDateTimeProvider(IOptions<RvtTimeZoneOptions> options)
    {
        localTimeZone = ResolveTimeZone(options.Value.Local);
    }

    public DateTime UtcNow => DateTime.UtcNow;

    public TimeZoneInfo LocalTimeZone => localTimeZone;

    // Function summary: Converts a UTC timestamp to the configured local time zone.
    public DateTime UtcToLocal(DateTime utcDateTime)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), localTimeZone);
    }

    // Function summary: Converts a configured-local timestamp to UTC.
    public DateTime LocalToUtc(DateTime localDateTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(new DateTime(localDateTime.Ticks, DateTimeKind.Unspecified), localTimeZone);
    }

    // Function summary: Formats UTC time in the configured local time zone.
    public string DisplayUtcAsLocal(DateTime utcDateTime, string format)
    {
        return UtcToLocal(utcDateTime).ToString(format, CultureInfo.InvariantCulture);
    }

    // Function summary: Resolves a configured time-zone ID, including cross-platform Windows/IANA conversion when available.
    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException) when (TryResolveMappedTimeZone(timeZoneId, out var mappedTimeZone))
        {
            return mappedTimeZone;
        }
        catch (InvalidTimeZoneException ex)
        {
            throw new InvalidOperationException($"Configured TimeZones:Local value '{timeZoneId}' is not a valid time-zone definition.", ex);
        }
        catch (TimeZoneNotFoundException ex)
        {
            throw new InvalidOperationException($"Configured TimeZones:Local value '{timeZoneId}' could not be found on this host.", ex);
        }
    }

    // Function summary: Resolves a configured time zone through runtime-supported Windows/IANA mappings.
    private static bool TryResolveMappedTimeZone(string timeZoneId, out TimeZoneInfo mappedTimeZone)
    {
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId, out var ianaId))
        {
            mappedTimeZone = TimeZoneInfo.FindSystemTimeZoneById(ianaId);
            return true;
        }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windowsId))
        {
            mappedTimeZone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
            return true;
        }

        mappedTimeZone = TimeZoneInfo.Utc;
        return false;
    }
}
