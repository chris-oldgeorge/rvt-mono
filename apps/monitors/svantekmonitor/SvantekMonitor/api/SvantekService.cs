namespace Svantek.Api;

// Summary: Cancellable scheduled entry points for Svantek monitor jobs.
public sealed class SvantekService : ISvantekMonitorJobs
{
    private readonly SvantekApi _svantekApi;

    public SvantekService(SvantekApi svantekApi)
    {
        _svantekApi = svantekApi;
    }

    public Task StoreMonitorsAsync(CancellationToken cancellationToken = default) =>
        _svantekApi.StoreMonitorsAsync(cancellationToken);

    public Task StoreNoiseLevelsAsync(CancellationToken cancellationToken = default) =>
        _svantekApi.StoreNoiseLevelsAsync(cancellationToken);

    public Task NotifySiteAveragesAsync(CancellationToken cancellationToken = default) =>
        _svantekApi.NotifySiteAveragesAsync(
            DateTime.UtcNow.Date.AddDays(-1),
            cancellationToken);

    public Task CheckForOfflineMonitorsAsync(CancellationToken cancellationToken = default) =>
        _svantekApi.CheckForOfflineMonitorsAsync(cancellationToken);

    public Task NotifyBatteryLevelsAsync(CancellationToken cancellationToken = default) =>
        _svantekApi.NotifyBatteryLevelsAsync(cancellationToken);

    public Task CheckForSoundRecordingsAsync(CancellationToken cancellationToken = default) =>
        _svantekApi.CheckForSoundRecordingsAsync(cancellationToken);
}
