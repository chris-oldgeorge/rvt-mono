using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Hosting;
using Svantek.Api;

// Summary: Starts the Svantek monitor as a one-shot worker, Quartz scheduler, or minimal API host.
// Major updates:
// - 2026-06-12 Monitor Migration: added one-shot job dispatch for AKS CronJob hosting.
// - 2026-06-18 Quartz scheduling: added config-driven scheduler host for container deployments.
// - 2026-06-18 Minimal API: replaced Azure Functions host fallback with ASP.NET Core endpoints.
// - 2026-07-03 Bootstrap refactor: delegated shared host flow to Rvt.Monitor.Common.
// - 2026-07-12 DI composition: monitor services are registered in the host container.

return await MonitorHost.RunAsync<SvantekMonitorJobDispatcher>(
    args,
    "SvantekMonitor",
    MonitorJobRunner.GetJobName,
    (jobName, services) => MonitorJobRunner.RunAsync(jobName, services.GetRequiredService<ISvantekMonitorJobs>()),
    app => app.MapSvantekMonitorApi(),
    ConfigureLogging,
    services => services.AddSvantekMonitor());

static void ConfigureLogging(ILoggingBuilder logging)
{
    logging.ClearProviders();
    logging.SetMinimumLevel(LogLevel.Trace);
    logging.AddConsole();
}

//Trace = 0, Debug = 1, Information = 2, Warning = 3, Error = 4, Critical = 5, and None = 6.
