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
        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
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

    public static async Task<int> RunAsync<TDispatcher>(
        string[] args,
        string monitorName,
        Func<string[], string?> getJobName,
        Func<string, IServiceProvider, Task<int>> runJobAsync,
        Action<WebApplication> mapApi,
        Action<ILoggingBuilder>? configureLogging = null,
        Action<IServiceCollection>? configureServices = null)
        where TDispatcher : class, IMonitorJobDispatcher
    {
        var configuration = BuildConfiguration(args);
        var jobName = getJobName(args);
        if (!string.IsNullOrWhiteSpace(jobName))
        {
            using var oneShotHost = CreateOneShotHost(args, configuration, monitorName, configureLogging, configureServices);
            await oneShotHost.StartAsync();
            var loggerFactory = oneShotHost.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("Rvt.Monitor.Job");
            try
            {
                return await MonitorJobTelemetry.ExecuteAsync(
                    monitorName,
                    jobName,
                    "one-shot",
                    logger,
                    () => runJobAsync(jobName, oneShotHost.Services));
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

        var apiEnabled = configuration.GetValue<bool>("MonitorApi:Enabled");
        var schedulerEnabled = MonitorInfrastructureOptions.IsQuartzSchedulerEnabled(configuration);

        if (apiEnabled)
        {
            var apiBuilder = WebApplication.CreateBuilder(args);
            apiBuilder.Configuration.AddConfiguration(configuration);
            configureLogging?.Invoke(apiBuilder.Logging);
            MonitorOpenTelemetry.ConfigureLogging(apiBuilder.Logging, apiBuilder.Configuration, monitorName);
            MonitorOpenTelemetry.ConfigureServices(apiBuilder.Services, apiBuilder.Configuration, monitorName);
            apiBuilder.Services.AddSingleton<IMonitorRuntimeDefaultsResolver>(new MonitorRuntimeDefaultsResolver(monitorName));
            apiBuilder.Services.AddSingleton(new MonitorExecutionModeContext(MonitorExecutionMode.Api));
            configureServices?.Invoke(apiBuilder.Services);

            if (schedulerEnabled)
            {
                apiBuilder.Services.AddMonitorQuartzScheduler<TDispatcher>(apiBuilder.Configuration, monitorName);
            }

            var app = apiBuilder.Build();
            mapApi(app);
            await app.RunAsync();
            return 0;
        }

        if (schedulerEnabled)
        {
            var schedulerHostBuilder = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(builder => builder.AddConfiguration(configuration))
                .ConfigureServices((context, services) =>
                {
                    MonitorOpenTelemetry.ConfigureServices(services, context.Configuration, monitorName);
                    services.AddSingleton<IMonitorRuntimeDefaultsResolver>(new MonitorRuntimeDefaultsResolver(monitorName));
                    services.AddSingleton(new MonitorExecutionModeContext(MonitorExecutionMode.QuartzScheduler));
                    configureServices?.Invoke(services);
                    services.AddMonitorQuartzScheduler<TDispatcher>(context.Configuration, monitorName);
                })
                .ConfigureLogging((context, logging) =>
                {
                    configureLogging?.Invoke(logging);
                    MonitorOpenTelemetry.ConfigureLogging(logging, context.Configuration, monitorName);
                });

            var schedulerHost = schedulerHostBuilder.Build();
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
        Action<IServiceCollection>? configureServices)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(builder => builder.AddConfiguration(configuration))
            .ConfigureServices((context, services) =>
            {
                MonitorOpenTelemetry.ConfigureServices(services, context.Configuration, monitorName);
                services.AddSingleton<IMonitorRuntimeDefaultsResolver>(new MonitorRuntimeDefaultsResolver(monitorName));
                services.AddSingleton(new MonitorExecutionModeContext(MonitorExecutionMode.OneShot));
                configureServices?.Invoke(services);
            })
            .ConfigureLogging((context, logging) =>
            {
                configureLogging?.Invoke(logging);
                MonitorOpenTelemetry.ConfigureLogging(logging, context.Configuration, monitorName);
            })
            .Build();
    }
}
