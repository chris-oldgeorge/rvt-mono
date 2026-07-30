using AirQ.Model.Dto;
using AirQ.Model.Dto;

namespace AirQ.Api.Db;

public interface IAirQMonitorQueries
{
    List<NoiseMonitorDto> ReadMonitorList(DateTime? lastDataTime);

    List<SiteMonitorsWithSiteHoursDto> ReadSiteMonitorsWithSiteHours(DateTime Day);
}
