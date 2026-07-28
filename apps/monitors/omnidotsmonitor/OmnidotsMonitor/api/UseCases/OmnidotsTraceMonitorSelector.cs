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
        IOrderedEnumerable<IGrouping<DateTime, VibrationMonitorDto>> eligible = monitors
            .Where(monitor => allowedSerialIds == null || allowedSerialIds.Contains(monitor.SerialId))
            .GroupBy(monitor => latestTraceEndTimes.TryGetValue(monitor.SerialId, out DateTime lastTraceAt)
                ? lastTraceAt
                : DateTime.MinValue)
            .OrderBy(group => group.Key);
        List<VibrationMonitorDto> ordered = new List<VibrationMonitorDto>();

        foreach (IGrouping<DateTime, VibrationMonitorDto>? priorityGroup in eligible)
        {
            VibrationMonitorDto[] monitorsAtPriority = priorityGroup
                .OrderBy(monitor => monitor.SerialId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            int offset = RotationOffset(rotationSlot, options.MaxMonitorsPerRun, monitorsAtPriority.Length);
            ordered.AddRange(monitorsAtPriority.Skip(offset));
            ordered.AddRange(monitorsAtPriority.Take(offset));
        }

        return ordered.Take(options.MaxMonitorsPerRun).ToArray();
    }

    private static int RotationOffset(long rotationSlot, int maxMonitorsPerRun, int count)
    {
        long normalizedSlot = ((rotationSlot % count) + count) % count;
        return (int)((normalizedSlot * (maxMonitorsPerRun % count)) % count);
    }
}
