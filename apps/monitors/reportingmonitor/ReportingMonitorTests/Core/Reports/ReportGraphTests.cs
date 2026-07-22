using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Pdf.Documents;

namespace Rvt.Reporting.Core.Tests.Reports;

/// <summary>
/// Verifies report-period graph grouping and alert-limit overlays for generated PDFs.
/// Major updates: 2026-06-25 added ID12/ID13 graph coverage; added invariant SVG coordinate coverage for rendered graphs.
/// </summary>
public sealed class ReportGraphTests
{
    [Fact]
    public void BuildReportGraphs_GroupsNoiseDailyAveragesAcrossMonitors()
    {
        var measuredAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var site = new SiteReportData
        {
            Monitors =
            [
                new MonitorReportData
                {
                    SerialId = "N1",
                    FleetNumber = "Noise 1",
                    TypeOfMonitor = MonitorType.Noise,
                    NoiseDailyAverage = [new MeasurementPoint(measuredAt, 54m)]
                },
                new MonitorReportData
                {
                    SerialId = "N2",
                    FleetNumber = "Noise 2",
                    TypeOfMonitor = MonitorType.Noise,
                    NoiseDailyAverage = [new MeasurementPoint(measuredAt, 58m)]
                }
            ]
        };

        var graphs = QuestPdfReportRenderer.BuildReportGraphs(site);

        var graph = Assert.Single(graphs, item => item.Title == "Noise Daily Averages");
        Assert.Equal("dB", graph.Unit);
        Assert.Equal(2, graph.Series.Count);
        Assert.Contains(graph.Series, series => series.Name == "Noise 1" && series.Points.Single().Value == 54m);
        Assert.Contains(graph.Series, series => series.Name == "Noise 2" && series.Points.Single().Value == 58m);
    }

    [Fact]
    public void BuildReportGraphs_AddsMatchingAlertLimitLines()
    {
        var measuredAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var monitor = new MonitorReportData
        {
            SerialId = "N1",
            TypeOfMonitor = MonitorType.Noise,
            NoiseDailyAverage = [new MeasurementPoint(measuredAt, 54m)],
            AlertRules =
            [
                new AlertRuleData(AlertType.Alert, "NoiseDailyAverage", 70m, 86400, "dB", "Daily Average", 0),
                new AlertRuleData(AlertType.Caution, "NoiseHourlyAverage", 60m, 3600, "dB", "Hourly Average", 0)
            ]
        };

        var graph = Assert.Single(QuestPdfReportRenderer.BuildReportGraphs(new SiteReportData { Monitors = [monitor] }));

        var limit = Assert.Single(graph.Limits);
        Assert.Equal(70m, limit.Value);
        Assert.Equal("Alert 70 dB", limit.Label);
    }

    [Fact]
    public void BuildGraphSvg_UsesInvariantDecimalCoordinates_WhenCurrentCultureUsesCommaDecimalSeparator()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("el-GR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("el-GR");

            var measuredAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
            var graph = new ReportGraph(
                "Noise Hourly Averages",
                MonitorType.Noise,
                "Hourly",
                "dB",
                [
                    new ReportGraphSeries("Noise 1",
                    [
                        new MeasurementPoint(measuredAt, 52m),
                        new MeasurementPoint(measuredAt.AddMinutes(30), 55.33m),
                        new MeasurementPoint(measuredAt.AddHours(1), 58m)
                    ])
                ],
                []);

            var svg = InvokeBuildGraphSvg(graph);
            var points = Regex.Match(svg, "points=\"([^\"]+)\"").Groups[1].Value;

            Assert.DoesNotMatch(new Regex("""points="[^"]*\d+,\d+,\d+"""), svg);
            Assert.Contains(".", points);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static string InvokeBuildGraphSvg(ReportGraph graph)
    {
        var method = typeof(QuestPdfReportRenderer).GetMethod("BuildGraphSvg", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(null, [graph]));
    }
}
