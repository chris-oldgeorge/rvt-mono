using AirQ.Model.Dto;

namespace AirQ.Api.Db;

public interface IAirQMonitorCommands
{
    void WriteMonitorList(List<NoiseMonitorDto> monitors);

    void UpdateMonitorStatus(string serialId, NoiseMonitorStatus monitorStatus);

    void WriteLatestTimestamp(string serialId, DateTime lastDataTime);

    void SetMonitorOffline(Guid monitorId, bool offline);
}
