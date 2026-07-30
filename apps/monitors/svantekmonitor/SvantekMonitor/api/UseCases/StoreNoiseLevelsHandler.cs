using System.Data;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Rules;
using Svantek.Api.Db;
using Svantek.Api.Ports;
using Svantek.Model.Dto;
using Svantek.Model.Http;

namespace Svantek.Api.UseCases;

// Summary: Reads Svantek noise samples, persists them, updates latest timestamps, and evaluates alert rules.
public sealed class StoreNoiseLevelsHandler
{
    private const string _vendorDateFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly ISvantekVendorGateway _gateway;
    private readonly SvantekMonitorReader _monitorReader;
    private readonly ISvantekRuleQueries _ruleQueries;
    private readonly ISvantekMonitorCommands _monitorCommands;
    private readonly ISvantekMeasurementCommands _measurementCommands;
    private readonly ISvantekOperationalCommands _operationalCommands;
    private readonly SvantekRuleProcessor _ruleProcessor;
    private readonly NoiseRequestWindowCalculator _windowCalculator;
    private readonly TimeProvider _timeProvider;

    public StoreNoiseLevelsHandler(
        ISvantekVendorGateway gateway,
        SvantekMonitorReader monitorReader,
        ISvantekRuleQueries ruleQueries,
        ISvantekMonitorCommands monitorCommands,
        ISvantekMeasurementCommands measurementCommands,
        ISvantekOperationalCommands operationalCommands,
        SvantekRuleProcessor ruleProcessor,
        NoiseRequestWindowCalculator windowCalculator,
        TimeProvider? timeProvider = null)
    {
        _gateway = gateway;
        _monitorReader = monitorReader;
        _ruleQueries = ruleQueries;
        _monitorCommands = monitorCommands;
        _measurementCommands = measurementCommands;
        _operationalCommands = operationalCommands;
        _ruleProcessor = ruleProcessor;
        _windowCalculator = windowCalculator;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Fleet reads are setup. Failure here means no independent project unit can be identified,
        // so it must fault immediately rather than be converted into an aggregate.
        List<NoiseMonitorReadDto> monitors = await _monitorReader.ReadMonitorsAsync(
            lastDataTime: null,
            cancellationToken).ConfigureAwait(false);
        DateTime utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        SvantekFailureCollector failures = new(_operationalCommands);

        foreach (IGrouping<int, NoiseMonitorReadDto> projectMonitors in monitors.GroupBy(monitor => monitor.ProjectId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            int projectId = projectMonitors.Key;
            try
            {
                await StoreProjectAsync(
                    projectId,
                    [.. projectMonitors],
                    utcNow,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Capture($"StoreNoiseLevels project {projectId}", exception, cancellationToken);
            }
        }

        failures.ThrowIfAny("StoreNoiseLevels");
    }

    private async Task StoreProjectAsync(
        int projectId,
        IReadOnlyList<NoiseMonitorReadDto> monitors,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (RvtLogger.Logger.IsEnabled(LogLevel.Debug))
        {
            RvtLogger.Logger.LogDebug("StoreNoiseLevels reading for project {ProjectId}", projectId);
        }
        Dictionary<int, IReadOnlyList<NoiseRequestWindow>> windowsByMonitor = monitors.ToDictionary(
            monitor => monitor.PointId,
            monitor => _windowCalculator.Calculate(
                monitor.DeployedStart,
                monitor.LastDataTime,
                monitor.LastStatusTimestamp,
                utcNow));
        int requestCount = windowsByMonitor.Count == 0
            ? 0
            : windowsByMonitor.Max(pair => pair.Value.Count);

        for (int requestIndex = 0; requestIndex < requestCount; requestIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Dictionary<int, NoiseMonitorReadDto> requestedMonitors = monitors
                .Where(monitor => windowsByMonitor[monitor.PointId].Count > requestIndex)
                .ToDictionary(monitor => monitor.PointId);
            List<MultiDataArgument> arguments = [.. requestedMonitors.Values.Select(monitor =>
            {
                NoiseRequestWindow window = windowsByMonitor[monitor.PointId][requestIndex];
                if (RvtLogger.Logger.IsEnabled(LogLevel.Debug))
                {
                    RvtLogger.Logger.LogDebug(
                        "StoreNoiseLevels request monitor {SerialId} from {Start} to {End}",
                        monitor.SerialId,
                        window.Start,
                        window.End);
                }
                return new MultiDataArgument
                {
                    point = monitor.PointId,
                    time_from = window.Start.ToString(_vendorDateFormat, CultureInfo.InvariantCulture),
                    time_to = window.End.ToString(_vendorDateFormat, CultureInfo.InvariantCulture)
                };
            })];

            List<MultiData> response = await _gateway.GetDataMultiAsync(
                projectId.ToString(CultureInfo.InvariantCulture),
                arguments,
                cancellationToken).ConfigureAwait(false);
            foreach (MultiData monitorData in response)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!requestedMonitors.TryGetValue(monitorData.point, out NoiseMonitorReadDto? monitor))
                {
                    throw new InvalidOperationException(
                        $"Monitor data point {monitorData.point} was not found in project {projectId}.");
                }

                if (monitorData.data.status == "ok")
                {
                    await StoreMonitorSamplesAsync(
                        monitor,
                        monitorData,
                        cancellationToken).ConfigureAwait(false);
                }
                else if (monitorData.data.status == "no_data")
                {
                    if (RvtLogger.Logger.IsEnabled(LogLevel.Debug))
                    {
                        RvtLogger.Logger.LogDebug(
                            "StoreNoiseLevels no data for {SerialId}",
                            monitor.SerialId);
                    }
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Monitor {monitor.SerialId} returned status {monitorData.data.status}.");
                }
            }
        }
    }

    private async Task StoreMonitorSamplesAsync(
        NoiseMonitorReadDto monitor,
        MultiData monitorData,
        CancellationToken cancellationToken)
    {
        if (RvtLogger.Logger.IsEnabled(LogLevel.Debug))
        {
            RvtLogger.Logger.LogDebug("StoreNoiseLevels data received for {SerialId}", monitor.SerialId);
        }
        DataTable table = CreateResultsTable();
        DateTime? firstDataTime = null;
        DateTime? lastDataTime = null;

        foreach (DataPoint? measuringData in monitorData.data.results.SelectMany(result => result.data))
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTime sampleTime = DateTime.Parse(measuringData.timestamp, CultureInfo.InvariantCulture);
            DataRow row = table.NewRow();
            row["SerialId"] = monitor.SerialId;
            row["SampleTime"] = sampleTime;
            row["LAeq"] = ParseLevel(measuringData.values[0]);
            row["LAmax"] = ParseLevel(measuringData.values[1]);
            row["LA90"] = ParseLevel(measuringData.values[2]);
            row["LA10"] = ParseLevel(measuringData.values[3]);
            row["LCeq"] = ParseLevel(measuringData.values[4]);
            row["LCmax"] = ParseLevel(measuringData.values[5]);
            row["LC90"] = ParseLevel(measuringData.values[6]);
            row["LC10"] = ParseLevel(measuringData.values[7]);
            table.Rows.Add(row);
            firstDataTime ??= sampleTime;
            if (!lastDataTime.HasValue || lastDataTime < sampleTime)
            {
                lastDataTime = sampleTime;
            }
        }

        if (table.Rows.Count == 0)
        {
            return;
        }

        if (!firstDataTime.HasValue || !lastDataTime.HasValue)
        {
            throw new InvalidOperationException(
                $"Noise sample timestamps were unavailable for monitor {monitor.SerialId}.");
        }

        await _measurementCommands.InsertNoiseRecordsTableAsync(table, cancellationToken).ConfigureAwait(false);
        DateTime utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        // Clamp the watermark to min(sampleTime, utcNow). A future-dated vendor
        // timestamp persisted as LastDataTime15Min would push every subsequent
        // request window past the wall clock, so no data would ever be asked
        // for again.
        DateTime watermark = lastDataTime.Value > utcNow
            ? DateTime.SpecifyKind(utcNow, lastDataTime.Value.Kind)
            : lastDataTime.Value;
        DateTime start = monitor.PeriodStartDate;
        DateTime end = watermark;
        int startHour = (start.Hour / 8) * 8;
        start = new DateTime(start.Year, start.Month, start.Day, startHour, 0, 0, start.Kind);
        if (start == firstDataTime.Value)
        {
            start = start.AddHours(-8);
        }

        for (DateTime endPeriod = start.AddHours(8); endPeriod <= end; endPeriod = endPeriod.AddHours(8))
        {
            await _measurementCommands.Create8hourAverageAsync(
                monitor.SerialId,
                endPeriod,
                cancellationToken).ConfigureAwait(false);
        }

        await _monitorCommands.WriteLatestTimestampAsync(
            monitor.SerialId,
            watermark,
            cancellationToken).ConfigureAwait(false);
        if (monitor.Offline && watermark > utcNow.AddDays(-1))
        {
            await _monitorCommands.SetMonitorOfflineAsync(
                monitor.Id,
                offline: false,
                cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        List<RvtAlertRuleDto> rules = _ruleQueries.ReadRules(monitor.SerialId);
        await _ruleProcessor.ProcessRulesAsync(monitor, rules, monitor.PeriodStartDate, watermark, cancellationToken).ConfigureAwait(false);
    }

    private static DataTable CreateResultsTable()
    {
        DataTable table = new() { TableName = "Results" };
        table.Columns.Add("SerialId", typeof(string));
        table.Columns.Add("SampleTime", typeof(DateTime));
        foreach (string? field in new[] { "LAeq", "LAmax", "LA90", "LA10", "LCeq", "LCmax", "LC90", "LC10" })
        {
            table.Columns.Add(field, typeof(double)).AllowDBNull = true;
        }

        return table;
    }

    // Vendor levels are invariant-formatted; anything unparseable is stored as
    // NULL (the columns allow it) instead of a fabricated 0.0 dB reading that
    // would drag averages down and reset latched rules.
    private static object ParseLevel(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : DBNull.Value;
}
