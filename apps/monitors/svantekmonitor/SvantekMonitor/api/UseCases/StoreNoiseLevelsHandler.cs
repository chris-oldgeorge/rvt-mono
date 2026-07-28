using System.Data;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Rules;
using Svantek.Api.Db;
using Svantek.Api.Http;
using Svantek.Model.Http;
using SvantekMonitor.model.dto;

namespace Svantek.Api.UseCases;

// Summary: Reads Svantek noise samples, persists them, updates latest timestamps, and evaluates alert rules.
public sealed class StoreNoiseLevelsHandler(
    SvantekHttpGateway gateway,
    SvantekMonitorReader monitorReader,
    ISvantekRuleQueries ruleQueries,
    ISvantekMonitorCommands monitorCommands,
    ISvantekMeasurementCommands measurementCommands,
    ISvantekOperationalCommands operationalCommands,
    SvantekRuleProcessor ruleProcessor,
    NoiseRequestWindowCalculator windowCalculator,
    TimeProvider? timeProvider = null)
{
    private const string VendorDateFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly SvantekHttpGateway gateway = gateway;
    private readonly SvantekMonitorReader monitorReader = monitorReader;
    private readonly ISvantekRuleQueries ruleQueries = ruleQueries;
    private readonly ISvantekMonitorCommands monitorCommands = monitorCommands;
    private readonly ISvantekMeasurementCommands measurementCommands = measurementCommands;
    private readonly ISvantekOperationalCommands operationalCommands = operationalCommands;
    private readonly SvantekRuleProcessor ruleProcessor = ruleProcessor;
    private readonly NoiseRequestWindowCalculator windowCalculator = windowCalculator;
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Fleet reads are setup. Failure here means no independent project unit can be identified,
        // so it must fault immediately rather than be converted into an aggregate.
        List<NoiseMonitorReadDto> monitors = await monitorReader.ReadMonitorsAsync(
            lastDataTime: null,
            cancellationToken).ConfigureAwait(false);
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        SvantekFailureCollector failures = new(operationalCommands);

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
                failures.Capture($"StoreNoiseLevels project {projectId}", exception);
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
        RvtLogger.Logger.LogDebug("StoreNoiseLevels reading for project {ProjectId}", projectId);
        Dictionary<int, IReadOnlyList<NoiseRequestWindow>> windowsByMonitor = monitors.ToDictionary(
            monitor => monitor.PointId,
            monitor => windowCalculator.Calculate(
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
                RvtLogger.Logger.LogDebug(
                    "StoreNoiseLevels request monitor {SerialId} from {Start} to {End}",
                    monitor.SerialId,
                    window.Start,
                    window.End);
                return new MultiDataArgument
                {
                    point = monitor.PointId,
                    time_from = window.Start.ToString(VendorDateFormat, CultureInfo.InvariantCulture),
                    time_to = window.End.ToString(VendorDateFormat, CultureInfo.InvariantCulture)
                };
            })];

            List<MultiData> response = await gateway.GetDataMultiAsync(
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
                    RvtLogger.Logger.LogDebug(
                        "StoreNoiseLevels no data for {SerialId}",
                        monitor.SerialId);
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
        RvtLogger.Logger.LogDebug("StoreNoiseLevels data received for {SerialId}", monitor.SerialId);
        DataTable table = CreateResultsTable();
        DateTime? firstDataTime = null;
        DateTime? lastDataTime = null;

        foreach (DataPoint? measuringData in monitorData.data.results.SelectMany(result => result.data))
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTime sampleTime = DateTime.Parse(measuringData.timestamp);
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

        await measurementCommands.InsertNoiseRecordsTableAsync(table, cancellationToken).ConfigureAwait(false);
        DateTime start = monitor.PeriodStartDate;
        DateTime end = lastDataTime.Value;
        int startHour = (start.Hour / 8) * 8;
        start = new DateTime(start.Year, start.Month, start.Day, startHour, 0, 0, start.Kind);
        if (start == firstDataTime.Value)
        {
            start = start.AddHours(-8);
        }

        for (DateTime endPeriod = start.AddHours(8); endPeriod <= end; endPeriod = endPeriod.AddHours(8))
        {
            await measurementCommands.Create8hourAverageAsync(
                monitor.SerialId,
                endPeriod,
                cancellationToken).ConfigureAwait(false);
        }

        await monitorCommands.WriteLatestTimestampAsync(
            monitor.SerialId,
            lastDataTime.Value,
            cancellationToken).ConfigureAwait(false);
        if (monitor.Offline && lastDataTime > timeProvider.GetUtcNow().UtcDateTime.AddDays(-1))
        {
            await monitorCommands.SetMonitorOfflineAsync(
                monitor.Id,
                offline: false,
                cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        List<RvtAlertRuleDto> rules = ruleQueries.ReadRules(monitor.SerialId);
        ruleProcessor.ProcessRules(monitor, rules, monitor.PeriodStartDate, lastDataTime.Value);
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

    private static double ParseLevel(string value) =>
        double.TryParse(value, out double parsed)
            ? parsed
            : 0.0;
}
