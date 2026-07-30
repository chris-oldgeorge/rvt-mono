using Rvt.Monitor.Common.Alerts;

namespace Svantek.Api;

// Summary: Cancellable scheduled entry points for Svantek monitor jobs.
public sealed class SvantekService : ISvantekMonitorJobs
{
    private readonly SvantekApi _svantekApi;
    private readonly DurableAlertDispatcher _alertDispatcher;
    private readonly DurableAlertCleanupService _alertCleanup;

    public SvantekService(
        SvantekApi svantekApi,
        DurableAlertDispatcher alertDispatcher,
        DurableAlertCleanupService alertCleanup)
    {
        _svantekApi = svantekApi;
        _alertDispatcher = alertDispatcher;
        _alertCleanup = alertCleanup;
    }

    public Task DispatchAlertsAsync(CancellationToken cancellationToken = default) =>
        _alertDispatcher.DispatchAsync(cancellationToken);

    public Task CleanupAlertsAsync(CancellationToken cancellationToken = default) =>
        _alertCleanup.CleanupAsync(cancellationToken);

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
