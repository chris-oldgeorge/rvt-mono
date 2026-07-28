using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Svantek.Api.SvantekApi;

namespace SvantekMonitor.model.dto;

// Summary: Tracks the latest noise notification context used to prevent duplicate Svantek alerts.
// Major updates:
// - 2026-06-18: Inherits from DtoBase after C# naming cleanup.
public class NoiseNotificationLatest(Guid NotificationId, Guid MonitorId, string FleetNr, string SerialId, int ProjectId, int PointId, DateTime NotificationTime, int AvgPeriod) : DtoBase
{
    public Guid NotificationId { get; set; } = NotificationId;
    public Guid MonitorId { get; set; } = MonitorId;
    public string FleetNr { get; set; } = FleetNr;
    public string SerialId { get; set; } = SerialId;
    public int ProjectId { get; set; } = ProjectId;
    public int PointId { get; set; } = PointId;
    public DateTime NotificationTime { get; set; } = NotificationTime;
    public int AvgPeriod { get; set; } = AvgPeriod;
}
