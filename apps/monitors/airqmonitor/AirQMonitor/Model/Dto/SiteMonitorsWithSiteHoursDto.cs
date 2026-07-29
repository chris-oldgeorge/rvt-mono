namespace AirQ.Model.Dto;

public class SiteMonitorsWithSiteHoursDto(Guid monitorId,
    string fleetnr,
    string serialId,
    int typeOfMonitor,
    bool offline,
    Guid siteId,
    string? siteName,
    TimeSpan? startTime, TimeSpan? endTime)
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
