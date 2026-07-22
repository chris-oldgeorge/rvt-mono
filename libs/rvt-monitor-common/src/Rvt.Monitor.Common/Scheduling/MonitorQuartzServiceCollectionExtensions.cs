using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Rvt.Monitor.Common.Scheduling;

// Summary: Registers config-driven Quartz scheduling for monitor worker containers.
// Major updates:
// - 2026-06-18 Quartz scheduling: added shared Quartz registration from MonitorScheduler config.
public static class MonitorQuartzServiceCollectionExtensions
{
    public static IServiceCollection AddMonitorQuartzScheduler<TDispatcher>(
        this IServiceCollection services,
        IConfiguration configuration,
        string monitorName)
        where TDispatcher : class, IMonitorJobDispatcher
    {
        var options = MonitorSchedulerOptions.Bind(configuration);
        var infrastructure = MonitorInfrastructureOptions.Bind(configuration);
        if (!options.Enabled || !infrastructure.AllowsQuartzScheduler)
        {
            return services;
        }

        ValidateSchedules<TDispatcher>(options, monitorName);
        services.AddSingleton<IMonitorJobDispatcher, TDispatcher>();
        services.AddQuartz(quartz =>
        {
            quartz.UseInMemoryStore();
            quartz.UseDefaultThreadPool(threadPool => threadPool.MaxConcurrency = 1);

            var timeZone = ResolveTimeZone(options.TimeZoneId);
            foreach (var schedule in options.GetEnabledJobs())
            {
                var jobKey = new JobKey(schedule.Name, monitorName);
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

    private static void ValidateSchedules<TDispatcher>(MonitorSchedulerOptions options, string monitorName)
        where TDispatcher : class, IMonitorJobDispatcher
    {
        var dispatcher = CreateDispatcherForValidation<TDispatcher>();
        foreach (var schedule in options.GetEnabledJobs())
        {
            if (string.IsNullOrWhiteSpace(schedule.Name))
            {
                throw new InvalidOperationException($"{monitorName} has a configured Quartz job without a name.");
            }

            if (!dispatcher.SupportedJobNames.Contains(schedule.Name))
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

    private static IMonitorJobDispatcher CreateDispatcherForValidation<TDispatcher>()
        where TDispatcher : class, IMonitorJobDispatcher
    {
        try
        {
            return (IMonitorJobDispatcher?)Activator.CreateInstance(typeof(TDispatcher), nonPublic: true)
                ?? throw new InvalidOperationException(
                    $"Unable to create {typeof(TDispatcher).Name} for Quartz schedule validation.");
        }
        catch (MissingMethodException exception)
        {
            throw new InvalidOperationException(
                $"{typeof(TDispatcher).Name} must expose a parameterless constructor for Quartz schedule validation.",
                exception);
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
