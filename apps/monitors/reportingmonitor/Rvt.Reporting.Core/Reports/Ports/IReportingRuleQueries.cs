using Rvt.Reporting.Core.Models;

namespace Rvt.Reporting.Core.Reports;

public interface IReportingRuleQueries
{
    Task<IReadOnlyList<ReportRule>> GetDueReportRulesAsync(DateTimeOffset maxLastGeneratedUtc, CancellationToken cancellationToken);

    Task<ReportRule?> GetReportRuleAsync(Guid reportRuleId, CancellationToken cancellationToken);

    /// <summary>
    /// The periods this rule has already produced a report for, on or after
    /// <paramref name="fromUtc"/>. Backfilling missed periods must not
    /// regenerate one that already exists: the advisory generation lock only
    /// serialises concurrent runs, it does not record that a period is done.
    /// </summary>
    Task<IReadOnlyList<GeneratedReportPeriod>> GetGeneratedPeriodsAsync(
        Guid reportRuleId,
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken);
}

public sealed record GeneratedReportPeriod(FrequencyType Frequency, DateTimeOffset PeriodStartUtc);
