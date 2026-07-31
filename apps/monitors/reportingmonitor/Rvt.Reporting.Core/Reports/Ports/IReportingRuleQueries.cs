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
    /// <remarks>
    /// This is a snapshot, so it is only a fast path that skips locking periods
    /// already known to be done. <see cref="HasGeneratedPeriodAsync"/> is the
    /// authoritative check.
    /// </remarks>
    Task<IReadOnlyList<GeneratedReportPeriod>> GetGeneratedPeriodsAsync(
        Guid reportRuleId,
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether this exact rule, frequency and period start already has a report.
    /// Asked once the generation lock for the period is held, so a run that
    /// waited behind - or arrived after - a competing run observes the report
    /// that run committed instead of acting on a snapshot taken before it.
    /// </summary>
    Task<bool> HasGeneratedPeriodAsync(
        Guid reportRuleId,
        FrequencyType frequency,
        DateTimeOffset periodStartUtc,
        CancellationToken cancellationToken);
}

public sealed record GeneratedReportPeriod(FrequencyType Frequency, DateTimeOffset PeriodStartUtc);
