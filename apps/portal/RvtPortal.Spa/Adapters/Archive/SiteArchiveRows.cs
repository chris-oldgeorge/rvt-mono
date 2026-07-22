// File summary: Defines typed row projections used by site archive CSV exports.
// Major updates:
// - 2026-07-09 pending Moved site archive row DTOs out of the archive orchestration service.

namespace RvtPortal.Spa.Adapters.Archive;

internal sealed class MonitorArchiveRow
{
    public string? Monitor { get; set; }
    public string? SerialId { get; set; }
    public string? Type { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? What3words { get; set; }
    public string? ContractNumber { get; set; }
    public DateTime? OnHireDate { get; set; }
    public DateTime? OffHireDate { get; set; }
}

internal sealed class BreachArchiveRow
{
    public string? Monitor { get; set; }
    public string? SerialId { get; set; }
    public string? Type { get; set; }
    public string? AlertType { get; set; }
    public DateTime? NotificationTime { get; set; }
    public double? LimitOn { get; set; }
    public double? Level { get; set; }
    public string? Period { get; set; }
    public string? Parameter { get; set; }
    public DateTime? ClosedTime { get; set; }
    public Guid? ClosedByUser { get; set; }
    public string? ClosedNote { get; set; }
}

internal sealed class DustArchiveRow
{
    public string? Monitor { get; set; }
    public string? SerialId { get; set; }
    public string? Period { get; set; }
    public DateTime? SampleTime { get; set; }
    public double? Pm1 { get; set; }
    public double? Pm2_5 { get; set; }
    public double? Pm10 { get; set; }
    public double? PmTotal { get; set; }
}

internal sealed class NoiseArchiveRow
{
    public string? Monitor { get; set; }
    public string? SerialId { get; set; }
    public DateTime? SampleTime { get; set; }
    public double? LAeq { get; set; }
    public double? LAmax { get; set; }
    public double? LA90 { get; set; }
    public double? LA10 { get; set; }
    public double? LCeq { get; set; }
    public double? LCmax { get; set; }
    public double? LC90 { get; set; }
    public double? LC10 { get; set; }
}

internal sealed class VibrationArchiveRow
{
    public string? Monitor { get; set; }
    public string? SerialId { get; set; }
    public DateTime? SampleTime { get; set; }
    public double? XFdom { get; set; }
    public double? XVtop { get; set; }
    public double? XVtopOverflow { get; set; }
    public double? YFdom { get; set; }
    public double? YVtop { get; set; }
    public double? YVtopOverflow { get; set; }
    public double? ZFdom { get; set; }
    public double? ZVtop { get; set; }
    public double? ZVtopOverflow { get; set; }
}

internal sealed class TraceListArchiveRow
{
    public string? Monitor { get; set; }
    public string? SerialId { get; set; }
    public Guid? TraceId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

internal sealed class TraceDataArchiveRow
{
    public Guid? TraceId { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Z { get; set; }
}

internal sealed class ReportArchiveRow
{
    public string? ReportLink { get; set; }
}
