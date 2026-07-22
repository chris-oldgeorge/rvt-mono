using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;
using Rvt.Reporting.Core.Scheduling;

namespace Rvt.Reporting.Core.Tests.Reports;

/// <summary>
/// Verifies orchestration side effects around hidden one-time rules and scheduled rule state.
/// Major updates: 2026-06-24 initial report generation service coverage; covered customer-logo handoff to PDF rendering; 2026-06-25 covered report insight handoff.
/// </summary>
public sealed class ReportGenerationServiceTests
{
    [Fact]
    public async Task GenerateOneTimeReportAsync_PersistsReportWithHiddenOneTimeRuleWithoutUpdatingLastGenerated()
    {
        var repository = new FakeReportingRepository();
        var service = CreateService(repository);
        var request = new OneTimeReportRequest
        {
            SiteId = repository.SiteId,
            RequestedByUserId = Guid.NewGuid(),
            FromUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ToUtc = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero),
            RecipientEmails = ["ops@example.com"]
        };

        var response = await service.GenerateOneTimeReportAsync(request, CancellationToken.None);

        Assert.Equal(repository.OneTimeRuleId, response.ReportRuleId);
        Assert.Single(repository.InsertedReports);
        Assert.Single(repository.SentRows);
        Assert.Equal(0, repository.LastGeneratedUpdates);
        Assert.NotNull(repository.RendererLogo);
        Assert.NotNull(repository.RendererInsights);
        Assert.Equal("Narrative from fake provider", repository.RendererInsights.Narrative);
    }

    private static ReportGenerationService CreateService(FakeReportingRepository repository) => new(
        repository,
        repository.Renderer,
        new FakeStorage(),
        new FakeSender(),
        new FakeLogoProvider(),
        new FakeNarrativeProvider(),
        TimeProvider.System);

    private sealed class FakeReportingRepository : IReportingRepository
    {
        public Guid SiteId { get; } = Guid.NewGuid();

        public Guid OneTimeRuleId { get; } = Guid.NewGuid();

        public List<GeneratedReport> InsertedReports { get; } = [];

        public List<(Guid ReportId, string Email)> SentRows { get; } = [];

        public int LastGeneratedUpdates { get; private set; }

        public FakeRenderer Renderer { get; } = new();

        public CustomerLogo? RendererLogo => Renderer.CustomerLogo;

        public ReportInsights? RendererInsights => Renderer.Insights;

        public Task<bool> CanConnectAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<IReadOnlyList<ReportRule>> GetDueReportRulesAsync(DateTimeOffset maxLastGeneratedUtc, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReportRule>>([]);

        public Task<ReportRule?> GetReportRuleAsync(Guid reportRuleId, CancellationToken cancellationToken) => Task.FromResult<ReportRule?>(null);

        public Task<Guid> GetOrCreateOneTimeReportRuleAsync(Guid siteId, Guid requestedByUserId, string reportName, CancellationToken cancellationToken) => Task.FromResult(OneTimeRuleId);

        public Task<SiteReportData> LoadSiteReportDataAsync(Guid siteId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) => Task.FromResult(new SiteReportData { Id = siteId, SiteName = "RVT Test Site", Postcode = "AB1" });

        public Task<RuleGenerationLock?> TryAcquireGenerationLockAsync(Guid reportRuleId, ReportPeriod period, CancellationToken cancellationToken) => Task.FromResult<RuleGenerationLock?>(new RuleGenerationLock(() => ValueTask.CompletedTask));

        public Task<GeneratedReport> InsertReportAsync(Guid siteId, Guid reportRuleId, FrequencyType frequency, DateTimeOffset generatedAtUtc, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, Uri reportUri, CancellationToken cancellationToken)
        {
            var report = new GeneratedReport(Guid.NewGuid(), reportRuleId, reportUri, periodStartUtc, periodEndUtc);
            InsertedReports.Add(report);
            return Task.FromResult(report);
        }

        public Task InsertReportSentAsync(Guid reportId, DateTimeOffset sentAtUtc, string recipientEmail, string statusMessage, CancellationToken cancellationToken)
        {
            SentRows.Add((reportId, recipientEmail));
            return Task.CompletedTask;
        }

        public Task UpdateReportRuleLastGeneratedAsync(Guid reportRuleId, DateTimeOffset generatedAtUtc, CancellationToken cancellationToken)
        {
            LastGeneratedUpdates++;
            return Task.CompletedTask;
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
}
