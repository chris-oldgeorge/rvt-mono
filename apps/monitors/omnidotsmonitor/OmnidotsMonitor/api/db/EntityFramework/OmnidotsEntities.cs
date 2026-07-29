namespace Omnidots.Api.Db.EntityFramework;

public sealed class OmnidotsMonitorStatusEntity
{
    public Guid Id { get; set; }
    public string SerialId { get; set; } = string.Empty;
    public int? MeasurementDuration { get; set; }
    public double? DataSaveLevel { get; set; }
    public bool VdvEnabled { get; set; }
    public string? VdvX { get; set; }
    public string? VdvY { get; set; }
    public string? VdvZ { get; set; }
    public int? VdvPeriod { get; set; }
    public double? TraceSaveLevel { get; set; }
    public double? TracePreTrigger { get; set; }
    public double? TracePostTrigger { get; set; }
    public double? AlarmValue { get; set; }
    public double? FlatLevel { get; set; }
    public bool DisableLed { get; set; }
    public int LogFlushInterval { get; set; }
    public string? GuideLine { get; set; }
    public string BuildingLevel { get; set; } = string.Empty;
    public bool VectorEnabled { get; set; }
    public bool AtopEnabled { get; set; }
    public bool VtopEnabled { get; set; }
}

public sealed class OmnidotsSensorEntity
{
    public Guid Id { get; set; }
    public string SerialId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime Lastseen { get; set; }
    public int BatteryCharge { get; set; }
    public string ConnectedUsing { get; set; } = string.Empty;
    public bool Online { get; set; }
}

public sealed class OmnidotsPeakLevelEntity
{
    public string SerialId { get; set; } = string.Empty;
    public DateTime SampleTime { get; set; }
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

public sealed class OmnidotsVeffLevelEntity
{
    public string SerialId { get; set; } = string.Empty;
    public DateTime SampleTime { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Z { get; set; }
}

public sealed class OmnidotsVdvLevelEntity
{
    public string SerialId { get; set; } = string.Empty;
    public DateTime SampleTime { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Z { get; set; }
    public string? VdvX { get; set; }
    public string? VdvY { get; set; }
    public string? VdvZ { get; set; }
}

public sealed class OmnidotsErrorMessageEntity
{
    public string Tag { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public DateTime ErrorTime { get; set; }
}

public sealed class OmnidotsTraceIndexEntity
{
    public Guid Id { get; set; }
    public string? SerialId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public sealed class OmnidotsImportCursorEntity
{
    public string SerialId { get; set; } = string.Empty;
    public string Series { get; set; } = string.Empty;

    /// <summary>The newest committed sample instant, represented in UTC.</summary>
    public DateTime LastSampleAt { get; set; }

    /// <summary>The instant at which this cursor last advanced, represented in UTC.</summary>
    public DateTime UpdatedAt { get; set; }
}

public sealed class OmnidotsTraceEntity
{
    public Guid TraceId { get; set; }
    public int SampleIndex { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Z { get; set; }
}
