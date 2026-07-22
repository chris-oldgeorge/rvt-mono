using System.Globalization;
using Rvt.Reporting.Core.Models;

namespace Rvt.Reporting.Core.Reports;

/// <summary>
/// Builds deterministic executive-summary and alert-intensity insights from report-period data.
/// Major updates: 2026-06-25 added ID15/ID17 report insight calculations.
/// </summary>
public static class ReportInsightBuilder
{
    public static ReportExecutiveSummary BuildExecutiveSummary(SiteReportData site, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        ArgumentNullException.ThrowIfNull(site);

        var summaries = site.Monitors
            .GroupBy(static monitor => monitor.TypeOfMonitor)
            .OrderBy(static group => group.Key)
            .Select(group => BuildMonitorTypeSummary(group.Key, group.ToArray()))
            .ToArray();

        return new ReportExecutiveSummary(fromUtc, toUtc, summaries);
    }

    public static IReadOnlyList<ReportAlertHeatmap> BuildAlertHeatmaps(SiteReportData site)
    {
        ArgumentNullException.ThrowIfNull(site);

        return site.Monitors
            .GroupBy(static monitor => monitor.TypeOfMonitor)
            .Select(group =>
            {
                var cells = group
                    .SelectMany(static monitor => monitor.Notifications)
                    .GroupBy(static notification => new
                    {
                        Day = DateOnly.FromDateTime(notification.CreatedAt.UtcDateTime),
                        notification.CreatedAt.UtcDateTime.Hour
                    })
                    .Select(cell => new ReportAlertHeatmapCell(
                        cell.Key.Day,
                        cell.Key.Hour,
                        cell.Count(static notification => notification.AlertType == AlertType.Alert),
                        cell.Count(static notification => notification.AlertType == AlertType.Caution),
                        cell.Max(static notification => notification.Level)))
                    .OrderBy(static cell => cell.Day)
                    .ThenBy(static cell => cell.Hour)
                    .ToArray();

                return new ReportAlertHeatmap(group.Key, cells);
            })
            .Where(static heatmap => heatmap.Cells.Count > 0)
            .OrderBy(static heatmap => heatmap.MonitorType)
            .ToArray();
    }

    public static string BuildDefaultNarrative(string siteName, ReportExecutiveSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        if (summary.MonitorTypes.Count == 0)
        {
            return $"{siteName} had no monitor breach data available for the selected reporting period.";
        }

        var parts = summary.MonitorTypes.Select(static item =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"{item.MonitorType}: {item.AlertBreaches} alert breach(es), {item.CautionBreaches} caution breach(es), status {item.Status}"));

        var worst = summary.MonitorTypes
            .Where(static item => item.WorstDay is not null)
            .OrderByDescending(static item => item.WorstDayBreaches)
            .FirstOrDefault();
        var worstText = worst is null
            ? "No worst breach period was identified."
            : $"The busiest breach period was {worst.MonitorType} on {worst.WorstDay:yyyy-MM-dd} at {worst.WorstHour:00}:00 UTC with {worst.WorstHourBreaches} event(s).";

        return $"{siteName} report summary: {string.Join("; ", parts)}. {worstText}";
    }

    public static ReportInsights BuildDeterministicInsights(SiteReportData site, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        var summary = BuildExecutiveSummary(site, fromUtc, toUtc);
        return new ReportInsights(summary, BuildAlertHeatmaps(site), BuildDefaultNarrative(site.SiteName, summary));
    }

    private static MonitorTypeExecutiveSummary BuildMonitorTypeSummary(MonitorType monitorType, MonitorReportData[] monitors)
    {
        var notifications = monitors.SelectMany(static monitor => monitor.Notifications).ToArray();
        var alertBreaches = notifications.Count(static notification => notification.AlertType == AlertType.Alert);
        var cautionBreaches = notifications.Count(static notification => notification.AlertType == AlertType.Caution);
        var worstDay = notifications
            .GroupBy(static notification => DateOnly.FromDateTime(notification.CreatedAt.UtcDateTime))
            .Select(static group => new { Day = group.Key, Count = group.Count() })
            .OrderByDescending(static item => item.Count)
            .ThenBy(static item => item.Day)
            .FirstOrDefault();
        var worstHour = notifications
            .GroupBy(static notification => notification.CreatedAt.UtcDateTime.Hour)
            .Select(static group => new { Hour = group.Key, Count = group.Count() })
            .OrderByDescending(static item => item.Count)
            .ThenBy(static item => item.Hour)
            .FirstOrDefault();

        return new MonitorTypeExecutiveSummary(
            monitorType,
            monitors.Length,
            alertBreaches,
            cautionBreaches,
            worstDay?.Day,
            worstDay?.Count ?? 0,
            worstHour?.Hour,
            worstHour?.Count ?? 0,
            alertBreaches > 0 ? ReportTrafficLightStatus.Red : cautionBreaches > 0 ? ReportTrafficLightStatus.Amber : ReportTrafficLightStatus.Green);
    }
}
