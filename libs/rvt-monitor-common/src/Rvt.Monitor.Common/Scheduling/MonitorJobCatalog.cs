namespace Rvt.Monitor.Common.Scheduling;

// Summary: One declarative source of truth for the jobs a monitor supports.
// Major updates:
// - 2026-07-29 Job catalog: replaced the per-monitor dispatcher name set plus
//   runner switch, which were two hand-maintained lists of the same thing.

/// <summary>
/// Maps a monitor's job names to the work each one performs.
/// </summary>
/// <remarks>
/// Every monitor previously declared its job names twice: once as the
/// dispatcher's <c>SupportedJobNames</c> set for Quartz schedule validation and
/// again as a <c>switch</c> in its job runner. The two drifted independently,
/// and a name present in only one of them failed either at schedule validation
/// or at dispatch. Here the names are the dictionary keys, so
/// <see cref="JobNames"/> and <see cref="RunAsync"/> cannot disagree.
/// </remarks>
/// <typeparam name="TContext">
/// What the jobs run against — a monitor service, a job interface, or a service
/// provider when the jobs resolve their own scoped dependencies.
/// </typeparam>
public sealed class MonitorJobCatalog<TContext>
{
    private readonly string _description;
    private readonly IReadOnlyDictionary<string, Func<TContext, CancellationToken, Task>> _jobs;

    /// <param name="description">
    /// How the monitor names itself when rejecting an unknown job, for example
    /// <c>"AirQ monitor"</c>.
    /// </param>
    public MonitorJobCatalog(
        string description,
        IReadOnlyDictionary<string, Func<TContext, CancellationToken, Task>> jobs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(jobs);

        _description = description;
        _jobs = jobs;
        JobNames = jobs.Keys.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// The job names this monitor supports, for Quartz schedule validation.
    /// </summary>
    public IReadOnlySet<string> JobNames { get; }

    /// <summary>
    /// Runs <paramref name="jobName"/>, returning the monitor's process exit
    /// code: 0 when the job ran, 2 when no such job exists.
    /// </summary>
    public async Task<int> RunAsync(string jobName, TContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(jobName);

        if (!_jobs.TryGetValue(jobName.Trim(), out Func<TContext, CancellationToken, Task>? job))
        {
            await Console.Error.WriteLineAsync($"Unknown {_description} job '{jobName}'.");
            return 2;
        }

        await job(context, cancellationToken).ConfigureAwait(false);
        return 0;
    }
}
