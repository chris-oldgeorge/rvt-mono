// The namespace follows this project's established scheme rather than the
// folder path; IDE0130 would require a name no sibling file uses.
#pragma warning disable IDE0130
using System.Globalization;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Pdf.Documents;

namespace Rvt.Reporting.Core.Tests.Reports;

/// <summary>
/// Report text must not pick up the container's locale. Under a comma-decimal
/// culture the threshold "42.5" previously rendered as "42,5", so the same
/// report read differently depending on where it was generated.
/// </summary>
public sealed class ReportInvariantFormattingTests
{
    [Fact]
    public void BuildReportGraphs_FormatsThresholdsInvariantlyUnderACommaDecimalCulture()
    {
        string label = WithCulture(
            new CultureInfo("de-DE"),
            static () => QuestPdfReportRenderer.BuildReportGraphs(CreateSiteWithThreshold(42.5m))
                .SelectMany(static graph => graph.Limits)
                .Select(static limit => limit.Label)
                .Single());

        Assert.Contains("42.5", label, StringComparison.Ordinal);
        Assert.DoesNotContain("42,5", label, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildReportGraphs_ProducesTheSameLabelInEveryCulture()
    {
        static string BuildLabel() => QuestPdfReportRenderer.BuildReportGraphs(CreateSiteWithThreshold(42.5m))
            .SelectMany(static graph => graph.Limits)
            .Select(static limit => limit.Label)
            .Single();

        string invariant = WithCulture(CultureInfo.InvariantCulture, BuildLabel);
        string german = WithCulture(new CultureInfo("de-DE"), BuildLabel);
        string french = WithCulture(new CultureInfo("fr-FR"), BuildLabel);

        Assert.Equal(invariant, german);
        Assert.Equal(invariant, french);
    }

    [Fact]
    public void BuildReportChrome_FormatsTheGeneratedTimestampInvariantly()
    {
        DateTimeOffset generatedAt = new DateTimeOffset(2026, 3, 9, 14, 5, 0, TimeSpan.Zero);

        ReportChrome chrome = WithCulture(
            new CultureInfo("ar-SA"),
            () => QuestPdfReportRenderer.BuildReportChrome(
                "Monthly report",
                generatedAt,
                generatedAt.AddDays(-31),
                generatedAt,
                new SiteReportData()));

        Assert.Contains("2026-03-09 14:05 UTC", chrome.BodyReportDateText, StringComparison.Ordinal);
    }

    private static SiteReportData CreateSiteWithThreshold(decimal threshold) => new()
    {
        Monitors =
        [
            new MonitorReportData
            {
                SerialId = "N1",
                FleetNumber = "Noise 1",
                TypeOfMonitor = MonitorType.Noise,
                NoiseDailyAverage =
                [
                    new MeasurementPoint(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), 50m)
                ],
                AlertRules =
                [
                    new AlertRuleData(AlertType.Alert, "NoiseDailyAverage", threshold, null, "dB", "Daily average", 0)
                ]
            }
        ]
    };

    private static T WithCulture<T>(CultureInfo culture, Func<T> action)
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}
