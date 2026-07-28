using AirQ.Api.UseCases;
using Rvt.Monitor.Common.Configuration;

namespace AirQ.Api;

// Summary: Service entry points that schedule AirQ monitor import, alerting, and liveness checks.
// Major updates:
// - 2026-07-12 DI composition: dependencies are injected; wiring moved to AirQMonitorServices.
// - 2026-07-12 TimerInfo removal: dropped the unused Azure Functions-era TimerInfo parameters.
public sealed class AirQService(AirQApi airQApi) : IAirQDateImporter
{
    private readonly AirQApi _airQApi = airQApi;

    public Task StoreMonitorsAsync(CancellationToken cancellationToken = default)
    {
        // limit on get monitors is 24 times a day, get at 2 minutes past the hour.
        return _airQApi.StoreMonitorsAsync(RvtConfig.USER_ID, RvtConfig.USER_AUTH, cancellationToken);
    }

    public Task CheckForOfflineMonitorsAsync(CancellationToken cancellationToken = default)
    {
        return _airQApi.CheckForOfflineMonitorsAsync(cancellationToken);
    }

    public Task StoreNoiseLevelsAsync(CancellationToken cancellationToken = default)
    {
        // data is updated every 15 mins at 0, 15, 30 and 45 mins past the hour
        // timer trigger is 5 minutes after this in case of delay
        return _airQApi.StoreNoiseLevelsAsync(RvtConfig.USER_ID, RvtConfig.USER_AUTH, cancellationToken);
    }

    public Task StoreNoiseLevelsForDateAsync(string date, CancellationToken cancellationToken = default)
    {
        return _airQApi.StoreNoiseLevelsForDateAsync(RvtConfig.USER_ID, RvtConfig.USER_AUTH, date, cancellationToken);
    }

    public Task StoreAllNoiseLevelsForYesterdayAsync(CancellationToken cancellationToken = default)
    {
        // runs every day at 3 am
        return _airQApi.StoreAllNoiseLevelsForYesterdayAsync(RvtConfig.USER_ID, RvtConfig.USER_AUTH, cancellationToken);
    }

    public Task NotifySiteAveragesAsync(CancellationToken cancellationToken = default)
    {

        // fixme - problem with running at 00:05 means that users wont be notified
        // maybe split and run the collection at 00:05 and the notify at 09:00 next day
        return _airQApi.NotifySiteAveragesAsync(DateTime.Today.AddDays(-1), cancellationToken);
    }

    public Task ClearOlderErrorMessagesAsync(CancellationToken cancellationToken = default)
    {
        return _airQApi.ClearOlderErrorMessagesAsync(cancellationToken);
    }

}
