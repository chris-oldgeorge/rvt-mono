using Omnidots.Model.Dto;

namespace Omnidots.Api.Db;

public interface IOmnidotsMonitorCommands
{
    void WriteMonitorList(List<VibrationMonitorDto> monitors);

    void SetMonitorOffline(Guid monitorId, bool offline);

    void SetMonitorBatteryStatus(Guid monitorId, byte batteryStatus);
}
