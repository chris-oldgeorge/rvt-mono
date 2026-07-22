using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Rvt.Monitor.Common.Hosting;

public static class MonitorJobTelemetry
{
    public const string ActivitySourceName = "Rvt.Monitor.Common.MonitorJobs";
    public const string MeterName = "Rvt.Monitor.Common.MonitorJobs";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> StartedCounter = Meter.CreateCounter<long>("rvt.monitor.job.started");
    private static readonly Counter<long> CompletedCounter = Meter.CreateCounter<long>("rvt.monitor.job.completed");
    private static readonly Counter<long> FailedCounter = Meter.CreateCounter<long>("rvt.monitor.job.failed");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("rvt.monitor.job.duration", "s");

    public static async Task<int> ExecuteAsync(
        string monitorName,
        string jobName,
        string executionMode,
        ILogger logger,
        Func<Task<int>> runAsync)
    {
        using var activity = ActivitySource.StartActivity("Monitor job", ActivityKind.Internal);
        var start = Stopwatch.GetTimestamp();
        var tags = CreateTags(monitorName, jobName, executionMode);
        SetActivityTags(activity, tags);

        StartedCounter.Add(1, tags);
        logger.LogInformation(
            "Monitor job started {MonitorName} {JobName} {ExecutionMode}",
            monitorName,
            jobName,
            executionMode);

        try
        {
            var exitCode = await runAsync();
            var durationSeconds = Stopwatch.GetElapsedTime(start).TotalSeconds;
            Duration.Record(durationSeconds, CreateTags(monitorName, jobName, executionMode, exitCode));

            if (exitCode == 0)
            {
                CompletedCounter.Add(1, tags);
                activity?.SetStatus(ActivityStatusCode.Ok);
                logger.LogInformation(
                    "Monitor job completed {MonitorName} {JobName} {ExecutionMode} {ExitCode} {DurationSeconds}",
                    monitorName,
                    jobName,
                    executionMode,
                    exitCode,
                    durationSeconds);
            }
            else
            {
                FailedCounter.Add(1, CreateTags(monitorName, jobName, executionMode, exitCode));
                activity?.SetStatus(ActivityStatusCode.Error, $"Exit code {exitCode}");
                logger.LogError(
                    "Monitor job failed {MonitorName} {JobName} {ExecutionMode} {ExitCode} {DurationSeconds}",
                    monitorName,
                    jobName,
                    executionMode,
                    exitCode,
                    durationSeconds);
            }

            activity?.SetTag("rvt.monitor.exit_code", exitCode);
            activity?.SetTag("rvt.monitor.duration_seconds", durationSeconds);
            return exitCode;
        }
        catch (Exception exception)
        {
            var durationSeconds = Stopwatch.GetElapsedTime(start).TotalSeconds;
            FailedCounter.Add(1, tags);
            Duration.Record(durationSeconds, tags);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.SetTag("exception.type", exception.GetType().FullName);
            activity?.SetTag("exception.message", exception.Message);
            logger.LogError(
                exception,
                "Monitor job failed {MonitorName} {JobName} {ExecutionMode} {DurationSeconds}",
                monitorName,
                jobName,
                executionMode,
                durationSeconds);
            throw;
        }
    }

    private static KeyValuePair<string, object?>[] CreateTags(
        string monitorName,
        string jobName,
        string executionMode,
        int? exitCode = null)
    {
        var tags = new List<KeyValuePair<string, object?>>
        {
            new("rvt.monitor.name", monitorName),
            new("rvt.monitor.job.name", jobName),
            new("rvt.monitor.execution.mode", executionMode)
        };

        if (exitCode is not null)
        {
            tags.Add(new KeyValuePair<string, object?>("rvt.monitor.exit_code", exitCode));
        }

        return tags.ToArray();
    }

    private static void SetActivityTags(Activity? activity, IEnumerable<KeyValuePair<string, object?>> tags)
    {
        if (activity is null)
        {
            return;
        }

        foreach (var tag in tags)
        {
            activity.SetTag(tag.Key, tag.Value);
        }
    }
}
