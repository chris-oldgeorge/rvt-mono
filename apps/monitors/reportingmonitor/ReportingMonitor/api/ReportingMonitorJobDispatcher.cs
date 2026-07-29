using Rvt.Monitor.Common.Scheduling;

namespace ReportingMonitor.Api;

// Summary: Dispatches Quartz scheduler job names through the reporting job catalog.
// Major updates:
// - 2026-07-29 Job catalog: schedule validation reads the catalog directly, so
//   the dispatcher no longer needs a parameterless constructor, nullable
//   dependencies, or a separate handler constructor for tests. Each job owns
//   its own scope, so the singleton dispatcher holds only the root provider.
public sealed class ReportingMonitorJobDispatcher(IServiceProvider services) : IMonitorJobDispatcher
{
    public IReadOnlySet<string> SupportedJobNames => ReportingMonitorJobs.Catalog.JobNames;

    public Task<int> RunAsync(string jobName, CancellationToken cancellationToken) =>
        ReportingMonitorJobs.Catalog.RunAsync(jobName, services, cancellationToken);
}
