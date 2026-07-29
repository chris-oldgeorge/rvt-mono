using AirQ.Api.Db;
using AirQ.Api.Ports;
using AirQ.Model.Dto;
using AirQ.Model.Http;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Rules;

namespace AirQ.Api.UseCases
{
    // Summary: Reads the latest AirQ noise samples, persists them and 8-hour averages, and evaluates alert rules.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiMonitorsNoiseLevels).
    public class StoreNoiseLevelsHandler
    {
        private readonly IAirQVendorGateway _gateway;
        private readonly AirQMonitorReader _monitorReader;
        private readonly IAirQRuleQueries _ruleQueries;
        private readonly IAirQMonitorCommands _monitorCommands;
        private readonly IAirQMeasurementCommands _measurementCommands;
        private readonly IAirQOperationalCommands _operationalCommands;
        private readonly IMonitorEventPublisher _eventPublisher;
        private readonly AirQRuleProcessor _ruleProcessor;
        private readonly TimeProvider _timeProvider;

        public StoreNoiseLevelsHandler(
            IAirQVendorGateway gateway,
            AirQMonitorReader monitorReader,
            IAirQRuleQueries ruleQueries,
            IAirQMonitorCommands monitorCommands,
            IAirQMeasurementCommands measurementCommands,
            IAirQOperationalCommands operationalCommands,
            IMonitorEventPublisher eventPublisher,
            AirQRuleProcessor ruleProcessor,
            TimeProvider timeProvider)
        {
            _gateway = gateway;
            _monitorReader = monitorReader;
            _ruleQueries = ruleQueries;
            _monitorCommands = monitorCommands;
            _measurementCommands = measurementCommands;
            _operationalCommands = operationalCommands;
            _eventPublisher = eventPublisher;
            _ruleProcessor = ruleProcessor;
            _timeProvider = timeProvider;
        }

        public async Task RunAsync(string userId, string userAuth, CancellationToken cancellationToken = default)
        {
            try
            {
                List<NoiseMonitorDto> monitors = _monitorReader.ReadMonitors();
                List<Exception> failures = [];
                foreach (NoiseMonitorDto monitor in monitors)
                {

                    if (!monitor.MonitorStatus.IsMonitorActive())
                    {
                        RvtLogger.Logger.LogWarning("StoreNoiseLevels skipping inactive monitor serialId={Value1} status={Value2} errorCount={Value3}", monitor.SerialId, monitor.MonitorStatus.Status, monitor.MonitorStatus.ErrorCount);
                        continue;
                    }

                    DateTime lastDataTime = monitor.LastDataTime
                        ?? _timeProvider.GetUtcNow().UtcDateTime.AddYears(-1);

                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        DateTime preLastDate = lastDataTime; //Saving this as it get changed below and neede to calculate the time period.

                        LatestSamplesResult latest = await _gateway.GetLatestSamplesAsync(userId, userAuth, monitor.SerialId, lastDataTime, cancellationToken);
                        List<SampleResponse> samples = latest.Samples;
                        lastDataTime = latest.LatestDateTime;
                        if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
                        {
                            RvtLogger.Logger.LogInformation("GetLatestSamples SerialId={Value1} number of samples={Value2} lastDataTime={Value3}", monitor.SerialId, samples.Count, lastDataTime);
                        }
                        List<NoiseDto> dtos = [];
                        foreach (SampleResponse sample in samples)
                        {
                            dtos.Add(new NoiseDto(sample));
                        }

                        if (dtos.Count > 0)
                        {
                            _measurementCommands.InsertNoiseDtos(monitor.SerialId, dtos);
                            //process 8 hour averages.
                            DateTime start = preLastDate;
                            DateTime end = dtos.Last().SampleTime;
                            int starthour = (start.Hour / 8) * 8;
                            start = new DateTime(start.Year, start.Month, start.Day, starthour, 0, 0);//This should now be 00:00, 08:00 or 16:00, start time for an averge
                            if (start == dtos.First().SampleTime)//special case! in case you get a sample time of exactly 00:00:00 then that should be the end time for the period
                            {
                                start = start.AddHours(-8);
                            }

                            DateTime endperiod = start.AddHours(8); //end time for the averaging period.
                            while (endperiod <= end) // end of a period exist within the samples.
                            {
                                if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
                                {
                                    RvtLogger.Logger.LogInformation("Create average SerialId={Value1} number of endperiod={Value2}", monitor.SerialId, endperiod);
                                }
                                _measurementCommands.Create8hourAverage(monitor.SerialId, endperiod);
                                start = start.AddHours(8);
                                endperiod = start.AddHours(8);
                            }

                            _monitorCommands.WriteLatestTimestamp(monitor.SerialId, lastDataTime);
                            if (monitor.Offline)
                            {
                                _monitorCommands.SetMonitorOffline(monitor.Id, false);
                            }

                            await _eventPublisher.PublishDataInsertedAsync((DateTime)lastDataTime!, monitor.SerialId, cancellationToken: cancellationToken);

                            List<RvtAlertRuleDto> rules = _ruleQueries.ReadRules(monitor.SerialId);
                            await _ruleProcessor.ProcessRulesV2Async(monitor, rules, preLastDate, (DateTime)lastDataTime, dtos, cancellationToken);
                        }

                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        monitor.MonitorStatus.ErrorCount++;
                        _monitorCommands.UpdateMonitorStatus(monitor.SerialId, monitor.MonitorStatus);
                        _operationalCommands.HandleException(string.Format("StoreNoiseLevels SerialId={0}", monitor.SerialId), e);
                        failures.Add(e);
                    }
                }

                if (failures.Count > 0)
                {
                    throw new AggregateException("One or more AirQ noise-level imports failed.", failures);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e) when (e is not AggregateException)
            {
                _operationalCommands.HandleException("StoreNoiseLevels", e);
                throw;
            }
        }
    }
}
