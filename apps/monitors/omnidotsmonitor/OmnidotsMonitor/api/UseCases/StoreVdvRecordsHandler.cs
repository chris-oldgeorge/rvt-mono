using Microsoft.Extensions.Logging;
using Omnidots.Api.Db;
using Omnidots.Api.Http;
using Omnidots.Model.Dto;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;

namespace Omnidots.Api.UseCases
{
    // Summary: Fetches Omnidots VDV records into the measurement store and publishes insert notifications.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiVibrationLevels).
    public class StoreVdvRecordsHandler
    {
        private readonly OmnidotsHttpGateway gateway;
        private readonly OmnidotsMonitorReader monitorReader;
        private readonly IOmnidotsMonitorCommands monitorCommands;
        private readonly IOmnidotsImportCursorQueries cursorQueries;
        private readonly IOmnidotsMeasurementImportCommands importCommands;
        private readonly IOmnidotsOperationalCommands operationalCommands;
        private readonly IMonitorEventPublisher eventPublisher;

        public StoreVdvRecordsHandler(
            OmnidotsHttpGateway gateway,
            OmnidotsMonitorReader monitorReader,
            IOmnidotsMonitorCommands monitorCommands,
            IOmnidotsImportCursorQueries cursorQueries,
            IOmnidotsMeasurementImportCommands importCommands,
            IOmnidotsOperationalCommands operationalCommands,
            IMonitorEventPublisher eventPublisher)
        {
            this.gateway = gateway;
            this.monitorReader = monitorReader;
            this.monitorCommands = monitorCommands;
            this.cursorQueries = cursorQueries;
            this.importCommands = importCommands;
            this.operationalCommands = operationalCommands;
            this.eventPublisher = eventPublisher;
        }

        public void Run(TimeSpan lookback)
        {
            var token = gateway.Authenticate().Token!;
            var monitors = monitorReader.ReadMonitors();
            var utcNow = DateTime.UtcNow;
            var failures = new List<OmnidotsMonitorFailure>();
            foreach (var monitor in monitors)
            {
                if ("OmniDots guest".Equals(monitor.CustomerDisplayName))
                {
                    RvtLogger.Logger.LogWarning("StoreVdvRecords Not collecting data for monitor={Value1}", monitor.CustomerDisplayName);
                    continue;
                }

                try
                {
                    var startTime = ResolveStart(monitor.SerialId, utcNow, lookback);
                    var records = gateway.GetVdvRecords(token, startTime, utcNow, monitor.SerialId);
                    var dtos = records!.Samples!
                        .Select(sample => new VdvRecordDto(sample))
                        .OrderBy(dto => dto.SampleTime)
                        .ToList();

                    if (dtos.Count > 0)
                    {
                        var newestSampleAt = dtos[^1].SampleTime;
                        var ps = DateTime.Now;
                        importCommands.ImportVdvRecords(monitor.SerialId, dtos, newestSampleAt);
                        var ts = DateTime.Now - ps;
                        RvtLogger.Logger.LogInformation("InsertVdvRecords for serialId={Value1} INSERT number of dtos={Value2} took={Value3}ms avg={Value4} ms",
                             monitor.SerialId, dtos.Count, ts.TotalMilliseconds, (ts.TotalMilliseconds / dtos.Count));

                        monitorCommands.SetMonitorOffline(monitor.Id, false);

                        eventPublisher.PublishDataInserted(newestSampleAt, monitor.SerialId);
                    }
                    else
                    {
                        RvtLogger.Logger.LogDebug("StoreVdvRecords no samples for serialId={Value1}", monitor.SerialId);
                    }
                }
                catch (Exception e)
                {
                    var msg = string.Format("StoreVdvRecords serialId={0}", monitor.SerialId);
                    RvtLogger.Logger.LogError(e, "StoreVdvRecords failed for serialId={Value1}", monitor.SerialId);
                    failures.Add(OmnidotsMonitorFailure.Record(
                        monitor.SerialId,
                        e,
                        () => operationalCommands.HandleException(msg, e)));
                }
            }

            if (failures.Count > 0)
            {
                throw new OmnidotsImportException("StoreVdvRecords", failures);
            }

        }

        private DateTime ResolveStart(string serialId, DateTime utcNow, TimeSpan lookback)
        {
            var cursor = cursorQueries.ReadImportCursor(
                serialId,
                OmnidotsMeasurementSeries.Vdv);
            var latestMeasurement = cursor ?? cursorQueries.ReadLatestMeasurementTime(
                serialId,
                OmnidotsMeasurementSeries.Vdv);
            return latestMeasurement.HasValue
                ? latestMeasurement.Value.AddMinutes(-5)
                : SampleFetchWindow.Start(utcNow, lookback, TimeSpan.FromMinutes(5));
        }
    }
}
