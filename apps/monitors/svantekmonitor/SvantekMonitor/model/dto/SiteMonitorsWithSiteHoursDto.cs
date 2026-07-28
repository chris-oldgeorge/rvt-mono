using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;

namespace SvantekMonitor.model.dto;

// Summary: Combines deployed Svantek monitor identity with the site hours used for reporting windows.
// Major updates:
// - 2026-06-18: Inherits from DtoBase after C# naming cleanup.
public class SiteMonitorsWithSiteHoursDto(Guid monitorId,
    string fleetnr,
    string serialId,
    int typeOfMonitor,
    bool offline,
    Guid siteId,
    string? siteName,
    TimeSpan? startTime, TimeSpan? endTime) : DtoBase
{
    public Guid Id { get; } = monitorId;
    public string FleetNr { get; } = fleetnr;
    public string SerialId { get; } = serialId;
    public int TypeOfMonitor { get; } = typeOfMonitor;
    public bool Offline { get; set; } = offline;
    public Guid SiteId { get; } = siteId;
    public string? SiteName { get; } = siteName;
    public TimeSpan? StartTime { get; } = startTime;
    public TimeSpan? EndTime { get; } = endTime;
}
