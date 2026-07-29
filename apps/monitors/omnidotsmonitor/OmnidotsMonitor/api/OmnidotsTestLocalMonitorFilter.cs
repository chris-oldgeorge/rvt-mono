using Omnidots.Model.Dto;

namespace Omnidots.Api;

public static class OmnidotsTestLocalMonitorFilter
{
    public const string TargetSerialId = "14768";
    public const string TargetFleetNr = "R17222V-QUCILO";

    public static List<VibrationMonitorDto> Apply(List<VibrationMonitorDto> monitors, bool enabled)
    {
        if (!enabled)
        {
            return monitors;
        }

        return monitors.Where(IsTargetReadMonitor).ToList();
    }

    public static List<VibrationMonitorDto> ApplyCatalog(List<VibrationMonitorDto> monitors, bool enabled)
    {
        if (!enabled)
        {
            return monitors;
        }

        return monitors.Where(monitor => string.Equals(monitor.SerialId, TargetSerialId, StringComparison.Ordinal)).ToList();
    }

    private static bool IsTargetReadMonitor(VibrationMonitorDto monitor) =>
        string.Equals(monitor.SerialId, TargetSerialId, StringComparison.Ordinal) &&
        string.Equals(monitor.FleetNr, TargetFleetNr, StringComparison.OrdinalIgnoreCase);
}
