using Omnidots.Api.Db;
using Omnidots.Api.Http;
using Omnidots.Api.Ports;
using Omnidots.Api.UseCases;
using Omnidots.Model.Config;
using Rvt.Communication.Abstractions;
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
        private readonly StoreMonitorsHandler _storeMonitors;
        private readonly CheckForOfflineMonitorsHandler _checkForOfflineMonitors;
        private readonly StorePeakRecordsHandler _storePeakRecords;
        private readonly StoreVeffRecordsHandler _storeVeffRecords;
        private readonly StoreVdvRecordsHandler _storeVdvRecords;
        private readonly StoreTracesHandler _storeTraces;
        private readonly NotifyBatteryLevelsHandler _notifyBatteryLevels;
        private readonly ClearOlderErrorMessagesHandler _clearOlderErrorMessages;
        private readonly MonitoringHandler _monitoring;

        public OmnidotsApi(IHttpClient httpClient, IDBClient dbClient, IMqttClient mqttClient, IMessageService messageService)
            : this(httpClient, dbClient, mqttClient, messageService, RvtConfig.TESTLOCAL)
        {
        }

        public OmnidotsApi(IHttpClient httpClient, IDBClient dbClient, IMqttClient mqttClient, IMessageService messageService, bool testLocal)
            : this(
                httpClient,
                dbClient,
                mqttClient,
                messageService,
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
            IMessageService messageService,
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
                messageService,
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
            IMessageService messageService,
            bool testLocal,
            OmnidotsMonitoringOptions monitoringOptions,
            IOmnidotsMonitoringNotifier monitoringNotifier,
            OmnidotsTraceCollectionOptions traceCollectionOptions,
            TimeProvider timeProvider)
        {
            _gateway = new OmnidotsHttpGateway(httpClient, RvtConfig.USER_ID, RvtConfig.USER_AUTH);
            OmnidotsMonitorReader monitorReader = new(dbClient, testLocal);
            MonitorEventPublisher eventPublisher = new(mqttClient, RvtConfig.INSERT_TOPIC, RvtConfig.ALERT_TOPIC);
            OmnidotsRuleProcessor ruleProcessor = new(dbClient, dbClient, messageService, RvtConfig.PORTAL_BASE_URL);
            _storeMonitors = new StoreMonitorsHandler(_gateway, dbClient, dbClient, testLocal);
            _checkForOfflineMonitors = new CheckForOfflineMonitorsHandler(
                dbClient,
                monitorReader,
                dbClient,
                dbClient,
                dbClient,
                ruleProcessor);
            _storePeakRecords = new StorePeakRecordsHandler(
                _gateway,
                monitorReader,
                dbClient,
                cursorQueries,
                importCommands,
                dbClient,
                eventPublisher);
            _storeVeffRecords = new StoreVeffRecordsHandler(
                _gateway,
                monitorReader,
                dbClient,
                cursorQueries,
                importCommands,
                dbClient,
                eventPublisher);
            _storeVdvRecords = new StoreVdvRecordsHandler(
                _gateway,
                monitorReader,
                dbClient,
                cursorQueries,
                importCommands,
                dbClient,
                eventPublisher);
            _storeTraces = new StoreTracesHandler(
                _gateway,
                monitorReader,
                dbClient,
                dbClient,
                traceQueries,
                traceCollectionOptions,
                timeProvider);
            _notifyBatteryLevels = new NotifyBatteryLevelsHandler(monitorReader, dbClient, ruleProcessor);
            _clearOlderErrorMessages = new ClearOlderErrorMessagesHandler(dbClient);
            _monitoring = new MonitoringHandler(
                monitorReader,
                monitoringOptions,
                monitoringNotifier,
                timeProvider);
        }

        public Task StoreMonitorsAsync(CancellationToken cancellationToken = default) =>
            _storeMonitors.RunAsync(cancellationToken);

        public Task CheckForOfflineMonitorsAsync(CancellationToken cancellationToken = default) =>
            _checkForOfflineMonitors.RunAsync(cancellationToken);

        public Task StorePeakRecordsLastDataTimeAsync(CancellationToken cancellationToken = default) =>
            _storePeakRecords.RunAsync(cancellationToken);

        public Task StoreVeffRecordsAsync(TimeSpan lookback, CancellationToken cancellationToken = default) =>
            _storeVeffRecords.RunAsync(lookback, cancellationToken);

        public Task StoreVdvRecordsAsync(TimeSpan lookback, CancellationToken cancellationToken = default) =>
            _storeVdvRecords.RunAsync(lookback, cancellationToken);

        public Task StoreTracesAsync(DateTime last, CancellationToken cancellationToken = default) =>
            _storeTraces.RunAsync(last, cancellationToken);

        public Task NotifyBatteryLevelsAsync(CancellationToken cancellationToken = default) =>
            _notifyBatteryLevels.RunAsync(cancellationToken);

        public Task ClearOlderErrorMessagesAsync(CancellationToken cancellationToken = default) =>
            _clearOlderErrorMessages.RunAsync(cancellationToken);

        internal Task MonitoringAsync(CancellationToken cancellationToken = default) =>
            _monitoring.RunAsync(cancellationToken);

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
            MaxMonitorsPerRun = int.MaxValue
        };
    }
}
