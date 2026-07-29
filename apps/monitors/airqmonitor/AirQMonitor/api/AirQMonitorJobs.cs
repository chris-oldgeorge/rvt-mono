// The namespace follows this project's established scheme rather than the
// folder path; IDE0130 would require a name no sibling file uses.
#pragma warning disable IDE0130
using Rvt.Monitor.Common.Scheduling;

namespace AirQ.Api;

// Summary: The AirQ job catalog — the single list of jobs this monitor supports.
// Major updates:
// - 2026-07-29 Job catalog: replaced the dispatcher name set and the runner
//   switch, which were two hand-maintained lists of the same job names.
internal static class AirQMonitorJobs
{
    public static readonly MonitorJobCatalog<AirQService> Catalog = new(
        "AirQ monitor",
        new Dictionary<string, Func<AirQService, CancellationToken, Task>>(StringComparer.Ordinal)
        {
            ["StoreMonitors"] = (service, cancellationToken) => service.StoreMonitorsAsync(cancellationToken),
            ["CheckForOfflineMonitors"] = (service, cancellationToken) => service.CheckForOfflineMonitorsAsync(cancellationToken),
            ["StoreNoiseLevels"] = (service, cancellationToken) => service.StoreNoiseLevelsAsync(cancellationToken),
            ["StoreAllNoiseLevelsForYesterday"] = (service, cancellationToken) => service.StoreAllNoiseLevelsForYesterdayAsync(cancellationToken),
            ["NotifySiteAverages"] = (service, cancellationToken) => service.NotifySiteAveragesAsync(cancellationToken),
            ["ClearOlderErrorMessages"] = (service, cancellationToken) => service.ClearOlderErrorMessagesAsync(cancellationToken)
        });
}
