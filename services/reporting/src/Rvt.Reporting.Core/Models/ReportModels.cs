using System.ComponentModel.DataAnnotations;

namespace Rvt.Reporting.Core.Models;

/// <summary>
/// Core report DTOs shared by scheduling, persistence, rendering, and delivery adapters.
/// Major updates: 2026-06-24 initial ACS/Quartz reporting service port; added customer-logo model for report branding; 2026-06-25 added report alert-rule triggered counts; 2026-06-25 added report graph DTOs; 2026-06-25 added executive insight and alert heatmap DTOs; 2026-06-26 added effective monitor ownership windows.
/// </summary>
public enum FrequencyType
{
    Off = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    WeeklyAndMonthly = 4,
    OneTime = 5
}

public enum MonitorType
{
    Dust = 0,
    Noise = 1,
    Vibration = 2
}

public enum DataBucketType
{
    Daily = 0,
    Site = 1,
    Hourly = 2
}

public enum AlertType
{
    Alert = 0,
    Caution = 1
}

public enum ReportTrafficLightStatus
{
    Green = 0,
    Amber = 1,
    Red = 2
}

public sealed record ReportRule
{
    public Guid Id { get; init; }
    public Guid SiteId { get; init; }
    public Guid UserId { get; init; }
    public FrequencyType Frequency { get; init; }
    public DayOfWeek? DayOfWeek { get; init; }
    public int? DayOfMonth { get; init; }
    public DateTimeOffset? LastGenerated { get; init; }
    public string? ReportName { get; init; }
    public bool IsHiddenSystemRule { get; init; }
    public IReadOnlyList<ReportRecipient> Recipients { get; init; } = [];

    public IReadOnlyList<string> RecipientEmails => Recipients.Select(static recipient => recipient.Email).ToArray();
}

public sealed record ReportRecipient(Guid UserId, string Email);

public sealed record SiteReportData
{
    public Guid Id { get; init; }
    public string SiteName { get; init; } = string.Empty;
    public DateTimeOffset CreateDate { get; init; }
    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? Postcode { get; init; }
    public string? City { get; init; }
    public string? County { get; init; }
    public string? Contracts { get; init; }
    public string? CompanyName { get; init; }
    public Guid? CompanyId { get; init; }
    public IReadOnlyList<MonitorReportData> Monitors { get; init; } = [];
    public ReportInsights? Insights { get; init; }

    public string SiteAddress => string.Join(
        ", ",
        new[] { AddressLine1, AddressLine2, City, County, Postcode }.Where(static value => !string.IsNullOrWhiteSpace(value)));
}

public sealed record MonitorReportData
{
    public Guid Id { get; init; }
    public bool Active { get; init; }
    public Guid? DeploymentId { get; init; }
    public string? FleetNumber { get; init; }
    public string SerialId { get; init; } = string.Empty;
    public MonitorType TypeOfMonitor { get; init; }
    public bool Offline { get; init; }
    public bool HasAlerts { get; init; }
    public bool HasCautions { get; init; }
    public float? Latitude { get; init; }
    public float? Longitude { get; init; }
    public DateTimeOffset? StartDate { get; init; }
    public DateTimeOffset? EndDate { get; init; }
    public DateTimeOffset EffectiveFrom { get; init; }
    public DateTimeOffset EffectiveTo { get; init; }
    public string? What3Words { get; init; }
    public DateTimeOffset? LastDataTime { get; init; }
    public string? Location { get; init; }
    public DateTimeOffset? CalibrationDate { get; init; }
    public IReadOnlyList<AlertRuleData> AlertRules { get; init; } = [];
    public IReadOnlyList<NotificationData> Notifications { get; init; } = [];
    public IReadOnlyList<MeasurementPoint> DustHourlyAverage { get; init; } = [];
    public IReadOnlyList<MeasurementPoint> DustDailyAverage { get; init; } = [];
    public IReadOnlyList<MeasurementPoint> NoiseHourlyAverage { get; init; } = [];
    public IReadOnlyList<MeasurementPoint> NoiseDailyAverage { get; init; } = [];
    public IReadOnlyList<MeasurementPoint> NoiseSiteAverage { get; init; } = [];
    public IReadOnlyList<MeasurementPoint> VibrationDailyPeak { get; init; } = [];

    public string Unit => TypeOfMonitor switch
    {
        MonitorType.Dust => "ug/m3",
        MonitorType.Noise => "dB",
        MonitorType.Vibration => "mm/s",
        _ => string.Empty
    };
}

public sealed record AlertRuleData(
    AlertType AlertType,
    string Field,
    decimal Threshold,
    int? AveragingPeriodSeconds,
    string? Unit,
    string? Name,
    int TriggeredCount,
    string? LatestClosedNote = null)
{
    public string? AveragingPeriodLabel => AveragingPeriodSeconds is null
        ? null
        : $"{Math.Max(1, AveragingPeriodSeconds.Value / 60)} mins";
}

public sealed record NotificationData(
    AlertType AlertType,
    DateTimeOffset CreatedAt,
    string Field,
    decimal Threshold,
    decimal Level,
    int? AveragingPeriodSeconds,
    DateTimeOffset? ClosedAt,
    string? ClosedNote,
    string? Message);

public sealed record MeasurementPoint(DateTimeOffset MeasuredAt, decimal Value, string? Label = null);

public sealed record ReportGraph(
    string Title,
    MonitorType MonitorType,
    string AveragePeriodLabel,
    string Unit,
    IReadOnlyList<ReportGraphSeries> Series,
    IReadOnlyList<ReportGraphLimit> Limits);

public sealed record ReportGraphSeries(string Name, IReadOnlyList<MeasurementPoint> Points);

public sealed record ReportGraphLimit(AlertType AlertType, decimal Value, string Unit, string Label);

public sealed record ReportInsights(
    ReportExecutiveSummary ExecutiveSummary,
    IReadOnlyList<ReportAlertHeatmap> AlertHeatmaps,
    string Narrative);

public sealed record ReportExecutiveSummary(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    IReadOnlyList<MonitorTypeExecutiveSummary> MonitorTypes)
{
    public int TotalAlertBreaches => MonitorTypes.Sum(static item => item.AlertBreaches);

    public int TotalCautionBreaches => MonitorTypes.Sum(static item => item.CautionBreaches);
}

public sealed record MonitorTypeExecutiveSummary(
    MonitorType MonitorType,
    int MonitorCount,
    int AlertBreaches,
    int CautionBreaches,
    DateOnly? WorstDay,
    int WorstDayBreaches,
    int? WorstHour,
    int WorstHourBreaches,
    ReportTrafficLightStatus Status);

public sealed record ReportAlertHeatmap(
    MonitorType MonitorType,
    IReadOnlyList<ReportAlertHeatmapCell> Cells);

public sealed record ReportAlertHeatmapCell(
    DateOnly Day,
    int Hour,
    int AlertCount,
    int CautionCount,
    decimal MaxLevel);

public sealed record RenderedReport(string FileName, string ContentType, byte[] Content);

public sealed record CustomerLogo(byte[] Content, string ContentType);

public sealed record StoredReport(Guid Id, Uri Uri);

public sealed record GeneratedReport(Guid ReportId, Guid ReportRuleId, Uri ReportUri, DateTimeOffset PeriodStartUtc, DateTimeOffset PeriodEndUtc);

public sealed record ReportDeliveryRecipient([property: EmailAddress] string Email);
