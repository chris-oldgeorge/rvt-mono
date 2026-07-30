using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Scheduling;

namespace Rvt.Reporting.Core.Reports;

/// <summary>
/// Coordinates report data loading, rendering, storage, notification, and persistence.
/// Major updates: 2026-06-24 introduced ACS/Quartz orchestration and one-time report path; added optional customer-logo rendering handoff; 2026-06-25 added executive insight narrative hydration; 2026-06-29 moved generated-report metadata writes behind an atomic repository transaction.
/// </summary>
[method: SuppressMessage(
    "Maintainability",
    "S107:Methods should not have too many parameters",
    Justification = "Constructor injection keeps report workflow ports explicit and independently replaceable.")]
public sealed class ReportGenerationService(
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
    ILogger<ReportGenerationService> logger) : IReportGenerationService
{
    private readonly IReportingRuleQueries _ruleQueries = ruleQueries;
    private readonly IReportingDataQueries _dataQueries = dataQueries;
    private readonly IReportingGenerationLocks _generationLocks = generationLocks;
    private readonly IReportingGenerationCommands _generationCommands = generationCommands;
    private readonly IReportPdfRenderer _renderer = renderer;
    private readonly IReportStorage _storage = storage;
    private readonly IReportMessageSender _messageSender = messageSender;
    private readonly ICustomerLogoProvider _customerLogoProvider = customerLogoProvider;
    private readonly IReportNarrativeProvider _narrativeProvider = narrativeProvider;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<ReportGenerationService> _logger = logger;

    public async Task<IReadOnlyList<GeneratedReport>> GenerateScheduledReportsAsync(DateTimeOffset triggerUtc, CancellationToken cancellationToken)
    {
        IReadOnlyList<ReportRule> dueRules = await _ruleQueries.GetDueReportRulesAsync(triggerUtc.Date, cancellationToken).ConfigureAwait(false);
        List<GeneratedReport> generatedReports = [];
        // Per-rule failures used to be logged only, so the Quartz job completed
        // successfully and nobody was told a report had not been produced.
        // Each rule stays an independent unit, but the run now fails visibly.
        List<Exception> failures = [];

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
                failures.Add(new InvalidOperationException(
                    $"Scheduled report generation failed for rule {rule.Id}.",
                    exception));
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "One or more scheduled report rules failed to generate.",
                failures);
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

        IReadOnlyList<ReportPeriod> periods = ReportPeriodCalculator.CreatePeriods(rule, triggerUtc);
        if (periods.Count == 0)
        {
            return [];
        }

        // Backfilled periods must be idempotent: the advisory generation lock
        // only serialises concurrent runs, it does not record that a period is
        // already done, so an existing report for the same rule/frequency/start
        // is the only thing that can say so.
        HashSet<(FrequencyType Frequency, DateTimeOffset StartUtc)> alreadyGenerated =
            [.. (await _ruleQueries.GetGeneratedPeriodsAsync(
                    rule.Id,
                    periods.Min(period => period.StartUtc),
                    cancellationToken).ConfigureAwait(false))
                .Select(generated => (generated.Frequency, generated.PeriodStartUtc))];

        List<GeneratedReport> generatedReports = [];
        foreach (ReportPeriod period in periods)
        {
            if (alreadyGenerated.Contains((period.Frequency, period.StartUtc)))
            {
                continue;
            }

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

public sealed class OneTimeReportValidationException(IReadOnlyList<ValidationError> errors) : ArgumentException(string.Join("; ", errors.Select(static error => $"{error.Field}: {error.Message}")))
{
    public IReadOnlyList<ValidationError> Errors { get; } = errors;
}
