namespace RvtPortal.Application.Time;

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
}

public sealed class RvtDateTimeProvider : IRvtDateTimeProvider
{
    // Function summary: Initializes the provider with the configured local time-zone identifier.
    public RvtDateTimeProvider(RvtTimeZoneOptions options)
    {
        LocalTimeZone = ResolveTimeZone(options.Local);
    }

    public DateTime UtcNow => DateTime.UtcNow;

    public TimeZoneInfo LocalTimeZone { get; }

    // Function summary: Converts a UTC timestamp to the configured local time zone.
    public DateTime UtcToLocal(DateTime utcDateTime)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), LocalTimeZone);
    }

    // Function summary: Converts a configured-local timestamp to UTC.
    public DateTime LocalToUtc(DateTime localDateTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(new DateTime(localDateTime.Ticks, DateTimeKind.Unspecified), LocalTimeZone);
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
        catch (TimeZoneNotFoundException) when (TryResolveMappedTimeZone(timeZoneId, out TimeZoneInfo? mappedTimeZone))
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
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId, out string? ianaId))
        {
            mappedTimeZone = TimeZoneInfo.FindSystemTimeZoneById(ianaId);
            return true;
        }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out string? windowsId))
        {
            mappedTimeZone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
            return true;
        }

        mappedTimeZone = TimeZoneInfo.Utc;
        return false;
    }
}
