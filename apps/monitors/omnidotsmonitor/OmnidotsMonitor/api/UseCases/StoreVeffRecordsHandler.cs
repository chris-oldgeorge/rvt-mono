using Microsoft.Extensions.Logging;
using Omnidots.Api.Db;
using Omnidots.Api.Ports;
using Omnidots.Model.Dto;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;

namespace Omnidots.Api.UseCases;

// Summary: Fetches Omnidots Veff records into the measurement store and publishes insert notifications.
// Major updates:
// - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiVibrationLevels).
public class StoreVeffRecordsHandler(
    IOmnidotsVendorGateway gateway,
    OmnidotsMonitorReader monitorReader,
    IOmnidotsMonitorCommands monitorCommands,
    IOmnidotsImportCursorQueries cursorQueries,
    IOmnidotsMeasurementImportCommands importCommands,
    IOmnidotsOperationalCommands operationalCommands,
    IMonitorEventPublisher eventPublisher)
{
    private readonly IOmnidotsVendorGateway _gateway = gateway;
    private readonly OmnidotsMonitorReader _monitorReader = monitorReader;
    private readonly IOmnidotsMonitorCommands _monitorCommands = monitorCommands;
    private readonly IOmnidotsImportCursorQueries _cursorQueries = cursorQueries;
    private readonly IOmnidotsMeasurementImportCommands _importCommands = importCommands;
    private readonly IOmnidotsOperationalCommands _operationalCommands = operationalCommands;
    private readonly IMonitorEventPublisher _eventPublisher = eventPublisher;

    public async Task RunAsync(TimeSpan lookback, CancellationToken cancellationToken = default)
    {
        string token = (await _gateway.AuthenticateAsync(cancellationToken)).Token!;
        List<VibrationMonitorDto> monitors = _monitorReader.ReadMonitors();
        DateTime utcNow = DateTime.UtcNow;
        List<OmnidotsMonitorFailure> failures = [];
        foreach (VibrationMonitorDto monitor in monitors)
        {

            if ("OmniDots guest".Equals(monitor.CustomerDisplayName))
            {
                RvtLogger.Logger.LogWarning("Not collecting data for monitor={Value1}", monitor.CustomerDisplayName);
                continue;
            }
            try
            {
                DateTime startTime = ResolveStart(monitor.SerialId, utcNow, lookback);
                VeffRecords records = await _gateway.GetVeffRecordsAsync(token, startTime, utcNow, monitor.SerialId, cancellationToken);
                List<VeffRecordDto> dtos = [.. records!.Samples!
                    .Select(sample => new VeffRecordDto(sample))
                    .OrderBy(dto => dto.SampleTime)];

                if (dtos.Count > 0)
                {
                    DateTime newestSampleAt = dtos[^1].SampleTime;
                    DateTime ps = DateTime.Now;
                    _importCommands.ImportVeffRecords(monitor.SerialId, dtos, newestSampleAt);
                    TimeSpan ts = DateTime.Now - ps;
                    RvtLogger.Logger.LogInformation("InsertVeffRecords for serialId={Value1} INSERT number of dtos={Value2} took={Value3}ms avg={Value4} ms",
                         monitor.SerialId, dtos.Count, ts.TotalMilliseconds, (ts.TotalMilliseconds / dtos.Count));

                    _monitorCommands.SetMonitorOffline(monitor.Id, false);
                    await _eventPublisher.PublishDataInsertedAsync(newestSampleAt, monitor.SerialId, cancellationToken: cancellationToken);
                }
                else
                {
                    RvtLogger.Logger.LogDebug("StoreVeffRecords no samples for serialId={Value1}", monitor.SerialId);
                }
            }
            catch (Exception e)
            {
                string msg = string.Format("StoreVeffRecords serialId={0}", monitor.SerialId);
                RvtLogger.Logger.LogError(e, "StoreVeffRecords failed for serialId={Value1}", monitor.SerialId);
                failures.Add(OmnidotsMonitorFailure.Record(
                    monitor.SerialId,
                    e,
                    () => _operationalCommands.HandleException(msg, e)));
            }
        }

        if (failures.Count > 0)
        {
            throw new OmnidotsImportException("StoreVeffRecords", failures);
        }
    }

    private DateTime ResolveStart(string serialId, DateTime utcNow, TimeSpan lookback)
    {
        DateTime? cursor = _cursorQueries.ReadImportCursor(
            serialId,
            OmnidotsMeasurementSeries.Veff);
        DateTime? latestMeasurement = cursor ?? _cursorQueries.ReadLatestMeasurementTime(
            serialId,
            OmnidotsMeasurementSeries.Veff);
        return latestMeasurement.HasValue
            ? latestMeasurement.Value.AddMinutes(-5)
            : SampleFetchWindow.Start(utcNow, lookback, TimeSpan.FromMinutes(5));
    }
}
