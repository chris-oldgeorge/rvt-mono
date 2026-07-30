using SvantekMonitor.model.dto;

namespace Svantek.Api.Db;

public interface ISvantekNotificationQueries
{
    Task<List<NoiseNotificationLatest>> ReadLatestNotificationAsync(
        CancellationToken cancellationToken = default);
}
