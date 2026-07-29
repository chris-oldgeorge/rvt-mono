using Rvt.Monitor.Common.Scheduling;

namespace AirQ.Api;

// Summary: Dispatches Quartz scheduler job names through the AirQ job catalog.
// Major updates:
// - 2026-06-18 Quartz scheduling: added config-driven container scheduler dispatch.
// - 2026-07-12 DI composition: receives the container-managed AirQService instead of constructing one per run.
// - 2026-07-29 Job catalog: schedule validation reads the catalog directly, so
//   the dispatcher no longer needs a parameterless constructor, a nullable
//   service, or a runtime guard against being used unconstructed.
internal sealed class AirQMonitorJobDispatcher(AirQService service) : IMonitorJobDispatcher
{
    public IReadOnlySet<string> SupportedJobNames => AirQMonitorJobs.Catalog.JobNames;

    public Task<int> RunAsync(string jobName, CancellationToken cancellationToken) =>
        AirQMonitorJobs.Catalog.RunAsync(jobName, service, cancellationToken);
}
