using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;

namespace Omnidots.Api
{
    // Summary: Service entry points that schedule Omnidots monitor import, alerting, and liveness checks.
    // Major updates:
    // - 2026-07-12 DI composition: dependencies are injected; wiring moved to OmnidotsMonitorServices.
    // - 2026-07-12 TimerInfo removal: dropped the Azure Functions-era TimerInfo parameters; StoreTraces takes the window start directly.
    public class OmnidotsService
    {

        private readonly OmnidotsApi omnidotsApi;

        public OmnidotsService(OmnidotsApi omnidotsApi)
        {
            this.omnidotsApi = omnidotsApi;
        }

        public string Liveness() => RvtConfig.SERVICE_NAME + RvtConfig.SERVICE_VERSION;

        public void StoreMonitors()
        {
            omnidotsApi.StoreMonitors();
        }

        public void CheckForOfflineMonitors()
        {
            omnidotsApi.CheckForOfflineMonitors();
        }

        public void StorePeakRecordsLastDataTime()
        {
            omnidotsApi.StorePeakRecordsLastDataTime();
        }

        public void StoreVeffRecords(TimeSpan lookback)
        {
            omnidotsApi.StoreVeffRecords(lookback);
        }

        public void StoreVdvRecords(TimeSpan lookback)
        {
            omnidotsApi.StoreVdvRecords(lookback);
        }

        public void StoreTraces(DateTime since)
        {
            omnidotsApi.StoreTraces(since);
        }

        public void NotifyBatteryLevels()
        {
            omnidotsApi.NotifyBatteryLevels();
        }

        public void ClearOlderErrorMessages()
        {
            omnidotsApi.ClearOlderErrorMessages();
        }

        public Task MonitoringAsync(CancellationToken cancellationToken = default)
        {
            RvtLogger.Logger.LogInformation("Starting Monitoring");
            return omnidotsApi.MonitoringAsync(cancellationToken);
        }
    }
}
