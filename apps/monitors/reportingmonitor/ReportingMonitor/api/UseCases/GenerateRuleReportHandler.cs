using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;

namespace ReportingMonitor.Api.UseCases;

public sealed class GenerateRuleReportHandler(IReportGenerationService service)
{
    public Task<IReadOnlyList<GeneratedReport>> HandleAsync(
        Guid reportRuleId,
        DateTimeOffset triggerUtc,
        CancellationToken cancellationToken) =>
        service.GenerateRuleAsync(reportRuleId, triggerUtc, cancellationToken);
}
