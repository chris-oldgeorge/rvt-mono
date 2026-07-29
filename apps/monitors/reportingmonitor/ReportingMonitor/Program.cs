using ReportingMonitor.Api;
using Rvt.Monitor.Common.Hosting;

return await MonitorHost.RunAsync<ReportingMonitorJobDispatcher>(
    args,
    "ReportingMonitor",
    ReportingMonitorJobs.Catalog.JobNames,
    (jobName, services, cancellationToken) => ReportingMonitorJobs.Catalog.RunAsync(jobName, services, cancellationToken),
    app => app.MapReportingMonitorApi(),
    configureServices: (services, configuration) =>
        services.AddReportingMonitor(configuration));
