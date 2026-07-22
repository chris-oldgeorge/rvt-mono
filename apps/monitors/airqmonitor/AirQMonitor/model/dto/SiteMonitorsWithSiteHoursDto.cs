namespace AirQMonitor.model.dto
{
    public class SiteMonitorsWithSiteHoursDto
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
