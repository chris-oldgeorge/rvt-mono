using Microsoft.Extensions.DependencyInjection;
using ReportingMonitor.Api.UseCases;
using Rvt.Monitor.Common.Scheduling;

namespace ReportingMonitor.Api;

// Summary: The reporting job catalog — the single list of jobs this monitor supports.
// Major updates:
// - 2026-07-29 Job catalog: replaced the dispatcher name set and its switch,
//   which were two hand-maintained lists of the same job names.
internal static class ReportingMonitorJobs
{
    public static readonly MonitorJobCatalog<IServiceProvider> Catalog = new(
        "reporting monitor",
        new Dictionary<string, Func<IServiceProvider, CancellationToken, Task>>(StringComparer.Ordinal)
        {
            // The handler is scoped, so the job owns a scope for its run rather
            // than capturing one at singleton-dispatcher construction.
            ["GenerateScheduledReports"] = async (services, cancellationToken) =>
            {
                await using AsyncServiceScope scope = services.CreateAsyncScope();
                GenerateScheduledReportsHandler handler =
                    scope.ServiceProvider.GetRequiredService<GenerateScheduledReportsHandler>();
                await handler.HandleAsync(DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            }
        });
}
