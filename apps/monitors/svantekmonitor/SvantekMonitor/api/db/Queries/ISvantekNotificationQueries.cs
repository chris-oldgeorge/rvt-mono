using Svantek.Model.Dto;

namespace Svantek.Api.Db;

public interface ISvantekNotificationQueries
{
    Task<List<NoiseNotificationLatest>> ReadLatestNotificationAsync(
        CancellationToken cancellationToken = default);
}
