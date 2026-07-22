using SvantekMonitor.model.dto;

namespace Svantek.Api.Db;

public interface ISvantekNotificationQueries
{
    List<NoiseNotificationLatest> ReadLatestNotification();

    Task<List<NoiseNotificationLatest>> ReadLatestNotificationAsync(
        CancellationToken cancellationToken = default);
}
