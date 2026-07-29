
namespace Omnidots.Model.Dto;


public class VibrationMonitorStatusDto(string serialId, int measurementDuration,
    double dataSaveLevel, bool vdvEnabled,
    string? vdvX, string? vdvY, string? vdvZ, int vdvPeriod,
    double traceSaveLevel, double tracePreTrigger, double tracePostTrigger,
    double alarmValue, double? flatLevel, bool disableLed, int logFlushInterval,
    string? guideLine, string buildingLevel, bool vectorEnabled,
    bool atopEnabled, bool vtopEnabled)
{
    public string SerialId { get; } = serialId;
    public int MeasurementDuration { get; } = measurementDuration;
    public double DataSaveLevel { get; } = dataSaveLevel;
    public bool VdvEnabled { get; } = vdvEnabled;
    public string? VdvX { get; } = vdvX;
    public string? VdvY { get; } = vdvY;
    public string? VdvZ { get; } = vdvZ;
    public int VdvPeriod { get; } = vdvPeriod;
    public double TraceSaveLevel { get; } = traceSaveLevel;
    public double TracePreTrigger { get; } = tracePreTrigger;
    public double TracePostTrigger { get; } = tracePostTrigger;
    public double AlarmValue { get; } = alarmValue;
    public double? FlatLevel { get; } = flatLevel;
    public bool DisableLed { get; } = disableLed;
    public int LogFlushInterval { get; } = logFlushInterval;
    public string? GuideLine { get; } = guideLine;
    public string BuildingLevel { get; } = buildingLevel;
    public bool VectorEnabled { get; } = vectorEnabled;
    public bool AtopEnabled { get; } = atopEnabled;
    public bool VtopEnabled { get; } = vtopEnabled;
}
