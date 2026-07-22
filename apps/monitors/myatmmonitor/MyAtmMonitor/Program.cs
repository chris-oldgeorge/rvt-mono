using Microsoft.Extensions.DependencyInjection;
using MyAtm.Api;
using Rvt.Monitor.Common.Hosting;

// Summary: Starts the MyAtm monitor as a one-shot worker, Quartz scheduler, or minimal API host.
// Major updates:
// - 2026-06-12 Monitor Migration: added one-shot job dispatch for AKS CronJob hosting.
// - 2026-06-18 Quartz scheduling: added config-driven scheduler host for container deployments.
// - 2026-06-18 Minimal API: replaced Azure Functions host fallback with ASP.NET Core endpoints.
// - 2026-07-03 Bootstrap refactor: delegated shared host flow to Rvt.Monitor.Common.
// - 2026-07-12 DI composition: monitor services are registered in the host container.

return await MonitorHost.RunAsync<MyAtmMonitorJobDispatcher>(
    args,
    "MyAtmMonitor",
    MonitorJobRunner.GetJobName,
    (jobName, services) => MonitorJobRunner.RunAsync(jobName, services.GetRequiredService<MyAtmService>()),
    app => app.MapMyAtmMonitorApi(),
    configureServices: services => services.AddMyAtmMonitor());
