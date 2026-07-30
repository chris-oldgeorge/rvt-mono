using Svantek.Model.Dto;

namespace Svantek.Api.Db;

public interface ISvantekMonitorCommands
{
    Task WriteMonitorListAsync(
        IReadOnlyList<NoiseMonitorDto> monitors,
        CancellationToken cancellationToken = default);

    Task WriteLatestTimestampAsync(
        string serialId,
        DateTime lastDataTime,
        CancellationToken cancellationToken = default);

    Task SetMonitorOfflineAsync(
        Guid monitorId,
        bool offline,
        CancellationToken cancellationToken = default);

    Task SetMonitorBatteryStatusAsync(
        Guid monitorId,
        byte batteryStatus,
        CancellationToken cancellationToken = default);
}
