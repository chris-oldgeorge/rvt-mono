namespace Rvt.Monitor.Common.Data.Entities;

public sealed class MonitorEntity
{
    public Guid Id { get; set; }
    public string? FleetNr { get; set; }
    public string SerialId { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public DateTime ListedAtTime { get; set; }
    public string Model { get; set; } = string.Empty;
    public int? LocationId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationAddress { get; set; }
    public string? TimeZone { get; set; }
    public string? CustomerDisplayName { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string FirmwareVersion { get; set; } = string.Empty;
    public int TypeOfMonitor { get; set; }
    public bool? Offline { get; set; }
    public DateTime? LastDataTime1Min { get; set; }
    public DateTime? LastDataTime15Min { get; set; }
    public DateTime? LastDataTime1Hour { get; set; }
    public DateTime? LastDataTime24Hour { get; set; }
    public byte? BatteryStatus { get; set; }
}
