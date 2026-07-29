using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;

namespace SvantekMonitor.model.dto
{
    // Summary: Combines deployed Svantek monitor identity with the site hours used for reporting windows.
    // Major updates:
    // - 2026-06-18: Inherits from DtoBase after C# naming cleanup.
    public class SiteMonitorsWithSiteHoursDto : DtoBase
    {
        public Guid Id { get; }
        public string FleetNr { get; }
        public string SerialId { get; }
        public int TypeOfMonitor { get; }
        public bool Offline { get; set; }
        public Guid SiteId { get; }
        public string? SiteName { get; }
        public TimeSpan? StartTime { get; }
        public TimeSpan? EndTime { get; }

        public SiteMonitorsWithSiteHoursDto(Guid monitorId,
            string fleetnr,
            string serialId,
            int typeOfMonitor,
            bool offline,
            Guid siteId,
            string? siteName,
            TimeSpan? startTime, TimeSpan? endTime)

        {
            Id = monitorId;
            FleetNr = fleetnr;
            SerialId = serialId;
            TypeOfMonitor = typeOfMonitor;
            Offline = offline;
            SiteId = siteId;
            SiteName = siteName;
            StartTime = startTime;
            EndTime = endTime;

        }
    }
}
