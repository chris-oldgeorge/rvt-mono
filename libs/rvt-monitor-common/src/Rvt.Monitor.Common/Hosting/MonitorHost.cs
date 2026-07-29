using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Scheduling;

namespace Rvt.Monitor.Common.Hosting;

// Summary: Shares monitor startup across one-shot jobs, minimal APIs, and Quartz scheduler hosts.
// Major updates:
// - 2026-07-03 Bootstrap refactor: centralized repeated monitor Program.cs host flow.
// - 2026-07-12 DI composition: added configureServices hook; one-shot jobs run against the host service provider.
// - 2026-07-12 RvtConfig cleanup: declares the monitor kind explicitly instead of relying on assembly-name sniffing.
public static class MonitorHost
{
    private const string NoExecutionModeMessage =
        "No monitor execution mode configured. Set MonitorApi:Enabled=true, MonitorScheduler:Enabled=true, or pass --job <name>.";

    public static IConfiguration BuildConfiguration(string[] args)
    {
        string environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();
    }

    /// <param name="supportedJobNames">
    /// The monitor's job catalog names, used to validate the configured Quartz
    /// schedule before the container is built.
    /// </param>
    public static async Task<int> RunAsync<TDispatcher>(
        string[] args,
        string monitorName,
        IReadOnlySet<string> supportedJobNames,
        Func<string, IServiceProvider, CancellationToken, Task<int>> runJobAsync,
        Action<WebApplication> mapApi,
        Action<ILoggingBuilder>? configureLogging = null,
        Action<IServiceCollection, IConfiguration>? configureServices = null)
        where TDispatcher : class, IMonitorJobDispatcher
    {
        IConfiguration configuration = BuildConfiguration(args);
        string? jobName = MonitorJobArguments.GetJobName(args);
        if (!string.IsNullOrWhiteSpace(jobName))
        {
            using IHost oneShotHost = CreateOneShotHost(args, configuration, monitorName, configureLogging, configureServices);
            await oneShotHost.StartAsync();
            ILoggerFactory loggerFactory = oneShotHost.Services.GetRequiredService<ILoggerFactory>();
            ILogger logger = loggerFactory.CreateLogger("Rvt.Monitor.Job");

            // SIGTERM reaches the job through this token: the console lifetime trips
            // ApplicationStopping, which cancels the in-flight vendor calls instead of
            // leaving them running until the container is killed.
            IHostApplicationLifetime lifetime = oneShotHost.Services.GetRequiredService<IHostApplicationLifetime>();
            try
            {
                return await MonitorJobTelemetry.ExecuteAsync(
                    monitorName,
                    jobName,
                    "one-shot",
                    logger,
                    () => runJobAsync(jobName, oneShotHost.Services, lifetime.ApplicationStopping));
            }
            catch (OperationCanceledException) when (lifetime.ApplicationStopping.IsCancellationRequested)
            {
                logger.LogWarning(
                    "Monitor job {JobName} was stopped by host shutdown before it completed.",
                    jobName);
                return 1;
            }
            catch (Exception exception)
            {
                await Console.Error.WriteLineAsync(exception.Message);
                return 1;
            }
            finally
            {
                await oneShotHost.StopAsync();
            }
        }

        bool apiEnabled = configuration.GetValue<bool>("MonitorApi:Enabled");
        bool schedulerEnabled = MonitorInfrastructureOptions.IsQuartzSchedulerEnabled(configuration);

        if (apiEnabled)
        {
            WebApplicationBuilder apiBuilder = WebApplication.CreateBuilder(args);
            apiBuilder.Configuration.AddConfiguration(configuration);
            configureLogging?.Invoke(apiBuilder.Logging);
            MonitorOpenTelemetry.ConfigureLogging(apiBuilder.Logging, apiBuilder.Configuration, monitorName);
            MonitorOpenTelemetry.ConfigureServices(apiBuilder.Services, apiBuilder.Configuration, monitorName);
            apiBuilder.Services.AddSingleton<IMonitorRuntimeDefaultsResolver>(new MonitorRuntimeDefaultsResolver(monitorName));
            apiBuilder.Services.AddSingleton(new MonitorExecutionModeContext(MonitorExecutionMode.Api));
            configureServices?.Invoke(apiBuilder.Services, apiBuilder.Configuration);

            if (schedulerEnabled)
            {
                apiBuilder.Services.AddMonitorQuartzScheduler<TDispatcher>(apiBuilder.Configuration, monitorName, supportedJobNames);
            }

            WebApplication app = apiBuilder.Build();
            mapApi(app);
            await app.RunAsync();
            return 0;
        }

        if (schedulerEnabled)
        {
            IHostBuilder schedulerHostBuilder = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(builder => builder.AddConfiguration(configuration))
                .ConfigureServices((context, services) =>
                {
                    MonitorOpenTelemetry.ConfigureServices(services, context.Configuration, monitorName);
                    services.AddSingleton<IMonitorRuntimeDefaultsResolver>(new MonitorRuntimeDefaultsResolver(monitorName));
                    services.AddSingleton(new MonitorExecutionModeContext(MonitorExecutionMode.QuartzScheduler));
                    configureServices?.Invoke(services, context.Configuration);
                    services.AddMonitorQuartzScheduler<TDispatcher>(context.Configuration, monitorName, supportedJobNames);
                })
                .ConfigureLogging((context, logging) =>
                {
                    configureLogging?.Invoke(logging);
                    MonitorOpenTelemetry.ConfigureLogging(logging, context.Configuration, monitorName);
                });

            IHost schedulerHost = schedulerHostBuilder.Build();
            await schedulerHost.RunAsync();
            return 0;
        }

        await Console.Error.WriteLineAsync(NoExecutionModeMessage);
        return 2;
    }

    private static IHost CreateOneShotHost(
        string[] args,
        IConfiguration configuration,
        string monitorName,
        Action<ILoggingBuilder>? configureLogging,
        Action<IServiceCollection, IConfiguration>? configureServices)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(builder => builder.AddConfiguration(configuration))
            .ConfigureServices((context, services) =>
            {
                MonitorOpenTelemetry.ConfigureServices(services, context.Configuration, monitorName);
                services.AddSingleton<IMonitorRuntimeDefaultsResolver>(new MonitorRuntimeDefaultsResolver(monitorName));
                services.AddSingleton(new MonitorExecutionModeContext(MonitorExecutionMode.OneShot));
                configureServices?.Invoke(services, context.Configuration);
            })
            .ConfigureLogging((context, logging) =>
            {
                configureLogging?.Invoke(logging);
                MonitorOpenTelemetry.ConfigureLogging(logging, context.Configuration, monitorName);
            })
            .Build();
    }
}
