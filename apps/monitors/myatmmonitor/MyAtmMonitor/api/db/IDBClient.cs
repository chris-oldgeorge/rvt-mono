using Rvt.Monitor.Common.Delivery;

namespace MyAtm.Api.Db
{
    public interface IDBClient :
        IMyAtmMonitorQueries,
        IMyAtmRuleQueries,
        IMyAtmMeasurementQueries,
        IMyAtmHealthQueries,
        IMyAtmSiteScheduleQueries,
        IMyAtmMonitorCommands,
        IMyAtmMeasurementCommands,
        IMyAtmOperationalCommands,
        IMyAtmDustImportCommands,
        IMyAtmAlertCommitCommands,
        IMyAtmAccessoryCommands,
        IMonitorDeliveryOutboxCommands,
        IMonitorDeliveryOutboxQueries
    {
    }
}
