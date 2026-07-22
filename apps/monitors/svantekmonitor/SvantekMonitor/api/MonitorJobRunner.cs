namespace Svantek.Api;

// Summary: Maps Kubernetes CronJob names to the existing Svantek monitor operations.
// Major updates:
// - 2026-06-12 Monitor Migration: introduced one-shot execution for AKS-hosted monitor jobs.
// - 2026-07-12 DI composition: receives the container-managed SvantekService instead of constructing one.
internal static class MonitorJobRunner
{
    public static string? GetJobName(string[] args)
    {
        var cliJob = args.SkipWhile(arg => arg != "--job").Skip(1).FirstOrDefault();
        return string.IsNullOrWhiteSpace(cliJob)
            ? Environment.GetEnvironmentVariable("RVT__MONITOR_JOB")
            : cliJob;
    }

    public static async Task<int> RunAsync(
        string jobName,
        ISvantekMonitorJobs service,
        CancellationToken cancellationToken = default)
    {
        switch (jobName.Trim())
        {
            case "StoreMonitors":
                await service.StoreMonitorsAsync(cancellationToken);
                return 0;
            case "StoreNoiseLevels":
                await service.StoreNoiseLevelsAsync(cancellationToken);
                return 0;
            case "NotifySiteAverages":
                await service.NotifySiteAveragesAsync(cancellationToken);
                return 0;
            case "CheckForOfflineMonitors":
                await service.CheckForOfflineMonitorsAsync(cancellationToken);
                return 0;
            case "NotifyBatteryLevels":
                await service.NotifyBatteryLevelsAsync(cancellationToken);
                return 0;
            case "CheckForSoundRecordings":
                await service.CheckForSoundRecordingsAsync(cancellationToken);
                return 0;
            default:
                await Console.Error.WriteLineAsync($"Unknown Svantek monitor job '{jobName}'.");
                return 2;
        }
    }

}
