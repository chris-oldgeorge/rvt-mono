using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Storage;
using Svantek.Api.Db;
using Svantek.Api.Http;
using Svantek.Api.UseCases;
using Svantek.Model.Config;

namespace Svantek.Api;

// Summary: Async facade over the Svantek scheduled use-case handlers.
public class SvantekApi
{
    public static readonly DateTime JAN1_1970 = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public enum BatteryAlertType
    {
        Off = 0,
        BatteryAlert = 1,
        BatteryCaution = 2
    }

    private readonly StoreMonitorsHandler storeMonitors;
    private readonly StoreNoiseLevelsHandler storeNoiseLevels;
    private readonly NotifySiteAveragesHandler notifySiteAverages;
    private readonly CheckForOfflineMonitorsHandler checkForOfflineMonitors;
    private readonly NotifyBatteryLevelsHandler notifyBatteryLevels;
    private readonly CheckForSoundRecordingsHandler checkForSoundRecordings;

    public SvantekApi(
        IHttpClient httpClient,
        IDBClient dbClient,
        IMqttClient mqttClient,
        IMessageService messageService,
        string apiKey)
        : this(httpClient, dbClient, mqttClient, messageService, apiKey, RvtConfig.TESTLOCAL)
    {
    }

    public SvantekApi(
        IHttpClient httpClient,
        IDBClient dbClient,
        IMqttClient mqttClient,
        IMessageService messageService,
        string apiKey,
        bool testLocal)
        : this(
            httpClient,
            dbClient,
            mqttClient,
            messageService,
            apiKey,
            MissingBlobStorageService.Instance,
            testLocal)
    {
    }

    public SvantekApi(
        IHttpClient httpClient,
        IDBClient dbClient,
        IMqttClient mqttClient,
        IMessageService messageService,
        string apiKey,
        IBlobStorageService blobStorage)
        : this(
            httpClient,
            dbClient,
            mqttClient,
            messageService,
            apiKey,
            blobStorage,
            RvtConfig.TESTLOCAL)
    {
    }

    public SvantekApi(
        IHttpClient httpClient,
        IDBClient dbClient,
        IMqttClient mqttClient,
        IMessageService messageService,
        string apiKey,
        IBlobStorageService blobStorage,
        bool testLocal,
        NoiseRequestWindowCalculator? noiseRequestWindowCalculator = null,
        TimeProvider? timeProvider = null)
    {
        var gateway = new SvantekHttpGateway(httpClient, apiKey);
        var monitorReader = new SvantekMonitorReader(dbClient, testLocal);
        var eventPublisher = new MonitorEventPublisher(
            mqttClient,
            RvtConfig.INSERT_TOPIC,
            RvtConfig.ALERT_TOPIC);
        var ruleProcessor = new SvantekRuleProcessor(
            dbClient,
            dbClient,
            messageService,
            eventPublisher);
        var calculator = noiseRequestWindowCalculator ??
            new NoiseRequestWindowCalculator(new SvantekImportOptions());

        storeMonitors = new StoreMonitorsHandler(gateway, dbClient, dbClient, testLocal);
        storeNoiseLevels = new StoreNoiseLevelsHandler(
            gateway,
            monitorReader,
            dbClient,
            dbClient,
            dbClient,
            dbClient,
            ruleProcessor,
            calculator,
            timeProvider);
        notifySiteAverages = new NotifySiteAveragesHandler(
            dbClient,
            dbClient,
            dbClient,
            dbClient,
            ruleProcessor);
        checkForOfflineMonitors = new CheckForOfflineMonitorsHandler(
            dbClient,
            monitorReader,
            dbClient,
            dbClient,
            ruleProcessor);
        notifyBatteryLevels = new NotifyBatteryLevelsHandler(
            monitorReader,
            dbClient,
            dbClient,
            dbClient,
            ruleProcessor);
        checkForSoundRecordings = new CheckForSoundRecordingsHandler(
            dbClient,
            dbClient,
            gateway,
            blobStorage);
    }

    public Task StoreMonitorsAsync(CancellationToken cancellationToken = default) =>
        storeMonitors.RunAsync(cancellationToken);

    public Task StoreNoiseLevelsAsync(CancellationToken cancellationToken = default) =>
        storeNoiseLevels.RunAsync(cancellationToken);

    public Task NotifySiteAveragesAsync(
        DateTime date,
        CancellationToken cancellationToken = default) =>
        notifySiteAverages.RunAsync(date, cancellationToken);

    public Task CheckForOfflineMonitorsAsync(CancellationToken cancellationToken = default) =>
        checkForOfflineMonitors.RunAsync(cancellationToken);

    public Task NotifyBatteryLevelsAsync(CancellationToken cancellationToken = default) =>
        notifyBatteryLevels.RunAsync(cancellationToken);

    public Task CheckForSoundRecordingsAsync(CancellationToken cancellationToken = default) =>
        checkForSoundRecordings.RunAsync(cancellationToken);

    private sealed class MissingBlobStorageService : IBlobStorageService
    {
        public static MissingBlobStorageService Instance { get; } = new();

        public Task<BlobStorageWriteResult> WriteAsync(
            BlobStorageWriteRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "IBlobStorageService must be supplied to upload sound recordings.");

        public Task DeleteAsync(
            string objectName,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "IBlobStorageService must be supplied to delete sound recordings.");
    }
}
