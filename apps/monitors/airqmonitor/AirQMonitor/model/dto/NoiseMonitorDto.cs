using System.Globalization;
using AirQ.Model.Http;

namespace AirQ.Model.Dto
{

    // Summary: Represents AirQ monitor metadata and status normalized for persistence.
    // Major updates:
    // - 2026-06-18 Test stability: parse AirQ API coordinates with invariant culture.
    public class NoiseMonitorStatus
    {
        public static readonly string ACTIVE = "Active";

        public DateTime UpdateTime { get; }
        public string Status { get; }
        public int ErrorCount { get; set; }
        public string? BatteryVoltage { get; }
        public DateTime? CalibrationDate { get; }
        public DateTime? FilterChangeDate { get; }
        public string? PumpHours { get; }

        public NoiseMonitorStatus(DateTime updateTime, string status, int errorCount, string? batteryVoltage,
                                  DateTime? calibrationDate, DateTime? filterChangeDate, string? pumpHours)
        {
            UpdateTime = updateTime;
            Status = status;
            ErrorCount = errorCount;
            BatteryVoltage = batteryVoltage;
            CalibrationDate = calibrationDate;
            FilterChangeDate = filterChangeDate;
            PumpHours = pumpHours;
        }

        public bool IsMonitorActive()
        {
            if (!ACTIVE.Equals(Status))
            {
                return false;
            }
            return true;
        }
    }

    public class NoiseMonitorDto
    {
        public static readonly int MONITOR_TYPE_NOISE = 1;

        public Guid Id { get; }
        public DateTime ListedAtTime { get; }
        public DateTime? LastDataTime { get; set; }
        public string SerialId { get; }
        public string Model { get; }
        public string FirmwareVersion { get; }
        public string Manufacturer { get; }
        public int TypeOfMonitor { get; } = MONITOR_TYPE_NOISE;
        public string FleetNr { get; }
        public float Latitude { get; }
        public float Longitude { get; }
        public string? Address { get; }
        public string? TimeZone { get; }
        public string? CustomerDisplayName { get; }
        public bool Offline { get; set; }

        public NoiseMonitorStatus MonitorStatus { get; }


        public NoiseMonitorDto(Guid id, DateTime listedAtTime, DateTime? lastDataTime, string serialId, string model,
                             string firmwareVersion, string manufacturer, string? fleetNr,
                             float latitude, float longitude, string? address, string? timeZone,
                             string? customerDisplayName, bool offline,
                             NoiseMonitorStatus monitorStatus)
        {
            Id = id;
            ListedAtTime = listedAtTime;
            LastDataTime = lastDataTime;
            SerialId = serialId;
            Model = model;
            FirmwareVersion = firmwareVersion;
            Manufacturer = manufacturer;
            FleetNr = fleetNr ?? string.Empty;
            Latitude = latitude;
            Longitude = longitude;
            Address = address;
            TimeZone = timeZone;
            CustomerDisplayName = customerDisplayName;
            MonitorStatus = monitorStatus;
            Offline = offline;
        }

        public NoiseMonitorDto(InstrumentResponse monitor, MetaDataResponse metaData)
        {
            var utcNow = DateTime.UtcNow;
            Id = Guid.NewGuid();
            ListedAtTime = utcNow;
            SerialId = monitor.InstrumentID!;
            Model = monitor.Name!;
            FirmwareVersion = "Unknown";
            Manufacturer = "Turnkey";
            FleetNr = string.Empty;
            Latitude = float.Parse(monitor.Latitude!, CultureInfo.InvariantCulture);
            Longitude = float.Parse(monitor.Longitude!, CultureInfo.InvariantCulture);
            Address = string.Format("{0}, {1}, {2}", monitor.Location!, monitor.City!, monitor.Country);
            TimeZone = monitor.TimeZone!;
            CustomerDisplayName = monitor.Type;
            Offline = false;

            MonitorStatus = new NoiseMonitorStatus(updateTime: utcNow, status: monitor.Status!, errorCount: 0,
                                                   batteryVoltage: metaData.BatteryVoltage,
                                                   calibrationDate: metaData.CalibrationDate,
                                                   filterChangeDate: metaData.FilterChangeDate,
                                                   pumpHours: metaData.PumpHours);

        }
    }
}
