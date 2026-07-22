namespace AirQ.Api.UseCases;

public interface IAirQDateImporter
{
    void StoreNoiseLevelsForDate(string date);
}
