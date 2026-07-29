using AirQ.Model.Dto;

namespace AirQ.Api.Db;

public interface IAirQMeasurementCommands
{
    void InsertNoiseDtos(string serialId, List<NoiseDto> dtos);

    void WriteDailyAverage(Guid siteId, Guid monitorId, string field, double level, DateTime timestamp);

    void Create8hourAverage(string serialId, DateTime SampleTime);
}
