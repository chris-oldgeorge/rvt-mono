using Svantek.Model.Config;

namespace Svantek.Api;

public sealed record NoiseRequestWindow(DateTime Start, DateTime End);

public sealed class NoiseRequestWindowCalculator
{
    private static readonly DateTime _utcMin = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
    private static readonly DateTime _utcMax = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);

    private readonly SvantekImportOptions _options;

    public NoiseRequestWindowCalculator(SvantekImportOptions options)
    {
        options.Validate();
        _options = options;
    }

    public IReadOnlyList<NoiseRequestWindow> Calculate(
        DateTime deploymentStart,
        DateTime? watermark,
        DateTime? lastStatusTimestamp,
        DateTime utcNow)
    {
        DateTime normalizedDeployment = NormalizeUtc(deploymentStart);
        DateTime? normalizedWatermark = watermark.HasValue ? NormalizeUtc(watermark.Value) : null;
        DateTime? normalizedStatus = lastStatusTimestamp.HasValue ? NormalizeUtc(lastStatusTimestamp.Value) : null;
        DateTime normalizedNow = NormalizeUtc(utcNow);

        // A watermark ahead of the wall clock would put the window start after
        // its end, so the monitor would never be asked for data again. Clamping
        // it to now lets a monitor poisoned by an earlier future-dated sample
        // recover on its own.
        DateTime start = normalizedWatermark.HasValue
            ? LaterOf(normalizedDeployment, SaturatingSubtract(EarlierOf(normalizedWatermark.Value, normalizedNow), _options.WatermarkOverlap))
            : LaterOf(normalizedDeployment, SaturatingSubtract(normalizedNow, _options.MaximumInitialBackfill));
        DateTime candidateEnd = normalizedStatus.HasValue
            ? SaturatingAdd(normalizedStatus.Value, TimeSpan.FromHours(1))
            : normalizedNow;
        DateTime end = EarlierOf(candidateEnd, normalizedNow);
        if (end <= start)
        {
            return [];
        }

        List<NoiseRequestWindow> windows = [];
        for (DateTime cursor = start; cursor < end;)
        {
            DateTime windowEnd = EarlierOf(SaturatingAdd(cursor, _options.MaximumRequestWindow), end);
            windows.Add(new NoiseRequestWindow(cursor, windowEnd));
            cursor = windowEnd;
        }

        return windows;
    }

    private static DateTime LaterOf(DateTime first, DateTime second) =>
        first >= second ? first : second;

    private static DateTime EarlierOf(DateTime first, DateTime second) =>
        first <= second ? first : second;

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static DateTime SaturatingAdd(DateTime value, TimeSpan duration)
    {
        long remainingTicks = DateTime.MaxValue.Ticks - value.Ticks;
        return remainingTicks < duration.Ticks
            ? _utcMax
            : new DateTime(value.Ticks + duration.Ticks, DateTimeKind.Utc);
    }

    private static DateTime SaturatingSubtract(DateTime value, TimeSpan duration)
    {
        long availableTicks = value.Ticks - DateTime.MinValue.Ticks;
        return availableTicks < duration.Ticks
            ? _utcMin
            : new DateTime(value.Ticks - duration.Ticks, DateTimeKind.Utc);
    }
}
