using Svantek.Model.Http;
using SvantekMonitor.model.dto;

namespace Svantek.Model.Dto
{
    // Summary: Captures legacy Svantek status metadata used by tests and compatibility seed data.
    // Major updates:
    // - 2026-06-18 Warning remediation: restored compatibility model for Svantek test fixtures.
    public sealed class NoiseMonitorStatus
    {
        public const string ACTIVE = "Active";

        public NoiseMonitorStatus(
            DateTime statusTime,
            string status,
            int errorCount,
            string batteryVoltage,
            DateTime calibrationDate,
            DateTime filterChangeDate,
            string pumpHours)
        {
            StatusTime = statusTime;
            Status = status;
            ErrorCount = errorCount;
            BatteryVoltage = batteryVoltage;
            CalibrationDate = calibrationDate;
            FilterChangeDate = filterChangeDate;
            PumpHours = pumpHours;
        }

        public DateTime StatusTime { get; }
        public string Status { get; }
        public int ErrorCount { get; }
        public string BatteryVoltage { get; }
        public DateTime CalibrationDate { get; }
        public DateTime FilterChangeDate { get; }
        public string PumpHours { get; }
    }

    // Summary: Represents monitor catalog and status metadata received from Svantek.
    // Major updates:
    // - 2026-06-18 Warning remediation: restored fixture-friendly constructor and optional location metadata.
    // - 2026-06-18 Warning remediation: added default-safe string initialisation for nullable analysis.
    public class NoiseMonitorDto : DtoBase
    {
        public static readonly int MONITOR_TYPE_NOISE = 1;
        public Guid Id { get; set; }
        public string FleetNr { get; set; } = string.Empty;
        public string SerialId { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public int PointId { get; set; }
        public DateTime ListedAtTime { get; set; }
        public string Model { get; set; } = string.Empty;
        public string CustomerDisplayName { get; set; } = string.Empty;
        public string Manufacturer { get; } = "Svantek";
        public int TypeOfMonitor { get; } = MONITOR_TYPE_NOISE;
        public string FirmwareVersion { get; set; } = string.Empty;

        //For sensor table
        public bool Active { get; set; }
        public string LastLogin { get; set; } = string.Empty;
        public string LastLogout { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public string LastStatusTimestamp { get; set; } = string.Empty;
        public int BatteryCharge { get; set; }
        public int BatteryTimeToEmpty { get; set; }
        public string PowerSource { get; set; } = string.Empty;
        public bool IsBatteryCharging { get; set; }
        public int GsmSignalQuality { get; set; }
        public string MeasurementState { get; set; } = string.Empty;

        //CustomerId LocationId Latitude Longitude LocationAddress TimeZone
        public DateTime? LastDataTime { get; set; }
        public bool Offline { get; set; }
        public float Latitude { get; set; }
        public float Longitude { get; set; }
        public string? Address { get; set; }
        public string? TimeZone { get; set; }
        public NoiseMonitorStatus? MonitorStatus { get; set; }

        public NoiseMonitorDto() { }

        public NoiseMonitorDto(
            Guid id,
            DateTime listedAtTime,
            DateTime? lastDataTime,
            string serialId,
            string model,
            string firmwareVersion,
            string manufacturer,
            string fleetNr,
            float latitude,
            float longitude,
            string address,
            string timeZone,
            string customerDisplayName,
            bool offline,
            NoiseMonitorStatus monitorStatus)
        {
            Id = id;
            ListedAtTime = listedAtTime;
            LastDataTime = lastDataTime;
            SerialId = serialId;
            Model = model;
            FirmwareVersion = firmwareVersion;
            FleetNr = fleetNr;
            Latitude = latitude;
            Longitude = longitude;
            Address = address;
            TimeZone = timeZone;
            CustomerDisplayName = customerDisplayName;
            Offline = offline;
            MonitorStatus = monitorStatus;
            LastStatusTimestamp = monitorStatus.StatusTime.ToString("O");
            IsOnline = string.Equals(monitorStatus.Status, NoiseMonitorStatus.ACTIVE, StringComparison.OrdinalIgnoreCase);
        }

    }

}
