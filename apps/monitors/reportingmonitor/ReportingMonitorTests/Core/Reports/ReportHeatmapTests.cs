// The namespace follows this project's established scheme rather than the
// folder path; IDE0130 would require a name no sibling file uses.
#pragma warning disable IDE0130
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Pdf.Documents;

namespace Rvt.Reporting.Core.Tests.Reports;

/// <summary>
/// Guards the alert heatmap SVG geometry. The viewBox was previously a fixed
/// 640x190, which clipped every row past the eighth day and silently truncated
/// monthly and 31-day one-time reports.
/// </summary>
public sealed partial class ReportHeatmapTests
{
    private const decimal _top = 20m;
    private const decimal _cellHeight = 20m;

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(28)]
    [InlineData(31)]
    public void BuildHeatmapSvg_KeepsEveryDayRowInsideTheViewBox(int dayCount)
    {
        string svg = InvokeBuildHeatmapSvg(CreateHeatmap(dayCount));

        decimal viewBoxHeight = ParseViewBoxHeight(svg);
        decimal lowestRowBottom = _top + (dayCount * _cellHeight);

        Assert.True(
            viewBoxHeight >= lowestRowBottom,
            $"{dayCount} day(s) need at least {lowestRowBottom} height but the viewBox is {viewBoxHeight}.");
    }

    [Fact]
    public void BuildHeatmapSvg_RendersARowLabelForEveryDay()
    {
        const int dayCount = 31;

        string svg = InvokeBuildHeatmapSvg(CreateHeatmap(dayCount));

        for (int offset = 0; offset < dayCount; offset++)
        {
            DateOnly day = new DateOnly(2026, 3, 1).AddDays(offset);
            string label = day.ToString("dd/MM", CultureInfo.InvariantCulture);
            Assert.Contains(label, svg, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void BuildHeatmapSvg_GrowsHeightWithDayCount()
    {
        decimal shortReport = ParseViewBoxHeight(InvokeBuildHeatmapSvg(CreateHeatmap(2)));
        decimal longReport = ParseViewBoxHeight(InvokeBuildHeatmapSvg(CreateHeatmap(30)));

        Assert.True(longReport > shortReport);
    }

    [Fact]
    public void BuildHeatmapSvg_RendersCountsForPopulatedCells()
    {
        DateOnly day = new(2026, 3, 1);
        ReportAlertHeatmap heatmap = new(
            MonitorType.Noise,
            [new ReportAlertHeatmapCell(day, 5, 2, 1, 78m)]);

        string svg = InvokeBuildHeatmapSvg(heatmap);

        Assert.Contains(">3</text>", svg, StringComparison.Ordinal);
    }

    private static ReportAlertHeatmap CreateHeatmap(int dayCount)
    {
        DateOnly start = new(2026, 3, 1);
        ReportAlertHeatmapCell[] cells =
        [
            .. Enumerable.Range(0, dayCount)
                .Select(offset => new ReportAlertHeatmapCell(start.AddDays(offset), 12, 1, 0, 80m)),
        ];
        return new ReportAlertHeatmap(MonitorType.Noise, cells);
    }

    private static decimal ParseViewBoxHeight(string svg)
    {
        Match match = ViewBoxPattern().Match(svg);
        Assert.True(match.Success, "The rendered SVG must declare a viewBox.");
        return decimal.Parse(match.Groups["height"].Value, CultureInfo.InvariantCulture);
    }

    private static string InvokeBuildHeatmapSvg(ReportAlertHeatmap heatmap)
    {
        MethodInfo? method = typeof(QuestPdfReportRenderer).GetMethod("BuildHeatmapSvg", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(null, [heatmap]));
    }

    [GeneratedRegex(
        @"viewBox=""0 0 (?<width>[\d.]+) (?<height>[\d.]+)""",
        RegexOptions.CultureInvariant)]
    private static partial Regex ViewBoxPattern();
}
