using Omnidots.Api.Db;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Utilities;
using static Omnidots.Api.OmnidotsApi;

namespace Omnidots.Model.Dto
{

    public class VibrationMonitorDto
    {
        public static readonly int MONITOR_TYPE_VIBRATION = 2;

        public Guid Id { get; }
        public DateTime ListedAtTime { get; }
        public DateTime? LastDataTime { get; set; }
        public string SerialId { get; }
        public string Model { get; }
        public string FirmwareVersion { get; }
        public string Manufacturer { get; }
        public int TypeOfMonitor { get; } = MONITOR_TYPE_VIBRATION;
        public string? FleetNr { get; }
        public float Latitude { get; }
        public float Longitude { get; }
        public string? Address { get; }
        public string? TimeZone { get; }
        public string? CustomerDisplayName { get; }
        public VibrationMonitorStatusDto MonitorStatus { get; }
        public SensorDto? Sensor;
        public bool Offline { get; set; }
        public BatteryAlertType BatteryStatus { get; set; }
        public DateTime? LastSeen { get; set; }
        public DateTime? DeployDate { get; set; }


        public VibrationMonitorDto(Guid id, DateTime listedAtTime, DateTime? lastDataTime, string serialId, string model,
                             string firmwareVersion, string manufacturer, string? fleetNr,
                             float latitude, float longitude, string? address, string? timeZone, string? customerDisplayName,
                             VibrationMonitorStatusDto monitorStatus, SensorDto? sensor, bool offline, BatteryAlertType batteryStatus,
                             DateTime? lastSeen, DateTime? deployDate)
        {
            Id = id;
            ListedAtTime = DateTimeUtil.TruncateMillis(listedAtTime);
            LastDataTime = lastDataTime != null ? DateTimeUtil.TruncateMillis((DateTime)lastDataTime!) : null;
            SerialId = serialId;
            Model = model;
            FirmwareVersion = firmwareVersion;
            Manufacturer = manufacturer;
            FleetNr = fleetNr;
            Latitude = latitude;
            Longitude = longitude;
            Address = address;
            TimeZone = timeZone;
            CustomerDisplayName = customerDisplayName;
            MonitorStatus = monitorStatus;
            Sensor = sensor;
            Offline = offline;
            BatteryStatus = batteryStatus;
            LastSeen = lastSeen;
            DeployDate = deployDate;
        }

        public VibrationMonitorDto(MeasuringPoint measuringPoint)
        {

            Id = Guid.NewGuid();
            ListedAtTime = DateTime.UtcNow;
            SerialId = measuringPoint.Id!.ToString();

            Model = measuringPoint.Category != null ? measuringPoint.Category : OmnidotsProtocol.UNKNOWN;
            FirmwareVersion = OmnidotsProtocol.UNKNOWN;
            Manufacturer = OmnidotsProtocol.UNKNOWN;
            Address = OmnidotsProtocol.UNKNOWN;
            TimeZone = measuringPoint.TimeZone!;
            CustomerDisplayName = measuringPoint.Name;
            Offline = false;

            MonitorStatus = new VibrationMonitorStatusDto(
                serialId: SerialId,
                measurementDuration: measuringPoint.MeasurementDuration,
                dataSaveLevel: measuringPoint.DataSaveLevel,
                vdvEnabled: IsOnOff(measuringPoint.VdvEnabled),
                vdvX: measuringPoint.VdvX,
                vdvY: measuringPoint.VdvY,
                vdvZ: measuringPoint.VdvZ,
                vdvPeriod: measuringPoint.VdvPeriod,
                traceSaveLevel: measuringPoint.TraceSaveLevel,
                tracePreTrigger: measuringPoint.TracePreTrigger,
                tracePostTrigger: measuringPoint.TracePostTrigger,
                alarmValue: measuringPoint.AlarmValue,
                flatLevel: measuringPoint.FlatLevel,
                disableLed: measuringPoint.DisableLed,
                logFlushInterval: measuringPoint.LogFlushInterval,
                guideLine: measuringPoint.GuideLine,
                buildingLevel: measuringPoint.BuildingLevel ?? OmnidotsProtocol.UNSPECIFIED,
                vectorEnabled: IsOnOff(measuringPoint.VectorEnabled),
                atopEnabled: IsOnOff(measuringPoint.AtopEnabled),
                vtopEnabled: IsOnOff(measuringPoint.VtopEnabled));

            if (measuringPoint.Sensor != null)
            {
                var s = measuringPoint.Sensor!;
                Sensor = new SensorDto(serialId: SerialId, name: s.Name, lastseen: s.Lastseen, batteryCharge: s.BatteryCharge,
                                       connectedUsing: s.ConnectedUsing, online: s.Online);
            }
        }

        public DateTime GetLastDataTime()
        {
            if (LastDataTime == null)
                if (DeployDate == null) // It should never be null but just to ensure it doesn't crash.
                    return DateTimeUtil.JAN1_1970;
                else
                    return (DateTime)DeployDate!;
            else
                return ((DateTime)DeployDate! > (DateTime)LastDataTime!) ? (DateTime)DeployDate! : (DateTime)LastDataTime!; //If last read is before deploy it is for a previous deployment do use deployment date
        }

        private static bool IsOnOff(string? field)
        {
            if (field == null)
            {
                return false;
            }
            return String.Compare(OmnidotsProtocol.ON, field, StringComparison.OrdinalIgnoreCase) == 0;
        }
    }
}
