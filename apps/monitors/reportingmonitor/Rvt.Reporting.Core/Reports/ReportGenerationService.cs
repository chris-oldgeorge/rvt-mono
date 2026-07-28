using Microsoft.Extensions.Logging;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Scheduling;

namespace Rvt.Reporting.Core.Reports;

/// <summary>
/// Coordinates report data loading, rendering, storage, notification, and persistence.
/// Major updates: 2026-06-24 introduced ACS/Quartz orchestration and one-time report path; added optional customer-logo rendering handoff; 2026-06-25 added executive insight narrative hydration; 2026-06-29 moved generated-report metadata writes behind an atomic repository transaction.
/// </summary>
public sealed class ReportGenerationService : IReportGenerationService
{
    private readonly IReportingRuleQueries _ruleQueries;
    private readonly IReportingDataQueries _dataQueries;
    private readonly IReportingGenerationLocks _generationLocks;
    private readonly IReportingGenerationCommands _generationCommands;
    private readonly IReportPdfRenderer _renderer;
    private readonly IReportStorage _storage;
    private readonly IReportMessageSender _messageSender;
    private readonly ICustomerLogoProvider _customerLogoProvider;
    private readonly IReportNarrativeProvider _narrativeProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReportGenerationService> _logger;

    public ReportGenerationService(
        IReportingRuleQueries ruleQueries,
        IReportingDataQueries dataQueries,
        IReportingGenerationLocks generationLocks,
        IReportingGenerationCommands generationCommands,
        IReportPdfRenderer renderer,
        IReportStorage storage,
        IReportMessageSender messageSender,
        ICustomerLogoProvider customerLogoProvider,
        IReportNarrativeProvider narrativeProvider,
        TimeProvider timeProvider,
        ILogger<ReportGenerationService> logger)
    {
        _ruleQueries = ruleQueries;
        _dataQueries = dataQueries;
        _generationLocks = generationLocks;
        _generationCommands = generationCommands;
        _renderer = renderer;
        _storage = storage;
        _messageSender = messageSender;
        _customerLogoProvider = customerLogoProvider;
        _narrativeProvider = narrativeProvider;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GeneratedReport>> GenerateScheduledReportsAsync(DateTimeOffset triggerUtc, CancellationToken cancellationToken)
    {
        IReadOnlyList<ReportRule> dueRules = await _ruleQueries.GetDueReportRulesAsync(triggerUtc.Date, cancellationToken).ConfigureAwait(false);
        List<GeneratedReport> generatedReports = [];

        foreach (ReportRule rule in dueRules)
        {
            try
            {
                generatedReports.AddRange(await GeneratePeriodsForRuleAsync(
                    rule,
                    triggerUtc,
                    updateLastGenerated: true,
                    cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Scheduled report generation failed for rule {ReportRuleId}.",
                    rule.Id);
            }
        }

        return generatedReports;
    }

    public async Task<IReadOnlyList<GeneratedReport>> GenerateRuleAsync(Guid reportRuleId, DateTimeOffset triggerUtc, CancellationToken cancellationToken)
    {
        ReportRule? rule = await _ruleQueries.GetReportRuleAsync(reportRuleId, cancellationToken).ConfigureAwait(false);
        if (rule is null)
        {
            return [];
        }

        return await GeneratePeriodsForRuleAsync(rule, triggerUtc, updateLastGenerated: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OneTimeReportResponse> GenerateOneTimeReportAsync(OneTimeReportRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<ValidationError> errors = OneTimeReportValidator.Validate(request);
        if (errors.Count > 0)
        {
            throw new OneTimeReportValidationException(errors);
        }

        SiteReportData site = await LoadSiteWithInsightsAsync(request.SiteId, request.FromUtc, request.ToUtc, cancellationToken).ConfigureAwait(false);
        CustomerLogo? customerLogo = await _customerLogoProvider.GetSiteLogoAsync(site.Id, cancellationToken).ConfigureAwait(false);
        DateTimeOffset generatedAtUtc = _timeProvider.GetUtcNow();
        RenderedReport rendered = await _renderer.RenderAsync(request.ReportName, generatedAtUtc, request.FromUtc, request.ToUtc, site, customerLogo, cancellationToken).ConfigureAwait(false);
        Uri reportUri = await _storage.StoreAsync(rendered, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ReportDeliverySaveRequest> deliveries = await SendReportsAsync(request.RecipientEmails, site.Postcode, rendered, cancellationToken).ConfigureAwait(false);

        GeneratedReport report = await _generationCommands.SaveGeneratedReportAsync(new GeneratedReportSaveRequest(
            request.SiteId,
            null,
            new OneTimeReportRuleSaveRequest(request.RequestedByUserId, request.ReportName),
            FrequencyType.OneTime,
            generatedAtUtc,
            request.FromUtc,
            request.ToUtc,
            reportUri,
            deliveries,
            UpdateLastGenerated: false), cancellationToken).ConfigureAwait(false);

        return new OneTimeReportResponse(report.ReportId, report.ReportRuleId, reportUri, request.FromUtc, request.ToUtc);
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

        List<GeneratedReport> generatedReports = [];
        foreach (ReportPeriod period in ReportPeriodCalculator.CreatePeriods(rule, triggerUtc))
        {
            await using RuleGenerationLock? generationLock = await _generationLocks.TryAcquireAsync(rule.Id, period, cancellationToken).ConfigureAwait(false);
            if (generationLock is null)
            {
                continue;
            }

            SiteReportData site = await LoadSiteWithInsightsAsync(rule.SiteId, period.StartUtc, period.EndUtc, cancellationToken).ConfigureAwait(false);
            CustomerLogo? customerLogo = await _customerLogoProvider.GetSiteLogoAsync(site.Id, cancellationToken).ConfigureAwait(false);
            DateTimeOffset generatedAtUtc = _timeProvider.GetUtcNow();
            RenderedReport rendered = await _renderer.RenderAsync(rule.ReportName, generatedAtUtc, period.StartUtc, period.EndUtc, site, customerLogo, cancellationToken).ConfigureAwait(false);
            Uri reportUri = await _storage.StoreAsync(rendered, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ReportDeliverySaveRequest> deliveries = await SendReportsAsync(rule.RecipientEmails, site.Postcode, rendered, cancellationToken).ConfigureAwait(false);

            GeneratedReport report = await _generationCommands.SaveGeneratedReportAsync(new GeneratedReportSaveRequest(
                rule.SiteId,
                rule.Id,
                null,
                period.Frequency,
                generatedAtUtc,
                period.StartUtc,
                period.EndUtc,
                reportUri,
                deliveries,
                updateLastGenerated), cancellationToken).ConfigureAwait(false);

            generatedReports.Add(report);
        }

        return generatedReports;
    }

    private async Task<SiteReportData> LoadSiteWithInsightsAsync(Guid siteId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        SiteReportData site = await _dataQueries.LoadSiteReportDataAsync(siteId, fromUtc, toUtc, cancellationToken).ConfigureAwait(false);
        ReportExecutiveSummary summary = ReportInsightBuilder.BuildExecutiveSummary(site, fromUtc, toUtc);
        IReadOnlyList<ReportAlertHeatmap> heatmaps = ReportInsightBuilder.BuildAlertHeatmaps(site);
        string narrative = await _narrativeProvider.CreateNarrativeAsync(new ReportNarrativeContext(site.SiteName, summary, heatmaps), cancellationToken).ConfigureAwait(false);
        return site with { Insights = new ReportInsights(summary, heatmaps, narrative) };
    }

    private async Task<IReadOnlyList<ReportDeliverySaveRequest>> SendReportsAsync(
        IReadOnlyList<string> recipientEmails,
        string? sitePostcode,
        RenderedReport rendered,
        CancellationToken cancellationToken)
    {
        List<ReportDeliverySaveRequest> deliveries = new(recipientEmails.Count);
        foreach (string recipientEmail in recipientEmails)
        {
            DateTimeOffset sentAtUtc = _timeProvider.GetUtcNow();
            try
            {
                ReportSendResult sendResult = await _messageSender.SendAsync(
                    recipientEmail,
                    sitePostcode ?? string.Empty,
                    rendered,
                    cancellationToken).ConfigureAwait(false);
                deliveries.Add(new ReportDeliverySaveRequest(
                    sentAtUtc,
                    recipientEmail,
                    sendResult.Success ? null : BoundedError(sendResult.StatusMessage)));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Report delivery failed for {RecipientEmail}.",
                    recipientEmail);
                deliveries.Add(new ReportDeliverySaveRequest(
                    sentAtUtc,
                    recipientEmail,
                    $"Delivery provider threw {exception.GetType().Name}."));
            }
        }

        return deliveries;
    }

    private static string BoundedError(string? message)
    {
        const int maximumLength = 1024;
        string error = string.IsNullOrWhiteSpace(message) ? "Report delivery failed." : message.Trim();
        return error.Length <= maximumLength ? error : error[..maximumLength];
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
