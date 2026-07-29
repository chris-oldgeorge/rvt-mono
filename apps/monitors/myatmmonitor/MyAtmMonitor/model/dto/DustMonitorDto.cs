
using MyAtm.Api;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;

namespace MyAtm.Model.Dto
{

    public record DustMonitorDto
    {
        public static readonly int MONITOR_TYPE_DUST = 0;

        public Guid Id { get; }
        public int CustomerId { get; }
        public DateTime ListedAtTime { get; }
        public DateTime? LastDataTime1Min { get; set; }
        public DateTime? LastDataTime15Min { get; set; }
        public DateTime? LastDataTime1Hour { get; set; }
        public DateTime? LastDataTime24Hour { get; set; }
        public string SerialId { get; }
        public string Model { get; }
        public string FirmwareVersion { get; }
        public string Manufacturer { get; }
        public int TypeOfMonitor { get; } = MONITOR_TYPE_DUST;
        public string? FleetNr { get; set; }
        public int LocationId { get; }
        public float Latitude { get; }
        public float Longitude { get; }
        public string? Address { get; }
        public string? TimeZone { get; }
        public string? CustomerDisplayName { get; }
        public bool Offline { get; set; }

        public DustMonitorDto(Json.DeviceInfo.DustMonitorInfo device)
        {
            Id = Guid.NewGuid();
            CustomerId = device.CurrentCustomerAssignment!.customer!.id;
            ListedAtTime = DateTime.UtcNow;
            SerialId = device.SerialNumber!;
            Model = device.Model!;
            LocationId = device.CurrentLocation!.Id;
            Latitude = (float)device.CurrentLocation!.Latitude;
            Longitude = (float)device.CurrentLocation.Longitude;
            Address = device.CurrentLocation!.Address;
            TimeZone = device.CurrentLocation!.TimeZone;
            CustomerDisplayName = device.CurrentCustomerAssignment!.customer.displayName;
            FirmwareVersion = "not set";
            if (device.Configuration != null && device.Configuration!.FirmwareVersion != null)
                FirmwareVersion = device.Configuration!.FirmwareVersion!;
            Manufacturer = "Palas GmbH";
            Offline = false;
        }

        //  All devices from https://api.my-atmosphere.cloud/api are produced by Palas GmbH.

        public DustMonitorDto(Guid id, int customerId, DateTime listedAtTime, string serialId,
                                 string model, int locationId, float latitude, float longitude, string? address,
                                 string? timeZone, string? customerDisplayName, DateTime? lastDataTime1Min,
                                 DateTime? lastDataTime15Min, DateTime? lastDataTime1Hour, DateTime? lastDataTime24Hour,
                                 string manufacturer, string firmwareVersion, string? fleetNr,
                                 bool offline)
        {
            Id = id;
            CustomerId = customerId;
            ListedAtTime = listedAtTime;
            SerialId = serialId!;
            Model = model;
            LocationId = locationId;
            Latitude = latitude;
            Longitude = longitude;
            Address = address;
            TimeZone = timeZone;
            CustomerDisplayName = customerDisplayName;
            LastDataTime1Min = lastDataTime1Min;
            LastDataTime15Min = lastDataTime15Min;
            LastDataTime1Hour = lastDataTime1Hour;
            LastDataTime24Hour = lastDataTime24Hour;
            Manufacturer = manufacturer;
            FirmwareVersion = firmwareVersion;
            FleetNr = fleetNr;
            Offline = offline;
        }

        public DateTime? GetLastDataTime(Period period)
        {
            switch (period)
            {
                case Period.Minutes1:
                    return LastDataTime1Min;
                case Period.Minutes15:
                    return LastDataTime15Min;
                case Period.Hours1:
                    return LastDataTime1Hour;
                case Period.Hours24:
                    return LastDataTime24Hour;
                default:
                    throw AdapterException.Of("GetLastDataTime Unknown Period " + period);
            }
        }


        public static int PeriodToSeconds(Period period)
        {
            int periodSeconds = 0;
            switch (period)
            {
                case Period.Minutes1:
                    periodSeconds = 60;
                    break;
                case Period.Minutes15:
                    periodSeconds = 900;
                    break;
                case Period.Hours1:
                    periodSeconds = 3600;
                    break;
                case Period.Hours8:
                    periodSeconds = 28800;
                    break;
                case Period.Hours24:
                    periodSeconds = 86400;
                    break;

                default:
                    throw AdapterException.Of("ReadRules Unknown Period " + period);
            }
            return periodSeconds;
        }
    }
}
