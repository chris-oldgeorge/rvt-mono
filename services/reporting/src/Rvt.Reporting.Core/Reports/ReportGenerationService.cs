using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Scheduling;

namespace Rvt.Reporting.Core.Reports;

/// <summary>
/// Coordinates report data loading, rendering, storage, notification, and persistence.
/// Major updates: 2026-06-24 introduced ACS/Quartz orchestration and one-time report path; added optional customer-logo rendering handoff; 2026-06-25 added executive insight narrative hydration.
/// </summary>
public sealed class ReportGenerationService : IReportGenerationService
{
    private readonly IReportingRepository _repository;
    private readonly IReportPdfRenderer _renderer;
    private readonly IReportStorage _storage;
    private readonly IReportMessageSender _messageSender;
    private readonly ICustomerLogoProvider _customerLogoProvider;
    private readonly IReportNarrativeProvider _narrativeProvider;
    private readonly TimeProvider _timeProvider;

    public ReportGenerationService(
        IReportingRepository repository,
        IReportPdfRenderer renderer,
        IReportStorage storage,
        IReportMessageSender messageSender,
        ICustomerLogoProvider customerLogoProvider,
        IReportNarrativeProvider narrativeProvider,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _renderer = renderer;
        _storage = storage;
        _messageSender = messageSender;
        _customerLogoProvider = customerLogoProvider;
        _narrativeProvider = narrativeProvider;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<GeneratedReport>> GenerateScheduledReportsAsync(DateTimeOffset triggerUtc, CancellationToken cancellationToken)
    {
        var dueRules = await _repository.GetDueReportRulesAsync(triggerUtc.Date, cancellationToken).ConfigureAwait(false);
        var generatedReports = new List<GeneratedReport>();

        foreach (var rule in dueRules)
        {
            generatedReports.AddRange(await GeneratePeriodsForRuleAsync(rule, triggerUtc, updateLastGenerated: true, cancellationToken).ConfigureAwait(false));
        }

        return generatedReports;
    }

    public async Task<IReadOnlyList<GeneratedReport>> GenerateRuleAsync(Guid reportRuleId, DateTimeOffset triggerUtc, CancellationToken cancellationToken)
    {
        var rule = await _repository.GetReportRuleAsync(reportRuleId, cancellationToken).ConfigureAwait(false);
        if (rule is null)
        {
            return [];
        }

        return await GeneratePeriodsForRuleAsync(rule, triggerUtc, updateLastGenerated: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OneTimeReportResponse> GenerateOneTimeReportAsync(OneTimeReportRequest request, CancellationToken cancellationToken)
    {
        var errors = OneTimeReportValidator.Validate(request);
        if (errors.Count > 0)
        {
            throw new OneTimeReportValidationException(errors);
        }

        var reportRuleId = await _repository.GetOrCreateOneTimeReportRuleAsync(
            request.SiteId,
            request.RequestedByUserId,
            request.ReportName,
            cancellationToken).ConfigureAwait(false);

        var site = await LoadSiteWithInsightsAsync(request.SiteId, request.FromUtc, request.ToUtc, cancellationToken).ConfigureAwait(false);
        var customerLogo = await _customerLogoProvider.GetSiteLogoAsync(site.Id, cancellationToken).ConfigureAwait(false);
        var generatedAtUtc = _timeProvider.GetUtcNow();
        var rendered = await _renderer.RenderAsync(request.ReportName, generatedAtUtc, request.FromUtc, request.ToUtc, site, customerLogo, cancellationToken).ConfigureAwait(false);
        var reportUri = await _storage.StoreAsync(rendered, cancellationToken).ConfigureAwait(false);

        var report = await _repository.InsertReportAsync(
            request.SiteId,
            reportRuleId,
            FrequencyType.OneTime,
            generatedAtUtc,
            request.FromUtc,
            request.ToUtc,
            reportUri,
            cancellationToken).ConfigureAwait(false);

        foreach (var recipientEmail in request.RecipientEmails)
        {
            var sentAtUtc = _timeProvider.GetUtcNow();
            var sendResult = await _messageSender.SendAsync(recipientEmail, site.Postcode ?? string.Empty, rendered, cancellationToken).ConfigureAwait(false);
            await _repository.InsertReportSentAsync(report.ReportId, sentAtUtc, recipientEmail, sendResult.StatusMessage, cancellationToken).ConfigureAwait(false);
        }

        return new OneTimeReportResponse(report.ReportId, reportRuleId, reportUri, request.FromUtc, request.ToUtc);
    }

    private async Task<IReadOnlyList<GeneratedReport>> GeneratePeriodsForRuleAsync(
        ReportRule rule,
        DateTimeOffset triggerUtc,
        bool updateLastGenerated,
        CancellationToken cancellationToken)
    {
        if (rule.IsHiddenSystemRule || rule.Frequency is FrequencyType.Off or FrequencyType.OneTime)
        {
            return [];
        }

        var generatedReports = new List<GeneratedReport>();
        foreach (var period in ReportPeriodCalculator.CreatePeriods(rule, triggerUtc))
        {
            await using var generationLock = await _repository.TryAcquireGenerationLockAsync(rule.Id, period, cancellationToken).ConfigureAwait(false);
            if (generationLock is null)
            {
                continue;
            }

            var site = await LoadSiteWithInsightsAsync(rule.SiteId, period.StartUtc, period.EndUtc, cancellationToken).ConfigureAwait(false);
            var customerLogo = await _customerLogoProvider.GetSiteLogoAsync(site.Id, cancellationToken).ConfigureAwait(false);
            var generatedAtUtc = _timeProvider.GetUtcNow();
            var rendered = await _renderer.RenderAsync(rule.ReportName, generatedAtUtc, period.StartUtc, period.EndUtc, site, customerLogo, cancellationToken).ConfigureAwait(false);
            var reportUri = await _storage.StoreAsync(rendered, cancellationToken).ConfigureAwait(false);

            var report = await _repository.InsertReportAsync(
                rule.SiteId,
                rule.Id,
                period.Frequency,
                generatedAtUtc,
                period.StartUtc,
                period.EndUtc,
                reportUri,
                cancellationToken).ConfigureAwait(false);

            foreach (var recipientEmail in rule.RecipientEmails)
            {
                var sendResult = await _messageSender.SendAsync(recipientEmail, site.Postcode ?? string.Empty, rendered, cancellationToken).ConfigureAwait(false);
                await _repository.InsertReportSentAsync(report.ReportId, _timeProvider.GetUtcNow(), recipientEmail, sendResult.StatusMessage, cancellationToken).ConfigureAwait(false);
            }

            if (updateLastGenerated)
            {
                await _repository.UpdateReportRuleLastGeneratedAsync(rule.Id, generatedAtUtc, cancellationToken).ConfigureAwait(false);
            }

            generatedReports.Add(report);
        }

        return generatedReports;
    }

    private async Task<SiteReportData> LoadSiteWithInsightsAsync(Guid siteId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        var site = await _repository.LoadSiteReportDataAsync(siteId, fromUtc, toUtc, cancellationToken).ConfigureAwait(false);
        var summary = ReportInsightBuilder.BuildExecutiveSummary(site, fromUtc, toUtc);
        var heatmaps = ReportInsightBuilder.BuildAlertHeatmaps(site);
        var narrative = await _narrativeProvider.CreateNarrativeAsync(new ReportNarrativeContext(site.SiteName, summary, heatmaps), cancellationToken).ConfigureAwait(false);
        return site with { Insights = new ReportInsights(summary, heatmaps, narrative) };
    }
}

public sealed class OneTimeReportValidationException : ArgumentException
{
    public OneTimeReportValidationException(IReadOnlyList<ValidationError> errors)
        : base(string.Join("; ", errors.Select(static error => $"{error.Field}: {error.Message}")))
    {
        Errors = errors;
    }

    public IReadOnlyList<ValidationError> Errors { get; }
}
