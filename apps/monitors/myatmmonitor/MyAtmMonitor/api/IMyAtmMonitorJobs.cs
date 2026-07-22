namespace MyAtm.Api;

public interface IMyAtmMonitorJobs
{
    Task StoreMonitorsAsync(CancellationToken cancellationToken = default);
    Task CheckForOfflineMonitorsAsync(CancellationToken cancellationToken = default);
    Task StoreDustLevelsAsync(CancellationToken cancellationToken = default);
    Task Store15MinAverageDustLevelsAsync(CancellationToken cancellationToken = default);
    Task Store1HourAverageDustLevelsAsync(CancellationToken cancellationToken = default);
    Task Store24HourAverageDustLevelsAsync(CancellationToken cancellationToken = default);
    Task Process8HourAverageDustLevelsAsync(CancellationToken cancellationToken = default);
    Task ClearOlderErrorMessagesAsync(CancellationToken cancellationToken = default);
    Task StoreAccessoryInfoAsync(CancellationToken cancellationToken = default);

    Task DispatchOutboxAsync(CancellationToken cancellationToken = default);
}
