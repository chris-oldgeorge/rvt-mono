namespace Rvt.Monitor.Common.Hosting;

// Summary: Reads the one-shot job name from the command line or the environment.
// Major updates:
// - 2026-07-29 Job catalog: replaced five byte-identical GetJobName copies.

/// <summary>
/// Resolves which job a one-shot monitor run should execute.
/// </summary>
public static class MonitorJobArguments
{
    /// <summary>
    /// The environment variable a Kubernetes CronJob sets to name its job.
    /// </summary>
    public const string JobNameEnvironmentVariable = "RVT__MONITOR_JOB";

    /// <summary>
    /// Returns the job named by <c>--job &lt;name&gt;</c>, falling back to
    /// <see cref="JobNameEnvironmentVariable"/>, or <see langword="null"/> when
    /// neither names one and the host should pick another execution mode.
    /// </summary>
    public static string? GetJobName(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? cliJob = args.SkipWhile(arg => arg != "--job").Skip(1).FirstOrDefault();
        return string.IsNullOrWhiteSpace(cliJob)
            ? Environment.GetEnvironmentVariable(JobNameEnvironmentVariable)
            : cliJob;
    }
}
