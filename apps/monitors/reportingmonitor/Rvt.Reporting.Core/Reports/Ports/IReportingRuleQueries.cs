using Rvt.Reporting.Core.Models;

namespace Rvt.Reporting.Core.Reports;

public interface IReportingRuleQueries
{
    Task<IReadOnlyList<ReportRule>> GetDueReportRulesAsync(DateTimeOffset maxLastGeneratedUtc, CancellationToken cancellationToken);

    Task<ReportRule?> GetReportRuleAsync(Guid reportRuleId, CancellationToken cancellationToken);
}
