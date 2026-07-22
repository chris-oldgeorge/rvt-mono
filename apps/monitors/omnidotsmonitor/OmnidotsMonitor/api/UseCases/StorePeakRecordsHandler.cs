using System.Data;
using Microsoft.Extensions.Logging;
using Omnidots.Api.Db;
using Omnidots.Api.Http;
using Omnidots.Model.Dto;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;

namespace Omnidots.Api.UseCases
{
    // Summary: Fetches Omnidots peak records into the measurement store, with backfill variants driven by last-data timestamps.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiVibrationLevels).
    public class StorePeakRecordsHandler
    {
        private readonly OmnidotsHttpGateway gateway;
        private readonly OmnidotsMonitorReader monitorReader;
        private readonly IOmnidotsMonitorQueries monitorQueries;
        private readonly IOmnidotsImportCursorQueries cursorQueries;
        private readonly IOmnidotsMeasurementImportCommands importCommands;
        private readonly IOmnidotsOperationalCommands operationalCommands;
        private readonly IMonitorEventPublisher eventPublisher;

        public StorePeakRecordsHandler(
            OmnidotsHttpGateway gateway,
            OmnidotsMonitorReader monitorReader,
            IOmnidotsMonitorQueries monitorQueries,
            IOmnidotsImportCursorQueries cursorQueries,
            IOmnidotsMeasurementImportCommands importCommands,
            IOmnidotsOperationalCommands operationalCommands,
            IMonitorEventPublisher eventPublisher)
        {
            this.gateway = gateway;
            this.monitorReader = monitorReader;
            this.monitorQueries = monitorQueries;
            this.cursorQueries = cursorQueries;
            this.importCommands = importCommands;
            this.operationalCommands = operationalCommands;
            this.eventPublisher = eventPublisher;
        }

        public void Run()
        {
            RvtLogger.Logger.LogInformation("StorePeakRecords called");
            var monitors = monitorReader.ReadMonitors();
            var token = gateway.Authenticate().Token!;
            var utcNow = DateTime.UtcNow;
            RunFleet(monitors, monitor =>
            {
                var startTime = ResolvePeakStart(monitor);
                StorePeakRecords(monitor: monitor, startTime: startTime, endTime: utcNow, token: token);
            });
        }

        private DateTime ResolvePeakStart(VibrationMonitorDto monitor)
        {
            var cursor = cursorQueries.ReadImportCursor(
                monitor.SerialId,
                OmnidotsMeasurementSeries.Peak);
            if (cursor.HasValue)
            {
                return cursor.Value.AddMinutes(-5);
            }

            var latestMeasurement = cursorQueries.ReadLatestMeasurementTime(
                monitor.SerialId,
                OmnidotsMeasurementSeries.Peak);
            if (latestMeasurement.HasValue)
            {
                return latestMeasurement.Value.AddMinutes(-5);
            }

            var deployDate = monitor.DeployDate ?? monitorQueries.ReadDeployStartDate(monitor.Id);
            var fallback = monitor.LastDataTime.HasValue && monitor.LastDataTime.Value > deployDate
                ? monitor.LastDataTime.Value
                : deployDate;
            return fallback.AddMinutes(-5);
        }

        private void RunFleet(IEnumerable<VibrationMonitorDto> monitors, Action<VibrationMonitorDto> import)
        {
            var failures = new List<OmnidotsMonitorFailure>();
            foreach (var monitor in monitors)
            {
                try
                {
                    import(monitor);
                }
                catch (Exception e)
                {
                    RecordFailure(monitor.SerialId, e, failures);
                }
            }

            if (failures.Count > 0)
            {
                throw new OmnidotsImportException("StorePeakRecords", failures);
            }
        }

        private int StorePeakRecords(VibrationMonitorDto monitor, DateTime startTime, DateTime? endTime, string token)
        {
            RvtLogger.Logger.LogInformation("StorePeakRecords for serialId={Value1} startTime={Value2} endTime={Value3}",
                monitor.SerialId, startTime, endTime);

            if ("OmniDots guest".Equals(monitor.CustomerDisplayName))
            {
                RvtLogger.Logger.LogWarning("Not collecting data for monitor name={Value1}", monitor.CustomerDisplayName);
                return -1;
            }

            var records = gateway.GetPeakRecords(token: token, startTime: startTime, endTime: endTime,
                                                 measuringPointId: monitor.SerialId);

            DataTable table = new DataTable();
            table.TableName = "Results";

            table.Columns.Add("SerialId", typeof(string));
            table.Columns.Add("SampleTime", typeof(DateTime));
            DataColumn dc = table.Columns.Add("XFdom", typeof(double));
            dc.AllowDBNull = true;
            dc = table.Columns.Add("XVtop", typeof(double));
            dc.AllowDBNull = true;
            dc = table.Columns.Add("XVtopOverflow", typeof(double));
            dc.AllowDBNull = true;
            dc = table.Columns.Add("YFdom", typeof(double));
            dc.AllowDBNull = true;
            dc = table.Columns.Add("YVtop", typeof(double));
            dc.AllowDBNull = true;
            dc = table.Columns.Add("YVtopOverflow", typeof(double));
            dc.AllowDBNull = true;
            dc = table.Columns.Add("ZFdom", typeof(double));
            dc.AllowDBNull = true;
            dc = table.Columns.Add("ZVtop", typeof(double));
            dc.AllowDBNull = true;
            dc = table.Columns.Add("ZVtopOverflow", typeof(double));
            dc.AllowDBNull = true;

            foreach (var sample in records!.Samples!.OrderBy(sample => sample.Timestamp))
            {
                var row = table.NewRow();
                row["SerialId"] = monitor.SerialId;
                var offset = DateTimeOffset.FromUnixTimeMilliseconds((long)sample.Timestamp);
                row["SampleTime"] = offset.DateTime;
                if (sample.X != null)
                {
                    row["XFdom"] = sample.X.Fdom;
                    row["XVtop"] = sample.X.Vtop;
                    row["XVtopOverflow"] = sample.X.VtopOverflow;
                }
                if (sample.Y != null)
                {
                    row["YFdom"] = sample.Y.Fdom;
                    row["YVtop"] = sample.Y.Vtop;
                    row["YVtopOverflow"] = sample.Y.VtopOverflow;
                }
                if (sample.Z != null)
                {
                    row["ZFdom"] = sample.Z.Fdom;
                    row["ZVtop"] = sample.Z.Vtop;
                    row["ZVtopOverflow"] = sample.Z.VtopOverflow;
                }
                table.Rows.Add(row);
            }

            if (table.Rows.Count > 0)
            {
                var newestSampleAt = table.Rows
                    .Cast<DataRow>()
                    .Max(row => (DateTime)row["SampleTime"]);
                var ps = DateTime.Now;
                importCommands.ImportPeakRecords(monitor.SerialId, table, newestSampleAt);
                var ts = DateTime.Now - ps;
                RvtLogger.Logger.LogInformation("StorePeakRecords for serialId={Value1} INSERT number of dtos={Value2} took={Value3}ms avg={Value4} ms",
                     monitor.SerialId, table.Rows.Count, ts.TotalMilliseconds, (ts.TotalMilliseconds / table.Rows.Count));
                monitor.LastDataTime = newestSampleAt;
                eventPublisher.PublishDataInserted(newestSampleAt, monitor.SerialId);
            }
            else
            {
                RvtLogger.Logger.LogInformation("StorePeakRecords no samples for serialId={Value1}", monitor.SerialId);
            }
            return table.Rows.Count;
        }

        private void RecordFailure(string serialId, Exception exception, ICollection<OmnidotsMonitorFailure> failures)
        {
            var msg = string.Format("StorePeakRecords serialId={0}", serialId);
            RvtLogger.Logger.LogError(exception, "StorePeakRecords failed for serialId={Value1}", serialId);
            failures.Add(OmnidotsMonitorFailure.Record(
                serialId,
                exception,
                () => operationalCommands.HandleException(msg, exception)));
        }
    }
}
