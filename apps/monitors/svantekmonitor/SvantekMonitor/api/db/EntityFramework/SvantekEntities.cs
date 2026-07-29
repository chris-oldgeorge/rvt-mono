namespace Svantek.Api.Db.EntityFramework;

public sealed class SvantekMonitorStatusEntity
{
    public string SerialId { get; set; } = string.Empty;
    public DateTime UpdateTime { get; set; }
    public string Status { get; set; } = "Active";
    public int ErrorCount { get; set; }
    public string? BatteryVoltage { get; set; }
    public DateTime? CalibrationDate { get; set; }
    public DateTime? FilterChangeDate { get; set; }
    public string? PumpHours { get; set; }
    public int? ProjectId { get; set; }
    public int? PointId { get; set; }
    public bool? Active { get; set; }
    public string? LastLogin { get; set; }
    public string? LastLogout { get; set; }
    public bool? IsOnline { get; set; }
    public string? LastStatusTimestamp { get; set; }
    public int? BatteryCharge { get; set; }
    public int? BatteryTimeToEmpty { get; set; }
    public string? PowerSource { get; set; }
    public bool? IsBatteryCharging { get; set; }
    public int? GsmSignalQuality { get; set; }
    public string? MeasurementState { get; set; }
}

public sealed class SvantekNoiseLevelEntity
{
    public string SerialId { get; set; } = string.Empty;
    public DateTime SampleTime { get; set; }
    public double? LAeq { get; set; }
    public double? LAmax { get; set; }
    public double? LA90 { get; set; }
    public double? LA10 { get; set; }
    public double? LCeq { get; set; }
    public double? LCmax { get; set; }
    public double? LC90 { get; set; }
    public double? LC10 { get; set; }
}

public sealed class SvantekNoise8HourAverageEntity
{
    public string SerialId { get; set; } = string.Empty;
    public DateTime SampleTime { get; set; }
    public double? LAeq { get; set; }
    public double? LAmax { get; set; }
    public double? LA90 { get; set; }
    public double? LA10 { get; set; }
    public double? LCeq { get; set; }
    public double? LCmax { get; set; }
    public double? LC90 { get; set; }
    public double? LC10 { get; set; }
    public int NumberOfSamples { get; set; }
}

public sealed class SvantekErrorMessageEntity
{
    public string Tag { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public DateTime ErrorTime { get; set; }
}
