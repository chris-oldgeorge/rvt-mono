using AirQ.Api.Db;
using AirQ.Api.Ports;
using AirQ.Model.Dto;
using AirQ.Model.Http;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;

namespace AirQ.Api.UseCases
{
    // Summary: Backfills AirQ noise samples for a single date across all active monitors.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiMonitorsNoiseLevels).
    public class StoreNoiseLevelsForDateHandler
    {
        private readonly IAirQVendorGateway _gateway;
        private readonly AirQMonitorReader monitorReader;
        private readonly IAirQMeasurementCommands measurementCommands;
        private readonly IAirQOperationalCommands operationalCommands;

        public StoreNoiseLevelsForDateHandler(
            IAirQVendorGateway gateway,
            AirQMonitorReader monitorReader,
            IAirQMeasurementCommands measurementCommands,
            IAirQOperationalCommands operationalCommands)
        {
            _gateway = gateway;
            this.monitorReader = monitorReader;
            this.measurementCommands = measurementCommands;
            this.operationalCommands = operationalCommands;
        }

        public async Task RunAsync(string userId, string userAuth, string dateStr, CancellationToken cancellationToken = default)
        {
            try
            {
                List<NoiseMonitorDto> monitors = monitorReader.ReadMonitors();
                var failures = new List<Exception>();
                foreach (NoiseMonitorDto monitor in monitors)
                {
                    if (!monitor.MonitorStatus.IsMonitorActive())
                    {
                        RvtLogger.Logger.LogWarning("StoreNoiseLevelsForDate skipping inactive monitor serialId={Value1} status={Value2} errorCount={Value3}",
                                monitor.SerialId, monitor.MonitorStatus.Status, monitor.MonitorStatus.ErrorCount);

                        continue;
                    }
                    string serialId = monitor!.SerialId;

                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        List<SampleResponse> samples = await _gateway.GetSamplesForDateAsync(userId, userAuth, serialId, dateStr, cancellationToken);

                        var dtos = new List<NoiseDto>();
                        foreach (SampleResponse sample in samples)
                        {
                            dtos.Add(new NoiseDto(sample));
                        }
                        measurementCommands.InsertNoiseDtos(serialId, dtos);

                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        operationalCommands.HandleException(string.Format("StoreAllNoiseLevelsForDate SerialId={0}", monitor.SerialId), e);
                        failures.Add(e);
                    }
                }

                if (failures.Count > 0)
                {
                    throw new AggregateException("One or more AirQ date imports failed.", failures);
                }
            }
            catch (AggregateException)
            {
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                operationalCommands.HandleException("StoreAllNoiseLevelsForDate", e);
                throw;
            }
        }
    }
}
