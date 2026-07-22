using Rvt.Monitor.Common.Scheduling;

namespace Svantek.Api;

// Summary: Dispatches Quartz scheduler job names to the existing Svantek monitor runner.
// Major updates:
// - 2026-06-18 Quartz scheduling: added config-driven container scheduler dispatch.
// - 2026-07-12 DI composition: receives the container-managed SvantekService instead of constructing one per run.
internal sealed class SvantekMonitorJobDispatcher : IMonitorJobDispatcher
{
    private readonly ISvantekMonitorJobs? service;

    // Used only by Quartz schedule validation, which reads SupportedJobNames and never runs jobs.
    public SvantekMonitorJobDispatcher()
    {
    }

    public SvantekMonitorJobDispatcher(ISvantekMonitorJobs service)
    {
        this.service = service;
    }

    public IReadOnlySet<string> SupportedJobNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "StoreMonitors",
        "StoreNoiseLevels",
        "NotifySiteAverages",
        "CheckForOfflineMonitors",
        "NotifyBatteryLevels",
        "CheckForSoundRecordings"
    };

    public Task<int> RunAsync(string jobName, CancellationToken cancellationToken)
    {
        if (service == null)
        {
            throw new InvalidOperationException(
                "SvantekMonitorJobDispatcher was created without an ISvantekMonitorJobs and cannot run jobs.");
        }

        return MonitorJobRunner.RunAsync(jobName, service, cancellationToken);
    }
}
