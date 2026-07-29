namespace Svantek.Api;

public interface ISvantekMonitorJobs
{
    Task StoreMonitorsAsync(CancellationToken cancellationToken = default);
    Task StoreNoiseLevelsAsync(CancellationToken cancellationToken = default);
    Task NotifySiteAveragesAsync(CancellationToken cancellationToken = default);
    Task CheckForOfflineMonitorsAsync(CancellationToken cancellationToken = default);
    Task NotifyBatteryLevelsAsync(CancellationToken cancellationToken = default);
    Task CheckForSoundRecordingsAsync(CancellationToken cancellationToken = default);
}
