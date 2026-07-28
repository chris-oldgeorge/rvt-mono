using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Svantek.Api.SvantekApi;

namespace SvantekMonitor.model.dto;

// Summary: Represents a deployed Svantek monitor selected for data collection and battery checks.
// Major updates:
// - 2026-06-18: Inherits from DtoBase after C# naming cleanup.
public class NoiseMonitorReadDto(Guid Id, string FleetNr, string SerialId, int ProjectId, int PointId, DateTime ListedAtTime, DateTime? LastDataTime, DateTime? LastStatusTimestamp, DateTime DeployedStart, bool Offline, Svantek.Api.SvantekApi.BatteryAlertType BatteryStatus, int BatteryCharge) : DtoBase
{
    public Guid Id { get; set; } = Id;
    public string FleetNr { get; set; } = FleetNr;
    public string SerialId { get; set; } = SerialId;
    public int ProjectId { get; set; } = ProjectId;
    public int PointId { get; set; } = PointId;
    public DateTime ListedAtTime { get; set; } = ListedAtTime;
    public DateTime? LastDataTime { get; set; } = LastDataTime;
    public DateTime? LastStatusTimestamp { get; set; } = LastStatusTimestamp;
    public DateTime DeployedStart { get; set; } = DeployedStart;
    public bool Offline { get; set; } = Offline;
    public int BatteryCharge { get; set; } = BatteryCharge;
    public BatteryAlertType BatteryStatus { get; set; } = BatteryStatus;

    //Need a proper start date, the date since last read or deployment start.
    public DateTime PeriodStartDate
    {
        get
        {
            return (LastDataTime != null && LastDataTime > DeployedStart) ? (DateTime)LastDataTime : DeployedStart;
        }
    }
}
