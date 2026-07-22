using AirQ.Api.UseCases;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;

namespace AirQ.Api
{
    // Summary: Service entry points that schedule AirQ monitor import, alerting, and liveness checks.
    // Major updates:
    // - 2026-07-12 DI composition: dependencies are injected; wiring moved to AirQMonitorServices.
    // - 2026-07-12 TimerInfo removal: dropped the unused Azure Functions-era TimerInfo parameters.
    public sealed class AirQService : IAirQDateImporter
    {
        private readonly AirQApi airQApi;

        public AirQService(AirQApi airQApi)
        {
            this.airQApi = airQApi;
        }

        public void StoreMonitors()
        {
            // limit on get monitors is 24 times a day, get at 2 minutes past the hour.
            airQApi.StoreMonitors(RvtConfig.USER_ID, RvtConfig.USER_AUTH);
        }

        public void CheckForOfflineMonitors()
        {
            airQApi.CheckForOfflineMonitors();
        }

        public void StoreNoiseLevels()
        {
            // data is updated every 15 mins at 0, 15, 30 and 45 mins past the hour
            // timer trigger is 5 minutes after this in case of delay
            airQApi.StoreNoiseLevels(RvtConfig.USER_ID, RvtConfig.USER_AUTH);
        }

        public void StoreNoiseLevelsForDate(string date)
        {
            airQApi.StoreNoiseLevelsForDate(RvtConfig.USER_ID, RvtConfig.USER_AUTH, date);
        }

        public void StoreAllNoiseLevelsForYesterday()
        {
            // runs every day at 3 am
            airQApi.StoreAllNoiseLevelsForYesterday(RvtConfig.USER_ID, RvtConfig.USER_AUTH);
        }

        public void NotifySiteAverages()
        {

            // fixme - problem with running at 00:05 means that users wont be notified
            // maybe split and run the collection at 00:05 and the notify at 09:00 next day
            airQApi.NotifySiteAverages(DateTime.Today.AddDays(-1));
        }

        public void ClearOlderErrorMessages()
        {
            airQApi.ClearOlderErrorMessages();
        }

    }
}
