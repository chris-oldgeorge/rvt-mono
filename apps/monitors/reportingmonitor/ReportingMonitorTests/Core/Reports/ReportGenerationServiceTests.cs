using Microsoft.Extensions.Logging.Abstractions;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;
using Rvt.Reporting.Core.Scheduling;

namespace Rvt.Reporting.Core.Tests.Reports;

/// <summary>
/// Verifies orchestration side effects around hidden one-time rules and scheduled rule state.
/// Major updates: 2026-06-24 initial report generation service coverage; covered customer-logo handoff to PDF rendering; 2026-06-25 covered report insight handoff; 2026-06-29 added atomic generated-report save request coverage.
/// </summary>
public sealed class ReportGenerationServiceTests
{
    [Fact]
    public async Task GenerateOneTimeReportAsync_PersistsReportWithHiddenOneTimeRuleWithoutUpdatingLastGenerated()
    {
        FakeRuleQueries rules = new();
        FakeDataQueries data = new();
        FakeGenerationLocks locks = new();
        FakeGenerationCommands commands = new();
        ReportGenerationService service = CreateService(rules, data, locks, commands);
        OneTimeReportRequest request = new()
        {
            SiteId = data.Site.Id,
            RequestedByUserId = Guid.NewGuid(),
            FromUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ToUtc = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero),
            RecipientEmails = ["ops@example.com"]
        };

        OneTimeReportResponse response = await service.GenerateOneTimeReportAsync(request, CancellationToken.None);

        Assert.Equal(commands.OneTimeRuleId, response.ReportRuleId);
        Assert.Single(commands.InsertedReports);
        Assert.Single(commands.SentRows);
        GeneratedReportSaveRequest saveRequest = Assert.Single(commands.SaveRequests);
        Assert.Null(saveRequest.ReportRuleId);
        Assert.NotNull(saveRequest.OneTimeReportRule);
        Assert.Equal(request.RequestedByUserId, saveRequest.OneTimeReportRule.RequestedByUserId);
        Assert.Equal(request.ReportName, saveRequest.OneTimeReportRule.ReportName);
        Assert.Equal(FrequencyType.OneTime, saveRequest.Frequency);
        Assert.False(saveRequest.UpdateLastGenerated);
        ReportDeliverySaveRequest delivery = Assert.Single(saveRequest.Deliveries);
        Assert.Equal("ops@example.com", delivery.RecipientEmail);
        Assert.Null(delivery.ErrorMessage);
        Assert.Equal(0, commands.LastGeneratedUpdates);
        Assert.NotNull(data.RendererLogo);
        Assert.NotNull(data.RendererInsights);
        Assert.Equal("Narrative from fake provider", data.RendererInsights.Narrative);
    }

    [Fact]
    public async Task GenerateRuleAsync_SavesReportRecipientsAndLastGeneratedInSingleRequest()
    {
        Guid ruleId = Guid.NewGuid();
        Guid siteId = Guid.NewGuid();
        Guid recipientId = Guid.NewGuid();
        FakeRuleQueries rules = new()
        {
            Rule = new ReportRule
            {
                Id = ruleId,
                SiteId = siteId,
                Frequency = FrequencyType.Daily,
                ReportName = "Daily Site Report",
                Recipients = [new ReportRecipient(recipientId, "daily@example.com")]
            }
        };
        FakeDataQueries data = new();
        FakeGenerationLocks locks = new();
        FakeGenerationCommands commands = new();
        ReportGenerationService service = CreateService(rules, data, locks, commands);
        DateTimeOffset triggerUtc = new(2026, 6, 30, 8, 15, 0, TimeSpan.Zero);

        IReadOnlyList<GeneratedReport> reports = await service.GenerateRuleAsync(ruleId, triggerUtc, CancellationToken.None);

        Assert.Single(reports);
        GeneratedReportSaveRequest saveRequest = Assert.Single(commands.SaveRequests);
        Assert.Equal(siteId, saveRequest.SiteId);
        Assert.Equal(ruleId, saveRequest.ReportRuleId);
        Assert.Null(saveRequest.OneTimeReportRule);
        Assert.Equal(FrequencyType.Daily, saveRequest.Frequency);
        Assert.True(saveRequest.UpdateLastGenerated);
        Assert.Equal(new DateTimeOffset(2026, 6, 29, 0, 0, 0, TimeSpan.Zero), saveRequest.PeriodStartUtc);
        Assert.Equal(new DateTimeOffset(2026, 6, 29, 23, 59, 59, 999, TimeSpan.Zero), saveRequest.PeriodEndUtc);
        ReportDeliverySaveRequest delivery = Assert.Single(saveRequest.Deliveries);
        Assert.Equal("daily@example.com", delivery.RecipientEmail);
        Assert.Null(delivery.ErrorMessage);
        Assert.Equal(1, commands.LastGeneratedUpdates);
    }

    [Fact]
    public async Task GenerateOneTimeReportAsync_PersistsThrownFailureAndContinuesRemainingRecipients()
    {
        FakeRuleQueries rules = new();
        FakeDataQueries data = new();
        FakeGenerationLocks locks = new();
        FakeGenerationCommands commands = new();
        ThrowingThenSuccessfulSender sender = new();
        ReportGenerationService service = CreateService(rules, data, locks, commands, sender);
        OneTimeReportRequest request = new()
        {
            SiteId = data.Site.Id,
            RequestedByUserId = Guid.NewGuid(),
            FromUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ToUtc = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero),
            RecipientEmails = ["fails@example.com", "works@example.com"]
        };

        await service.GenerateOneTimeReportAsync(request, CancellationToken.None);

        Assert.Equal(["fails@example.com", "works@example.com"], sender.AttemptedRecipients);
        Assert.Equal(1, commands.HiddenRuleUpserts);
        Assert.Single(commands.InsertedReports);
        IReadOnlyList<ReportDeliverySaveRequest> deliveries = Assert.Single(commands.SaveRequests).Deliveries;
        Assert.Equal("Delivery provider threw InvalidOperationException.", deliveries[0].ErrorMessage);
        Assert.Null(deliveries[1].ErrorMessage);
    }

    [Fact]
    public async Task GenerateOneTimeReportAsync_BoundsReturnedDeliveryFailure()
    {
        FakeRuleQueries rules = new();
        FakeDataQueries data = new();
        FakeGenerationCommands commands = new();
        ReportGenerationService service = CreateService(
            rules,
            data,
            new FakeGenerationLocks(),
            commands,
            new FailedSender(new string('x', 1200)));
        OneTimeReportRequest request = new()
        {
            SiteId = data.Site.Id,
            RequestedByUserId = Guid.NewGuid(),
            FromUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ToUtc = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero),
            RecipientEmails = ["failed@example.com"]
        };

        await service.GenerateOneTimeReportAsync(request, CancellationToken.None);

        string? error = Assert.Single(Assert.Single(commands.SaveRequests).Deliveries).ErrorMessage;
        Assert.NotNull(error);
        Assert.Equal(1024, error.Length);
    }

    [Fact]
    public async Task GenerateOneTimeReportAsync_PropagatesRequestedDeliveryCancellation()
    {
        FakeDataQueries data = new();
        FakeGenerationCommands commands = new();
        ReportGenerationService service = CreateService(
            new FakeRuleQueries(),
            data,
            new FakeGenerationLocks(),
            commands,
            new CancellingSender());
        OneTimeReportRequest request = new()
        {
            SiteId = data.Site.Id,
            RequestedByUserId = Guid.NewGuid(),
            FromUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ToUtc = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero),
            RecipientEmails = ["cancelled@example.com"]
        };
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GenerateOneTimeReportAsync(request, cancellation.Token));

        Assert.Empty(commands.SaveRequests);
    }

    [Fact]
    public async Task GenerateRuleAsync_UsesSeparateRuleDataLockAndCommandPorts()
    {
        SiteReportData site = Site();
        FakeRuleQueries rules = new() { Rule = DailyRule(site.Id) };
        FakeDataQueries data = new() { Site = site };
        FakeGenerationLocks locks = new();
        FakeGenerationCommands commands = new();
        ReportGenerationService service = CreateService(rules, data, locks, commands);

        await service.GenerateRuleAsync(rules.Rule!.Id, new DateTimeOffset(2026, 6, 30, 8, 0, 0, TimeSpan.Zero), CancellationToken.None);

        Assert.Equal(rules.Rule.Id, Assert.Single(rules.RequestedRuleIds));
        Assert.Single(data.Requests);
        Assert.Single(locks.Requests);
        Assert.Single(commands.SaveRequests);
    }

    [Fact]
    public async Task GenerateScheduledReportsAsync_ContinuesAfterRuleFailure()
    {
        Guid failedSiteId = Guid.NewGuid();
        Guid successfulSiteId = Guid.NewGuid();
        ReportRule failedRule = DailyRule(failedSiteId);
        ReportRule successfulRule = DailyRule(successfulSiteId);
        FakeRuleQueries rules = new() { DueRules = [failedRule, successfulRule] };
        FakeDataQueries data = new();
        data.FailingSiteIds.Add(failedSiteId);
        FakeGenerationCommands commands = new();
        ReportGenerationService service = CreateService(rules, data, new FakeGenerationLocks(), commands);

        // Every rule is still attempted, but the run no longer completes
        // successfully while a report silently failed to be produced.
        AggregateException aggregate = await Assert.ThrowsAsync<AggregateException>(() =>
            service.GenerateScheduledReportsAsync(
                new DateTimeOffset(2026, 6, 30, 8, 0, 0, TimeSpan.Zero),
                CancellationToken.None));

        Assert.Single(aggregate.InnerExceptions);
        Assert.Contains(failedRule.Id.ToString(), aggregate.InnerExceptions[0].Message, StringComparison.Ordinal);
        Assert.Equal(successfulRule.Id, Assert.Single(commands.SaveRequests).ReportRuleId);
    }

    [Fact]
    public async Task GenerateScheduledReportsAsync_PropagatesRequestedCancellation()
    {
        Guid cancelledSiteId = Guid.NewGuid();
        FakeRuleQueries rules = new() { DueRules = [DailyRule(cancelledSiteId)] };
        FakeDataQueries data = new();
        data.CancelledSiteIds.Add(cancelledSiteId);
        ReportGenerationService service = CreateService(
            rules,
            data,
            new FakeGenerationLocks(),
            new FakeGenerationCommands());
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GenerateScheduledReportsAsync(
                new DateTimeOffset(2026, 6, 30, 8, 0, 0, TimeSpan.Zero),
                cancellation.Token));
    }

    private static ReportGenerationService CreateService(
        FakeRuleQueries rules,
        FakeDataQueries data,
        FakeGenerationLocks locks,
        FakeGenerationCommands commands,
        IReportMessageSender? sender = null) => new(
        rules,
        data,
        locks,
        commands,
        data.Renderer,
        new FakeStorage(),
        sender ?? new FakeSender(),
        new FakeLogoProvider(),
        new FakeNarrativeProvider(),
        TimeProvider.System,
        NullLogger<ReportGenerationService>.Instance);

    // A run that failed or never happened used to lose its period for good:
    // periods were derived from the trigger day alone and never revisited, so
    // a weekly rule lost the whole week. Periods now resume from LastGenerated.
    [Fact]
    public async Task GenerateScheduledReportsAsync_RegeneratesPeriodsMissedSinceLastGenerated()
    {
        Guid siteId = Guid.NewGuid();
        ReportRule rule = DailyRule(siteId) with
        {
            LastGenerated = new DateTimeOffset(2026, 6, 27, 8, 0, 0, TimeSpan.Zero)
        };
        FakeRuleQueries rules = new() { DueRules = [rule] };
        FakeGenerationCommands commands = new();
        ReportGenerationService service = CreateService(
            rules,
            new FakeDataQueries(),
            new FakeGenerationLocks(),
            commands);

        IReadOnlyList<GeneratedReport> reports = await service.GenerateScheduledReportsAsync(
            new DateTimeOffset(2026, 6, 30, 8, 0, 0, TimeSpan.Zero),
            CancellationToken.None);

        // Trigger days 27, 28, 29 and 30 - the 27th's own period is regenerated
        // because nothing records that it completed, and the three days the
        // job did not run are no longer lost.
        Assert.Equal(4, reports.Count);
        Assert.Equal(
            [
                new DateTimeOffset(2026, 6, 26, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 27, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 28, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 29, 0, 0, 0, TimeSpan.Zero)
            ],
            commands.SaveRequests.Select(request => request.PeriodStartUtc).ToArray());
    }

    [Fact]
    public async Task GenerateScheduledReportsAsync_SkipsPeriodsThatAlreadyHaveAReport()
    {
        Guid siteId = Guid.NewGuid();
        ReportRule rule = DailyRule(siteId) with
        {
            LastGenerated = new DateTimeOffset(2026, 6, 27, 8, 0, 0, TimeSpan.Zero)
        };
        FakeRuleQueries rules = new() { DueRules = [rule] };
        rules.GeneratedPeriods.Add(new GeneratedReportPeriod(
            FrequencyType.Daily,
            new DateTimeOffset(2026, 6, 26, 0, 0, 0, TimeSpan.Zero)));
        rules.GeneratedPeriods.Add(new GeneratedReportPeriod(
            FrequencyType.Daily,
            new DateTimeOffset(2026, 6, 28, 0, 0, 0, TimeSpan.Zero)));
        FakeGenerationCommands commands = new();
        ReportGenerationService service = CreateService(
            rules,
            new FakeDataQueries(),
            new FakeGenerationLocks(),
            commands);

        IReadOnlyList<GeneratedReport> reports = await service.GenerateScheduledReportsAsync(
            new DateTimeOffset(2026, 6, 30, 8, 0, 0, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Equal(2, reports.Count);
        Assert.Equal(
            [
                new DateTimeOffset(2026, 6, 27, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 29, 0, 0, 0, TimeSpan.Zero)
            ],
            commands.SaveRequests.Select(request => request.PeriodStartUtc).ToArray());
    }

    [Fact]
    public async Task GenerateScheduledReportsAsync_BoundsTheBackfillSoALongOutageCannotSpam()
    {
        Guid siteId = Guid.NewGuid();
        ReportRule rule = DailyRule(siteId) with
        {
            LastGenerated = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero)
        };
        FakeRuleQueries rules = new() { DueRules = [rule] };
        FakeGenerationCommands commands = new();
        ReportGenerationService service = CreateService(
            rules,
            new FakeDataQueries(),
            new FakeGenerationLocks(),
            commands);

        IReadOnlyList<GeneratedReport> reports = await service.GenerateScheduledReportsAsync(
            new DateTimeOffset(2026, 6, 30, 8, 0, 0, TimeSpan.Zero),
            CancellationToken.None);

        // Six months of missed daily periods collapse to the most recent four.
        Assert.Equal(ReportPeriodCalculator.MaximumBackfillPeriods, reports.Count);
        Assert.Equal(
            new DateTimeOffset(2026, 6, 29, 0, 0, 0, TimeSpan.Zero),
            commands.SaveRequests[^1].PeriodStartUtc);
    }

    [Fact]
    public async Task GenerateScheduledReportsAsync_NeverGeneratedRule_ProducesOnlyTheCurrentPeriod()
    {
        FakeRuleQueries rules = new() { DueRules = [DailyRule(Guid.NewGuid())] };
        FakeGenerationCommands commands = new();
        ReportGenerationService service = CreateService(
            rules,
            new FakeDataQueries(),
            new FakeGenerationLocks(),
            commands);

        IReadOnlyList<GeneratedReport> reports = await service.GenerateScheduledReportsAsync(
            new DateTimeOffset(2026, 6, 30, 8, 0, 0, TimeSpan.Zero),
            CancellationToken.None);

        // No LastGenerated means no history to invent for a rule that may have
        // been created yesterday.
        Assert.Single(reports);
        Assert.Equal(
            new DateTimeOffset(2026, 6, 29, 0, 0, 0, TimeSpan.Zero),
            Assert.Single(commands.SaveRequests).PeriodStartUtc);
    }

    private static ReportRule DailyRule(Guid siteId) => new()
    {
        Id = Guid.NewGuid(),
        SiteId = siteId,
        Frequency = FrequencyType.Daily,
        ReportName = "Daily Site Report",
        Recipients = [new ReportRecipient(Guid.NewGuid(), "daily@example.com")]
    };

    private static SiteReportData Site() => new()
    {
        Id = Guid.NewGuid(),
        SiteName = "RVT Test Site",
        Postcode = "AB1"
    };

    private sealed class FakeRuleQueries : IReportingRuleQueries
    {
        public List<Guid> RequestedRuleIds { get; } = [];

        public ReportRule? Rule { get; init; }

        public IReadOnlyList<ReportRule> DueRules { get; init; } = [];

        public Task<IReadOnlyList<ReportRule>> GetDueReportRulesAsync(DateTimeOffset maxLastGeneratedUtc, CancellationToken cancellationToken) =>
            Task.FromResult(DueRules.Count > 0
                ? DueRules
                : Rule is null ? [] : (IReadOnlyList<ReportRule>)[Rule]);

        public Task<ReportRule?> GetReportRuleAsync(Guid reportRuleId, CancellationToken cancellationToken)
        {
            RequestedRuleIds.Add(reportRuleId);
            return Task.FromResult(Rule?.Id == reportRuleId ? Rule : null);
        }

        public List<GeneratedReportPeriod> GeneratedPeriods { get; } = [];

        public Task<IReadOnlyList<GeneratedReportPeriod>> GetGeneratedPeriodsAsync(
            Guid reportRuleId,
            DateTimeOffset fromUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GeneratedReportPeriod>>(
                [.. GeneratedPeriods.Where(period => period.PeriodStartUtc >= fromUtc)]);
    }

    private sealed class FakeDataQueries : IReportingDataQueries
    {
        public List<(Guid SiteId, DateTimeOffset FromUtc, DateTimeOffset ToUtc)> Requests { get; } = [];

        public SiteReportData Site { get; init; } = Site();

        public HashSet<Guid> FailingSiteIds { get; } = [];

        public HashSet<Guid> CancelledSiteIds { get; } = [];

        public FakeRenderer Renderer { get; } = new();

        public CustomerLogo? RendererLogo => Renderer.CustomerLogo;

        public ReportInsights? RendererInsights => Renderer.Insights;

        public Task<SiteReportData> LoadSiteReportDataAsync(Guid siteId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
        {
            Requests.Add((siteId, fromUtc, toUtc));
            if (FailingSiteIds.Contains(siteId))
            {
                throw new InvalidOperationException($"Site {siteId} failed to load.");
            }

            if (CancelledSiteIds.Contains(siteId))
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return Task.FromResult(Site with { Id = siteId });
        }
    }

    private sealed class FakeGenerationLocks : IReportingGenerationLocks
    {
        public List<(Guid ReportRuleId, ReportPeriod Period)> Requests { get; } = [];

        public Task<RuleGenerationLock?> TryAcquireAsync(Guid reportRuleId, ReportPeriod period, CancellationToken cancellationToken)
        {
            Requests.Add((reportRuleId, period));
            return Task.FromResult<RuleGenerationLock?>(new RuleGenerationLock(() => ValueTask.CompletedTask));
        }
    }

    private sealed class FakeGenerationCommands : IReportingGenerationCommands
    {
        public Guid OneTimeRuleId { get; } = Guid.NewGuid();

        public List<GeneratedReport> InsertedReports { get; } = [];

        public List<(Guid ReportId, string Email)> SentRows { get; } = [];

        public List<GeneratedReportSaveRequest> SaveRequests { get; } = [];

        public int LastGeneratedUpdates { get; private set; }

        public int HiddenRuleUpserts { get; private set; }

        public Task<GeneratedReport> SaveGeneratedReportAsync(GeneratedReportSaveRequest request, CancellationToken cancellationToken)
        {
            SaveRequests.Add(request);
            Guid reportRuleId = request.ReportRuleId ?? OneTimeRuleId;
            if (request.OneTimeReportRule is not null)
            {
                HiddenRuleUpserts++;
            }

            GeneratedReport report = new(Guid.NewGuid(), reportRuleId, request.ReportUri, request.PeriodStartUtc, request.PeriodEndUtc);
            InsertedReports.Add(report);
            SentRows.AddRange(request.Deliveries.Select(delivery => (report.ReportId, delivery.RecipientEmail)));
            if (request.UpdateLastGenerated)
            {
                LastGeneratedUpdates++;
            }

            return Task.FromResult(report);
        }
    }

    private sealed class FakeRenderer : IReportPdfRenderer
    {
        public CustomerLogo? CustomerLogo { get; private set; }

        public ReportInsights? Insights { get; private set; }

        public Task<RenderedReport> RenderAsync(string? reportName, DateTimeOffset generatedAtUtc, DateTimeOffset fromUtc, DateTimeOffset toUtc, SiteReportData site, CustomerLogo? customerLogo, CancellationToken cancellationToken)
        {
            CustomerLogo = customerLogo;
            Insights = site.Insights;
            return Task.FromResult(new RenderedReport("report.pdf", "application/pdf", [1, 2, 3]));
        }
    }

    private sealed class FakeNarrativeProvider : IReportNarrativeProvider
    {
        public Task<string> CreateNarrativeAsync(ReportNarrativeContext context, CancellationToken cancellationToken) => Task.FromResult("Narrative from fake provider");
    }

    private sealed class FakeLogoProvider : ICustomerLogoProvider
    {
        public Task<CustomerLogo?> GetSiteLogoAsync(Guid siteId, CancellationToken cancellationToken) => Task.FromResult<CustomerLogo?>(new CustomerLogo([9, 8, 7], "image/png"));
    }

    private sealed class FakeStorage : IReportStorage
    {
        public Task<Uri> StoreAsync(RenderedReport report, CancellationToken cancellationToken) => Task.FromResult(new Uri("https://storage.example/report.pdf"));
    }

    private sealed class FakeSender : IReportMessageSender
    {
        public Task<ReportSendResult> SendAsync(string recipientEmail, string sitePostcode, RenderedReport report, CancellationToken cancellationToken) => Task.FromResult(new ReportSendResult(true, "Sent ok"));
    }

    private sealed class ThrowingThenSuccessfulSender : IReportMessageSender
    {
        public List<string> AttemptedRecipients { get; } = [];

        public Task<ReportSendResult> SendAsync(
            string recipientEmail,
            string sitePostcode,
            RenderedReport report,
            CancellationToken cancellationToken)
        {
            AttemptedRecipients.Add(recipientEmail);
            return recipientEmail == "fails@example.com"
                ? throw new InvalidOperationException("Delivery failed before metadata persistence.")
                : Task.FromResult(new ReportSendResult(true, "Sent ok"));
        }
    }

    private sealed class FailedSender(string errorMessage) : IReportMessageSender
    {
        public Task<ReportSendResult> SendAsync(
            string recipientEmail,
            string sitePostcode,
            RenderedReport report,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ReportSendResult(false, errorMessage));
    }

    private sealed class CancellingSender : IReportMessageSender
    {
        public Task<ReportSendResult> SendAsync(
            string recipientEmail,
            string sitePostcode,
            RenderedReport report,
            CancellationToken cancellationToken) =>
            throw new OperationCanceledException(cancellationToken);
    }
}
