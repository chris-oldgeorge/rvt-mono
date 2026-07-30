using Microsoft.Extensions.Logging;
using Omnidots.Api.Db;
using Omnidots.Api.Ports;
using Omnidots.Model.Config;
using Omnidots.Model.Dto;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;

namespace Omnidots.Api.UseCases
{
    // Summary: Fetches Omnidots VDV records into the measurement store and publishes insert notifications.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiVibrationLevels).
    public class StoreVdvRecordsHandler
    {
        private readonly IOmnidotsVendorGateway _gateway;
        private readonly OmnidotsMonitorReader _monitorReader;
        private readonly IOmnidotsMonitorCommands _monitorCommands;
        private readonly IOmnidotsImportCursorQueries _cursorQueries;
        private readonly IOmnidotsMeasurementImportCommands _importCommands;
        private readonly IOmnidotsOperationalCommands _operationalCommands;
        private readonly IMonitorEventPublisher _eventPublisher;
        private readonly OmnidotsImportOptions _importOptions;

        public StoreVdvRecordsHandler(
            IOmnidotsVendorGateway gateway,
            OmnidotsMonitorReader monitorReader,
            IOmnidotsMonitorCommands monitorCommands,
            IOmnidotsImportCursorQueries cursorQueries,
            IOmnidotsMeasurementImportCommands importCommands,
            IOmnidotsOperationalCommands operationalCommands,
            IMonitorEventPublisher eventPublisher,
            OmnidotsImportOptions? importOptions = null)
        {
            _gateway = gateway;
            _monitorReader = monitorReader;
            _monitorCommands = monitorCommands;
            _cursorQueries = cursorQueries;
            _importCommands = importCommands;
            _operationalCommands = operationalCommands;
            _eventPublisher = eventPublisher;
            _importOptions = importOptions ?? new OmnidotsImportOptions();
            _importOptions.Validate();
        }

        public async Task RunAsync(TimeSpan lookback, CancellationToken cancellationToken = default)
        {
            string token = (await _gateway.AuthenticateAsync(cancellationToken)).Token!;
            List<VibrationMonitorDto> monitors = _monitorReader.ReadMonitors();
            DateTime utcNow = DateTime.UtcNow;
            List<OmnidotsMonitorFailure> failures = await OmnidotsFleetImport.RunAsync(
                "StoreVdvRecords",
                monitors,
                _operationalCommands,
                async monitor =>
                {
                    if ("OmniDots guest".Equals(monitor.CustomerDisplayName))
                    {
                        RvtLogger.Logger.LogWarning("StoreVdvRecords Not collecting data for monitor={Value1}", monitor.CustomerDisplayName);
                        return;
                    }


                    DateTime startTime = ResolveStart(monitor.SerialId, utcNow, lookback);
                    // One request per bounded window instead of one unbounded
                    // request against the client's 4 MB / 30 s limits.
                    foreach ((DateTime windowStart, DateTime windowEnd) in SampleFetchWindow.Split(
                        startTime,
                        utcNow,
                        _importOptions.MaximumRequestWindow))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await StoreWindowAsync(monitor, windowStart, windowEnd, token, cancellationToken);
                    }
                },
                cancellationToken);

            OmnidotsFleetImport.ThrowIfAny("StoreVdvRecords", failures);

        }

        private async Task StoreWindowAsync(
            VibrationMonitorDto monitor,
            DateTime windowStart,
            DateTime windowEnd,
            string token,
            CancellationToken cancellationToken)
        {
            VdvRecords records = await _gateway.GetVdvRecordsAsync(token, windowStart, windowEnd, monitor.SerialId, cancellationToken);
            List<VdvRecordDto> dtos = [.. records!.Samples!
                .Select(sample => new VdvRecordDto(sample))
                .OrderBy(dto => dto.SampleTime)];

            if (dtos.Count == 0)
            {
                if (RvtLogger.Logger.IsEnabled(LogLevel.Debug))
                {
                    RvtLogger.Logger.LogDebug("StoreVdvRecords no samples for serialId={Value1}", monitor.SerialId);
                }

                return;
            }

            DateTime newestSampleAt = dtos[^1].SampleTime;
            DateTime ps = DateTime.Now;
            _importCommands.ImportVdvRecords(monitor.SerialId, dtos, newestSampleAt);
            TimeSpan ts = DateTime.Now - ps;
            if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
            {
                RvtLogger.Logger.LogInformation("InsertVdvRecords for serialId={Value1} INSERT number of dtos={Value2} took={Value3}ms avg={Value4} ms",
                     monitor.SerialId, dtos.Count, ts.TotalMilliseconds, (ts.TotalMilliseconds / dtos.Count));
            }

            _monitorCommands.SetMonitorOffline(monitor.Id, false);
            await _eventPublisher.PublishDataInsertedAsync(newestSampleAt, monitor.SerialId, cancellationToken: cancellationToken);
        }

        private DateTime ResolveStart(string serialId, DateTime utcNow, TimeSpan lookback)
        {
            DateTime? cursor = _cursorQueries.ReadImportCursor(
                serialId,
                OmnidotsMeasurementSeries.Vdv);
            DateTime? latestMeasurement = cursor ?? _cursorQueries.ReadLatestMeasurementTime(
                serialId,
                OmnidotsMeasurementSeries.Vdv);
            return latestMeasurement.HasValue
                ? latestMeasurement.Value.AddMinutes(-5)
                : SampleFetchWindow.Start(utcNow, lookback, TimeSpan.FromMinutes(5));
        }
    }
}
