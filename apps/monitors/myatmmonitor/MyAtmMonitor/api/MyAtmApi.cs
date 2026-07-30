using Microsoft.Extensions.Logging.Abstractions;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Api.Ports;
using MyAtm.Api.UseCases;
using MyAtm.Model;
using MyAtm.Model.Config;
using MyAtm.Model.Json;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Configuration;
using MyAtm.Delivery;
using Rvt.Monitor.Common.Mqtt;

namespace MyAtm.Api
{
    // Summary: Facade over the MyAtm use-case handlers; keeps the historical public surface.
    // Major updates:
    // - 2026-07-12 God-class split: logic moved to MyAtmHttpGateway, MyAtmRuleProcessor, and api/UseCases handlers.
    public class MyAtmApi
    {

        private readonly StoreMonitorsHandler _storeMonitors;
        private readonly CheckForOfflineMonitorsHandler _checkForOfflineMonitors;
        private readonly ClearMonitorsOfflineFlagHandler _clearMonitorsOfflineFlag;
        private readonly ClearOlderErrorMessagesHandler _clearOlderErrorMessages;
        private readonly StoreDustLevelsHandler _storeDustLevels;
        private readonly ProcessDustLevelsHandler _processDustLevels;
        private readonly StoreAccessoryInfoHandler _storeAccessoryInfo;
        private readonly MonitorDeliveryDispatcher _outboxDispatcher;

        public MyAtmApi(IHttpClient httpClient, IDBClient dbClient, IMqttClient rvtMqttClient, INotificationDeliveryService notificationDelivery)
            : this(httpClient, dbClient, rvtMqttClient, notificationDelivery, RvtConfig.TESTLOCAL, new MyAtmMonitorOptions
            {
                PortalBaseUrl = string.IsNullOrWhiteSpace(RvtConfig.PORTAL_BASE_URL)
                    ? "https://www.rvtcloud.com/"
                    : RvtConfig.PORTAL_BASE_URL
            })
        {
        }

        public MyAtmApi(IHttpClient httpClient, IDBClient dbClient, IMqttClient rvtMqttClient, INotificationDeliveryService notificationDelivery, bool testLocal)
            : this(httpClient, dbClient, rvtMqttClient, notificationDelivery, testLocal, new MyAtmMonitorOptions
            {
                PortalBaseUrl = string.IsNullOrWhiteSpace(RvtConfig.PORTAL_BASE_URL)
                    ? "https://www.rvtcloud.com/"
                    : RvtConfig.PORTAL_BASE_URL
            })
        {
        }

        public MyAtmApi(
            IHttpClient httpClient,
            IDBClient dbClient,
            IMqttClient rvtMqttClient,
            INotificationDeliveryService notificationDelivery,
            bool testLocal,
            MyAtmMonitorOptions options)
            : this(
                httpClient,
                dbClient,
                testLocal,
                options,
                CreateDispatcher(dbClient, rvtMqttClient, notificationDelivery, options))
        {
        }

        public MyAtmApi(
            IHttpClient httpClient,
            IDBClient dbClient,
            bool testLocal,
            MyAtmMonitorOptions options,
            MonitorDeliveryDispatcher outboxDispatcher)
        {
            options.Validate();
            IMyAtmVendorGateway gateway = new MyAtmHttpGateway(
                httpClient,
                options.DevicePageSize,
                options.MeasurementPageSize,
                options.AccessoryPageSize);
            MyAtmMonitorReader monitorReader = new(dbClient, dbClient, testLocal);
            MyAtmRuleProcessor ruleProcessor = new(dbClient);

            _outboxDispatcher = outboxDispatcher ?? throw new ArgumentNullException(nameof(outboxDispatcher));
            _storeMonitors = new StoreMonitorsHandler(
                gateway,
                dbClient,
                dbClient,
                testLocal,
                options.DevicePageSize,
                options.MaxDevicePagesPerRun);
            _checkForOfflineMonitors = new CheckForOfflineMonitorsHandler(
                dbClient,
                monitorReader,
                dbClient,
                dbClient,
                dbClient,
                ruleProcessor,
                TimeProvider.System);
            _clearMonitorsOfflineFlag = new ClearMonitorsOfflineFlagHandler(monitorReader, dbClient);
            _clearOlderErrorMessages = new ClearOlderErrorMessagesHandler(dbClient);
            _storeDustLevels = new StoreDustLevelsHandler(
                gateway,
                monitorReader,
                dbClient,
                dbClient,
                dbClient,
                new MyAtmRuleEvaluator(),
                TimeProvider.System,
                options.MaxPagesPerMonitorPerRun);
            _processDustLevels = new ProcessDustLevelsHandler(
                dbClient,
                dbClient,
                dbClient,
                dbClient,
                ruleProcessor,
                TimeProvider.System,
                testLocal);
            _storeAccessoryInfo = new StoreAccessoryInfoHandler(gateway, monitorReader, dbClient, dbClient, dbClient, options.MaxPagesPerMonitorPerRun);
        }

        public Task StoreMonitorsAsync(int customerId, CancellationToken cancellationToken = default) =>
            _storeMonitors.RunAsync(customerId, cancellationToken);


        public Task CheckForOfflineMonitorsAsync(int customerId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _checkForOfflineMonitors.RunAsync(customerId, cancellationToken);
        }


        public Task ClearMonitorsOfflineFlagAsync(int customerId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _clearMonitorsOfflineFlag.Run(customerId);
            return Task.CompletedTask;
        }


        public Task ClearOlderErrorMessagesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _clearOlderErrorMessages.Run();
            return Task.CompletedTask;
        }


        public Task StoreDustLevelsAsync<T>(int customerId, Period period, CancellationToken cancellationToken = default)
            where T : BaseDeviceMeasurement =>
            _storeDustLevels.RunAsync<T>(customerId, period, cancellationToken);


        public Task ProcessDustLevelsAsync<T>(int customerId, Period period, CancellationToken cancellationToken = default)
            where T : BaseDeviceMeasurement
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _processDustLevels.RunAsync<T>(customerId, period, cancellationToken);
        }


        public Task StoreAccessoryInfoAsync(int customerId, CancellationToken cancellationToken = default) =>
            _storeAccessoryInfo.RunAsync(customerId, cancellationToken);


        public Task DispatchOutboxAsync(CancellationToken cancellationToken = default) =>
            _outboxDispatcher.DispatchDueAsync(cancellationToken);

        private static MonitorDeliveryDispatcher CreateDispatcher(
            IDBClient dbClient,
            IMqttClient mqttClient,
            INotificationDeliveryService notificationDelivery,
            MyAtmMonitorOptions options) =>
            new(
                dbClient,
                dbClient,
                new MyAtmDeliveryFailureSink(dbClient),
                mqttClient,
                notificationDelivery,
                NullLogger<MonitorDeliveryDispatcher>.Instance,
                options.ToDeliveryOptions(RvtConfig.INSERT_TOPIC, RvtConfig.ALERT_TOPIC));

    }
}
