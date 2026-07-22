using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;
using System.Globalization;

namespace Rvt.Reporting.Pdf.Documents;

public sealed record ReportChrome(string HeaderText, string BodyReportDateText, string FooterText);

/// <summary>
/// Renders RVT site reports with QuestPDF for the containerized reporting service.
/// Major updates: 2026-06-24 initial renderer replacing Azure Function-local PDF generation; added optional customer logo in report header; 2026-06-25 added alert-rule triggered-count tables; 2026-06-25 simplified report header/footer chrome; 2026-06-25 added report-period graphs with alert limit lines; 2026-06-25 added executive summary, closed notes, and alert heatmaps.
/// </summary>
public sealed class QuestPdfReportRenderer : IReportPdfRenderer
{
    private const string RvtLogoAssetName = "RVTlogo.svg";
    private static readonly string[] GraphColors = ["#2563eb", "#16a34a", "#9333ea", "#ea580c", "#0891b2", "#be123c"];

    public Task<RenderedReport> RenderAsync(string? reportName, DateTimeOffset generatedAtUtc, DateTimeOffset fromUtc, DateTimeOffset toUtc, SiteReportData site, CustomerLogo? customerLogo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(site);
        cancellationToken.ThrowIfCancellationRequested();

        QuestPDF.Settings.License = LicenseType.Community;
        var chrome = BuildReportChrome(reportName, generatedAtUtc, fromUtc, toUtc, site);
        var rvtLogoPath = FindRvtLogoPath();
        var graphs = BuildReportGraphs(site);
        var insights = site.Insights ?? ReportInsightBuilder.BuildDeterministicInsights(site, fromUtc, toUtc);
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(style => style.FontSize(10));
                page.Header().Row(row =>
                {
                    if (rvtLogoPath is null)
                    {
                        row.ConstantItem(170).AlignLeft().AlignMiddle().Text(chrome.HeaderText).FontSize(12).Bold().FontColor(Colors.Green.Darken2);
                    }
                    else
                    {
                        row.ConstantItem(170).AlignLeft().AlignMiddle().Svg(SvgImage.FromFile(rvtLogoPath)).FitWidth();
                    }

                    row.RelativeItem();
                    if (customerLogo is not null)
                    {
                        row.ConstantItem(120).Height(64).AlignRight().AlignMiddle().Image(customerLogo.Content).FitArea();
                    }
                });

                page.Content().PaddingVertical(20).Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text(chrome.BodyReportDateText).FontColor(Colors.Grey.Darken2);
                    column.Item().Text($"Company: {site.CompanyName ?? "N/A"}");
                    column.Item().Text($"Contracts: {site.Contracts ?? "N/A"}");
                    ComposeExecutiveSummary(column, insights);
                    ComposeGraphs(column, graphs);
                    ComposeAlertHeatmaps(column, insights.AlertHeatmaps);
                    column.Item().Text("Monitors").FontSize(14).Bold();

                    if (site.Monitors.Count == 0)
                    {
                        column.Item().Text("No active monitor data was available for this report period.");
                    }
                    else
                    {
                        foreach (var monitor in site.Monitors)
                        {
                            column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(monitorColumn =>
                            {
                                monitorColumn.Item().Text($"{monitor.TypeOfMonitor} - {monitor.FleetNumber ?? monitor.SerialId}").Bold();
                                monitorColumn.Item().Text($"Serial: {monitor.SerialId}");
                                monitorColumn.Item().Text($"Location: {monitor.Location ?? "N/A"}");
                                monitorColumn.Item().Text($"Last data: {monitor.LastDataTime?.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) ?? "N/A"}");
                                monitorColumn.Item().Text($"Rules: {monitor.AlertRules.Count}; Notifications: {monitor.Notifications.Count}");
                                ComposeAlertRuleTable(monitorColumn, "Alert Rules", monitor, AlertType.Alert);
                                ComposeAlertRuleTable(monitorColumn, "Caution Rules", monitor, AlertType.Caution);
                            });
                        }
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span(chrome.FooterText);
                    text.Span(" | ");
                    text.CurrentPageNumber();
                    text.Span("/");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

        return Task.FromResult(new RenderedReport(CreateFileName(generatedAtUtc, site.SiteName), "application/pdf", bytes));
    }

    public static IReadOnlyList<ReportGraph> BuildReportGraphs(SiteReportData site)
    {
        ArgumentNullException.ThrowIfNull(site);

        var graphs = new List<ReportGraph>();
        AddGraph(graphs, site.Monitors, MonitorType.Dust, "Dust Hourly Averages", "Hourly", "ug/m3", "DustHourlyAverage", 3600, static monitor => monitor.DustHourlyAverage);
        AddGraph(graphs, site.Monitors, MonitorType.Dust, "Dust Daily Averages", "Daily", "ug/m3", "DustDailyAverage", 86400, static monitor => monitor.DustDailyAverage);
        AddGraph(graphs, site.Monitors, MonitorType.Noise, "Noise Hourly Averages", "Hourly", "dB", "NoiseHourlyAverage", 3600, static monitor => monitor.NoiseHourlyAverage);
        AddGraph(graphs, site.Monitors, MonitorType.Noise, "Noise Daily Averages", "Daily", "dB", "NoiseDailyAverage", 86400, static monitor => monitor.NoiseDailyAverage);
        AddGraph(graphs, site.Monitors, MonitorType.Noise, "Noise Site Averages", "Site", "dB", "NoiseSiteAverage", null, static monitor => monitor.NoiseSiteAverage);
        AddGraph(graphs, site.Monitors, MonitorType.Vibration, "Vibration Daily Peaks", "Daily Peak", "mm/s", "VibrationDailyPeak", null, static monitor => monitor.VibrationDailyPeak);
        return graphs;
    }

    public static ReportChrome BuildReportChrome(string? reportName, DateTimeOffset generatedAtUtc, DateTimeOffset fromUtc, DateTimeOffset toUtc, SiteReportData site)
    {
        _ = reportName;
        _ = fromUtc;
        _ = toUtc;
        return new ReportChrome("RVT Cloud", $"Report date: {generatedAtUtc:yyyy-MM-dd HH:mm} UTC", "RVT Cloud Reporting");
    }

    private static string? FindRvtLogoPath()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            var candidatePath = Path.Combine(currentDirectory.FullName, "Assets", RvtLogoAssetName);
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return null;
    }

    private static void AddGraph(
        List<ReportGraph> graphs,
        IReadOnlyList<MonitorReportData> monitors,
        MonitorType monitorType,
        string title,
        string averagePeriodLabel,
        string unit,
        string graphField,
        int? averagingPeriodSeconds,
        Func<MonitorReportData, IReadOnlyList<MeasurementPoint>> selectPoints)
    {
        var typeMonitors = monitors.Where(monitor => monitor.TypeOfMonitor == monitorType).ToArray();
        var series = typeMonitors
            .Select(monitor => new ReportGraphSeries(MonitorLabel(monitor), selectPoints(monitor).OrderBy(static point => point.MeasuredAt).ToArray()))
            .Where(static item => item.Points.Count > 0)
            .ToArray();

        if (series.Length == 0)
        {
            return;
        }

        var limits = typeMonitors
            .SelectMany(monitor => monitor.AlertRules)
            .Where(rule => RuleMatchesGraph(rule, graphField, averagingPeriodSeconds))
            .GroupBy(rule => new { rule.AlertType, rule.Threshold, Unit = rule.Unit ?? unit })
            .Select(group => new ReportGraphLimit(
                group.Key.AlertType,
                group.Key.Threshold,
                group.Key.Unit,
                $"{group.Key.AlertType} {group.Key.Threshold:0.##} {group.Key.Unit}".Trim()))
            .OrderBy(static limit => limit.AlertType)
            .ThenBy(static limit => limit.Value)
            .ToArray();

        graphs.Add(new ReportGraph(title, monitorType, averagePeriodLabel, unit, series, limits));
    }

    private static bool RuleMatchesGraph(AlertRuleData rule, string graphField, int? averagingPeriodSeconds)
    {
        if (string.Equals(rule.Field, graphField, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return averagingPeriodSeconds.HasValue &&
            rule.AveragingPeriodSeconds == averagingPeriodSeconds &&
            (ContainsFieldToken(graphField, rule.Field) || ContainsFieldToken(rule.Name, graphField));
    }

    private static bool ContainsFieldToken(string? value, string token)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            !string.IsNullOrWhiteSpace(token) &&
            value.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static void ComposeGraphs(ColumnDescriptor column, IReadOnlyList<ReportGraph> graphs)
    {
        if (graphs.Count == 0)
        {
            return;
        }

        column.Item().PaddingTop(8).Text("Graphs").FontSize(14).Bold();
        foreach (var graph in graphs)
        {
            column.Item().PaddingTop(6).Column(graphColumn =>
            {
                graphColumn.Item().Text(graph.Title).Bold();
                graphColumn.Item().Text($"{graph.AveragePeriodLabel} values ({graph.Unit})").FontColor(Colors.Grey.Darken2);
                graphColumn.Item().Height(190).Svg(SvgImage.FromText(BuildGraphSvg(graph))).FitArea();
            });
        }
    }

    private static void ComposeExecutiveSummary(ColumnDescriptor column, ReportInsights insights)
    {
        column.Item().PaddingTop(8).Text("Executive Summary").FontSize(14).Bold();
        column.Item().Text(insights.Narrative).FontColor(Colors.Grey.Darken3);

        if (insights.ExecutiveSummary.MonitorTypes.Count == 0)
        {
            column.Item().Text("No monitor breach data was available for this report period.");
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn(1.5f);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Type");
                header.Cell().Element(HeaderCell).Text("Status");
                header.Cell().Element(HeaderCell).Text("Alerts");
                header.Cell().Element(HeaderCell).Text("Cautions");
                header.Cell().Element(HeaderCell).Text("Worst period");
            });

            foreach (var summary in insights.ExecutiveSummary.MonitorTypes)
            {
                table.Cell().Element(BodyCell).Text(summary.MonitorType.ToString());
                table.Cell().Element(BodyCell).Text(summary.Status.ToString()).FontColor(StatusColor(summary.Status)).Bold();
                table.Cell().Element(BodyCell).Text(summary.AlertBreaches.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCell).Text(summary.CautionBreaches.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCell).Text(WorstPeriodText(summary));
            }
        });
    }

    private static void ComposeAlertHeatmaps(ColumnDescriptor column, IReadOnlyList<ReportAlertHeatmap> heatmaps)
    {
        if (heatmaps.Count == 0)
        {
            return;
        }

        column.Item().PaddingTop(8).Text("Alert Heatmaps").FontSize(14).Bold();
        foreach (var heatmap in heatmaps)
        {
            column.Item().PaddingTop(6).Column(heatmapColumn =>
            {
                heatmapColumn.Item().Text($"{heatmap.MonitorType} alert intensity").Bold();
                heatmapColumn.Item().Text("Notification count by day and hour").FontColor(Colors.Grey.Darken2);
                heatmapColumn.Item().Height(170).Svg(SvgImage.FromText(BuildHeatmapSvg(heatmap))).FitArea();
            });
        }
    }

    private static string BuildGraphSvg(ReportGraph graph)
    {
        const decimal width = 640m;
        const decimal height = 210m;
        const decimal left = 54m;
        const decimal top = 16m;
        const decimal right = 16m;
        const decimal bottom = 46m;
        var plotWidth = width - left - right;
        var plotHeight = height - top - bottom;
        var points = graph.Series.SelectMany(static series => series.Points).ToArray();
        var minTime = points.Min(static point => point.MeasuredAt);
        var maxTime = points.Max(static point => point.MeasuredAt);
        var minValue = Math.Min(points.Min(static point => point.Value), graph.Limits.Select(static limit => limit.Value).DefaultIfEmpty(points.Min(static point => point.Value)).Min());
        var maxValue = Math.Max(points.Max(static point => point.Value), graph.Limits.Select(static limit => limit.Value).DefaultIfEmpty(points.Max(static point => point.Value)).Max());

        if (minValue == maxValue)
        {
            minValue -= 1;
            maxValue += 1;
        }

        var xRangeSeconds = Math.Max(1, (decimal)(maxTime - minTime).TotalSeconds);
        var yRange = maxValue - minValue;
        var lines = new List<string>
        {
            $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width:0} {height:0}">""",
            """<rect width="640" height="210" fill="#ffffff"/>""",
            $"""<line x1="{left:0}" y1="{top:0}" x2="{left:0}" y2="{height - bottom:0}" stroke="#6b7280" stroke-width="1"/>""",
            $"""<line x1="{left:0}" y1="{height - bottom:0}" x2="{width - right:0}" y2="{height - bottom:0}" stroke="#6b7280" stroke-width="1"/>""",
            $"""<text x="8" y="{top + 10:0}" font-family="Arial" font-size="10" fill="#374151">{XmlEscape(maxValue.ToString("0.##", CultureInfo.InvariantCulture))}</text>""",
            $"""<text x="8" y="{height - bottom:0}" font-family="Arial" font-size="10" fill="#374151">{XmlEscape(minValue.ToString("0.##", CultureInfo.InvariantCulture))}</text>""",
            $"""<text x="{left:0}" y="{height - 20:0}" font-family="Arial" font-size="10" fill="#374151">{XmlEscape(minTime.ToString("dd/MM", CultureInfo.InvariantCulture))}</text>""",
            $"""<text x="{width - 70:0}" y="{height - 20:0}" font-family="Arial" font-size="10" fill="#374151">{XmlEscape(maxTime.ToString("dd/MM", CultureInfo.InvariantCulture))}</text>"""
        };

        for (var index = 0; index < graph.Limits.Count; index++)
        {
            var limit = graph.Limits[index];
            var y = MapY(limit.Value, minValue, yRange, top, plotHeight);
            var color = limit.AlertType == AlertType.Alert ? "#dc2626" : "#f59e0b";
            lines.Add($"""<line x1="{SvgNumber(left, "0")}" y1="{SvgNumber(y)}" x2="{SvgNumber(width - right, "0")}" y2="{SvgNumber(y)}" stroke="{color}" stroke-width="1.4" stroke-dasharray="6 4"/>""");
            lines.Add($"""<text x="{SvgNumber(left + 6, "0")}" y="{SvgNumber(Math.Max(10, y - 4))}" font-family="Arial" font-size="10" fill="{color}">{XmlEscape(limit.Label)}</text>""");
        }

        for (var index = 0; index < graph.Series.Count; index++)
        {
            var series = graph.Series[index];
            var color = GraphColors[index % GraphColors.Length];
            var mapped = series.Points
                .Select(point => $"{SvgNumber(MapX(point.MeasuredAt, minTime, xRangeSeconds, left, plotWidth))},{SvgNumber(MapY(point.Value, minValue, yRange, top, plotHeight))}")
                .ToArray();
            lines.Add($"""<polyline points="{string.Join(' ', mapped)}" fill="none" stroke="{color}" stroke-width="2"/>""");

            foreach (var point in series.Points)
            {
                lines.Add($"""<circle cx="{SvgNumber(MapX(point.MeasuredAt, minTime, xRangeSeconds, left, plotWidth))}" cy="{SvgNumber(MapY(point.Value, minValue, yRange, top, plotHeight))}" r="2.4" fill="{color}"/>""");
            }

            var legendY = height - 8;
            var legendX = left + index * 110m;
            lines.Add($"""<circle cx="{legendX:0}" cy="{legendY - 4:0}" r="3" fill="{color}"/>""");
            lines.Add($"""<text x="{legendX + 8:0}" y="{legendY:0}" font-family="Arial" font-size="10" fill="#374151">{XmlEscape(series.Name)}</text>""");
        }

        lines.Add("</svg>");
        return string.Concat(lines);
    }

    private static string BuildHeatmapSvg(ReportAlertHeatmap heatmap)
    {
        const decimal width = 640m;
        const decimal height = 190m;
        const decimal left = 58m;
        const decimal top = 20m;
        const decimal cellWidth = 22m;
        const decimal cellHeight = 20m;
        var days = heatmap.Cells.Select(static cell => cell.Day).Distinct().Order().ToArray();
        var maxCount = Math.Max(1, heatmap.Cells.Max(static cell => cell.AlertCount + cell.CautionCount));
        var lines = new List<string>
        {
            $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width:0} {height:0}">""",
            $"""<rect width="{width:0}" height="{height:0}" fill="#ffffff"/>"""
        };

        for (var hour = 0; hour < 24; hour += 3)
        {
            var x = left + hour * cellWidth;
            lines.Add($"""<text x="{x:0}" y="14" font-family="Arial" font-size="9" fill="#374151">{hour:00}</text>""");
        }

        for (var dayIndex = 0; dayIndex < days.Length; dayIndex++)
        {
            var day = days[dayIndex];
            var y = top + dayIndex * cellHeight;
            lines.Add($"""<text x="4" y="{y + 14:0}" font-family="Arial" font-size="9" fill="#374151">{XmlEscape(day.ToString("dd/MM", CultureInfo.InvariantCulture))}</text>""");

            for (var hour = 0; hour < 24; hour++)
            {
                var cell = heatmap.Cells.SingleOrDefault(item => item.Day == day && item.Hour == hour);
                var count = cell is null ? 0 : cell.AlertCount + cell.CautionCount;
                var color = HeatmapColor(count, maxCount, cell?.AlertCount > 0);
                var x = left + hour * cellWidth;
                lines.Add($"""<rect x="{x:0}" y="{y:0}" width="{cellWidth - 1:0}" height="{cellHeight - 1:0}" fill="{color}" stroke="#ffffff" stroke-width="1"/>""");
                if (count > 0)
                {
                    lines.Add($"""<text x="{x + 7:0}" y="{y + 13:0}" font-family="Arial" font-size="8" fill="#111827">{count}</text>""");
                }
            }
        }

        lines.Add("</svg>");
        return string.Concat(lines);
    }

    private static decimal MapX(DateTimeOffset measuredAt, DateTimeOffset minTime, decimal xRangeSeconds, decimal left, decimal plotWidth)
    {
        return left + ((decimal)(measuredAt - minTime).TotalSeconds / xRangeSeconds * plotWidth);
    }

    private static decimal MapY(decimal value, decimal minValue, decimal yRange, decimal top, decimal plotHeight)
    {
        return top + (1 - ((value - minValue) / yRange)) * plotHeight;
    }

    private static string SvgNumber(decimal value, string format = "0.##") => value.ToString(format, CultureInfo.InvariantCulture);

    private static string MonitorLabel(MonitorReportData monitor) => monitor.FleetNumber ?? monitor.SerialId;

    private static string XmlEscape(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    private static void ComposeAlertRuleTable(ColumnDescriptor column, string title, MonitorReportData monitor, AlertType alertType)
    {
        var rules = monitor.AlertRules.Where(rule => rule.AlertType == alertType).ToArray();
        if (rules.Length == 0)
        {
            return;
        }

        var showAveragingPeriod = monitor.TypeOfMonitor != MonitorType.Vibration && rules.Any(static rule => rule.AveragingPeriodLabel is not null);
        var showClosedNote = rules.Any(static rule => !string.IsNullOrWhiteSpace(rule.LatestClosedNote));
        column.Item().PaddingTop(6).Text(title).Bold();
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn();
                if (showAveragingPeriod)
                {
                    columns.RelativeColumn();
                }

                columns.RelativeColumn();
                if (showClosedNote)
                {
                    columns.RelativeColumn(2);
                }
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Rule");
                header.Cell().Element(HeaderCell).Text("Threshold");
                if (showAveragingPeriod)
                {
                    header.Cell().Element(HeaderCell).Text("Average");
                }

                header.Cell().Element(HeaderCell).Text("Triggered");
                if (showClosedNote)
                {
                    header.Cell().Element(HeaderCell).Text("Closed note");
                }
            });

            foreach (var rule in rules)
            {
                table.Cell().Element(BodyCell).Text(rule.Name ?? rule.Field);
                table.Cell().Element(BodyCell).Text($"{rule.Threshold:0.##} {rule.Unit}".Trim());
                if (showAveragingPeriod)
                {
                    table.Cell().Element(BodyCell).Text(rule.AveragingPeriodLabel ?? "N/A");
                }

                table.Cell().Element(BodyCell).Text(rule.TriggeredCount.ToString(CultureInfo.InvariantCulture));
                if (showClosedNote)
                {
                    table.Cell().Element(BodyCell).Text(rule.LatestClosedNote ?? string.Empty);
                }
            }
        });
    }

    private static string WorstPeriodText(MonitorTypeExecutiveSummary summary)
    {
        return summary.WorstDay is null || summary.WorstHour is null
            ? "N/A"
            : $"{summary.WorstDay:yyyy-MM-dd} {summary.WorstHour:00}:00 UTC ({summary.WorstHourBreaches})";
    }

    private static string StatusColor(ReportTrafficLightStatus status) => status switch
    {
        ReportTrafficLightStatus.Red => Colors.Red.Darken2,
        ReportTrafficLightStatus.Amber => Colors.Orange.Darken2,
        _ => Colors.Green.Darken2
    };

    private static string HeatmapColor(int count, int maxCount, bool hasAlert)
    {
        if (count == 0)
        {
            return "#f3f4f6";
        }

        var intensity = (decimal)count / maxCount;
        if (hasAlert)
        {
            return intensity > 0.66m ? "#dc2626" : intensity > 0.33m ? "#f87171" : "#fecaca";
        }

        return intensity > 0.66m ? "#f59e0b" : intensity > 0.33m ? "#fbbf24" : "#fde68a";
    }

    private static IContainer HeaderCell(IContainer container) => container
        .DefaultTextStyle(style => style.Bold())
        .Background(Colors.Grey.Lighten3)
        .BorderBottom(1)
        .BorderColor(Colors.Grey.Lighten1)
        .Padding(4);

    private static IContainer BodyCell(IContainer container) => container
        .BorderBottom(1)
        .BorderColor(Colors.Grey.Lighten3)
        .Padding(4);

    private static string CreateFileName(DateTimeOffset generatedAtUtc, string siteName)
    {
        var safeName = string.Concat(siteName.Where(static character => !Path.GetInvalidFileNameChars().Contains(character) && !char.IsWhiteSpace(character)));
        return $"{generatedAtUtc:yyyyMMddHHmmssfff}_{safeName}_report.pdf";
    }
}
