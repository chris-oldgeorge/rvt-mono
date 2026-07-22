using Microsoft.Extensions.DependencyInjection;
using Rvt.Monitor.Common.Alerts;

namespace Omnidots.Api;

// Summary: Maps Kubernetes CronJob names to legacy Omnidots operations and Common durable-alert jobs.
// Major updates:
// - 2026-06-12 Monitor Migration: introduced one-shot execution for AKS-hosted monitor jobs.
// - 2026-07-15 Durable alerts: resolves each job's focused service from the host provider.
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
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        switch (jobName.Trim())
        {
            case "StoreMonitors":
                services.GetRequiredService<OmnidotsService>().StoreMonitors();
                return 0;
            case "CheckForOfflineMonitors":
                services.GetRequiredService<OmnidotsService>().CheckForOfflineMonitors();
                return 0;
            case "StorePeakRecordsLastDataTime":
                services.GetRequiredService<OmnidotsService>().StorePeakRecordsLastDataTime();
                return 0;
            case "StoreVeffRecords":
                services.GetRequiredService<OmnidotsService>().StoreVeffRecords(TimeSpan.FromHours(2));
                return 0;
            case "StoreVdvRecords":
                services.GetRequiredService<OmnidotsService>().StoreVdvRecords(TimeSpan.FromHours(2));
                return 0;
            case "StoreTraces":
                // Matches the old TimerInfo.ScheduleStatus.Last: the schedule window starts five minutes back.
                services.GetRequiredService<OmnidotsService>().StoreTraces(DateTime.UtcNow.AddMinutes(-5));
                return 0;
            case "NotifyBatteryLevels":
                services.GetRequiredService<OmnidotsService>().NotifyBatteryLevels();
                return 0;
            case "ClearOlderErrorMessages":
                services.GetRequiredService<OmnidotsService>().ClearOlderErrorMessages();
                return 0;
            case "Monitoring":
                await services.GetRequiredService<OmnidotsService>()
                    .MonitoringAsync(cancellationToken);
                return 0;
            case "DispatchAlerts":
                await services.GetRequiredService<DurableAlertDispatcher>()
                    .DispatchAsync(cancellationToken);
                return 0;
            case "CleanupAlerts":
                await services.GetRequiredService<DurableAlertCleanupService>()
                    .CleanupAsync(cancellationToken);
                return 0;
            default:
                await Console.Error.WriteLineAsync($"Unknown Omnidots monitor job '{jobName}'.");
                return 2;
        }
    }

}
