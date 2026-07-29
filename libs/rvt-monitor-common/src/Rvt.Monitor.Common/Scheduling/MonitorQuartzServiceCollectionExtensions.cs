using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Rvt.Monitor.Common.Scheduling;

// Summary: Registers config-driven Quartz scheduling for monitor worker containers.
// Major updates:
// - 2026-06-18 Quartz scheduling: added shared Quartz registration from MonitorScheduler config.
public static class MonitorQuartzServiceCollectionExtensions
{
    /// <param name="supportedJobNames">
    /// The monitor's job catalog names. Supplied rather than read from a
    /// dispatcher instance because validation runs while the service collection
    /// is still being built, before any dispatcher can be resolved.
    /// </param>
    public static IServiceCollection AddMonitorQuartzScheduler<TDispatcher>(
        this IServiceCollection services,
        IConfiguration configuration,
        string monitorName,
        IReadOnlySet<string> supportedJobNames)
        where TDispatcher : class, IMonitorJobDispatcher
    {
        ArgumentNullException.ThrowIfNull(supportedJobNames);

        MonitorSchedulerOptions options = MonitorSchedulerOptions.Bind(configuration);
        MonitorInfrastructureOptions infrastructure = MonitorInfrastructureOptions.Bind(configuration);
        if (!options.Enabled || !infrastructure.AllowsQuartzScheduler)
        {
            return services;
        }

        ValidateSchedules(options, monitorName, supportedJobNames);
        services.AddSingleton<IMonitorJobDispatcher, TDispatcher>();
        services.AddQuartz(quartz =>
        {
            quartz.UseInMemoryStore();
            quartz.UseDefaultThreadPool(threadPool => threadPool.MaxConcurrency = 1);

            TimeZoneInfo timeZone = ResolveTimeZone(options.TimeZoneId);
            foreach (MonitorJobSchedule schedule in options.GetEnabledJobs())
            {
                JobKey jobKey = new(schedule.Name, monitorName);
                quartz.AddJob<MonitorQuartzJob>(job => job
                    .WithIdentity(jobKey)
                    .UsingJobData(MonitorQuartzJob.JobNameDataKey, schedule.Name));

                quartz.AddTrigger(trigger => trigger
                    .WithIdentity($"{schedule.Name}.trigger", monitorName)
                    .ForJob(jobKey)
                    .WithCronSchedule(schedule.Cron, cron => cron.InTimeZone(timeZone)));
            }
        });

        services.AddQuartzHostedService(hosting =>
        {
            hosting.WaitForJobsToComplete = true;
        });

        return services;
    }

    private static void ValidateSchedules(
        MonitorSchedulerOptions options,
        string monitorName,
        IReadOnlySet<string> supportedJobNames)
    {
        foreach (MonitorJobSchedule schedule in options.GetEnabledJobs())
        {
            if (string.IsNullOrWhiteSpace(schedule.Name))
            {
                throw new InvalidOperationException($"{monitorName} has a configured Quartz job without a name.");
            }

            if (!supportedJobNames.Contains(schedule.Name))
            {
                throw new InvalidOperationException(
                    $"Configured Quartz job '{schedule.Name}' is not supported by {monitorName}.");
            }

            if (string.IsNullOrWhiteSpace(schedule.Cron) || !CronExpression.IsValidExpression(schedule.Cron))
            {
                throw new InvalidOperationException(
                    $"Configured Quartz job '{schedule.Name}' has an invalid cron expression '{schedule.Cron}'.");
            }
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }
}
