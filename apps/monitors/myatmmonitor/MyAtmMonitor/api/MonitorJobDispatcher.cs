using Rvt.Monitor.Common.Scheduling;

namespace MyAtm.Api;

// Summary: Dispatches Quartz scheduler job names to the existing MyAtm monitor runner.
// Major updates:
// - 2026-06-18 Quartz scheduling: added config-driven container scheduler dispatch.
// - 2026-07-12 DI composition: receives the container-managed MyAtmService instead of constructing one per run.
internal sealed class MyAtmMonitorJobDispatcher : IMonitorJobDispatcher
{
    private readonly IMyAtmMonitorJobs? service;

    // Used only by Quartz schedule validation, which reads SupportedJobNames and never runs jobs.
    public MyAtmMonitorJobDispatcher()
    {
    }

    public MyAtmMonitorJobDispatcher(IMyAtmMonitorJobs service)
    {
        this.service = service;
    }

    public IReadOnlySet<string> SupportedJobNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "StoreMonitors",
        "CheckForOfflineMonitors",
        "StoreDustLevels",
        "Store15MinAverageDustLevels",
        "Store1HourAverageDustLevels",
        "Store24HourAverageDustLevels",
        "Process8HourAverageDustLevels",
        "ClearOlderErrorMessages",
        "StoreAccessoryInfo",
        "DispatchOutbox"
    };

    public Task<int> RunAsync(string jobName, CancellationToken cancellationToken)
    {
        if (service == null)
        {
            throw new InvalidOperationException(
                "MyAtmMonitorJobDispatcher was created without a MyAtmService and cannot run jobs.");
        }

        return MonitorJobRunner.RunAsync(jobName, service, cancellationToken);
    }
}
