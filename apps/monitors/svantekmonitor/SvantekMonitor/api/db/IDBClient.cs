namespace Svantek.Api.Db
{

    public interface IDBClient :
        ISvantekMonitorQueries,
        ISvantekRuleQueries,
        ISvantekNotificationQueries,
        ISvantekMonitorCommands,
        ISvantekMeasurementCommands,
        ISvantekOperationalCommands
    {
    }
}
