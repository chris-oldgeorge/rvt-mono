
using Svantek.Model.Dto;
using SvantekMonitor.model.dto;

namespace Svantek.Api;

public static class SvantekTestLocalMonitorFilter
{
    public const string TargetSerialId = "157206";
    public const string TargetFleetNr = "E125V";

    public static List<NoiseMonitorReadDto> Apply(List<NoiseMonitorReadDto> monitors, bool enabled)
    {
        if (!enabled)
        {
            return monitors;
        }

        return monitors.Where(IsTargetReadMonitor).ToList();
    }

    public static List<NoiseMonitorDto> Apply(List<NoiseMonitorDto> monitors, bool enabled)
    {
        if (!enabled)
        {
            return monitors;
        }

        return monitors.Where(monitor => string.Equals(monitor.SerialId, TargetSerialId, StringComparison.Ordinal)).ToList();
    }

    private static bool IsTargetReadMonitor(NoiseMonitorReadDto monitor) =>
        string.Equals(monitor.SerialId, TargetSerialId, StringComparison.Ordinal) &&
        string.Equals(monitor.FleetNr, TargetFleetNr, StringComparison.OrdinalIgnoreCase);
}
