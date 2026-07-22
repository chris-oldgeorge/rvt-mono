using Rvt.Monitor.Common.Configuration;

namespace Svantek.Api;

// Summary: Cancellable scheduled entry points for Svantek monitor jobs.
public sealed class SvantekService : ISvantekMonitorJobs
{
    private readonly SvantekApi svantekApi;

    public SvantekService(SvantekApi svantekApi)
    {
        this.svantekApi = svantekApi;
    }

    public string Liveness() => RvtConfig.SERVICE_NAME + RvtConfig.SERVICE_VERSION;

    public Task StoreMonitorsAsync(CancellationToken cancellationToken = default) =>
        svantekApi.StoreMonitorsAsync(cancellationToken);

    public Task StoreNoiseLevelsAsync(CancellationToken cancellationToken = default) =>
        svantekApi.StoreNoiseLevelsAsync(cancellationToken);

    public Task NotifySiteAveragesAsync(CancellationToken cancellationToken = default) =>
        svantekApi.NotifySiteAveragesAsync(
            DateTime.UtcNow.Date.AddDays(-1),
            cancellationToken);

    public Task CheckForOfflineMonitorsAsync(CancellationToken cancellationToken = default) =>
        svantekApi.CheckForOfflineMonitorsAsync(cancellationToken);

    public Task NotifyBatteryLevelsAsync(CancellationToken cancellationToken = default) =>
        svantekApi.NotifyBatteryLevelsAsync(cancellationToken);

    public Task CheckForSoundRecordingsAsync(CancellationToken cancellationToken = default) =>
        svantekApi.CheckForSoundRecordingsAsync(cancellationToken);
}
