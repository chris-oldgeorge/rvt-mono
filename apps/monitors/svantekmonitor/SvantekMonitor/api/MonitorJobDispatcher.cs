using Rvt.Monitor.Common.Scheduling;

namespace Svantek.Api;

// Summary: Dispatches Quartz scheduler job names through the Svantek job catalog.
// Major updates:
// - 2026-06-18 Quartz scheduling: added config-driven container scheduler dispatch.
// - 2026-07-12 DI composition: receives the container-managed job service instead of constructing one per run.
// - 2026-07-29 Job catalog: schedule validation reads the catalog directly, so
//   the dispatcher no longer needs a parameterless constructor, a nullable
//   service, or a runtime guard against being used unconstructed.
internal sealed class SvantekMonitorJobDispatcher(ISvantekMonitorJobs service) : IMonitorJobDispatcher
{
    public IReadOnlySet<string> SupportedJobNames => SvantekMonitorJobs.Catalog.JobNames;

    public Task<int> RunAsync(string jobName, CancellationToken cancellationToken) =>
        SvantekMonitorJobs.Catalog.RunAsync(jobName, service, cancellationToken);
}
