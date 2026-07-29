using Rvt.Monitor.Common.Scheduling;

namespace Omnidots.Api;

// Summary: Dispatches Quartz scheduler job names through the Omnidots job catalog.
// Major updates:
// - 2026-06-18 Quartz scheduling: added config-driven container scheduler dispatch.
// - 2026-07-15 Durable alerts: dispatches both legacy monitor jobs and Common alert maintenance jobs.
// - 2026-07-29 Job catalog: schedule validation reads the catalog directly, so
//   the dispatcher no longer needs a parameterless constructor, a nullable
//   provider, or a runtime guard against being used unconstructed.
internal sealed class OmnidotsMonitorJobDispatcher(IServiceProvider services) : IMonitorJobDispatcher
{
    public IReadOnlySet<string> SupportedJobNames => OmnidotsMonitorJobs.Catalog.JobNames;

    public Task<int> RunAsync(string jobName, CancellationToken cancellationToken) =>
        OmnidotsMonitorJobs.Catalog.RunAsync(jobName, services, cancellationToken);
}
