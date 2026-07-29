using Svantek.Model.Dto;

namespace Svantek.Api.Db;

public interface ISvantekMonitorCommands
{
    void WriteMonitorList(List<NoiseMonitorDto> monitors);

    Task WriteMonitorListAsync(
        IReadOnlyList<NoiseMonitorDto> monitors,
        CancellationToken cancellationToken = default);

    void WriteLatestTimestamp(string serialId, DateTime lastDataTime);

    Task WriteLatestTimestampAsync(
        string serialId,
        DateTime lastDataTime,
        CancellationToken cancellationToken = default);

    void SetMonitorOffline(Guid monitorId, bool offline);

    Task SetMonitorOfflineAsync(
        Guid monitorId,
        bool offline,
        CancellationToken cancellationToken = default);

    void SetMonitorBatteryStatus(Guid monitorId, byte batteryStatus);

    Task SetMonitorBatteryStatusAsync(
        Guid monitorId,
        byte batteryStatus,
        CancellationToken cancellationToken = default);
}
