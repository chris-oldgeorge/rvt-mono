namespace Omnidots.Api.Db
{

    public interface IDBClient :
        IOmnidotsMonitorQueries,
        IOmnidotsRuleQueries,
        IOmnidotsMonitorCommands,
        IOmnidotsMeasurementCommands,
        IOmnidotsOperationalCommands
    {
    }
}
