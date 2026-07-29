using Microsoft.Extensions.Logging;
using Omnidots.Api.Db;
using Omnidots.Api.Ports;
using Omnidots.Model.Config;
using Omnidots.Model.Dto;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Utilities;

namespace Omnidots.Api.UseCases
{
    // Summary: Reads Omnidots trace lists and trace data into the measurement store.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiTraces).
    public class StoreTracesHandler
    {
        private readonly IOmnidotsVendorGateway _gateway;
        private readonly OmnidotsMonitorReader monitorReader;
        private readonly IOmnidotsMeasurementCommands measurementCommands;
        private readonly IOmnidotsOperationalCommands operationalCommands;
        private readonly IOmnidotsTraceQueries traceQueries;
        private readonly OmnidotsTraceCollectionOptions options;
        private readonly TimeProvider timeProvider;

        public StoreTracesHandler(
            IOmnidotsVendorGateway gateway,
            OmnidotsMonitorReader monitorReader,
            IOmnidotsMeasurementCommands measurementCommands,
            IOmnidotsOperationalCommands operationalCommands,
            IOmnidotsTraceQueries traceQueries,
            OmnidotsTraceCollectionOptions options,
            TimeProvider timeProvider)
        {
            _gateway = gateway;
            this.monitorReader = monitorReader;
            this.measurementCommands = measurementCommands;
            this.operationalCommands = operationalCommands;
            this.traceQueries = traceQueries;
            this.options = options;
            this.timeProvider = timeProvider;
        }

        public async Task RunAsync(DateTime last, CancellationToken cancellationToken = default)
        {
            long startedAt = timeProvider.GetTimestamp();
            List<VibrationMonitorDto> monitors = monitorReader.ReadMonitors(last);
            options.Validate();
            IReadOnlyList<VibrationMonitorDto> eligibleMonitors = EligibleMonitors(monitors);
            IReadOnlyDictionary<string, DateTime> latestTraceEndTimes = options.Enabled
                ? traceQueries.ReadLatestTraceEndTimes(
                    [.. eligibleMonitors.Select(monitor => monitor.SerialId)])
                    ?? new Dictionary<string, DateTime>()
                : new Dictionary<string, DateTime>();
            long rotationSlot = timeProvider.GetUtcNow().ToUnixTimeSeconds() / 300;
            IReadOnlyList<VibrationMonitorDto> selectedMonitors = OmnidotsTraceMonitorSelector.Select(
                monitors,
                latestTraceEndTimes,
                options,
                rotationSlot);
            int succeeded = 0;
            int tracesStored = 0;
            int samplesStored = 0;

            if (selectedMonitors.Count == 0)
            {
                LogSummary(eligibleMonitors.Count, 0, 0, 0, 0, 0, startedAt);
                return;
            }

            string token = (await _gateway.AuthenticateAsync(cancellationToken)).Token!;

            List<OmnidotsMonitorFailure> failures = await OmnidotsFleetImport.RunAsync(
                "StoreTraces",
                selectedMonitors,
                operationalCommands,
                async monitor =>
                {
                    TraceReadResult result = await ReadTracesAsync(token, monitor.SerialId, last, null, cancellationToken);
                    succeeded++;
                    tracesStored += result.TraceCount;
                    samplesStored += result.SampleCount;
                },
                cancellationToken);

            LogSummary(
                eligibleMonitors.Count,
                selectedMonitors.Count,
                succeeded,
                failures.Count,
                tracesStored,
                samplesStored,
                startedAt);

            OmnidotsFleetImport.ThrowIfAny("StoreTraces", failures);
        }

        private async Task<TraceReadResult> ReadTracesAsync(string token, string serialId, DateTime start, DateTime? end, CancellationToken cancellationToken)
        {
            TracesListResponse tracesList = await _gateway.GetTracesListAsync(token, serialId, start, end, cancellationToken);

            if (tracesList.Traces == null)
            {
                RvtLogger.Logger.LogInformation("ReadTraces for serialId={Value1} tracelist is empty.",
                    serialId);
                return new TraceReadResult(0, 0);
            }

            RvtLogger.Logger.LogInformation("ReadTraces for serialId={Value1}  traceslist size={Value2}",
                serialId, tracesList.Traces.Count);

            int traceCount = 0;
            int sampleCount = 0;
            foreach (TraceInfo traceInfo in tracesList.Traces)
            {
                DateTime tStart = DateTimeUtil.FromMillis(traceInfo.StartTime);
                DateTime tEnd = DateTimeUtil.FromMillis(traceInfo.EndTime);

                TracesReponse tracesResponse = await _gateway.GetTracesAsync(token, serialId, tStart, tEnd, cancellationToken);
                List<TraceData> traces = tracesResponse.Traces ?? [];
                RvtLogger.Logger.LogInformation("Number of traces={Value1}", traces.Count);
                measurementCommands.WriteTraces(serialId, traces);
                traceCount += traces.Count;
                sampleCount += traces.Sum(trace => Math.Max(
                    trace.X?.Count ?? 0,
                    Math.Max(trace.Y?.Count ?? 0, trace.Z?.Count ?? 0)));
            }

            return new TraceReadResult(traceCount, sampleCount);
        }

        private IReadOnlyList<VibrationMonitorDto> EligibleMonitors(
            IReadOnlyCollection<VibrationMonitorDto> monitors)
        {
            if (!options.Enabled)
            {
                return [];
            }

            if (options.AllowedSerialIds.Length == 0)
            {
                return [.. monitors];
            }

            HashSet<string> allowedSerialIds = new(options.AllowedSerialIds, StringComparer.OrdinalIgnoreCase);
            return [.. monitors.Where(monitor => allowedSerialIds.Contains(monitor.SerialId))];
        }

        private void LogSummary(
            int eligible,
            int attempted,
            int succeeded,
            int failed,
            int tracesStored,
            int samplesStored,
            long startedAt)
        {
            RvtLogger.Logger.LogInformation(
                "StoreTraces completed eligible={Eligible} attempted={Attempted} succeeded={Succeeded} failed={Failed} tracesStored={TracesStored} samplesStored={SamplesStored} elapsedMs={ElapsedMs}",
                eligible,
                attempted,
                succeeded,
                failed,
                tracesStored,
                samplesStored,
                timeProvider.GetElapsedTime(startedAt).TotalMilliseconds);
        }

        private sealed record TraceReadResult(int TraceCount, int SampleCount);
    }
}
