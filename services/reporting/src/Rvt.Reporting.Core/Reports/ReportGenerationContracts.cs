using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Scheduling;

namespace Rvt.Reporting.Core.Reports;

/// <summary>
/// Defines the reporting orchestration boundary used by Quartz jobs, internal APIs, and adapters.
/// Major updates: 2026-06-24 initial container-service reporting contracts; added optional customer-logo fetch/render contract; 2026-06-25 added report narrative provider contract.
/// </summary>
public interface IReportGenerationService
{
    Task<IReadOnlyList<GeneratedReport>> GenerateScheduledReportsAsync(DateTimeOffset triggerUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<GeneratedReport>> GenerateRuleAsync(Guid reportRuleId, DateTimeOffset triggerUtc, CancellationToken cancellationToken);

    Task<OneTimeReportResponse> GenerateOneTimeReportAsync(OneTimeReportRequest request, CancellationToken cancellationToken);
}

public interface IReportingRepository
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ReportRule>> GetDueReportRulesAsync(DateTimeOffset maxLastGeneratedUtc, CancellationToken cancellationToken);

    Task<ReportRule?> GetReportRuleAsync(Guid reportRuleId, CancellationToken cancellationToken);

    Task<Guid> GetOrCreateOneTimeReportRuleAsync(Guid siteId, Guid requestedByUserId, string reportName, CancellationToken cancellationToken);

    Task<SiteReportData> LoadSiteReportDataAsync(Guid siteId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);

    Task<RuleGenerationLock?> TryAcquireGenerationLockAsync(Guid reportRuleId, ReportPeriod period, CancellationToken cancellationToken);

    Task<GeneratedReport> InsertReportAsync(Guid siteId, Guid reportRuleId, FrequencyType frequency, DateTimeOffset generatedAtUtc, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, Uri reportUri, CancellationToken cancellationToken);

    Task InsertReportSentAsync(Guid reportId, DateTimeOffset sentAtUtc, string recipientEmail, string statusMessage, CancellationToken cancellationToken);

    Task UpdateReportRuleLastGeneratedAsync(Guid reportRuleId, DateTimeOffset generatedAtUtc, CancellationToken cancellationToken);
}

public interface IReportPdfRenderer
{
    Task<RenderedReport> RenderAsync(string? reportName, DateTimeOffset generatedAtUtc, DateTimeOffset fromUtc, DateTimeOffset toUtc, SiteReportData site, CustomerLogo? customerLogo, CancellationToken cancellationToken);
}

public interface ICustomerLogoProvider
{
    Task<CustomerLogo?> GetSiteLogoAsync(Guid siteId, CancellationToken cancellationToken);
}

public interface IReportNarrativeProvider
{
    Task<string> CreateNarrativeAsync(ReportNarrativeContext context, CancellationToken cancellationToken);
}

public interface IReportStorage
{
    Task<Uri> StoreAsync(RenderedReport report, CancellationToken cancellationToken);
}

public interface IReportMessageSender
{
    Task<ReportSendResult> SendAsync(string recipientEmail, string sitePostcode, RenderedReport report, CancellationToken cancellationToken);
}

public sealed record ReportSendResult(bool Success, string StatusMessage);

public sealed record ReportNarrativeContext(
    string SiteName,
    ReportExecutiveSummary ExecutiveSummary,
    IReadOnlyList<ReportAlertHeatmap> AlertHeatmaps);

public sealed class RuleGenerationLock : IAsyncDisposable
{
    private readonly Func<ValueTask> _releaseAsync;

    public RuleGenerationLock(Func<ValueTask> releaseAsync)
    {
        _releaseAsync = releaseAsync;
    }

    public ValueTask DisposeAsync() => _releaseAsync();
}
