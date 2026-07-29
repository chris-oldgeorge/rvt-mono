using System.Data;
using Microsoft.Extensions.Logging;
using Omnidots.Api.Db;
using Omnidots.Api.Ports;
using Omnidots.Model.Dto;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;

namespace Omnidots.Api.UseCases;

// Summary: Fetches Omnidots peak records into the measurement store, with backfill variants driven by last-data timestamps.
// Major updates:
// - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiVibrationLevels).
public class StorePeakRecordsHandler(
    IOmnidotsVendorGateway gateway,
    OmnidotsMonitorReader monitorReader,
    IOmnidotsMonitorQueries monitorQueries,
    IOmnidotsImportCursorQueries cursorQueries,
    IOmnidotsMeasurementImportCommands importCommands,
    IOmnidotsOperationalCommands operationalCommands,
    IMonitorEventPublisher eventPublisher)
{
    private readonly IOmnidotsVendorGateway _gateway = gateway;
    private readonly OmnidotsMonitorReader _monitorReader = monitorReader;
    private readonly IOmnidotsMonitorQueries _monitorQueries = monitorQueries;
    private readonly IOmnidotsImportCursorQueries _cursorQueries = cursorQueries;
    private readonly IOmnidotsMeasurementImportCommands _importCommands = importCommands;
    private readonly IOmnidotsOperationalCommands _operationalCommands = operationalCommands;
    private readonly IMonitorEventPublisher _eventPublisher = eventPublisher;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        RvtLogger.Logger.LogInformation("StorePeakRecords called");
        List<VibrationMonitorDto> monitors = _monitorReader.ReadMonitors();
        string token = (await _gateway.AuthenticateAsync(cancellationToken)).Token!;
        DateTime utcNow = DateTime.UtcNow;
        await RunFleetAsync(monitors, async monitor =>
        {
            DateTime startTime = ResolvePeakStart(monitor);
            await StorePeakRecordsAsync(monitor: monitor, startTime: startTime, endTime: utcNow, token: token,
                cancellationToken: cancellationToken);
        }, RecordFailure, cancellationToken);
    }

    private DateTime ResolvePeakStart(VibrationMonitorDto monitor)
    {
        DateTime? cursor = _cursorQueries.ReadImportCursor(
            monitor.SerialId,
            OmnidotsMeasurementSeries.Peak);
        if (cursor.HasValue)
        {
            return cursor.Value.AddMinutes(-5);
        }

        DateTime? latestMeasurement = _cursorQueries.ReadLatestMeasurementTime(
            monitor.SerialId,
            OmnidotsMeasurementSeries.Peak);
        if (latestMeasurement.HasValue)
        {
            return latestMeasurement.Value.AddMinutes(-5);
        }

        DateTime deployDate = monitor.DeployDate ?? _monitorQueries.ReadDeployStartDate(monitor.Id);
        DateTime fallback = monitor.LastDataTime.HasValue && monitor.LastDataTime.Value > deployDate
            ? monitor.LastDataTime.Value
            : deployDate;
        return fallback.AddMinutes(-5);
    }

    private static async Task RunFleetAsync(
        IEnumerable<VibrationMonitorDto> monitors,
        Func<VibrationMonitorDto, Task> import,
        Action<string, Exception, List<OmnidotsMonitorFailure>> recordFailure,
        CancellationToken cancellationToken)
    {
        List<OmnidotsMonitorFailure> failures = [];
        foreach (VibrationMonitorDto monitor in monitors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await import(monitor);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                recordFailure(monitor.SerialId, e, failures);
            }
        }

        if (failures.Count > 0)
        {
            throw new OmnidotsImportException("StorePeakRecords", failures);
        }
    }

    private async Task<int> StorePeakRecordsAsync(VibrationMonitorDto monitor, DateTime startTime, DateTime? endTime, string token, CancellationToken cancellationToken)
    {
        if (RvtLogger.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
        {
            RvtLogger.Logger.LogInformation("StorePeakRecords for serialId={Value1} startTime={Value2} endTime={Value3}",
            monitor.SerialId, startTime, endTime);
        }

        if ("OmniDots guest".Equals(monitor.CustomerDisplayName))
        {
            RvtLogger.Logger.LogWarning("Not collecting data for monitor name={Value1}", monitor.CustomerDisplayName);
            return -1;
        }

        PeakRecords records = await _gateway.GetPeakRecordsAsync(token: token, startTime: startTime, endTime: endTime,
                                             measuringPointId: monitor.SerialId, cancellationToken: cancellationToken);

        DataTable table = new()
        {
            TableName = "Results"
        };

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

        foreach (PeakSample? sample in records!.Samples!.OrderBy(sample => sample.Timestamp))
        {
            DataRow row = table.NewRow();
            row["SerialId"] = monitor.SerialId;
            DateTimeOffset offset = DateTimeOffset.FromUnixTimeMilliseconds((long)sample.Timestamp);
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
            DateTime newestSampleAt = table.Rows
                .Cast<DataRow>()
                .Max(row => (DateTime)row["SampleTime"]);
            DateTime ps = DateTime.Now;
            _importCommands.ImportPeakRecords(monitor.SerialId, table, newestSampleAt);
            TimeSpan ts = DateTime.Now - ps;
            if (RvtLogger.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
            {
                RvtLogger.Logger.LogInformation("StorePeakRecords for serialId={Value1} INSERT number of dtos={Value2} took={Value3}ms avg={Value4} ms",
                 monitor.SerialId, table.Rows.Count, ts.TotalMilliseconds, (ts.TotalMilliseconds / table.Rows.Count));
            }
            monitor.LastDataTime = newestSampleAt;
            await _eventPublisher.PublishDataInsertedAsync(newestSampleAt, monitor.SerialId, cancellationToken: cancellationToken);
        }
        else
        {
            if (RvtLogger.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information)) { RvtLogger.Logger.LogInformation("StorePeakRecords no samples for serialId={Value1}", monitor.SerialId); }
        }
        return table.Rows.Count;
    }

    private void RecordFailure(string serialId, Exception exception, List<OmnidotsMonitorFailure> failures)
    {
        string msg = string.Format("StorePeakRecords serialId={0}", serialId);
        RvtLogger.Logger.LogError(exception, "StorePeakRecords failed for serialId={Value1}", serialId);
        failures.Add(OmnidotsMonitorFailure.Record(
            serialId,
            exception,
            () => _operationalCommands.HandleException(msg, exception)));
    }
}
