using AirQ.Api.Db;
using AirQ.Api.Http;
using AirQ.Api.Ports;
using AirQ.Api.UseCases;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Mqtt;

namespace AirQ.Api;

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
        IAirQVendorGateway gateway = new AirQHttpGateway(httpClient);
        AirQTestLocalMonitorFilter testLocalFilter = AirQTestLocalMonitorFilter.Create(testLocal, testLocalSerialId);
        AirQMonitorReader monitorReader = new(dbClient, testLocalFilter);
        MonitorEventPublisher eventPublisher = new(mqttClient, RvtConfig.INSERT_TOPIC, RvtConfig.ALERT_TOPIC);
        AirQRuleProcessor ruleProcessor = new(dbClient, dbClient, messageService, eventPublisher);

        storeMonitors = new StoreMonitorsHandler(gateway, dbClient, dbClient, testLocalFilter);
        checkForOfflineMonitors = new CheckForOfflineMonitorsHandler(dbClient, monitorReader, dbClient, ruleProcessor);
        storeNoiseLevels = new StoreNoiseLevelsHandler(gateway, monitorReader, dbClient, dbClient, dbClient, dbClient, eventPublisher, ruleProcessor);
        storeNoiseLevelsForDate = new StoreNoiseLevelsForDateHandler(gateway, monitorReader, dbClient, dbClient);
        storeAllNoiseLevelsForYesterday = new StoreAllNoiseLevelsForYesterdayHandler(storeNoiseLevelsForDate);
        notifySiteAverages = new NotifySiteAveragesHandler(dbClient, dbClient, dbClient, dbClient, ruleProcessor);
        clearOlderErrorMessages = new ClearOlderErrorMessagesHandler(dbClient);
    }

    public Task StoreMonitorsAsync(string userId, string userAuth, CancellationToken cancellationToken = default) =>
        storeMonitors.RunAsync(userId, userAuth, cancellationToken);

    public Task CheckForOfflineMonitorsAsync(CancellationToken cancellationToken = default) =>
        checkForOfflineMonitors.RunAsync(cancellationToken);

    public Task StoreNoiseLevelsAsync(string userId, string userAuth, CancellationToken cancellationToken = default) =>
        storeNoiseLevels.RunAsync(userId, userAuth, cancellationToken);

    public Task StoreNoiseLevelsForDateAsync(string userId, string userAuth, string dateStr, CancellationToken cancellationToken = default) =>
        storeNoiseLevelsForDate.RunAsync(userId, userAuth, dateStr, cancellationToken);

    public Task StoreAllNoiseLevelsForYesterdayAsync(string userId, string userAuth, CancellationToken cancellationToken = default) =>
        storeAllNoiseLevelsForYesterday.RunAsync(userId, userAuth, cancellationToken);

    public Task NotifySiteAveragesAsync(DateTime date, CancellationToken cancellationToken = default) =>
        notifySiteAverages.RunAsync(date, cancellationToken);

    public Task ClearOlderErrorMessagesAsync(CancellationToken cancellationToken = default) =>
        clearOlderErrorMessages.RunAsync(cancellationToken);
}
