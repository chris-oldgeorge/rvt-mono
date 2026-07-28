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
                await services.GetRequiredService<OmnidotsService>().StoreMonitorsAsync(cancellationToken);
                return 0;
            case "CheckForOfflineMonitors":
                await services.GetRequiredService<OmnidotsService>().CheckForOfflineMonitorsAsync(cancellationToken);
                return 0;
            case "StorePeakRecordsLastDataTime":
                await services.GetRequiredService<OmnidotsService>().StorePeakRecordsLastDataTimeAsync(cancellationToken);
                return 0;
            case "StoreVeffRecords":
                await services.GetRequiredService<OmnidotsService>().StoreVeffRecordsAsync(TimeSpan.FromHours(2), cancellationToken);
                return 0;
            case "StoreVdvRecords":
                await services.GetRequiredService<OmnidotsService>().StoreVdvRecordsAsync(TimeSpan.FromHours(2), cancellationToken);
                return 0;
            case "StoreTraces":
                // Matches the old TimerInfo.ScheduleStatus.Last: the schedule window starts five minutes back.
                await services.GetRequiredService<OmnidotsService>().StoreTracesAsync(DateTime.UtcNow.AddMinutes(-5), cancellationToken);
                return 0;
            case "NotifyBatteryLevels":
                await services.GetRequiredService<OmnidotsService>().NotifyBatteryLevelsAsync(cancellationToken);
                return 0;
            case "ClearOlderErrorMessages":
                await services.GetRequiredService<OmnidotsService>().ClearOlderErrorMessagesAsync(cancellationToken);
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
