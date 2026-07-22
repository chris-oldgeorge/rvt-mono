using Rvt.Reporting.Core.Scheduling;

namespace Rvt.Reporting.Core.Reports;

public interface IReportingGenerationLocks
{
    Task<RuleGenerationLock?> TryAcquireAsync(Guid reportRuleId, ReportPeriod period, CancellationToken cancellationToken);
}
