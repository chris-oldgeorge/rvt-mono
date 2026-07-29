using AirQ.Model.Dto;

namespace AirQ.Api.Db;

public interface IAirQMonitorQueries
{
    List<NoiseMonitorDto> ReadMonitorList(DateTime? lastDataTime);

    SiteInfoDto ReadSiteInfo(Guid siteId);

    List<SiteMonitorsWithSiteHoursDto> ReadSiteMonitorsWithSiteHours(DateTime Day);
}
