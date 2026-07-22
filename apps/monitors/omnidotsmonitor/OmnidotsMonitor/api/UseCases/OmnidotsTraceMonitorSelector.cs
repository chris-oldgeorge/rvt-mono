using Omnidots.Model.Config;
using Omnidots.Model.Dto;

namespace Omnidots.Api.UseCases;

public static class OmnidotsTraceMonitorSelector
{
    public static IReadOnlyList<VibrationMonitorDto> Select(
        IReadOnlyCollection<VibrationMonitorDto> monitors,
        IReadOnlyDictionary<string, DateTime> latestTraceEndTimes,
        OmnidotsTraceCollectionOptions options,
        long rotationSlot)
    {
        options.Validate();
        if (!options.Enabled)
        {
            return [];
        }

        HashSet<string>? allowedSerialIds = options.AllowedSerialIds.Length == 0
            ? null
            : new HashSet<string>(options.AllowedSerialIds, StringComparer.OrdinalIgnoreCase);
        var eligible = monitors
            .Where(monitor => allowedSerialIds == null || allowedSerialIds.Contains(monitor.SerialId))
            .GroupBy(monitor => latestTraceEndTimes.TryGetValue(monitor.SerialId, out var lastTraceAt)
                ? lastTraceAt
                : DateTime.MinValue)
            .OrderBy(group => group.Key);
        var ordered = new List<VibrationMonitorDto>();

        foreach (var priorityGroup in eligible)
        {
            var monitorsAtPriority = priorityGroup
                .OrderBy(monitor => monitor.SerialId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var offset = RotationOffset(rotationSlot, options.MaxMonitorsPerRun, monitorsAtPriority.Length);
            ordered.AddRange(monitorsAtPriority.Skip(offset));
            ordered.AddRange(monitorsAtPriority.Take(offset));
        }

        return ordered.Take(options.MaxMonitorsPerRun).ToArray();
    }

    private static int RotationOffset(long rotationSlot, int maxMonitorsPerRun, int count)
    {
        var normalizedSlot = ((rotationSlot % count) + count) % count;
        return (int)((normalizedSlot * (maxMonitorsPerRun % count)) % count);
    }
}
