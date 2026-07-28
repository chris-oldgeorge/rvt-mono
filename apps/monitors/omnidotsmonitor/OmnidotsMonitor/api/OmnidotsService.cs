using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;

namespace Omnidots.Api;

// Summary: Service entry points that schedule Omnidots monitor import, alerting, and liveness checks.
// Major updates:
// - 2026-07-12 DI composition: dependencies are injected; wiring moved to OmnidotsMonitorServices.
// - 2026-07-12 TimerInfo removal: dropped the Azure Functions-era TimerInfo parameters; StoreTraces takes the window start directly.
public class OmnidotsService(OmnidotsApi omnidotsApi)
{

    private readonly OmnidotsApi omnidotsApi = omnidotsApi;

    public Task StoreMonitorsAsync(CancellationToken cancellationToken = default)
    {
        return omnidotsApi.StoreMonitorsAsync(cancellationToken);
    }

    public Task CheckForOfflineMonitorsAsync(CancellationToken cancellationToken = default)
    {
        return omnidotsApi.CheckForOfflineMonitorsAsync(cancellationToken);
    }

    public Task StorePeakRecordsLastDataTimeAsync(CancellationToken cancellationToken = default)
    {
        return omnidotsApi.StorePeakRecordsLastDataTimeAsync(cancellationToken);
    }

    public Task StoreVeffRecordsAsync(TimeSpan lookback, CancellationToken cancellationToken = default)
    {
        return omnidotsApi.StoreVeffRecordsAsync(lookback, cancellationToken);
    }

    public Task StoreVdvRecordsAsync(TimeSpan lookback, CancellationToken cancellationToken = default)
    {
        return omnidotsApi.StoreVdvRecordsAsync(lookback, cancellationToken);
    }

    public Task StoreTracesAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        return omnidotsApi.StoreTracesAsync(since, cancellationToken);
    }

    public Task NotifyBatteryLevelsAsync(CancellationToken cancellationToken = default)
    {
        return omnidotsApi.NotifyBatteryLevelsAsync(cancellationToken);
    }

    public Task ClearOlderErrorMessagesAsync(CancellationToken cancellationToken = default)
    {
        return omnidotsApi.ClearOlderErrorMessagesAsync(cancellationToken);
    }

    public Task MonitoringAsync(CancellationToken cancellationToken = default)
    {
        RvtLogger.Logger.LogInformation("Starting Monitoring");
        return omnidotsApi.MonitoringAsync(cancellationToken);
    }
}
