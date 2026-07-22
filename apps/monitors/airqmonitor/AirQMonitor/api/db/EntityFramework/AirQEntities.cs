namespace AirQ.Api.Db.EntityFramework;

public sealed class AirQNoiseLevelEntity
{
    public string SerialId { get; set; } = string.Empty;
    public DateTime SampleTime { get; set; }
    public double LAeq { get; set; }
    public double LAmax { get; set; }
    public double LA90 { get; set; }
    public double LA10 { get; set; }
    public double LCeq { get; set; }
    public double LCmax { get; set; }
    public double LC90 { get; set; }
    public double LC10 { get; set; }
}

public sealed class AirQMonitorStatusEntity
{
    public string Id { get; set; } = string.Empty;
    public string SerialId { get; set; } = string.Empty;
    public DateTime UpdateTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ErrorCount { get; set; }
    public string? BatteryVoltage { get; set; }
    public DateTime? CalibrationDate { get; set; }
    public DateTime? FilterChangeDate { get; set; }
    public string? PumpHours { get; set; }
}

public sealed class AirQErrorMessageEntity
{
    public string Tag { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public DateTime ErrorTime { get; set; }
}

public sealed class AirQNoise8HourAverageEntity
{
    public string SerialId { get; set; } = string.Empty;
    public DateTime SampleTime { get; set; }
    public double LAeq { get; set; }
    public double LAmax { get; set; }
    public double LA90 { get; set; }
    public double LA10 { get; set; }
    public double LCeq { get; set; }
    public double LCmax { get; set; }
    public double LC90 { get; set; }
    public double LC10 { get; set; }
    public int NumberOfSamples { get; set; }
}
