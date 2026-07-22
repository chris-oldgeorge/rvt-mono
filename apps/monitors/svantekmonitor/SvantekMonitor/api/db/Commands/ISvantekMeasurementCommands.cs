using System.Data;
using Svantek.Model.Dto;

namespace Svantek.Api.Db;

public interface ISvantekMeasurementCommands
{
    void InsertNoiseDtos(List<NoiseDto> dtos);

    void InsertNoiseDtos(string serialId, List<NoiseDto> dtos);

    void InsertNoiseRecordsTable(DataTable table);

    Task InsertNoiseRecordsTableAsync(
        DataTable table,
        CancellationToken cancellationToken = default);

    void Create8hourAverage(string serialId, DateTime sampleTime);

    Task Create8hourAverageAsync(
        string serialId,
        DateTime sampleTime,
        CancellationToken cancellationToken = default);

    void WriteDailyAverage(Guid siteId, Guid monitorId, string field, double level, DateTime timestamp);

    Task WriteDailyAverageAsync(
        Guid siteId,
        Guid monitorId,
        string field,
        double level,
        DateTime timestamp,
        CancellationToken cancellationToken = default);
}
