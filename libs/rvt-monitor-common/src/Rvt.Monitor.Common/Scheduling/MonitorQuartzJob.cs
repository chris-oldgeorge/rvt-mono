using Microsoft.Extensions.Logging;
using Quartz;
using Rvt.Monitor.Common.Hosting;

namespace Rvt.Monitor.Common.Scheduling;

// Summary: Executes a configured monitor job through the monitor-specific dispatcher.
// Major updates:
// - 2026-06-18 Quartz scheduling: introduced shared Quartz job execution bridge.
[DisallowConcurrentExecution]
public sealed class MonitorQuartzJob(IMonitorJobDispatcher dispatcher, ILogger<MonitorQuartzJob> logger) : IJob
{
    public const string JobNameDataKey = "JobName";

    public async Task Execute(IJobExecutionContext context)
    {
        var jobName = context.MergedJobDataMap.ContainsKey(JobNameDataKey)
            ? context.MergedJobDataMap.GetString(JobNameDataKey)
            : null;
        if (string.IsNullOrWhiteSpace(jobName))
        {
            throw new JobExecutionException("Quartz monitor job is missing JobName.");
        }

        var monitorName = context.JobDetail.Key.Group;
        var exitCode = await MonitorJobTelemetry.ExecuteAsync(
            monitorName,
            jobName,
            "quartz",
            logger,
            () => dispatcher.RunAsync(jobName, context.CancellationToken));

        if (exitCode != 0)
        {
            throw new JobExecutionException($"Monitor job '{jobName}' failed with exit code {exitCode}.");
        }
    }
}
