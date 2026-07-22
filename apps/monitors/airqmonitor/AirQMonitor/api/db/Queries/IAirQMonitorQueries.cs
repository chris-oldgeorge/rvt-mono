using AirQ.Model.Dto;
using AirQMonitor.model.dto;

namespace AirQ.Api.Db;

public interface IAirQMonitorQueries
{
    List<NoiseMonitorDto> ReadMonitorList(DateTime? lastDataTime);

    SiteInfoDto ReadSiteInfo(Guid siteId);

    List<SiteMonitorsWithSiteHoursDto> ReadSiteMonitorsWithSiteHours(DateTime Day);
}
