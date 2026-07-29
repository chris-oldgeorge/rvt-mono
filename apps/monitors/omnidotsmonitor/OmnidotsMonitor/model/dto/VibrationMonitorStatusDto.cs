
namespace Omnidots.Model.Dto
{

    public class VibrationMonitorStatusDto
    {
        public string SerialId { get; }
        public int MeasurementDuration { get; }
        public double DataSaveLevel { get; }
        public bool VdvEnabled { get; }
        public string? VdvX { get; }
        public string? VdvY { get; }
        public string? VdvZ { get; }
        public int VdvPeriod { get; }
        public double TraceSaveLevel { get; }
        public double TracePreTrigger { get; }
        public double TracePostTrigger { get; }
        public double AlarmValue { get; }
        public double? FlatLevel { get; }
        public bool DisableLed { get; }
        public int LogFlushInterval { get; }
        public string? GuideLine { get; }
        public string BuildingLevel { get; }
        public bool VectorEnabled { get; }
        public bool AtopEnabled { get; }
        public bool VtopEnabled { get; }

        public VibrationMonitorStatusDto(string serialId, int measurementDuration,
            double dataSaveLevel, bool vdvEnabled,
            string? vdvX, string? vdvY, string? vdvZ, int vdvPeriod,
            double traceSaveLevel, double tracePreTrigger, double tracePostTrigger,
            double alarmValue, double? flatLevel, bool disableLed, int logFlushInterval,
            string? guideLine, string buildingLevel, bool vectorEnabled,
            bool atopEnabled, bool vtopEnabled)
        {
            SerialId = serialId;
            MeasurementDuration = measurementDuration;
            DataSaveLevel = dataSaveLevel;
            VdvEnabled = vdvEnabled;
            VdvX = vdvX;
            VdvY = vdvY;
            VdvZ = vdvZ;
            VdvPeriod = vdvPeriod;
            TraceSaveLevel = traceSaveLevel;
            TracePreTrigger = tracePreTrigger;
            TracePostTrigger = tracePostTrigger;
            AlarmValue = alarmValue;
            FlatLevel = flatLevel;
            DisableLed = disableLed;
            LogFlushInterval = logFlushInterval;
            GuideLine = guideLine;
            BuildingLevel = buildingLevel;
            VectorEnabled = vectorEnabled;
            AtopEnabled = atopEnabled;
            VtopEnabled = vtopEnabled;
        }
    }
}
