using MyAtm.Api.UseCases;
using MyAtm.Model.Config;
using MyAtm.Model.Json;
using Rvt.Monitor.Common.Delivery;

namespace MyAtm.Api;

// Summary: Service entry points that schedule MyAtm monitor import, alerting, and liveness checks.
// Major updates:
// - 2026-07-12 DI composition: dependencies are injected; wiring moved to MyAtmMonitorServices.
// - 2026-07-12 TimerInfo removal: dropped the unused Azure Functions-era TimerInfo parameters.
public class MyAtmService(
    StoreMonitorsHandler storeMonitors,
    CheckForOfflineMonitorsHandler checkForOfflineMonitors,
    StoreDustLevelsHandler storeDustLevels,
    ProcessDustLevelsHandler processDustLevels,
    ClearOlderErrorMessagesHandler clearOlderErrorMessages,
    StoreAccessoryInfoHandler storeAccessoryInfo,
    MonitorDeliveryDispatcher outboxDispatcher,
    MyAtmMonitorOptions options) : IMyAtmMonitorJobs
{
    private readonly StoreMonitorsHandler storeMonitors = storeMonitors;
    private readonly CheckForOfflineMonitorsHandler checkForOfflineMonitors = checkForOfflineMonitors;
    private readonly StoreDustLevelsHandler storeDustLevels = storeDustLevels;
    private readonly ProcessDustLevelsHandler processDustLevels = processDustLevels;
    private readonly ClearOlderErrorMessagesHandler clearOlderErrorMessages = clearOlderErrorMessages;
    private readonly StoreAccessoryInfoHandler storeAccessoryInfo = storeAccessoryInfo;
    private readonly MonitorDeliveryDispatcher outboxDispatcher = outboxDispatcher;
    private readonly int customerId = options.CustomerId;

    public Task StoreMonitorsAsync(CancellationToken cancellationToken = default)
    {
        // update the devices list once per hour
        return storeMonitors.RunAsync(customerId, cancellationToken);
    }

    public Task CheckForOfflineMonitorsAsync(CancellationToken cancellationToken = default)
    {
        return checkForOfflineMonitors.RunAsync(customerId, cancellationToken);
    }

    public Task StoreDustLevelsAsync(CancellationToken cancellationToken = default)
    {
        // MyAtmosphere API will update dust levels every minute
        return storeDustLevels.RunAsync<DeviceMeasurement>(customerId, Period.Minutes1, cancellationToken);
    }

    public Task Store15MinAverageDustLevelsAsync(CancellationToken cancellationToken = default)
    {
        // Every 15 mins at 1 minute past the quater hr.
        return storeDustLevels.RunAsync<AvgDeviceMeasurement>(customerId, Period.Minutes15, cancellationToken);
    }

    public Task Store1HourAverageDustLevelsAsync(CancellationToken cancellationToken = default)
    {
        // 1 hr avg. every hour
        return storeDustLevels.RunAsync<AvgDeviceMeasurement>(customerId, Period.Hours1, cancellationToken);
    }

    public Task Store24HourAverageDustLevelsAsync(CancellationToken cancellationToken = default)
    {
        // 24 hr avg. once per day 10 mins past midnight
        return storeDustLevels.RunAsync<AvgDeviceMeasurement>(customerId, Period.Hours24, cancellationToken);
    }

    public Task Process8HourAverageDustLevelsAsync(CancellationToken cancellationToken = default)
    {
        // 8 hr avg. every hour at 1 min past the hour
        return processDustLevels.RunAsync<AvgDeviceMeasurement>(customerId, Period.Hours8, cancellationToken);
    }

    public Task ClearOlderErrorMessagesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        clearOlderErrorMessages.Run();
        return Task.CompletedTask;
    }

    public Task StoreAccessoryInfoAsync(CancellationToken cancellationToken = default)
    {
        // collect accessoory info every night - may not be needed
        return storeAccessoryInfo.RunAsync(customerId, cancellationToken);
    }

    public Task DispatchOutboxAsync(CancellationToken cancellationToken = default) =>
        outboxDispatcher.DispatchDueAsync(cancellationToken);
}
