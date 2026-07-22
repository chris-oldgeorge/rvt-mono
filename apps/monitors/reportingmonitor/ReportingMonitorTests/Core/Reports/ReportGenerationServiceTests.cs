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
        var rules = new FakeRuleQueries();
        var data = new FakeDataQueries();
        var locks = new FakeGenerationLocks();
        var commands = new FakeGenerationCommands();
        var service = CreateService(rules, data, locks, commands);
        var request = new OneTimeReportRequest
        {
            SiteId = data.Site.Id,
            RequestedByUserId = Guid.NewGuid(),
            FromUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ToUtc = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero),
            RecipientEmails = ["ops@example.com"]
        };

        var response = await service.GenerateOneTimeReportAsync(request, CancellationToken.None);

        Assert.Equal(commands.OneTimeRuleId, response.ReportRuleId);
        Assert.Single(commands.InsertedReports);
        Assert.Single(commands.SentRows);
        var saveRequest = Assert.Single(commands.SaveRequests);
        Assert.Null(saveRequest.ReportRuleId);
        Assert.NotNull(saveRequest.OneTimeReportRule);
        Assert.Equal(request.RequestedByUserId, saveRequest.OneTimeReportRule.RequestedByUserId);
        Assert.Equal(request.ReportName, saveRequest.OneTimeReportRule.ReportName);
        Assert.Equal(FrequencyType.OneTime, saveRequest.Frequency);
        Assert.False(saveRequest.UpdateLastGenerated);
        var delivery = Assert.Single(saveRequest.Deliveries);
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
        var ruleId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var rules = new FakeRuleQueries
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
        var data = new FakeDataQueries();
        var locks = new FakeGenerationLocks();
        var commands = new FakeGenerationCommands();
        var service = CreateService(rules, data, locks, commands);
        var triggerUtc = new DateTimeOffset(2026, 6, 30, 8, 15, 0, TimeSpan.Zero);

        var reports = await service.GenerateRuleAsync(ruleId, triggerUtc, CancellationToken.None);

        Assert.Single(reports);
        var saveRequest = Assert.Single(commands.SaveRequests);
        Assert.Equal(siteId, saveRequest.SiteId);
        Assert.Equal(ruleId, saveRequest.ReportRuleId);
        Assert.Null(saveRequest.OneTimeReportRule);
        Assert.Equal(FrequencyType.Daily, saveRequest.Frequency);
        Assert.True(saveRequest.UpdateLastGenerated);
        Assert.Equal(new DateTimeOffset(2026, 6, 29, 0, 0, 0, TimeSpan.Zero), saveRequest.PeriodStartUtc);
        Assert.Equal(new DateTimeOffset(2026, 6, 29, 23, 59, 59, 999, TimeSpan.Zero), saveRequest.PeriodEndUtc);
        var delivery = Assert.Single(saveRequest.Deliveries);
        Assert.Equal("daily@example.com", delivery.RecipientEmail);
        Assert.Null(delivery.ErrorMessage);
        Assert.Equal(1, commands.LastGeneratedUpdates);
    }

    [Fact]
    public async Task GenerateOneTimeReportAsync_PersistsThrownFailureAndContinuesRemainingRecipients()
    {
        var rules = new FakeRuleQueries();
        var data = new FakeDataQueries();
        var locks = new FakeGenerationLocks();
        var commands = new FakeGenerationCommands();
        var sender = new ThrowingThenSuccessfulSender();
        var service = CreateService(rules, data, locks, commands, sender);
        var request = new OneTimeReportRequest
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
        var deliveries = Assert.Single(commands.SaveRequests).Deliveries;
        Assert.Equal("Delivery provider threw InvalidOperationException.", deliveries[0].ErrorMessage);
        Assert.Null(deliveries[1].ErrorMessage);
    }

    [Fact]
    public async Task GenerateOneTimeReportAsync_BoundsReturnedDeliveryFailure()
    {
        var rules = new FakeRuleQueries();
        var data = new FakeDataQueries();
        var commands = new FakeGenerationCommands();
        var service = CreateService(
            rules,
            data,
            new FakeGenerationLocks(),
            commands,
            new FailedSender(new string('x', 1200)));
        var request = new OneTimeReportRequest
        {
            SiteId = data.Site.Id,
            RequestedByUserId = Guid.NewGuid(),
            FromUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ToUtc = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero),
            RecipientEmails = ["failed@example.com"]
        };

        await service.GenerateOneTimeReportAsync(request, CancellationToken.None);

        var error = Assert.Single(Assert.Single(commands.SaveRequests).Deliveries).ErrorMessage;
        Assert.NotNull(error);
        Assert.Equal(1024, error.Length);
    }

    [Fact]
    public async Task GenerateOneTimeReportAsync_PropagatesRequestedDeliveryCancellation()
    {
        var data = new FakeDataQueries();
        var commands = new FakeGenerationCommands();
        var service = CreateService(
            new FakeRuleQueries(),
            data,
            new FakeGenerationLocks(),
            commands,
            new CancellingSender());
        var request = new OneTimeReportRequest
        {
            SiteId = data.Site.Id,
            RequestedByUserId = Guid.NewGuid(),
            FromUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ToUtc = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero),
            RecipientEmails = ["cancelled@example.com"]
        };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GenerateOneTimeReportAsync(request, cancellation.Token));

        Assert.Empty(commands.SaveRequests);
    }

    [Fact]
    public async Task GenerateRuleAsync_UsesSeparateRuleDataLockAndCommandPorts()
    {
        var site = Site();
        var rules = new FakeRuleQueries { Rule = DailyRule(site.Id) };
        var data = new FakeDataQueries { Site = site };
        var locks = new FakeGenerationLocks();
        var commands = new FakeGenerationCommands();
        var service = CreateService(rules, data, locks, commands);

        await service.GenerateRuleAsync(rules.Rule!.Id, new DateTimeOffset(2026, 6, 30, 8, 0, 0, TimeSpan.Zero), CancellationToken.None);

        Assert.Equal(rules.Rule.Id, Assert.Single(rules.RequestedRuleIds));
        Assert.Single(data.Requests);
        Assert.Single(locks.Requests);
        Assert.Single(commands.SaveRequests);
    }

    [Fact]
    public async Task GenerateScheduledReportsAsync_ContinuesAfterRuleFailure()
    {
        var failedSiteId = Guid.NewGuid();
        var successfulSiteId = Guid.NewGuid();
        var failedRule = DailyRule(failedSiteId);
        var successfulRule = DailyRule(successfulSiteId);
        var rules = new FakeRuleQueries { DueRules = [failedRule, successfulRule] };
        var data = new FakeDataQueries();
        data.FailingSiteIds.Add(failedSiteId);
        var commands = new FakeGenerationCommands();
        var service = CreateService(rules, data, new FakeGenerationLocks(), commands);

        var reports = await service.GenerateScheduledReportsAsync(
            new DateTimeOffset(2026, 6, 30, 8, 0, 0, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Single(reports);
        Assert.Equal(successfulRule.Id, Assert.Single(commands.SaveRequests).ReportRuleId);
    }

    [Fact]
    public async Task GenerateScheduledReportsAsync_PropagatesRequestedCancellation()
    {
        var cancelledSiteId = Guid.NewGuid();
        var rules = new FakeRuleQueries { DueRules = [DailyRule(cancelledSiteId)] };
        var data = new FakeDataQueries();
        data.CancelledSiteIds.Add(cancelledSiteId);
        var service = CreateService(
            rules,
            data,
            new FakeGenerationLocks(),
            new FakeGenerationCommands());
        using var cancellation = new CancellationTokenSource();
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
            var reportRuleId = request.ReportRuleId ?? OneTimeRuleId;
            if (request.OneTimeReportRule is not null)
            {
                HiddenRuleUpserts++;
            }

            var report = new GeneratedReport(Guid.NewGuid(), reportRuleId, request.ReportUri, request.PeriodStartUtc, request.PeriodEndUtc);
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
