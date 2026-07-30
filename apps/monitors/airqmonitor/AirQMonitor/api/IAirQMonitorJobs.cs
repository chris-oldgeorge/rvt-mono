// The namespace follows this project's established scheme rather than the
// folder path; IDE0130 would require a name no sibling file uses.
#pragma warning disable IDE0130
namespace AirQ.Api;

// Summary: The scheduled-job surface of the AirQ monitor service.
// Major updates:
// - 2026-07-29 Job-shape convergence: AirQ was the only monitor whose job
//   catalog bound the concrete service; this interface mirrors
//   ISvantekMonitorJobs and IMyAtmMonitorJobs.
public interface IAirQMonitorJobs
{
    Task StoreMonitorsAsync(CancellationToken cancellationToken = default);
    Task CheckForOfflineMonitorsAsync(CancellationToken cancellationToken = default);
    Task StoreNoiseLevelsAsync(CancellationToken cancellationToken = default);
    Task StoreAllNoiseLevelsForYesterdayAsync(CancellationToken cancellationToken = default);
    Task NotifySiteAveragesAsync(CancellationToken cancellationToken = default);
    Task ClearOlderErrorMessagesAsync(CancellationToken cancellationToken = default);
    Task DispatchAlertsAsync(CancellationToken cancellationToken = default);
    Task CleanupAlertsAsync(CancellationToken cancellationToken = default);
}
