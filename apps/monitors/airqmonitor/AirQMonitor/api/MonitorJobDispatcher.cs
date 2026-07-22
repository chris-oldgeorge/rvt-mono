using Rvt.Monitor.Common.Scheduling;

namespace AirQ.Api;

// Summary: Dispatches Quartz scheduler job names to the existing AirQ monitor runner.
// Major updates:
// - 2026-06-18 Quartz scheduling: added config-driven container scheduler dispatch.
// - 2026-07-12 DI composition: receives the container-managed AirQService instead of constructing one per run.
internal sealed class AirQMonitorJobDispatcher : IMonitorJobDispatcher
{
    private readonly AirQService? service;

    // Used only by Quartz schedule validation, which reads SupportedJobNames and never runs jobs.
    public AirQMonitorJobDispatcher()
    {
    }

    public AirQMonitorJobDispatcher(AirQService service)
    {
        this.service = service;
    }

    public IReadOnlySet<string> SupportedJobNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "StoreMonitors",
        "CheckForOfflineMonitors",
        "StoreNoiseLevels",
        "StoreAllNoiseLevelsForYesterday",
        "NotifySiteAverages",
        "ClearOlderErrorMessages"
    };

    public Task<int> RunAsync(string jobName, CancellationToken cancellationToken)
    {
        if (service == null)
        {
            throw new InvalidOperationException(
                "AirQMonitorJobDispatcher was created without an AirQService and cannot run jobs.");
        }

        return MonitorJobRunner.RunAsync(jobName, service);
    }
}
