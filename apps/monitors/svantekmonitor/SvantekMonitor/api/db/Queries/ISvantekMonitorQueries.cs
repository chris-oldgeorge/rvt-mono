using Svantek.Model.Dto;

namespace Svantek.Api.Db;

public interface ISvantekMonitorQueries
{
    List<NoiseMonitorReadDto> ReadMonitorList(DateTime? lastDataTime);

    Task<List<NoiseMonitorReadDto>> ReadMonitorListAsync(
        DateTime? lastDataTime,
        CancellationToken cancellationToken = default);

    Task<List<SiteMonitorsWithSiteHoursDto>> ReadSiteMonitorsWithSiteHoursAsync(
        DateTime day,
        CancellationToken cancellationToken = default);
}
