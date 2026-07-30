using AirQ.Model.Dto;
using AirQMonitor.model.dto;

namespace AirQ.Api.Db;

public interface IAirQMonitorQueries
{
    List<NoiseMonitorDto> ReadMonitorList(DateTime? lastDataTime);

    List<SiteMonitorsWithSiteHoursDto> ReadSiteMonitorsWithSiteHours(DateTime Day);
}
