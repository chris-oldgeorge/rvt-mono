using Microsoft.Extensions.Logging.Abstractions;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Api.UseCases;
using MyAtm.Model.Config;
using MyAtm.Model.Json;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;

namespace MyAtm.Api;

// Summary: Facade over the MyAtm use-case handlers; keeps the historical public surface.
// Major updates:
// - 2026-07-12 God-class split: logic moved to MyAtmHttpGateway, MyAtmRuleProcessor, and api/UseCases handlers.
public class MyAtmApi
{

    // Vendor context: the "RVT Case Study" AQ Network currently has no assigned devices, so the
    // per-customer measurements endpoint returns nothing for it. Device 18129 was once assigned
    // there, was moved to another AQ Network, and its data is still reachable via the per-device
    // endpoint (GET /api/customers/146/devices/18129/measurements). 18129 was added to the DB manually.
    public static readonly DateTime JAN1_1970 = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly StoreMonitorsHandler storeMonitors;
    private readonly CheckForOfflineMonitorsHandler checkForOfflineMonitors;
    private readonly ClearMonitorsOfflineFlagHandler clearMonitorsOfflineFlag;
    private readonly ClearOlderErrorMessagesHandler clearOlderErrorMessages;
    private readonly StoreDustLevelsHandler storeDustLevels;
    private readonly ProcessDustLevelsHandler processDustLevels;
    private readonly StoreAccessoryInfoHandler storeAccessoryInfo;
    private readonly MonitorDeliveryDispatcher outboxDispatcher;

    public MyAtmApi(IHttpClient httpClient, IDBClient dbClient, IMqttClient rvtMqttClient, IMessageService messageClient)
        : this(httpClient, dbClient, rvtMqttClient, messageClient, RvtConfig.TESTLOCAL, new MyAtmMonitorOptions
        {
            PortalBaseUrl = string.IsNullOrWhiteSpace(RvtConfig.PORTAL_BASE_URL)
                ? "https://www.rvtcloud.com/"
                : RvtConfig.PORTAL_BASE_URL
        })
    {
    }

    public MyAtmApi(IHttpClient httpClient, IDBClient dbClient, IMqttClient rvtMqttClient, IMessageService messageClient, bool testLocal)
        : this(httpClient, dbClient, rvtMqttClient, messageClient, testLocal, new MyAtmMonitorOptions
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
        IMessageService messageClient,
        bool testLocal,
        MyAtmMonitorOptions options)
        : this(
            httpClient,
            dbClient,
            testLocal,
            options,
            CreateDispatcher(dbClient, rvtMqttClient, messageClient, options))
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
        MyAtmHttpGateway gateway = new(
            httpClient,
            options.DevicePageSize,
            options.MeasurementPageSize,
            options.AccessoryPageSize);
        MyAtmMonitorReader monitorReader = new(dbClient, dbClient, testLocal);
        MyAtmRuleProcessor ruleProcessor = new(dbClient, options.PortalBaseUrl);

        this.outboxDispatcher = outboxDispatcher ?? throw new ArgumentNullException(nameof(outboxDispatcher));
        storeMonitors = new StoreMonitorsHandler(
            gateway,
            dbClient,
            dbClient,
            testLocal,
            options.DevicePageSize,
            options.MaxDevicePagesPerRun);
        checkForOfflineMonitors = new CheckForOfflineMonitorsHandler(
            dbClient,
            monitorReader,
            dbClient,
            dbClient,
            dbClient,
            ruleProcessor,
            TimeProvider.System);
        clearMonitorsOfflineFlag = new ClearMonitorsOfflineFlagHandler(monitorReader, dbClient);
        clearOlderErrorMessages = new ClearOlderErrorMessagesHandler(dbClient);
        storeDustLevels = new StoreDustLevelsHandler(
            gateway,
            monitorReader,
            dbClient,
            dbClient,
            dbClient,
            new MyAtmRuleEvaluator(),
            TimeProvider.System,
            options.MaxPagesPerMonitorPerRun);
        processDustLevels = new ProcessDustLevelsHandler(
            dbClient,
            dbClient,
            dbClient,
            dbClient,
            ruleProcessor,
            TimeProvider.System,
            testLocal);
        storeAccessoryInfo = new StoreAccessoryInfoHandler(gateway, monitorReader, dbClient, dbClient, dbClient, options.MaxPagesPerMonitorPerRun);
    }

    public Task StoreMonitorsAsync(int customerId, CancellationToken cancellationToken = default) =>
        storeMonitors.RunAsync(customerId, cancellationToken);


    public Task CheckForOfflineMonitorsAsync(int customerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return checkForOfflineMonitors.RunAsync(customerId, cancellationToken);
    }


    public Task ClearMonitorsOfflineFlagAsync(int customerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        clearMonitorsOfflineFlag.Run(customerId);
        return Task.CompletedTask;
    }


    public Task ClearOlderErrorMessagesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        clearOlderErrorMessages.Run();
        return Task.CompletedTask;
    }


    public Task StoreDustLevelsAsync<T>(int customerId, Period period, CancellationToken cancellationToken = default)
        where T : BaseDeviceMeasurement =>
        storeDustLevels.RunAsync<T>(customerId, period, cancellationToken);


    public Task ProcessDustLevelsAsync<T>(int customerId, Period period, CancellationToken cancellationToken = default)
        where T : BaseDeviceMeasurement
    {
        cancellationToken.ThrowIfCancellationRequested();
        return processDustLevels.RunAsync<T>(customerId, period, cancellationToken);
    }


    public Task StoreAccessoryInfoAsync(int customerId, CancellationToken cancellationToken = default) =>
        storeAccessoryInfo.RunAsync(customerId, cancellationToken);


    public Task DispatchOutboxAsync(CancellationToken cancellationToken = default) =>
        outboxDispatcher.DispatchDueAsync(cancellationToken);

    private static MonitorDeliveryDispatcher CreateDispatcher(
        IDBClient dbClient,
        IMqttClient mqttClient,
        IMessageService messageService,
        MyAtmMonitorOptions options) =>
        new(
            dbClient,
            dbClient,
            new MyAtmDeliveryFailureSink(dbClient),
            mqttClient,
            new LegacyNotificationDeliveryService(messageService),
            NullLogger<MonitorDeliveryDispatcher>.Instance,
            options.ToDeliveryOptions(RvtConfig.INSERT_TOPIC, RvtConfig.ALERT_TOPIC));

    private sealed class LegacyNotificationDeliveryService(IMessageService messageService)
        : INotificationDeliveryService
    {
        public Task SendAsync(
            NotificationDeliveryRequest request,
            CancellationToken cancellationToken = default)
        {
            LegacyMessageKind message = request.Kind switch
            {
                NotificationMessageKind.Alert => LegacyMessageKind.Alert,
                NotificationMessageKind.Caution => LegacyMessageKind.Caution,
                NotificationMessageKind.Offline => LegacyMessageKind.Offline,
                NotificationMessageKind.BatteryCaution => LegacyMessageKind.Battery_Caution,
                NotificationMessageKind.BatteryAlert => LegacyMessageKind.Battery_Alert,
                _ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind, "Unsupported notification kind.")
            };
            LegacyMessageChannel channel = request.Channel switch
            {
                NotificationChannel.Email => LegacyMessageChannel.Email,
                NotificationChannel.Sms => LegacyMessageChannel.SMS,
                _ => throw new ArgumentOutOfRangeException(nameof(request), request.Channel, "Unsupported notification channel.")
            };
            RvtContactDto contact = request.Channel == NotificationChannel.Email
                ? new RvtContactDto(true, false, request.Destination, null, null, null)
                : new RvtContactDto(false, true, string.Empty, request.Destination, null, null);
            return messageService.SendMessageAsync(
                message,
                channel,
                contact,
                request.MonitorName,
                request.CallbackUrl,
                cancellationToken);
        }
    }
}
