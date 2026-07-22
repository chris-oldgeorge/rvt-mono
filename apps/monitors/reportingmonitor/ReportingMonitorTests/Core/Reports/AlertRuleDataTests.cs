using Rvt.Reporting.Core.Models;

namespace Rvt.Reporting.Core.Tests.Reports;

/// <summary>
/// Verifies report alert-rule display values shared by repository loading and PDF rendering.
/// Major updates: 2026-06-25 added triggered-count and vibration peak-only display coverage; added closed-note display coverage.
/// </summary>
public sealed class AlertRuleDataTests
{
    [Fact]
    public void AveragingPeriodLabel_ReturnsMinutesForNonVibrationRules()
    {
        var rule = new AlertRuleData(AlertType.Alert, "PM10", 150m, 900, "ug/m3", "PM10", 3);

        Assert.Equal("15 mins", rule.AveragingPeriodLabel);
    }

    [Fact]
    public void AveragingPeriodLabel_IsBlankWhenVibrationRepositoryClearsAveragingPeriod()
    {
        var rule = new AlertRuleData(AlertType.Alert, "Peak", 8m, null, "mm/s", "Peak", 4);

        Assert.Null(rule.AveragingPeriodLabel);
    }

    [Fact]
    public void LatestClosedNote_UsesNewestMatchingNotificationNote()
    {
        var rule = new AlertRuleData(AlertType.Alert, "PM10", 150m, 900, "ug/m3", "PM10", 3, "Investigated by site team");

        Assert.Equal("Investigated by site team", rule.LatestClosedNote);
    }
}
