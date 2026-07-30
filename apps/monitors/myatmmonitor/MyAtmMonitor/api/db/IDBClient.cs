using MyAtm.Delivery;

namespace MyAtm.Api.Db
{
    public interface IDBClient :
        IMyAtmMonitorQueries,
        IMyAtmRuleQueries,
        IMyAtmMeasurementQueries,
        IMyAtmHealthQueries,
        IMyAtmSiteScheduleQueries,
        IMyAtmMonitorCommands,
        IMyAtmOperationalCommands,
        IMyAtmDustImportCommands,
        IMyAtmAlertCommitCommands,
        IMyAtmAccessoryCommands,
        IMonitorDeliveryOutboxCommands,
        IMonitorDeliveryOutboxQueries
    {
    }
}
