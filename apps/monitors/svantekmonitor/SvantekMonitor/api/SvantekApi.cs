using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Configuration;
using Rvt.Storage;
using Svantek.Api.Db;
using Svantek.Api.Http;
using Svantek.Api.Ports;
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

    private readonly StoreMonitorsHandler _storeMonitors;
    private readonly StoreNoiseLevelsHandler _storeNoiseLevels;
    private readonly NotifySiteAveragesHandler _notifySiteAverages;
    private readonly CheckForOfflineMonitorsHandler _checkForOfflineMonitors;
    private readonly NotifyBatteryLevelsHandler _notifyBatteryLevels;
    private readonly CheckForSoundRecordingsHandler _checkForSoundRecordings;
    private readonly ClearOlderErrorMessagesHandler _clearOlderErrorMessages;

    public SvantekApi(
        IHttpClient httpClient,
        IDBClient dbClient,
        IAlertIngressPort alertIngress,
        string apiKey)
        : this(httpClient, dbClient, alertIngress, apiKey, RvtConfig.TESTLOCAL)
    {
    }

    public SvantekApi(
        IHttpClient httpClient,
        IDBClient dbClient,
        IAlertIngressPort alertIngress,
        string apiKey,
        bool testLocal)
        : this(
            httpClient,
            dbClient,
            alertIngress,
            apiKey,
            MissingObjectStorageClientFactory.Instance,
            testLocal)
    {
    }

    public SvantekApi(
        IHttpClient httpClient,
        IDBClient dbClient,
        IAlertIngressPort alertIngress,
        string apiKey,
        IObjectStorageClientFactory storageFactory)
        : this(
            httpClient,
            dbClient,
            alertIngress,
            apiKey,
            storageFactory,
            RvtConfig.TESTLOCAL)
    {
    }

    public SvantekApi(
        IHttpClient httpClient,
        IDBClient dbClient,
        IAlertIngressPort alertIngress,
        string apiKey,
        IObjectStorageClientFactory storageFactory,
        bool testLocal,
        NoiseRequestWindowCalculator? noiseRequestWindowCalculator = null,
        TimeProvider? timeProvider = null)
    {
        ISvantekVendorGateway gateway = new SvantekHttpGateway(httpClient, apiKey);
        SvantekMonitorReader monitorReader = new(dbClient, testLocal);
        SvantekRuleProcessor ruleProcessor = new(
            dbClient,
            dbClient,
            alertIngress);
        NoiseRequestWindowCalculator calculator = noiseRequestWindowCalculator ??
            new NoiseRequestWindowCalculator(new SvantekImportOptions());

        _storeMonitors = new StoreMonitorsHandler(gateway, dbClient, dbClient, testLocal);
        _storeNoiseLevels = new StoreNoiseLevelsHandler(
            gateway,
            monitorReader,
            dbClient,
            dbClient,
            dbClient,
            dbClient,
            ruleProcessor,
            calculator,
            timeProvider);
        _notifySiteAverages = new NotifySiteAveragesHandler(
            dbClient,
            dbClient,
            dbClient,
            dbClient,
            ruleProcessor);
        _checkForOfflineMonitors = new CheckForOfflineMonitorsHandler(
            dbClient,
            monitorReader,
            dbClient,
            dbClient,
            ruleProcessor);
        _notifyBatteryLevels = new NotifyBatteryLevelsHandler(
            monitorReader,
            dbClient,
            dbClient,
            dbClient,
            ruleProcessor);
        _checkForSoundRecordings = new CheckForSoundRecordingsHandler(
            dbClient,
            dbClient,
            gateway,
            storageFactory);
        _clearOlderErrorMessages = new ClearOlderErrorMessagesHandler(dbClient);
    }

    public Task StoreMonitorsAsync(CancellationToken cancellationToken = default) =>
        _storeMonitors.RunAsync(cancellationToken);

    public Task StoreNoiseLevelsAsync(CancellationToken cancellationToken = default) =>
        _storeNoiseLevels.RunAsync(cancellationToken);

    public Task NotifySiteAveragesAsync(
        DateTime date,
        CancellationToken cancellationToken = default) =>
        _notifySiteAverages.RunAsync(date, cancellationToken);

    public Task CheckForOfflineMonitorsAsync(CancellationToken cancellationToken = default) =>
        _checkForOfflineMonitors.RunAsync(cancellationToken);

    public Task NotifyBatteryLevelsAsync(CancellationToken cancellationToken = default) =>
        _notifyBatteryLevels.RunAsync(cancellationToken);

    public Task CheckForSoundRecordingsAsync(CancellationToken cancellationToken = default) =>
        _checkForSoundRecordings.RunAsync(cancellationToken);

    public Task ClearOlderErrorMessagesAsync(CancellationToken cancellationToken = default) =>
        _clearOlderErrorMessages.RunAsync(cancellationToken);

    private sealed class MissingObjectStorageClientFactory : IObjectStorageClientFactory
    {
        public static MissingObjectStorageClientFactory Instance { get; } = new();

        public IObjectStorageClient GetRequiredClient(string resourceName) =>
            MissingObjectStorageClient.Instance;
    }

    private sealed class MissingObjectStorageClient : IObjectStorageClient
    {
        public static MissingObjectStorageClient Instance { get; } = new();

        public Task<StorageWriteResult> WriteAsync(
            StorageWriteRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "IObjectStorageClientFactory must be supplied to upload sound recordings.");

        public Task<StorageReadResult?> OpenReadAsync(
            StorageObjectKey key,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "IObjectStorageClientFactory must be supplied to read sound recordings.");

        public Uri GetObjectUri(StorageObjectKey key) =>
            throw new InvalidOperationException(
                "IObjectStorageClientFactory must be supplied to resolve sound recording URIs.");

        public Task<bool> DeleteIfExistsAsync(
            StorageObjectKey key,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "IObjectStorageClientFactory must be supplied to delete sound recordings.");
    }
}
