using Microsoft.Extensions.Logging;
using Omnidots.Api.Db;
using Omnidots.Api.Http;
using Omnidots.Model.Config;
using Omnidots.Model.Dto;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Utilities;

namespace Omnidots.Api.UseCases
{
    // Summary: Reads Omnidots trace lists and trace data into the measurement store.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiTraces).
    public class StoreTracesHandler
    {
        private readonly OmnidotsHttpGateway gateway;
        private readonly OmnidotsMonitorReader monitorReader;
        private readonly IOmnidotsMeasurementCommands measurementCommands;
        private readonly IOmnidotsOperationalCommands operationalCommands;
        private readonly IOmnidotsTraceQueries traceQueries;
        private readonly OmnidotsTraceCollectionOptions options;
        private readonly TimeProvider timeProvider;

        public StoreTracesHandler(
            OmnidotsHttpGateway gateway,
            OmnidotsMonitorReader monitorReader,
            IOmnidotsMeasurementCommands measurementCommands,
            IOmnidotsOperationalCommands operationalCommands,
            IOmnidotsTraceQueries traceQueries,
            OmnidotsTraceCollectionOptions options,
            TimeProvider timeProvider)
        {
            this.gateway = gateway;
            this.monitorReader = monitorReader;
            this.measurementCommands = measurementCommands;
            this.operationalCommands = operationalCommands;
            this.traceQueries = traceQueries;
            this.options = options;
            this.timeProvider = timeProvider;
        }

        public void Run(DateTime last)
        {
            var startedAt = timeProvider.GetTimestamp();
            var monitors = monitorReader.ReadMonitors(last);
            options.Validate();
            var eligibleMonitors = EligibleMonitors(monitors);
            var latestTraceEndTimes = options.Enabled
                ? traceQueries.ReadLatestTraceEndTimes(
                    eligibleMonitors.Select(monitor => monitor.SerialId).ToArray())
                    ?? new Dictionary<string, DateTime>()
                : new Dictionary<string, DateTime>();
            var rotationSlot = timeProvider.GetUtcNow().ToUnixTimeSeconds() / 300;
            var selectedMonitors = OmnidotsTraceMonitorSelector.Select(
                monitors,
                latestTraceEndTimes,
                options,
                rotationSlot);
            var failures = new List<OmnidotsMonitorFailure>();
            var succeeded = 0;
            var tracesStored = 0;
            var samplesStored = 0;

            if (selectedMonitors.Count == 0)
            {
                LogSummary(eligibleMonitors.Count, 0, 0, 0, 0, 0, startedAt);
                return;
            }

            var token = gateway.Authenticate().Token!;

            foreach (var monitor in selectedMonitors)
            {
                try
                {
                    var result = ReadTraces(token, monitor.SerialId, last, null);
                    succeeded++;
                    tracesStored += result.TraceCount;
                    samplesStored += result.SampleCount;
                }
                catch (Exception e)
                {
                    var msg = string.Format("Failed to read traces for serialId={0}", monitor.SerialId!);
                    failures.Add(OmnidotsMonitorFailure.Record(
                        monitor.SerialId,
                        e,
                        () => operationalCommands.HandleException(msg, e)));
                }
            }

            LogSummary(
                eligibleMonitors.Count,
                selectedMonitors.Count,
                succeeded,
                failures.Count,
                tracesStored,
                samplesStored,
                startedAt);

            if (failures.Count > 0)
            {
                throw new OmnidotsImportException("StoreTraces", failures);
            }
        }

        private TraceReadResult ReadTraces(string token, string serialId, DateTime start, DateTime? end)
        {
            var tracesList = gateway.GetTracesList(token, serialId, start, end);

            if (tracesList.Traces == null)
            {
                RvtLogger.Logger.LogInformation("ReadTraces for serialId={Value1} tracelist is empty.",
                    serialId);
                return new TraceReadResult(0, 0);
            }

            RvtLogger.Logger.LogInformation("ReadTraces for serialId={Value1}  traceslist size={Value2}",
                serialId, tracesList.Traces.Count);

            var traceCount = 0;
            var sampleCount = 0;
            foreach (var traceInfo in tracesList.Traces)
            {
                var tStart = DateTimeUtil.FromMillis(traceInfo.StartTime);
                var tEnd = DateTimeUtil.FromMillis(traceInfo.EndTime);

                var tracesResponse = gateway.GetTraces(token, serialId, tStart, tEnd);
                var traces = tracesResponse.Traces ?? [];
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
                return monitors.ToArray();
            }

            var allowedSerialIds = new HashSet<string>(options.AllowedSerialIds, StringComparer.OrdinalIgnoreCase);
            return monitors.Where(monitor => allowedSerialIds.Contains(monitor.SerialId)).ToArray();
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
