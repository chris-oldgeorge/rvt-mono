using AirQ.Api.Db;
using AirQ.Api.Http;
using AirQ.Api.Ports;
using AirQ.Api.UseCases;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Mqtt;

namespace AirQ.Api
{
    // Summary: Facade over the AirQ use-case handlers; keeps the historical public surface.
    // Major updates:
    // - 2026-07-12 God-class split: logic moved to AirQHttpGateway, AirQRuleProcessor, and api/UseCases handlers.
    public class AirQApi
    {
        public static readonly DateTime JAN1_1970 = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly StoreMonitorsHandler _storeMonitors;
        private readonly CheckForOfflineMonitorsHandler _checkForOfflineMonitors;
        private readonly StoreNoiseLevelsHandler _storeNoiseLevels;
        private readonly StoreNoiseLevelsForDateHandler _storeNoiseLevelsForDate;
        private readonly StoreAllNoiseLevelsForYesterdayHandler _storeAllNoiseLevelsForYesterday;
        private readonly NotifySiteAveragesHandler _notifySiteAverages;
        private readonly ClearOlderErrorMessagesHandler _clearOlderErrorMessages;

        public AirQApi(IHttpClient httpClient, IDBClient dbClient, IMqttClient mqttClient, IAlertIngressPort alertIngress)
            : this(httpClient, dbClient, mqttClient, alertIngress, RvtConfig.TESTLOCAL, null)
        {
        }

        public AirQApi(
            IHttpClient httpClient,
            IDBClient dbClient,
            IMqttClient mqttClient,
            IAlertIngressPort alertIngress,
            bool testLocal,
            string? testLocalSerialId)
        {
            IAirQVendorGateway gateway = new AirQHttpGateway(httpClient);
            AirQTestLocalMonitorFilter testLocalFilter = AirQTestLocalMonitorFilter.Create(testLocal, testLocalSerialId);
            AirQMonitorReader monitorReader = new(dbClient, testLocalFilter);
            MonitorEventPublisher eventPublisher = new(mqttClient, RvtConfig.INSERT_TOPIC, RvtConfig.ALERT_TOPIC);
            AirQRuleProcessor ruleProcessor = new(dbClient, dbClient, alertIngress);

            _storeMonitors = new StoreMonitorsHandler(gateway, dbClient, dbClient, testLocalFilter);
            _checkForOfflineMonitors = new CheckForOfflineMonitorsHandler(dbClient, monitorReader, dbClient, ruleProcessor);
            _storeNoiseLevels = new StoreNoiseLevelsHandler(gateway, monitorReader, dbClient, dbClient, dbClient, dbClient, eventPublisher, ruleProcessor);
            _storeNoiseLevelsForDate = new StoreNoiseLevelsForDateHandler(gateway, monitorReader, dbClient, dbClient);
            _storeAllNoiseLevelsForYesterday = new StoreAllNoiseLevelsForYesterdayHandler(_storeNoiseLevelsForDate);
            _notifySiteAverages = new NotifySiteAveragesHandler(dbClient, dbClient, dbClient, dbClient, ruleProcessor);
            _clearOlderErrorMessages = new ClearOlderErrorMessagesHandler(dbClient);
        }

        public Task StoreMonitorsAsync(string userId, string userAuth, CancellationToken cancellationToken = default) =>
            _storeMonitors.RunAsync(userId, userAuth, cancellationToken);

        public Task CheckForOfflineMonitorsAsync(CancellationToken cancellationToken = default) =>
            _checkForOfflineMonitors.RunAsync(cancellationToken);

        public Task StoreNoiseLevelsAsync(string userId, string userAuth, CancellationToken cancellationToken = default) =>
            _storeNoiseLevels.RunAsync(userId, userAuth, cancellationToken);

        public Task StoreNoiseLevelsForDateAsync(string userId, string userAuth, string dateStr, CancellationToken cancellationToken = default) =>
            _storeNoiseLevelsForDate.RunAsync(userId, userAuth, dateStr, cancellationToken);

        public Task StoreAllNoiseLevelsForYesterdayAsync(string userId, string userAuth, CancellationToken cancellationToken = default) =>
            _storeAllNoiseLevelsForYesterday.RunAsync(userId, userAuth, cancellationToken);

        public Task NotifySiteAveragesAsync(DateTime date, CancellationToken cancellationToken = default) =>
            _notifySiteAverages.RunAsync(date, cancellationToken);

        public Task ClearOlderErrorMessagesAsync(CancellationToken cancellationToken = default) =>
            _clearOlderErrorMessages.RunAsync(cancellationToken);
    }
}
