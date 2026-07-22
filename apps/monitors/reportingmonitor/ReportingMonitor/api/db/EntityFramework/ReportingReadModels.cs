namespace ReportingMonitor.Api.Db.EntityFramework;

public sealed class SiteSearchRow
{
    public Guid Id { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public DateTimeOffset CreateDate { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Postcode { get; set; }
    public string? City { get; set; }
    public string? County { get; set; }
    public string? Contracts { get; set; }
    public string? CompanyName { get; set; }
    public Guid? CompanyId { get; set; }
    public bool Archived { get; set; }
}

public sealed class MonitorReportRow
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public bool Active { get; set; }
    public Guid? DeploymentId { get; set; }
    public string? FleetNumber { get; set; }
    public string SerialId { get; set; } = string.Empty;
    public int TypeOfMonitor { get; set; }
    public bool Offline { get; set; }
    public bool Alerts { get; set; }
    public bool Cautions { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public string? What3Words { get; set; }
    public DateTimeOffset? LastDataTime { get; set; }
    public string? Location { get; set; }
    public DateTimeOffset? CalibrationDate { get; set; }
}

public sealed class ReportingDeploymentRow
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
}

public sealed class ReportingContractRow
{
    public Guid Id { get; set; }
    public DateTimeOffset? OnHireDate { get; set; }
    public DateTimeOffset? OffHireDate { get; set; }
}

public sealed class ReportRecipientRow
{
    public Guid Id { get; set; }
    public Guid ReportRuleId { get; set; }
    public Guid UserId { get; set; }
}

public sealed class ReportingNotificationRow
{
    public Guid Id { get; set; }
    public Guid MonitorId { get; set; }
    public DateTimeOffset NotificationTime { get; set; }
    public double LimitOn { get; set; }
    public int AveragingPeriod { get; set; }
    public double Level { get; set; }
    public DateTimeOffset? ClosedTime { get; set; }
    public string? ClosedByNote { get; set; }
    public string AlertField { get; set; } = string.Empty;
    public int AlertType { get; set; }
}

public sealed class ReportingAlertRuleRow
{
    public Guid Id { get; set; }
    public Guid? MonitorId { get; set; }
    public string AlertField { get; set; } = string.Empty;
    public double LimitOn { get; set; }
    public int AlertType { get; set; }
    public int AveragingPeriod { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed class DustHourlyAverageRow
{
    public string SerialId { get; set; } = string.Empty;
    public int AveragingPeriodSeconds { get; set; }
    public DateTimeOffset SampleTime { get; set; }
    public double? Pm10 { get; set; }
}

public sealed class DustDailyAverageRow
{
    public string SerialId { get; set; } = string.Empty;
    public DateTimeOffset SampleTime { get; set; }
    public double? Pm10 { get; set; }
}

public sealed class NoiseHourlyAverageRow
{
    public string SerialId { get; set; } = string.Empty;
    public DateTimeOffset SampleTime { get; set; }
    public double? Laeq { get; set; }
}

public sealed class NoiseDailyAverageRow
{
    public string SerialId { get; set; } = string.Empty;
    public DateTimeOffset SampleTime { get; set; }
    public double? Laeq { get; set; }
}

public sealed class NoiseSiteAverageRow
{
    public string SerialId { get; set; } = string.Empty;
    public DateTimeOffset SampleTime { get; set; }
    public double? Laeq { get; set; }
}

public sealed class VibrationDailyPeakRow
{
    public string SerialId { get; set; } = string.Empty;
    public DateTimeOffset SampleTime { get; set; }
    public double? XVtop { get; set; }
    public double? YVtop { get; set; }
    public double? ZVtop { get; set; }
}
