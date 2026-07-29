using SvantekMonitor.model.dto;

namespace Svantek.Api.Db;

public interface ISvantekMonitorQueries
{
    List<NoiseMonitorReadDto> ReadMonitorList(DateTime? lastDataTime);

    Task<List<NoiseMonitorReadDto>> ReadMonitorListAsync(
        DateTime? lastDataTime,
        CancellationToken cancellationToken = default);

    List<SiteMonitorsWithSiteHoursDto> ReadSiteMonitorsWithSiteHours(DateTime day);

    Task<List<SiteMonitorsWithSiteHoursDto>> ReadSiteMonitorsWithSiteHoursAsync(
        DateTime day,
        CancellationToken cancellationToken = default);
}
