// The namespace follows this project's established scheme rather than the
// folder path; IDE0130 would require a name no sibling file uses.
#pragma warning disable IDE0130
using Rvt.Monitor.Common.Scheduling;

namespace MyAtm.Api;

// Summary: The MyAtm job catalog — the single list of jobs this monitor supports.
// Major updates:
// - 2026-07-29 Job catalog: replaced the dispatcher name set and the runner
//   switch, which were two hand-maintained lists of the same job names.
internal static class MyAtmMonitorJobs
{
    public static readonly MonitorJobCatalog<IMyAtmMonitorJobs> Catalog = new(
        "MyAtm monitor",
        new Dictionary<string, Func<IMyAtmMonitorJobs, CancellationToken, Task>>(StringComparer.Ordinal)
        {
            ["StoreMonitors"] = (service, cancellationToken) => service.StoreMonitorsAsync(cancellationToken),
            ["CheckForOfflineMonitors"] = (service, cancellationToken) => service.CheckForOfflineMonitorsAsync(cancellationToken),
            ["StoreDustLevels"] = (service, cancellationToken) => service.StoreDustLevelsAsync(cancellationToken),
            ["Store15MinAverageDustLevels"] = (service, cancellationToken) => service.Store15MinAverageDustLevelsAsync(cancellationToken),
            ["Store1HourAverageDustLevels"] = (service, cancellationToken) => service.Store1HourAverageDustLevelsAsync(cancellationToken),
            ["Store24HourAverageDustLevels"] = (service, cancellationToken) => service.Store24HourAverageDustLevelsAsync(cancellationToken),
            ["Process8HourAverageDustLevels"] = (service, cancellationToken) => service.Process8HourAverageDustLevelsAsync(cancellationToken),
            ["ClearOlderErrorMessages"] = (service, cancellationToken) => service.ClearOlderErrorMessagesAsync(cancellationToken),
            ["StoreAccessoryInfo"] = (service, cancellationToken) => service.StoreAccessoryInfoAsync(cancellationToken),
            ["DispatchOutbox"] = (service, cancellationToken) => service.DispatchOutboxAsync(cancellationToken)
        });
}
