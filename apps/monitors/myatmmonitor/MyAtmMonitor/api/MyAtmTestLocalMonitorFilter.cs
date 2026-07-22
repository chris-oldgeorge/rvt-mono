using MyAtm.Model.Dto;

namespace MyAtm.Api;

public static class MyAtmTestLocalMonitorFilter
{
    public const string TargetSerialId = "21972";
    public const string TargetFleetNr = "R6025V";

    public static List<DustMonitorDto> Apply(List<DustMonitorDto> monitors, bool enabled)
    {
        if (!enabled)
        {
            return monitors;
        }

        return monitors.Where(IsTargetReadMonitor).ToList();
    }

    public static List<DustMonitorDto> ApplyCatalog(List<DustMonitorDto> monitors, bool enabled)
    {
        if (!enabled)
        {
            return monitors;
        }

        return monitors.Where(monitor => IsTargetSerial(monitor.SerialId)).ToList();
    }

    public static bool IsTargetSerial(string? serialId) =>
        string.Equals(serialId, TargetSerialId, StringComparison.Ordinal);

    public static bool IsTargetReadMonitor(DustMonitorDto monitor) =>
        IsTargetSerial(monitor.SerialId) &&
        string.Equals(monitor.FleetNr, TargetFleetNr, StringComparison.OrdinalIgnoreCase);
}
