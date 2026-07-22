using Rvt.Monitor.Common.Scheduling;

namespace Omnidots.Api;

// Summary: Dispatches Quartz scheduler job names through the Omnidots monitor service provider.
// Major updates:
// - 2026-06-18 Quartz scheduling: added config-driven container scheduler dispatch.
// - 2026-07-15 Durable alerts: dispatches both legacy monitor jobs and Common alert maintenance jobs.
internal sealed class OmnidotsMonitorJobDispatcher : IMonitorJobDispatcher
{
    private readonly IServiceProvider? services;

    // Used only by Quartz schedule validation, which reads SupportedJobNames and never runs jobs.
    public OmnidotsMonitorJobDispatcher()
    {
    }

    public OmnidotsMonitorJobDispatcher(IServiceProvider services)
    {
        this.services = services;
    }

    public IReadOnlySet<string> SupportedJobNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "StoreMonitors",
        "CheckForOfflineMonitors",
        "StorePeakRecordsLastDataTime",
        "StoreVeffRecords",
        "StoreVdvRecords",
        "StoreTraces",
        "NotifyBatteryLevels",
        "ClearOlderErrorMessages",
        "Monitoring",
        "DispatchAlerts",
        "CleanupAlerts"
    };

    public Task<int> RunAsync(string jobName, CancellationToken cancellationToken)
    {
        if (services == null)
        {
            throw new InvalidOperationException(
                "OmnidotsMonitorJobDispatcher was created without a service provider and cannot run jobs.");
        }

        return MonitorJobRunner.RunAsync(jobName, services, cancellationToken);
    }
}
