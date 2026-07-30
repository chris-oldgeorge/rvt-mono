using MyAtm.Api.UseCases;
using MyAtm.Delivery;
using MyAtm.Model;
using MyAtm.Model.Config;
using MyAtm.Model.Json;

namespace MyAtm.Api
{
    // Summary: Service entry points that schedule MyAtm monitor import, alerting, and liveness checks.
    // Major updates:
    // - 2026-07-12 DI composition: dependencies are injected; wiring moved to MyAtmMonitorServices.
    // - 2026-07-12 TimerInfo removal: dropped the unused Azure Functions-era TimerInfo parameters.
    public class MyAtmService : IMyAtmMonitorJobs
    {
        private readonly StoreMonitorsHandler _storeMonitors;
        private readonly CheckForOfflineMonitorsHandler _checkForOfflineMonitors;
        private readonly StoreDustLevelsHandler _storeDustLevels;
        private readonly ProcessDustLevelsHandler _processDustLevels;
        private readonly ClearOlderErrorMessagesHandler _clearOlderErrorMessages;
        private readonly StoreAccessoryInfoHandler _storeAccessoryInfo;
        private readonly MonitorDeliveryDispatcher _outboxDispatcher;
        private readonly int _customerId;

        public MyAtmService(
            StoreMonitorsHandler storeMonitors,
            CheckForOfflineMonitorsHandler checkForOfflineMonitors,
            StoreDustLevelsHandler storeDustLevels,
            ProcessDustLevelsHandler processDustLevels,
            ClearOlderErrorMessagesHandler clearOlderErrorMessages,
            StoreAccessoryInfoHandler storeAccessoryInfo,
            MonitorDeliveryDispatcher outboxDispatcher,
            MyAtmMonitorOptions options)
        {
            _storeMonitors = storeMonitors;
            _checkForOfflineMonitors = checkForOfflineMonitors;
            _storeDustLevels = storeDustLevels;
            _processDustLevels = processDustLevels;
            _clearOlderErrorMessages = clearOlderErrorMessages;
            _storeAccessoryInfo = storeAccessoryInfo;
            _outboxDispatcher = outboxDispatcher;
            _customerId = options.CustomerId;
        }

        public Task StoreMonitorsAsync(CancellationToken cancellationToken = default)
        {
            // update the devices list once per hour
            return _storeMonitors.RunAsync(_customerId, cancellationToken);
        }

        public Task CheckForOfflineMonitorsAsync(CancellationToken cancellationToken = default)
        {
            return _checkForOfflineMonitors.RunAsync(_customerId, cancellationToken);
        }

        public Task StoreDustLevelsAsync(CancellationToken cancellationToken = default)
        {
            // MyAtmosphere API will update dust levels every minute
            return _storeDustLevels.RunAsync<DeviceMeasurement>(_customerId, Period.Minutes1, cancellationToken);
        }

        public Task Store15MinAverageDustLevelsAsync(CancellationToken cancellationToken = default)
        {
            // Every 15 mins at 1 minute past the quater hr.
            return _storeDustLevels.RunAsync<AvgDeviceMeasurement>(_customerId, Period.Minutes15, cancellationToken);
        }

        public Task Store1HourAverageDustLevelsAsync(CancellationToken cancellationToken = default)
        {
            // 1 hr avg. every hour
            return _storeDustLevels.RunAsync<AvgDeviceMeasurement>(_customerId, Period.Hours1, cancellationToken);
        }

        public Task Store24HourAverageDustLevelsAsync(CancellationToken cancellationToken = default)
        {
            // 24 hr avg. once per day 10 mins past midnight
            return _storeDustLevels.RunAsync<AvgDeviceMeasurement>(_customerId, Period.Hours24, cancellationToken);
        }

        public Task Process8HourAverageDustLevelsAsync(CancellationToken cancellationToken = default)
        {
            // 8 hr avg. every hour at 1 min past the hour
            return _processDustLevels.RunAsync<AvgDeviceMeasurement>(_customerId, Period.Hours8, cancellationToken);
        }

        public Task ClearOlderErrorMessagesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _clearOlderErrorMessages.Run();
            return Task.CompletedTask;
        }

        public Task StoreAccessoryInfoAsync(CancellationToken cancellationToken = default)
        {
            // collect accessoory info every night - may not be needed
            return _storeAccessoryInfo.RunAsync(_customerId, cancellationToken);
        }

        public Task DispatchOutboxAsync(CancellationToken cancellationToken = default) =>
            _outboxDispatcher.DispatchDueAsync(cancellationToken);
    }
}
