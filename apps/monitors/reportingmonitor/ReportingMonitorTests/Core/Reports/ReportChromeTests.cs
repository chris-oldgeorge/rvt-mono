using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Pdf.Documents;

namespace Rvt.Reporting.Core.Tests.Reports;

/// <summary>
/// Verifies PDF header/footer chrome text for customer report presentation.
/// Major updates: 2026-06-25 added ID10 header/footer review coverage; added ID15-ID17 report insight render smoke coverage.
/// </summary>
public sealed class ReportChromeTests
{
    [Fact]
    public void BuildReportChrome_KeepsHeaderMinimalAndMovesReportDateToBody()
    {
        var generatedAtUtc = new DateTimeOffset(2026, 6, 25, 7, 30, 0, TimeSpan.Zero);
        var site = new SiteReportData
        {
            SiteName = "North Site",
            AddressLine1 = "1 Boundary Road",
            City = "London",
            Postcode = "N1 1AA"
        };

        var chrome = QuestPdfReportRenderer.BuildReportChrome(
            "Weekly North Boundary",
            generatedAtUtc,
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 7, 23, 59, 59, TimeSpan.Zero),
            site);

        Assert.Equal("Report date: 2026-06-25 07:30 UTC", chrome.BodyReportDateText);
        Assert.DoesNotContain("Weekly North Boundary", chrome.HeaderText);
        Assert.DoesNotContain(site.SiteAddress, chrome.HeaderText);
        Assert.DoesNotContain("2026-06-01", chrome.HeaderText);
        Assert.DoesNotContain("Weekly North Boundary", chrome.FooterText);
        Assert.DoesNotContain("2026-06-25", chrome.FooterText);
    }

    [Fact]
    public async Task RenderAsync_GeneratesPdfWithHeaderLogoAsset()
    {
        var generatedAtUtc = new DateTimeOffset(2026, 6, 25, 7, 30, 0, TimeSpan.Zero);
        var site = new SiteReportData
        {
            Id = Guid.NewGuid(),
            SiteName = "North Site",
            CompanyName = "RVT",
            Contracts = "Boundary"
        };

        var renderer = new QuestPdfReportRenderer();
        var report = await renderer.RenderAsync(
            "Weekly North Boundary",
            generatedAtUtc,
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 7, 23, 59, 59, TimeSpan.Zero),
            site,
            customerLogo: null,
            CancellationToken.None);

        Assert.Equal("application/pdf", report.ContentType);
        Assert.True(report.Content.Length > 4);
        Assert.True(report.Content.AsSpan(0, 4).SequenceEqual("%PDF"u8));
    }

    [Fact]
    public async Task RenderAsync_GeneratesPdfWithExecutiveSummaryClosedNotesAndHeatmapData()
    {
        var generatedAtUtc = new DateTimeOffset(2026, 6, 25, 7, 30, 0, TimeSpan.Zero);
        var sampleTime = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        var site = new SiteReportData
        {
            Id = Guid.NewGuid(),
            SiteName = "North Site",
            Monitors =
            [
                new MonitorReportData
                {
                    Id = Guid.NewGuid(),
                    SerialId = "D1",
                    FleetNumber = "Dust 1",
                    TypeOfMonitor = MonitorType.Dust,
                    AlertRules =
                    [
                        new AlertRuleData(AlertType.Alert, "PM10", 150m, 3600, "ug/m3", "PM10", 1, "Closed after review")
                    ],
                    Notifications =
                    [
                        new NotificationData(AlertType.Alert, sampleTime, "PM10", 150m, 160m, 3600, sampleTime.AddHours(1), "Closed after review", null)
                    ],
                    DustHourlyAverage =
                    [
                        new MeasurementPoint(sampleTime, 120m),
                        new MeasurementPoint(sampleTime.AddHours(1), 160m)
                    ]
                }
            ]
        };

        var renderer = new QuestPdfReportRenderer();
        var report = await renderer.RenderAsync("Daily North", generatedAtUtc, sampleTime.AddDays(-1), sampleTime.AddDays(1), site, null, CancellationToken.None);

        Assert.Equal("application/pdf", report.ContentType);
        Assert.True(report.Content.Length > 4);
        Assert.True(report.Content.AsSpan(0, 4).SequenceEqual("%PDF"u8));
    }
}
