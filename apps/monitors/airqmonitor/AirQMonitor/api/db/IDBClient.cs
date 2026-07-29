namespace AirQ.Api.Db
{

    public interface IDBClient :
        IAirQMonitorQueries,
        IAirQRuleQueries,
        IAirQMonitorCommands,
        IAirQMeasurementCommands,
        IAirQOperationalCommands
    {
    }
}
