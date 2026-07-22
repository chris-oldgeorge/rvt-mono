using Rvt.Reporting.Core.Reports;

namespace ReportingMonitor.Api.UseCases;

public sealed class GenerateOneTimeReportHandler(IReportGenerationService service)
{
    public Task<OneTimeReportResponse> HandleAsync(OneTimeReportRequest request, CancellationToken cancellationToken) =>
        service.GenerateOneTimeReportAsync(request, cancellationToken);
}
