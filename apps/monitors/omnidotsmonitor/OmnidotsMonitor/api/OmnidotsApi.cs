using Omnidots.Api.Db;
using Omnidots.Api.Http;
using Omnidots.Api.Ports;
using Omnidots.Api.UseCases;
using Omnidots.Model.Config;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Mqtt;

namespace Omnidots.Api
{
    // Summary: Compatibility facade over scheduled Omnidots import, monitoring, and legacy rule handlers.
    // Major updates:
    // - 2026-07-12 God-class split: logic moved to OmnidotsHttpGateway, OmnidotsRuleProcessor, and api/UseCases handlers.
    // - 2026-07-15 Durable alerts: API configuration and webhook ingress resolve focused handlers directly.
    public class OmnidotsApi
    {
        public static readonly DateTime JAN1_1970 = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public enum BatteryAlertType
        {
            Off = 0,
            BatteryAlert = 1,
            BatteryCaution = 2
        }

        private readonly IOmnidotsVendorGateway _gateway;
        private readonly StoreMonitorsHandler storeMonitors;
        private readonly CheckForOfflineMonitorsHandler checkForOfflineMonitors;
        private readonly StorePeakRecordsHandler storePeakRecords;
        private readonly StoreVeffRecordsHandler storeVeffRecords;
        private readonly StoreVdvRecordsHandler storeVdvRecords;
        private readonly StoreTracesHandler storeTraces;
        private readonly NotifyBatteryLevelsHandler notifyBatteryLevels;
        private readonly ClearOlderErrorMessagesHandler clearOlderErrorMessages;
        private readonly MonitoringHandler monitoring;

        public OmnidotsApi(IHttpClient httpClient, IDBClient dbClient, IMqttClient mqttClient, IAlertIngressPort alertIngress)
            : this(httpClient, dbClient, mqttClient, alertIngress, RvtConfig.TESTLOCAL)
        {
        }

        public OmnidotsApi(IHttpClient httpClient, IDBClient dbClient, IMqttClient mqttClient, IAlertIngressPort alertIngress, bool testLocal)
            : this(
                httpClient,
                dbClient,
                mqttClient,
                alertIngress,
                testLocal,
                new OmnidotsMonitoringOptions(),
                new EmailOmnidotsMonitoringNotifier(new UnavailableEmailDeliveryPort()),
                TimeProvider.System)
        {
        }

        public OmnidotsApi(
            IHttpClient httpClient,
            IDBClient dbClient,
            IMqttClient mqttClient,
            IAlertIngressPort alertIngress,
            bool testLocal,
            OmnidotsMonitoringOptions monitoringOptions,
            IOmnidotsMonitoringNotifier monitoringNotifier,
            TimeProvider timeProvider)
            : this(
                httpClient,
                dbClient,
                RequirePort<IOmnidotsImportCursorQueries>(dbClient),
                RequirePort<IOmnidotsMeasurementImportCommands>(dbClient),
                RequirePort<IOmnidotsTraceQueries>(dbClient),
                mqttClient,
                alertIngress,
                testLocal,
                monitoringOptions,
                monitoringNotifier,
                LegacyTraceCollectionOptions(),
                timeProvider)
        {
        }

        public OmnidotsApi(
            IHttpClient httpClient,
            IDBClient dbClient,
            IOmnidotsImportCursorQueries cursorQueries,
            IOmnidotsMeasurementImportCommands importCommands,
            IOmnidotsTraceQueries traceQueries,
            IMqttClient mqttClient,
            IAlertIngressPort alertIngress,
            bool testLocal,
            OmnidotsMonitoringOptions monitoringOptions,
            IOmnidotsMonitoringNotifier monitoringNotifier,
            OmnidotsTraceCollectionOptions traceCollectionOptions,
            TimeProvider timeProvider)
        {
            _gateway = new OmnidotsHttpGateway(httpClient, RvtConfig.USER_ID, RvtConfig.USER_AUTH);
            OmnidotsMonitorReader monitorReader = new(dbClient, testLocal);
            MonitorEventPublisher eventPublisher = new(mqttClient, RvtConfig.INSERT_TOPIC, RvtConfig.ALERT_TOPIC);
            storeMonitors = new StoreMonitorsHandler(_gateway, dbClient, dbClient, testLocal);
            checkForOfflineMonitors = new CheckForOfflineMonitorsHandler(
                dbClient,
                monitorReader,
                dbClient,
                dbClient,
                dbClient,
                alertIngress);
            storePeakRecords = new StorePeakRecordsHandler(
                _gateway,
                monitorReader,
                dbClient,
                cursorQueries,
                importCommands,
                dbClient,
                eventPublisher);
            storeVeffRecords = new StoreVeffRecordsHandler(
                _gateway,
                monitorReader,
                dbClient,
                cursorQueries,
                importCommands,
                dbClient,
                eventPublisher);
            storeVdvRecords = new StoreVdvRecordsHandler(
                _gateway,
                monitorReader,
                dbClient,
                cursorQueries,
                importCommands,
                dbClient,
                eventPublisher);
            storeTraces = new StoreTracesHandler(
                _gateway,
                monitorReader,
                dbClient,
                dbClient,
                traceQueries,
                traceCollectionOptions,
                timeProvider);
            notifyBatteryLevels = new NotifyBatteryLevelsHandler(monitorReader, dbClient, alertIngress);
            clearOlderErrorMessages = new ClearOlderErrorMessagesHandler(dbClient);
            monitoring = new MonitoringHandler(
                monitorReader,
                monitoringOptions,
                monitoringNotifier,
                timeProvider);
        }

        public Task StoreMonitorsAsync(CancellationToken cancellationToken = default) =>
            storeMonitors.RunAsync(cancellationToken);

        public Task CheckForOfflineMonitorsAsync(CancellationToken cancellationToken = default) =>
            checkForOfflineMonitors.RunAsync(cancellationToken);

        public Task StorePeakRecordsLastDataTimeAsync(CancellationToken cancellationToken = default) =>
            storePeakRecords.RunAsync(cancellationToken);

        public Task StoreVeffRecordsAsync(TimeSpan lookback, CancellationToken cancellationToken = default) =>
            storeVeffRecords.RunAsync(lookback, cancellationToken);

        public Task StoreVdvRecordsAsync(TimeSpan lookback, CancellationToken cancellationToken = default) =>
            storeVdvRecords.RunAsync(lookback, cancellationToken);

        public Task StoreTracesAsync(DateTime last, CancellationToken cancellationToken = default) =>
            storeTraces.RunAsync(last, cancellationToken);

        public Task NotifyBatteryLevelsAsync(CancellationToken cancellationToken = default) =>
            notifyBatteryLevels.RunAsync(cancellationToken);

        public Task ClearOlderErrorMessagesAsync(CancellationToken cancellationToken = default) =>
            clearOlderErrorMessages.RunAsync(cancellationToken);

        internal Task MonitoringAsync(CancellationToken cancellationToken = default) =>
            monitoring.RunAsync(cancellationToken);

        private sealed class UnavailableEmailDeliveryPort : IEmailDeliveryPort
        {
            public Task SendAsync(
                EmailDeliveryRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromException(new EmailDeliveryException(
                    "OmnidotsCompatibility",
                    DeliveryFailureKind.Configuration,
                    "Configuration"));
            }
        }

        private static TPort RequirePort<TPort>(IDBClient dbClient)
            where TPort : class
        {
            return dbClient as TPort ?? throw new ArgumentException(
                $"The database client must implement {typeof(TPort).Name}.",
                nameof(dbClient));
        }

        private static OmnidotsTraceCollectionOptions LegacyTraceCollectionOptions() => new()
        {
            AllowedSerialIds = ["23423"],
            MaxMonitorsPerRun = int.MaxValue
        };
    }
}
