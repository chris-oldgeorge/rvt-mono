using AirQ.Api.Db;
using AirQ.Api.Http;
using AirQ.Api.UseCases;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;

namespace AirQ.Api
{
    // Summary: Facade over the AirQ use-case handlers; keeps the historical public surface.
    // Major updates:
    // - 2026-07-12 God-class split: logic moved to AirQHttpGateway, AirQRuleProcessor, and api/UseCases handlers.
    public class AirQApi
    {
        public static readonly DateTime JAN1_1970 = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly StoreMonitorsHandler storeMonitors;
        private readonly CheckForOfflineMonitorsHandler checkForOfflineMonitors;
        private readonly StoreNoiseLevelsHandler storeNoiseLevels;
        private readonly StoreNoiseLevelsForDateHandler storeNoiseLevelsForDate;
        private readonly StoreAllNoiseLevelsForYesterdayHandler storeAllNoiseLevelsForYesterday;
        private readonly NotifySiteAveragesHandler notifySiteAverages;
        private readonly ClearOlderErrorMessagesHandler clearOlderErrorMessages;

        public AirQApi(IHttpClient httpClient, IDBClient dbClient, IMqttClient mqttClient, IMessageService messageService)
            : this(httpClient, dbClient, mqttClient, messageService, RvtConfig.TESTLOCAL, null)
        {
        }

        public AirQApi(
            IHttpClient httpClient,
            IDBClient dbClient,
            IMqttClient mqttClient,
            IMessageService messageService,
            bool testLocal,
            string? testLocalSerialId)
        {
            var gateway = new AirQHttpGateway(httpClient);
            var testLocalFilter = AirQTestLocalMonitorFilter.Create(testLocal, testLocalSerialId);
            var monitorReader = new AirQMonitorReader(dbClient, testLocalFilter);
            var eventPublisher = new MonitorEventPublisher(mqttClient, RvtConfig.INSERT_TOPIC, RvtConfig.ALERT_TOPIC);
            var ruleProcessor = new AirQRuleProcessor(dbClient, dbClient, messageService, eventPublisher);

            storeMonitors = new StoreMonitorsHandler(gateway, dbClient, dbClient, testLocalFilter);
            checkForOfflineMonitors = new CheckForOfflineMonitorsHandler(dbClient, monitorReader, dbClient, ruleProcessor);
            storeNoiseLevels = new StoreNoiseLevelsHandler(gateway, monitorReader, dbClient, dbClient, dbClient, dbClient, eventPublisher, ruleProcessor);
            storeNoiseLevelsForDate = new StoreNoiseLevelsForDateHandler(gateway, monitorReader, dbClient, dbClient);
            storeAllNoiseLevelsForYesterday = new StoreAllNoiseLevelsForYesterdayHandler(storeNoiseLevelsForDate);
            notifySiteAverages = new NotifySiteAveragesHandler(dbClient, dbClient, dbClient, dbClient, ruleProcessor);
            clearOlderErrorMessages = new ClearOlderErrorMessagesHandler(dbClient);
        }

        public void StoreMonitors(string userId, string userAuth) => storeMonitors.Run(userId, userAuth);

        public void CheckForOfflineMonitors() => checkForOfflineMonitors.Run();

        public void StoreNoiseLevels(string userId, string userAuth) => storeNoiseLevels.Run(userId, userAuth);

        public void StoreNoiseLevelsForDate(string userId, string userAuth, string dateStr) => storeNoiseLevelsForDate.Run(userId, userAuth, dateStr);

        public void StoreAllNoiseLevelsForYesterday(string userId, string userAuth) => storeAllNoiseLevelsForYesterday.Run(userId, userAuth);

        public void NotifySiteAverages(DateTime date) => notifySiteAverages.Run(date);

        public void ClearOlderErrorMessages() => clearOlderErrorMessages.Run();
    }
}
