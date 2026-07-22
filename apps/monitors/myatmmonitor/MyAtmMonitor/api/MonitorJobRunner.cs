namespace MyAtm.Api;

// Summary: Maps Kubernetes CronJob names to the existing MyAtm monitor operations.
// Major updates:
// - 2026-06-12 Monitor Migration: introduced one-shot execution for AKS-hosted monitor jobs.
// - 2026-07-12 DI composition: receives the container-managed MyAtmService instead of constructing one.
internal static class MonitorJobRunner
{
    public static string? GetJobName(string[] args)
    {
        var cliJob = args.SkipWhile(arg => arg != "--job").Skip(1).FirstOrDefault();
        return string.IsNullOrWhiteSpace(cliJob)
            ? Environment.GetEnvironmentVariable("RVT__MONITOR_JOB")
            : cliJob;
    }

    public static async Task<int> RunAsync(string jobName, IMyAtmMonitorJobs service, CancellationToken cancellationToken = default)
    {
        switch (jobName.Trim())
        {
            case "StoreMonitors":
                await service.StoreMonitorsAsync(cancellationToken);
                return 0;
            case "CheckForOfflineMonitors":
                await service.CheckForOfflineMonitorsAsync(cancellationToken);
                return 0;
            case "StoreDustLevels":
                await service.StoreDustLevelsAsync(cancellationToken);
                return 0;
            case "Store15MinAverageDustLevels":
                await service.Store15MinAverageDustLevelsAsync(cancellationToken);
                return 0;
            case "Store1HourAverageDustLevels":
                await service.Store1HourAverageDustLevelsAsync(cancellationToken);
                return 0;
            case "Store24HourAverageDustLevels":
                await service.Store24HourAverageDustLevelsAsync(cancellationToken);
                return 0;
            case "Process8HourAverageDustLevels":
                await service.Process8HourAverageDustLevelsAsync(cancellationToken);
                return 0;
            case "ClearOlderErrorMessages":
                await service.ClearOlderErrorMessagesAsync(cancellationToken);
                return 0;
            case "StoreAccessoryInfo":
                await service.StoreAccessoryInfoAsync(cancellationToken);
                return 0;
            case "DispatchOutbox":
                await service.DispatchOutboxAsync(cancellationToken);
                return 0;
            default:
                await Console.Error.WriteLineAsync($"Unknown MyAtm monitor job '{jobName}'.");
                return 2;
        }
    }

}
