using AirQ.Api.Db;
using AirQ.Api.Ports;
using AirQ.Model.Dto;
using AirQ.Model.Http;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Rules;

namespace AirQ.Api.UseCases;

// Summary: Reads the latest AirQ noise samples, persists them and 8-hour averages, and evaluates alert rules.
// Major updates:
// - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiMonitorsNoiseLevels).
public class StoreNoiseLevelsHandler
{
    private readonly IAirQVendorGateway _gateway;
    private readonly AirQMonitorReader monitorReader;
    private readonly IAirQRuleQueries ruleQueries;
    private readonly IAirQMonitorCommands monitorCommands;
    private readonly IAirQMeasurementCommands measurementCommands;
    private readonly IAirQOperationalCommands operationalCommands;
    private readonly IMonitorEventPublisher eventPublisher;
    private readonly AirQRuleProcessor ruleProcessor;

    public StoreNoiseLevelsHandler(
        IAirQVendorGateway gateway,
        AirQMonitorReader monitorReader,
        IAirQRuleQueries ruleQueries,
        IAirQMonitorCommands monitorCommands,
        IAirQMeasurementCommands measurementCommands,
        IAirQOperationalCommands operationalCommands,
        IMonitorEventPublisher eventPublisher,
        AirQRuleProcessor ruleProcessor)
    {
        _gateway = gateway;
        this.monitorReader = monitorReader;
        this.ruleQueries = ruleQueries;
        this.monitorCommands = monitorCommands;
        this.measurementCommands = measurementCommands;
        this.operationalCommands = operationalCommands;
        this.eventPublisher = eventPublisher;
        this.ruleProcessor = ruleProcessor;
    }

    public async Task RunAsync(string userId, string userAuth, CancellationToken cancellationToken = default)
    {
        try
        {
            List<NoiseMonitorDto> monitors = monitorReader.ReadMonitors();
            List<Exception> failures = [];
            foreach (NoiseMonitorDto monitor in monitors)
            {

                if (!monitor.MonitorStatus.IsMonitorActive())
                {
                    RvtLogger.Logger.LogWarning("StoreNoiseLevels skipping inactive monitor serialId={Value1} status={Value2} errorCount={Value3}", monitor.SerialId, monitor.MonitorStatus.Status, monitor.MonitorStatus.ErrorCount);
                    continue;
                }

                DateTime lastDataTime = monitor.LastDataTime == null ? DateTime.Now.AddYears(-1) : (DateTime)monitor.LastDataTime!;

                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    DateTime preLastDate = lastDataTime; //Saving this as it get changed below and neede to calculate the time period.

                    LatestSamplesResult latest = await _gateway.GetLatestSamplesAsync(userId, userAuth, monitor.SerialId, lastDataTime, cancellationToken);
                    List<SampleResponse> samples = latest.Samples;
                    lastDataTime = latest.LatestDateTime;
                    RvtLogger.Logger.LogInformation("GetLatestSamples SerialId={Value1} number of samples={Value2} lastDataTime={Value3}", monitor.SerialId, samples.Count, lastDataTime);
                    List<NoiseDto> dtos = [];
                    foreach (SampleResponse sample in samples)
                    {
                        dtos.Add(new NoiseDto(sample));
                    }

                    if (dtos.Count > 0)
                    {
                        measurementCommands.InsertNoiseDtos(monitor.SerialId, dtos);
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
                            RvtLogger.Logger.LogInformation("Create average SerialId={Value1} number of endperiod={Value2}", monitor.SerialId, endperiod);
                            measurementCommands.Create8hourAverage(monitor.SerialId, endperiod);
                            start = start.AddHours(8);
                            endperiod = start.AddHours(8);
                        }

                        monitorCommands.WriteLatestTimestamp(monitor.SerialId, lastDataTime);
                        if (monitor.Offline)
                        {
                            monitorCommands.SetMonitorOffline(monitor.Id, false);
                        }

                        await eventPublisher.PublishDataInsertedAsync((DateTime)lastDataTime!, monitor.SerialId, cancellationToken: cancellationToken);

                        List<RvtAlertRuleDto> rules = ruleQueries.ReadRules(monitor.SerialId);
                        ruleProcessor.ProcessRulesV2(monitor, rules, preLastDate, (DateTime)lastDataTime, dtos);
                    }

                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
