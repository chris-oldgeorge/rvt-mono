using Microsoft.Extensions.DependencyInjection;
using ReportingMonitor.Api;
using Rvt.Monitor.Common.Hosting;

return await MonitorHost.RunAsync<ReportingMonitorJobDispatcher>(
    args,
    "ReportingMonitor",
    ReportingMonitorJobRunner.GetJobName,
    (jobName, services, cancellationToken) => services.GetRequiredService<ReportingMonitorJobDispatcher>()
        .RunAsync(jobName, cancellationToken),
    app => app.MapReportingMonitorApi(),
    configureServices: (services, configuration) =>
        services.AddReportingMonitor(configuration));
