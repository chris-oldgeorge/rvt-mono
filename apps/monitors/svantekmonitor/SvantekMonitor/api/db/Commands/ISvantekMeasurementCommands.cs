using System.Data;
using Svantek.Model.Dto;

namespace Svantek.Api.Db;

public interface ISvantekMeasurementCommands
{
    Task InsertNoiseRecordsTableAsync(
        DataTable table,
        CancellationToken cancellationToken = default);

    Task Create8hourAverageAsync(
        string serialId,
        DateTime sampleTime,
        CancellationToken cancellationToken = default);

    Task WriteDailyAverageAsync(
        Guid siteId,
        Guid monitorId,
        string field,
        double level,
        DateTime timestamp,
        CancellationToken cancellationToken = default);
}
