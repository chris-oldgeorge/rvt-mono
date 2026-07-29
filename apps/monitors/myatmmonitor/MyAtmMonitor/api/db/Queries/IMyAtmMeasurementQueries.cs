namespace MyAtm.Api.Db;

public interface IMyAtmMeasurementQueries
{
    DateTime? ReadLatestAccessoryTimestamp(string serialId);
}
