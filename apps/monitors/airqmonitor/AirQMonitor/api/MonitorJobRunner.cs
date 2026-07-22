namespace AirQ.Api;

// Summary: Maps Kubernetes CronJob names to the existing AirQ monitor operations.
// Major updates:
// - 2026-06-12 Monitor Migration: introduced one-shot execution for AKS-hosted monitor jobs.
// - 2026-07-12 DI composition: receives the container-managed AirQService instead of constructing one.
internal static class MonitorJobRunner
{
    public static string? GetJobName(string[] args)
    {
        var cliJob = args.SkipWhile(arg => arg != "--job").Skip(1).FirstOrDefault();
        return string.IsNullOrWhiteSpace(cliJob)
            ? Environment.GetEnvironmentVariable("RVT__MONITOR_JOB")
            : cliJob;
    }

    public static async Task<int> RunAsync(string jobName, AirQService service)
    {
        switch (jobName.Trim())
        {
            case "StoreMonitors":
                service.StoreMonitors();
                return 0;
            case "CheckForOfflineMonitors":
                service.CheckForOfflineMonitors();
                return 0;
            case "StoreNoiseLevels":
                service.StoreNoiseLevels();
                return 0;
            case "StoreAllNoiseLevelsForYesterday":
                service.StoreAllNoiseLevelsForYesterday();
                return 0;
            case "NotifySiteAverages":
                service.NotifySiteAverages();
                return 0;
            case "ClearOlderErrorMessages":
                service.ClearOlderErrorMessages();
                return 0;
            default:
                await Console.Error.WriteLineAsync($"Unknown AirQ monitor job '{jobName}'.");
                return 2;
        }
    }

}
