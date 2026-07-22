using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Svantek.Api.SvantekApi;

namespace SvantekMonitor.model.dto
{
    // Summary: Tracks the latest noise notification context used to prevent duplicate Svantek alerts.
    // Major updates:
    // - 2026-06-18: Inherits from DtoBase after C# naming cleanup.
    public class NoiseNotificationLatest : DtoBase
    {
        public Guid NotificationId { get; set; }
        public Guid MonitorId { get; set; }
        public string FleetNr { get; set; }
        public string SerialId { get; set; }
        public int ProjectId { get; set; }
        public int PointId { get; set; }
        public DateTime NotificationTime { get; set; }
        public int AvgPeriod { get; set; }


        public NoiseNotificationLatest(Guid NotificationId, Guid MonitorId, string FleetNr, string SerialId, int ProjectId, int PointId, DateTime NotificationTime, int AvgPeriod)
        {
            this.NotificationId = NotificationId;
            this.MonitorId = MonitorId;
            this.FleetNr = FleetNr;
            this.SerialId = SerialId;
            this.ProjectId = ProjectId;
            this.PointId = PointId;
            this.NotificationTime = NotificationTime;
            this.AvgPeriod = AvgPeriod;
        }

    }
}
