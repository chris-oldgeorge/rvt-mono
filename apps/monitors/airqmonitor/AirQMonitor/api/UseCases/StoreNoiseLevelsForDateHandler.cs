using AirQ.Api.Db;
using AirQ.Api.Http;
using AirQ.Model.Dto;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;

namespace AirQ.Api.UseCases
{
    // Summary: Backfills AirQ noise samples for a single date across all active monitors.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiMonitorsNoiseLevels).
    public class StoreNoiseLevelsForDateHandler
    {
        private readonly AirQHttpGateway gateway;
        private readonly AirQMonitorReader monitorReader;
        private readonly IAirQMeasurementCommands measurementCommands;
        private readonly IAirQOperationalCommands operationalCommands;

        public StoreNoiseLevelsForDateHandler(
            AirQHttpGateway gateway,
            AirQMonitorReader monitorReader,
            IAirQMeasurementCommands measurementCommands,
            IAirQOperationalCommands operationalCommands)
        {
            this.gateway = gateway;
            this.monitorReader = monitorReader;
            this.measurementCommands = measurementCommands;
            this.operationalCommands = operationalCommands;
        }

        public void Run(string userId, string userAuth, string dateStr)
        {
            try
            {
                var monitors = monitorReader.ReadMonitors();
                var failures = new List<Exception>();
                foreach (var monitor in monitors)
                {
                    if (!monitor.MonitorStatus.IsMonitorActive())
                    {
                        RvtLogger.Logger.LogWarning("StoreNoiseLevelsForDate skipping inactive monitor serialId={Value1} status={Value2} errorCount={Value3}",
                                monitor.SerialId, monitor.MonitorStatus.Status, monitor.MonitorStatus.ErrorCount);

                        continue;
                    }
                    var serialId = monitor!.SerialId;

                    try
                    {
                        var samples = gateway.GetSamplesForDate(userId, userAuth, serialId, dateStr);

                        var dtos = new List<NoiseDto>();
                        foreach (var sample in samples)
                        {
                            dtos.Add(new NoiseDto(sample));
                        }
                        measurementCommands.InsertNoiseDtos(serialId, dtos);

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
            catch (Exception e)
            {
                operationalCommands.HandleException("StoreAllNoiseLevelsForDate", e);
                throw;
            }
        }
    }
}
