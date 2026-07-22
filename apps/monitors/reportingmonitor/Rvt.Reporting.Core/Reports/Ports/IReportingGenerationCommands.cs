using Rvt.Reporting.Core.Models;

namespace Rvt.Reporting.Core.Reports;

public interface IReportingGenerationCommands
{
    Task<GeneratedReport> SaveGeneratedReportAsync(GeneratedReportSaveRequest request, CancellationToken cancellationToken);
}
