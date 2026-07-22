using Microsoft.Extensions.DependencyInjection;
using ReportingMonitor.Api;
using Rvt.Monitor.Common.Hosting;

return await MonitorHost.RunAsync<ReportingMonitorJobDispatcher>(
    args,
    "ReportingMonitor",
    ReportingMonitorJobRunner.GetJobName,
    (jobName, services) => services.GetRequiredService<ReportingMonitorJobDispatcher>().RunAsync(jobName, CancellationToken.None),
    app => app.MapReportingMonitorApi(),
    configureServices: services => services.AddReportingMonitor());
