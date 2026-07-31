using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using ReportingMonitor.Api.Db;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;

namespace ReportingMonitorTests;

/// <summary>
/// Two reporting containers generating the same rule at the same time, against a real
/// PostgreSQL database and the real advisory generation lock. Quartz runs on
/// UseInMemoryStore, so nothing coordinates the two schedulers and this is what a
/// deployment with more than one replica does every night.
/// Major updates: 2026-07-31 added with the in-lock idempotency re-check.
/// </summary>
[Trait("Category", "PostgreSqlIntegration")]
public sealed class TestConcurrentReportGeneration(ReportingDbFixture fixture) : IClassFixture<ReportingDbFixture>
{
    private static readonly DateTimeOffset _triggerUtc = new(2026, 7, 14, 8, 0, 0, TimeSpan.Zero);

    private ReportingDbFixture Fixture { get; } = fixture;

    /// <summary>
    /// One run is deliberately slower than the other, which is the interleaving that
    /// makes the defect reachable rather than merely possible: the quick run finishes
    /// the later periods and drops their locks while the slow run is still rendering
    /// the first, so the slow run then takes each of those locks cleanly holding a
    /// snapshot taken before any of them existed. Recipients are what a stale answer
    /// costs, so the emails are counted as well as the rows.
    /// </summary>
    [Fact]
    public async Task GenerateScheduledReportsAsync_TwoConcurrentRuns_ProduceOneReportAndOneEmailPerPeriod()
    {
        await Fixture.ResetAsync();
        await Fixture.SeedBackfillDueDailyRuleAsync(_triggerUtc.AddDays(-3));
        RecordingSender sender = new();
        StartGate gate = new(participants: 2);
        ReportGenerationService quickRun = CreateService(Fixture.Client, sender, gate, TimeSpan.FromMilliseconds(5));
        ReportGenerationService slowRun = CreateService(Fixture.SecondClient, sender, gate, TimeSpan.FromMilliseconds(250));

        IReadOnlyList<GeneratedReport>[] runs = await Task.WhenAll(
            quickRun.GenerateScheduledReportsAsync(_triggerUtc, CancellationToken.None),
            slowRun.GenerateScheduledReportsAsync(_triggerUtc, CancellationToken.None));

        DateTimeOffset[] expectedPeriodStarts =
        [
            new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero)
        ];
        IReadOnlyList<(int Frequency, DateTimeOffset PeriodStartUtc)> periods = await Fixture.GetReportPeriodsAsync();

        // Every period was covered exactly once. Both halves matter: a duplicate is the
        // defect, and a missing period would be the fix overreaching into a dropped report.
        Assert.Equal<IEnumerable<DateTimeOffset>>(expectedPeriodStarts, [.. periods.Select(period => period.PeriodStartUtc)]);
        Assert.All(periods, period => Assert.Equal((int)FrequencyType.Daily, period.Frequency));
        Assert.Equal(expectedPeriodStarts.Length, runs.Sum(reports => reports.Count));
        Assert.Equal(expectedPeriodStarts.Length, await Fixture.CountAsync("report_sent"));

        // One recipient, so one email per period and no more.
        Assert.Equal<IEnumerable<string>>(
            [.. expectedPeriodStarts.Select(PeriodTag)],
            [.. sender.SentPeriodTags.Order(StringComparer.Ordinal)]);
        // Both runs did real work, so the assertions above are about a race that
        // happened rather than one run finding nothing to do.
        Assert.All(runs, reports => Assert.NotEmpty(reports));
    }

    private static ReportGenerationService CreateService(
        ReportingDbClient client,
        RecordingSender sender,
        StartGate gate,
        TimeSpan renderDuration) => new(
        client,
        client,
        client,
        client,
        new SlowRenderer(renderDuration),
        new FakeStorage(),
        sender,
        new GatedLogoProvider(gate),
        new FakeNarrativeProvider(),
        TimeProvider.System,
        NullLogger<ReportGenerationService>.Instance);

    private static string PeriodTag(DateTimeOffset periodStartUtc) =>
        periodStartUtc.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Releases once every participant has arrived. Both runs fetch their due rules and
    /// take their pre-loop snapshot before the first logo fetch, so holding them here
    /// means neither can have committed a report the other's snapshot then misses by
    /// accident - the stale snapshot the test needs is guaranteed, not hoped for.
    /// </summary>
    private sealed class StartGate(int participants)
    {
        private readonly TaskCompletionSource _opened = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly int _participants = participants;
        private int _arrived;

        public Task WaitAsync()
        {
            if (Interlocked.Increment(ref _arrived) == _participants)
            {
                _opened.TrySetResult();
            }

            return _opened.Task;
        }
    }

    private sealed class GatedLogoProvider(StartGate gate) : ICustomerLogoProvider
    {
        private readonly StartGate _gate = gate;
        private int _fetched;

        public async Task<CustomerLogo?> GetSiteLogoAsync(Guid siteId, CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _fetched, 1) == 0)
            {
                await _gate.WaitAsync();
            }

            return new CustomerLogo([9, 8, 7], "image/png");
        }
    }

    /// <summary>
    /// Stands in for the seconds a real period costs - PDF render, blob write, narrative,
    /// an SMTP round per recipient - which is what widened this race from microseconds.
    /// </summary>
    private sealed class SlowRenderer(TimeSpan duration) : IReportPdfRenderer
    {
        private readonly TimeSpan _duration = duration;

        public async Task<RenderedReport> RenderAsync(
            string? reportName,
            DateTimeOffset generatedAtUtc,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            SiteReportData site,
            CustomerLogo? customerLogo,
            CancellationToken cancellationToken)
        {
            await Task.Delay(_duration, cancellationToken);
            return new RenderedReport($"report-{PeriodTag(fromUtc)}.pdf", "application/pdf", [1, 2, 3]);
        }
    }

    /// <summary>
    /// Shared by both runs, because the question is how many emails the recipient got,
    /// not how many either run believes it sent.
    /// </summary>
    private sealed class RecordingSender : IReportMessageSender
    {
        private readonly List<string> _sentPeriodTags = [];

        public IReadOnlyList<string> SentPeriodTags
        {
            get
            {
                lock (_sentPeriodTags)
                {
                    return [.. _sentPeriodTags];
                }
            }
        }

        public Task<ReportSendResult> SendAsync(
            string recipientEmail,
            string sitePostcode,
            RenderedReport report,
            CancellationToken cancellationToken)
        {
            lock (_sentPeriodTags)
            {
                _sentPeriodTags.Add(report.FileName["report-".Length..^".pdf".Length]);
            }

            return Task.FromResult(new ReportSendResult(true, "Sent ok"));
        }
    }

    private sealed class FakeStorage : IReportStorage
    {
        public Task<Uri> StoreAsync(RenderedReport report, CancellationToken cancellationToken) =>
            Task.FromResult(new Uri($"https://storage.example.test/{report.FileName}"));
    }

    private sealed class FakeNarrativeProvider : IReportNarrativeProvider
    {
        public Task<string> CreateNarrativeAsync(ReportNarrativeContext context, CancellationToken cancellationToken) =>
            Task.FromResult("Narrative from fake provider");
    }
}
