using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;

namespace ReportingMonitor.Api.UseCases;

public sealed class GenerateScheduledReportsHandler(IReportGenerationService service)
{
    public Task<IReadOnlyList<GeneratedReport>> HandleAsync(DateTimeOffset triggerUtc, CancellationToken cancellationToken) =>
        service.GenerateScheduledReportsAsync(triggerUtc, cancellationToken);
}
