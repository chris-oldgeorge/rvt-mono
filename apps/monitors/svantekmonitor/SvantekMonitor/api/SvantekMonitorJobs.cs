// The namespace follows this project's established scheme rather than the
// folder path; IDE0130 would require a name no sibling file uses.
#pragma warning disable IDE0130
using Rvt.Monitor.Common.Scheduling;

namespace Svantek.Api;

// Summary: The Svantek job catalog — the single list of jobs this monitor supports.
// Major updates:
// - 2026-07-29 Job catalog: replaced the dispatcher name set and the runner
//   switch, which were two hand-maintained lists of the same job names.
internal static class SvantekMonitorJobs
{
    public static readonly MonitorJobCatalog<ISvantekMonitorJobs> Catalog = new(
        "Svantek monitor",
        new Dictionary<string, Func<ISvantekMonitorJobs, CancellationToken, Task>>(StringComparer.Ordinal)
        {
            ["StoreMonitors"] = (service, cancellationToken) => service.StoreMonitorsAsync(cancellationToken),
            ["StoreNoiseLevels"] = (service, cancellationToken) => service.StoreNoiseLevelsAsync(cancellationToken),
            ["NotifySiteAverages"] = (service, cancellationToken) => service.NotifySiteAveragesAsync(cancellationToken),
            ["CheckForOfflineMonitors"] = (service, cancellationToken) => service.CheckForOfflineMonitorsAsync(cancellationToken),
            ["NotifyBatteryLevels"] = (service, cancellationToken) => service.NotifyBatteryLevelsAsync(cancellationToken),
            ["CheckForSoundRecordings"] = (service, cancellationToken) => service.CheckForSoundRecordingsAsync(cancellationToken),
            ["ClearOlderErrorMessages"] = (service, cancellationToken) => service.ClearOlderErrorMessagesAsync(cancellationToken),
            ["DispatchAlerts"] = (service, cancellationToken) => service.DispatchAlertsAsync(cancellationToken),
            ["CleanupAlerts"] = (service, cancellationToken) => service.CleanupAlertsAsync(cancellationToken)
        });
}
