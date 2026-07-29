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
public class StoreVeffRecordsHandler
{
    private readonly IOmnidotsVendorGateway _gateway;
    private readonly OmnidotsMonitorReader monitorReader;
    private readonly IOmnidotsMonitorCommands monitorCommands;
    private readonly IOmnidotsImportCursorQueries cursorQueries;
    private readonly IOmnidotsMeasurementImportCommands importCommands;
    private readonly IOmnidotsOperationalCommands operationalCommands;
    private readonly IMonitorEventPublisher eventPublisher;

    public StoreVeffRecordsHandler(
        IOmnidotsVendorGateway gateway,
        OmnidotsMonitorReader monitorReader,
        IOmnidotsMonitorCommands monitorCommands,
        IOmnidotsImportCursorQueries cursorQueries,
        IOmnidotsMeasurementImportCommands importCommands,
        IOmnidotsOperationalCommands operationalCommands,
        IMonitorEventPublisher eventPublisher)
    {
        _gateway = gateway;
        this.monitorReader = monitorReader;
        this.monitorCommands = monitorCommands;
        this.cursorQueries = cursorQueries;
        this.importCommands = importCommands;
        this.operationalCommands = operationalCommands;
        this.eventPublisher = eventPublisher;
    }

    public async Task RunAsync(TimeSpan lookback, CancellationToken cancellationToken = default)
    {
        string token = (await _gateway.AuthenticateAsync(cancellationToken)).Token!;
        List<VibrationMonitorDto> monitors = monitorReader.ReadMonitors();
        DateTime utcNow = DateTime.UtcNow;
        List<OmnidotsMonitorFailure> failures = await OmnidotsFleetImport.RunAsync(
            "StoreVeffRecords",
            monitors,
            operationalCommands,
            async monitor =>
            {
                if ("OmniDots guest".Equals(monitor.CustomerDisplayName))
                {
                    RvtLogger.Logger.LogWarning("Not collecting data for monitor={Value1}", monitor.CustomerDisplayName);
                    return;
                }

                DateTime startTime = ResolveStart(monitor.SerialId, utcNow, lookback);
                VeffRecords records = await _gateway.GetVeffRecordsAsync(token, startTime, utcNow, monitor.SerialId, cancellationToken);
                List<VeffRecordDto> dtos = [.. records!.Samples!
                    .Select(sample => new VeffRecordDto(sample))
                    .OrderBy(dto => dto.SampleTime)];

                if (dtos.Count > 0)
                {
                    DateTime newestSampleAt = dtos[^1].SampleTime;
                    DateTime ps = DateTime.Now;
                    importCommands.ImportVeffRecords(monitor.SerialId, dtos, newestSampleAt);
                    TimeSpan ts = DateTime.Now - ps;
                    RvtLogger.Logger.LogInformation("InsertVeffRecords for serialId={Value1} INSERT number of dtos={Value2} took={Value3}ms avg={Value4} ms",
                         monitor.SerialId, dtos.Count, ts.TotalMilliseconds, (ts.TotalMilliseconds / dtos.Count));

                    monitorCommands.SetMonitorOffline(monitor.Id, false);
                    await eventPublisher.PublishDataInsertedAsync(newestSampleAt, monitor.SerialId, cancellationToken: cancellationToken);
                }
                else
                {
                    RvtLogger.Logger.LogDebug("StoreVeffRecords no samples for serialId={Value1}", monitor.SerialId);
                }
            },
            cancellationToken);

        OmnidotsFleetImport.ThrowIfAny("StoreVeffRecords", failures);
    }

    private DateTime ResolveStart(string serialId, DateTime utcNow, TimeSpan lookback)
    {
        DateTime? cursor = cursorQueries.ReadImportCursor(
            serialId,
            OmnidotsMeasurementSeries.Veff);
        DateTime? latestMeasurement = cursor ?? cursorQueries.ReadLatestMeasurementTime(
            serialId,
            OmnidotsMeasurementSeries.Veff);
        return latestMeasurement.HasValue
            ? latestMeasurement.Value.AddMinutes(-5)
            : SampleFetchWindow.Start(utcNow, lookback, TimeSpan.FromMinutes(5));
    }
}
