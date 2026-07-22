using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Svantek.Api.SvantekApi;

namespace SvantekMonitor.model.dto
{
    // Summary: Represents a deployed Svantek monitor selected for data collection and battery checks.
    // Major updates:
    // - 2026-06-18: Inherits from DtoBase after C# naming cleanup.
    public class NoiseMonitorReadDto : DtoBase
    {
        public Guid Id { get; set; }
        public string FleetNr { get; set; }
        public string SerialId { get; set; }
        public int ProjectId { get; set; }
        public int PointId { get; set; }
        public DateTime ListedAtTime { get; set; }
        public DateTime? LastDataTime { get; set; } //Time of last read/saved data sample
        public DateTime? LastStatusTimestamp { get; set; } //Last update time according to Svantek
        public DateTime DeployedStart { get; set; } //Time the Monitor was assigend to a site
        public bool Offline { get; set; }
        public int BatteryCharge { get; set; }
        public BatteryAlertType BatteryStatus { get; set; }

        //Need a proper start date, the date since last read or deployment start.
        public DateTime PeriodStartDate
        {
            get
            {
                return (LastDataTime != null && LastDataTime > DeployedStart) ? (DateTime)LastDataTime : DeployedStart;
            }
        }
        public NoiseMonitorReadDto(Guid Id, string FleetNr, string SerialId, int ProjectId, int PointId, DateTime ListedAtTime, DateTime? LastDataTime, DateTime? LastStatusTimestamp, DateTime DeployedStart, bool Offline, BatteryAlertType BatteryStatus, int BatteryCharge)
        {
            this.Id = Id;
            this.FleetNr = FleetNr;
            this.SerialId = SerialId;
            this.ProjectId = ProjectId;
            this.PointId = PointId;
            this.ListedAtTime = ListedAtTime;
            this.LastDataTime = LastDataTime;
            this.LastStatusTimestamp = LastStatusTimestamp;
            this.DeployedStart = DeployedStart;
            this.Offline = Offline;
            this.BatteryStatus = BatteryStatus;
            this.BatteryCharge = BatteryCharge;
        }

    }
}
