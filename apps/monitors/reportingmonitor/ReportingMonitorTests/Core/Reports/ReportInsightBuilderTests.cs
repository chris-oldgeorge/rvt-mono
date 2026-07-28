using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;

namespace Rvt.Reporting.Core.Tests.Reports;

/// <summary>
/// Verifies executive-summary and alert-intensity insight calculations for report PDFs.
/// Major updates: 2026-06-25 added ID15/ID17 report insight coverage.
/// </summary>
public sealed class ReportInsightBuilderTests
{
    [Fact]
    public void BuildExecutiveSummary_GroupsBreachesWorstPeriodsAndTrafficLightsByMonitorType()
    {
        DateTimeOffset firstDay = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset secondDay = new(2026, 6, 2, 14, 0, 0, TimeSpan.Zero);
        SiteReportData site = new()
        {
            Monitors =
            [
                new MonitorReportData
                {
                    TypeOfMonitor = MonitorType.Dust,
                    Notifications =
                    [
                        Notification(AlertType.Alert, "PM10", 150m, 151m, firstDay),
                        Notification(AlertType.Alert, "PM10", 150m, 160m, secondDay),
                        Notification(AlertType.Caution, "PM10", 100m, 120m, secondDay.AddMinutes(20))
                    ]
                },
                new MonitorReportData
                {
                    TypeOfMonitor = MonitorType.Noise,
                    Notifications = [Notification(AlertType.Caution, "LAeq", 70m, 72m, firstDay.AddHours(1))]
                },
                new MonitorReportData { TypeOfMonitor = MonitorType.Vibration }
            ]
        };

        ReportExecutiveSummary summary = ReportInsightBuilder.BuildExecutiveSummary(site, firstDay, secondDay.AddDays(1));

        MonitorTypeExecutiveSummary dust = Assert.Single(summary.MonitorTypes, item => item.MonitorType == MonitorType.Dust);
        Assert.Equal(2, dust.AlertBreaches);
        Assert.Equal(1, dust.CautionBreaches);
        Assert.Equal(ReportTrafficLightStatus.Red, dust.Status);
        Assert.Equal(DateOnly.FromDateTime(secondDay.UtcDateTime), dust.WorstDay);
        Assert.Equal(2, dust.WorstDayBreaches);
        Assert.Equal(14, dust.WorstHour);
        Assert.Equal(2, dust.WorstHourBreaches);

        MonitorTypeExecutiveSummary noise = Assert.Single(summary.MonitorTypes, item => item.MonitorType == MonitorType.Noise);
        Assert.Equal(ReportTrafficLightStatus.Amber, noise.Status);

        MonitorTypeExecutiveSummary vibration = Assert.Single(summary.MonitorTypes, item => item.MonitorType == MonitorType.Vibration);
        Assert.Equal(ReportTrafficLightStatus.Green, vibration.Status);
    }

    [Fact]
    public void BuildAlertHeatmaps_GroupsNotificationCountsByMonitorTypeDayAndHour()
    {
        DateTimeOffset measuredAt = new(2026, 6, 3, 9, 15, 0, TimeSpan.Zero);
        SiteReportData site = new()
        {
            Monitors =
            [
                new MonitorReportData
                {
                    TypeOfMonitor = MonitorType.Noise,
                    Notifications =
                    [
                        Notification(AlertType.Alert, "LAeq", 80m, 84m, measuredAt),
                        Notification(AlertType.Caution, "LAeq", 70m, 75m, measuredAt.AddMinutes(10)),
                        Notification(AlertType.Alert, "LAeq", 80m, 90m, measuredAt.AddHours(1))
                    ]
                }
            ]
        };

        ReportAlertHeatmap heatmap = Assert.Single(ReportInsightBuilder.BuildAlertHeatmaps(site));
        ReportAlertHeatmapCell cell = Assert.Single(heatmap.Cells, item => item.Day == DateOnly.FromDateTime(measuredAt.UtcDateTime) && item.Hour == 9);

        Assert.Equal(MonitorType.Noise, heatmap.MonitorType);
        Assert.Equal(1, cell.AlertCount);
        Assert.Equal(1, cell.CautionCount);
        Assert.Equal(84m, cell.MaxLevel);
    }

    [Fact]
    public void BuildDefaultNarrative_UsesAvailableSummaryDataWhenAiIsUnavailable()
    {
        DateTimeOffset fromUtc = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset toUtc = new(2026, 6, 7, 23, 59, 59, TimeSpan.Zero);
        ReportExecutiveSummary summary = new(
            fromUtc,
            toUtc,
            [
                new MonitorTypeExecutiveSummary(MonitorType.Dust, 2, 3, 1, DateOnly.FromDateTime(fromUtc.UtcDateTime), 2, 14, 2, ReportTrafficLightStatus.Red)
            ]);

        string narrative = ReportInsightBuilder.BuildDefaultNarrative("North Site", summary);

        Assert.Contains("North Site", narrative, StringComparison.Ordinal);
        Assert.Contains("Dust", narrative, StringComparison.Ordinal);
        Assert.Contains("3 alert", narrative, StringComparison.Ordinal);
        Assert.Contains("1 caution", narrative, StringComparison.Ordinal);
    }

    private static NotificationData Notification(AlertType type, string field, decimal threshold, decimal level, DateTimeOffset createdAt) =>
        new(type, createdAt, field, threshold, level, 3600, null, null, null);
}
