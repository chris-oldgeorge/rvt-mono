using AirQ.Api.Db;
using AirQ.Api.Http;
using AirQ.Model.Dto;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;

namespace AirQ.Api.UseCases
{
    // Summary: Reads the latest AirQ noise samples, persists them and 8-hour averages, and evaluates alert rules.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiMonitorsNoiseLevels).
    public class StoreNoiseLevelsHandler
    {
        private readonly AirQHttpGateway gateway;
        private readonly AirQMonitorReader monitorReader;
        private readonly IAirQRuleQueries ruleQueries;
        private readonly IAirQMonitorCommands monitorCommands;
        private readonly IAirQMeasurementCommands measurementCommands;
        private readonly IAirQOperationalCommands operationalCommands;
        private readonly IMonitorEventPublisher eventPublisher;
        private readonly AirQRuleProcessor ruleProcessor;

        public StoreNoiseLevelsHandler(
            AirQHttpGateway gateway,
            AirQMonitorReader monitorReader,
            IAirQRuleQueries ruleQueries,
            IAirQMonitorCommands monitorCommands,
            IAirQMeasurementCommands measurementCommands,
            IAirQOperationalCommands operationalCommands,
            IMonitorEventPublisher eventPublisher,
            AirQRuleProcessor ruleProcessor)
        {
            this.gateway = gateway;
            this.monitorReader = monitorReader;
            this.ruleQueries = ruleQueries;
            this.monitorCommands = monitorCommands;
            this.measurementCommands = measurementCommands;
            this.operationalCommands = operationalCommands;
            this.eventPublisher = eventPublisher;
            this.ruleProcessor = ruleProcessor;
        }

        public void Run(string userId, string userAuth)
        {
            try
            {
                var monitors = monitorReader.ReadMonitors();
                var failures = new List<Exception>();
                foreach (var monitor in monitors)
                {

                    if (!monitor.MonitorStatus.IsMonitorActive())
                    {
                        RvtLogger.Logger.LogWarning("StoreNoiseLevels skipping inactive monitor serialId={Value1} status={Value2} errorCount={Value3}", monitor.SerialId, monitor.MonitorStatus.Status, monitor.MonitorStatus.ErrorCount);
                        continue;
                    }

                    DateTime lastDataTime = monitor.LastDataTime == null ? DateTime.Now.AddYears(-1) : (DateTime)monitor.LastDataTime!;

                    try
                    {
                        DateTime preLastDate = lastDataTime; //Saving this as it get changed below and neede to calculate the time period.

                        var samples = gateway.HttpGetLatestSamples(userId, userAuth, monitor.SerialId, ref lastDataTime);
                        RvtLogger.Logger.LogInformation("GetLatestSamples SerialId={Value1} number of samples={Value2} lastDataTime={Value3}", monitor.SerialId, samples.Count, lastDataTime);
                        var dtos = new List<NoiseDto>();
                        foreach (var sample in samples)
                        {
                            dtos.Add(new NoiseDto(sample));
                        }

                        if (dtos.Count > 0)
                        {
                            measurementCommands.InsertNoiseDtos(monitor.SerialId, dtos);
                            //process 8 hour averages.
                            var start = preLastDate;
                            var end = dtos.Last().SampleTime;
                            int starthour = (start.Hour / 8) * 8;
                            start = new DateTime(start.Year, start.Month, start.Day, starthour, 0, 0);//This should now be 00:00, 08:00 or 16:00, start time for an averge
                            if (start == dtos.First().SampleTime)//special case! in case you get a sample time of exactly 00:00:00 then that should be the end time for the period
                                start = start.AddHours(-8);
                            var endperiod = start.AddHours(8); //end time for the averaging period.
                            while (endperiod <= end) // end of a period exist within the samples.
                            {
                                RvtLogger.Logger.LogInformation("Create average SerialId={Value1} number of endperiod={Value2}", monitor.SerialId, endperiod);
                                measurementCommands.Create8hourAverage(monitor.SerialId, endperiod);
                                start = start.AddHours(8);
                                endperiod = start.AddHours(8);
                            }

                            monitorCommands.WriteLatestTimestamp(monitor.SerialId, lastDataTime);
                            if (monitor.Offline)
                                monitorCommands.SetMonitorOffline(monitor.Id, false);
                            eventPublisher.PublishDataInserted((DateTime)lastDataTime!, monitor.SerialId);

                            var rules = ruleQueries.ReadRules(monitor.SerialId);
                            ruleProcessor.ProcessRulesV2(monitor, rules, preLastDate, (DateTime)lastDataTime, dtos);
                        }

                    }
                    catch (Exception e)
                    {
                        monitor.MonitorStatus.ErrorCount++;
                        monitorCommands.UpdateMonitorStatus(monitor.SerialId, monitor.MonitorStatus);
                        operationalCommands.HandleException(string.Format("StoreNoiseLevels SerialId={0}", monitor.SerialId), e);
                        failures.Add(e);
                    }
                }

                if (failures.Count > 0)
                {
                    throw new AggregateException("One or more AirQ noise-level imports failed.", failures);
                }
            }
            catch (AggregateException)
            {
                throw;
            }
            catch (Exception e)
            {
                operationalCommands.HandleException("StoreNoiseLevels", e);
                throw;
            }
        }
    }
}
