// The namespace follows this project's established scheme rather than the
// folder path; IDE0130 would require a name no sibling file uses.
#pragma warning disable IDE0130
using Microsoft.Extensions.DependencyInjection;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Scheduling;

namespace Omnidots.Api;

// Summary: The Omnidots job catalog — legacy monitor jobs plus Common durable-alert maintenance.
// Major updates:
// - 2026-07-15 Durable alerts: resolves each job's focused service from the host provider.
// - 2026-07-29 Job catalog: replaced the dispatcher name set and the runner
//   switch, which were two hand-maintained lists of the same job names.
internal static class OmnidotsMonitorJobs
{
    public static readonly MonitorJobCatalog<IServiceProvider> Catalog = new(
        "Omnidots monitor",
        new Dictionary<string, Func<IServiceProvider, CancellationToken, Task>>(StringComparer.Ordinal)
        {
            ["StoreMonitors"] = (services, cancellationToken) =>
                services.GetRequiredService<OmnidotsService>().StoreMonitorsAsync(cancellationToken),
            ["CheckForOfflineMonitors"] = (services, cancellationToken) =>
                services.GetRequiredService<OmnidotsService>().CheckForOfflineMonitorsAsync(cancellationToken),
            ["StorePeakRecordsLastDataTime"] = (services, cancellationToken) =>
                services.GetRequiredService<OmnidotsService>().StorePeakRecordsLastDataTimeAsync(cancellationToken),
            ["StoreVeffRecords"] = (services, cancellationToken) =>
                services.GetRequiredService<OmnidotsService>().StoreVeffRecordsAsync(TimeSpan.FromHours(2), cancellationToken),
            ["StoreVdvRecords"] = (services, cancellationToken) =>
                services.GetRequiredService<OmnidotsService>().StoreVdvRecordsAsync(TimeSpan.FromHours(2), cancellationToken),

            // Matches the old TimerInfo.ScheduleStatus.Last: the schedule window starts five minutes back.
            ["StoreTraces"] = (services, cancellationToken) =>
                services.GetRequiredService<OmnidotsService>().StoreTracesAsync(DateTime.UtcNow.AddMinutes(-5), cancellationToken),
            ["NotifyBatteryLevels"] = (services, cancellationToken) =>
                services.GetRequiredService<OmnidotsService>().NotifyBatteryLevelsAsync(cancellationToken),
            ["ClearOlderErrorMessages"] = (services, cancellationToken) =>
                services.GetRequiredService<OmnidotsService>().ClearOlderErrorMessagesAsync(cancellationToken),
            ["Monitoring"] = (services, cancellationToken) =>
                services.GetRequiredService<OmnidotsService>().MonitoringAsync(cancellationToken),
            ["DispatchAlerts"] = (services, cancellationToken) =>
                services.GetRequiredService<DurableAlertDispatcher>().DispatchAsync(cancellationToken),
            ["CleanupAlerts"] = (services, cancellationToken) =>
                services.GetRequiredService<DurableAlertCleanupService>().CleanupAsync(cancellationToken)
        });
}
