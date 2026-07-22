using Svantek.Model.Config;

namespace Svantek.Api;

public sealed record NoiseRequestWindow(DateTime Start, DateTime End);

public sealed class NoiseRequestWindowCalculator
{
    private static readonly DateTime UtcMin = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
    private static readonly DateTime UtcMax = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);

    private readonly SvantekImportOptions options;

    public NoiseRequestWindowCalculator(SvantekImportOptions options)
    {
        options.Validate();
        this.options = options;
    }

    public IReadOnlyList<NoiseRequestWindow> Calculate(
        DateTime deploymentStart,
        DateTime? watermark,
        DateTime? lastStatusTimestamp,
        DateTime utcNow)
    {
        var normalizedDeployment = NormalizeUtc(deploymentStart);
        DateTime? normalizedWatermark = watermark.HasValue ? NormalizeUtc(watermark.Value) : null;
        DateTime? normalizedStatus = lastStatusTimestamp.HasValue ? NormalizeUtc(lastStatusTimestamp.Value) : null;
        var normalizedNow = NormalizeUtc(utcNow);

        var start = normalizedWatermark.HasValue
            ? LaterOf(normalizedDeployment, SaturatingSubtract(normalizedWatermark.Value, options.WatermarkOverlap))
            : LaterOf(normalizedDeployment, SaturatingSubtract(normalizedNow, options.MaximumInitialBackfill));
        var candidateEnd = normalizedStatus.HasValue
            ? SaturatingAdd(normalizedStatus.Value, TimeSpan.FromHours(1))
            : normalizedNow;
        var end = EarlierOf(candidateEnd, normalizedNow);
        if (end <= start)
        {
            return [];
        }

        var windows = new List<NoiseRequestWindow>();
        for (var cursor = start; cursor < end;)
        {
            var windowEnd = EarlierOf(SaturatingAdd(cursor, options.MaximumRequestWindow), end);
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
        var remainingTicks = DateTime.MaxValue.Ticks - value.Ticks;
        return remainingTicks < duration.Ticks
            ? UtcMax
            : new DateTime(value.Ticks + duration.Ticks, DateTimeKind.Utc);
    }

    private static DateTime SaturatingSubtract(DateTime value, TimeSpan duration)
    {
        var availableTicks = value.Ticks - DateTime.MinValue.Ticks;
        return availableTicks < duration.Ticks
            ? UtcMin
            : new DateTime(value.Ticks - duration.Ticks, DateTimeKind.Utc);
    }
}
