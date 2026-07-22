namespace Rvt.Monitor.Common.Scheduling;

// Summary: Dispatches configured scheduler job names to monitor-specific job runners.
// Major updates:
// - 2026-06-18 Quartz scheduling: introduced the monitor job dispatch boundary.
public interface IMonitorJobDispatcher
{
    IReadOnlySet<string> SupportedJobNames { get; }

    Task<int> RunAsync(string jobName, CancellationToken cancellationToken);
}
