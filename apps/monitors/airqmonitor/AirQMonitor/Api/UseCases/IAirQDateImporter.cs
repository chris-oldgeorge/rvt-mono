namespace AirQ.Api.UseCases;

public interface IAirQDateImporter
{
    Task StoreNoiseLevelsForDateAsync(string date, CancellationToken cancellationToken = default);
}
