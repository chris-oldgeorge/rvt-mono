using AirQ.Api.Db;
using AirQ.Model.Dto;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;

namespace AirQ.Api.UseCases
{
    // Summary: Writes daily site noise averages and alerts contacts on site-hours rule breaches.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiMonitorsNoiseLevels).
    // - 2026-07-30 Run resilience: skip sites without hours and isolate per-monitor failures
    //   (the Svantek twin's shape).
    public class NotifySiteAveragesHandler
    {
        private readonly IAirQMonitorQueries _monitorQueries;
        private readonly IAirQRuleQueries _ruleQueries;
        private readonly IAirQMeasurementCommands _measurementCommands;
        private readonly IAirQOperationalCommands _operationalCommands;
        private readonly AirQRuleProcessor _ruleProcessor;

        public NotifySiteAveragesHandler(
            IAirQMonitorQueries monitorQueries,
            IAirQRuleQueries ruleQueries,
            IAirQMeasurementCommands measurementCommands,
            IAirQOperationalCommands operationalCommands,
            AirQRuleProcessor ruleProcessor)
        {
            _monitorQueries = monitorQueries;
            _ruleQueries = ruleQueries;
            _measurementCommands = measurementCommands;
            _operationalCommands = operationalCommands;
            _ruleProcessor = ruleProcessor;
        }

        public async Task RunAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<SiteMonitorsWithSiteHoursDto> monitors = _monitorQueries.ReadSiteMonitorsWithSiteHours(date);
            List<Exception> failures = [];
            foreach (SiteMonitorsWithSiteHoursDto monitor in monitors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    // A site without configured hours has no averaging window;
                    // skip it instead of faulting the whole run.
                    if (!monitor.StartTime.HasValue || !monitor.EndTime.HasValue)
                    {
                        continue;
                    }

                    await ProcessMonitorAsync(monitor, date, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    failures.Add(e);
                    try
                    {
                        // Recording is best-effort: a database outage while
                        // writing the error row must not replace the original
                        // failure (MyAtm's collector semantics).
                        _operationalCommands.HandleException(string.Format("NotifySiteAverages SerialId={0}", monitor.SerialId), e);
                    }
                    catch (Exception recordingException)
                    {
                        failures.Add(recordingException);
                    }
                }
            }

            if (failures.Count > 0)
            {
                throw new AggregateException("One or more AirQ site-average notifications failed.", failures);
            }
        }

        private async Task ProcessMonitorAsync(
            SiteMonitorsWithSiteHoursDto monitor,
            DateTime date,
            CancellationToken cancellationToken)
        {
            double level = _ruleQueries.GetAverageNoiseLevel(serialNumber: monitor.SerialId,
                                          columnName: "LAeq", // Assuming that is enough for now.
                                          start: date + monitor.StartTime!.Value,
                                          end: date + monitor.EndTime!.Value);

            _measurementCommands.WriteDailyAverage(siteId: monitor.SiteId,
                                       monitorId: monitor.Id,
                                       field: "lAeq",
                                       level: level,
                                       timestamp: date);
            List<RvtAlertRuleDto> allRules = _ruleQueries.ReadRules(monitor.SerialId);
            if (allRules != null && allRules.Count > 0)
            {
                List<RvtAlertRuleDto> rules = [.. allRules.Where(x => x.AveragingPeriod == 0 && x.Field == "LAeq").OrderBy(x => x.AlertType)];
                AlertType previousAlert = AlertType.Ignore;
                foreach (RvtAlertRuleDto? rule in rules)
                {
                    if (rule.LimitOn <= level && !rule.IsActive && !rule.IsDeleted)
                    {
                        //Either an alert or cautioon with no previous alert
                        if (rule.AlertType == AlertType.Alert || (previousAlert != AlertType.Alert && rule.AlertType == AlertType.Caution)) //Not to send cautions if we have sent alerts but if there are two alert rules lets go for it
                        {
                            //New breach generate notification
                            await _ruleProcessor.SignalAlertAsync(serialId: monitor.SerialId,
                            alertTime: date + monitor.EndTime!.Value,
                            limitOn: rule.LimitOn,
                            averagingPeriod: 0,
                            level: level,
                            alertType: rule.AlertType,
                            field: rule.Field,
                            cancellationToken: cancellationToken);

                            rule.IsActive = true;
                            _operationalCommands.UpdateAlertRule(rule);
                            previousAlert = rule.AlertType;
                        }
                    }
                    else if (rule.LimitOff >= level && rule.IsActive)
                    {
                        //turn off active rule
                        rule.IsActive = false;
                        _operationalCommands.UpdateAlertRule(rule);
                    }
                    else if (rule.IsActive)
                    {
                        previousAlert = rule.AlertType;
                    }
                }
            }
        }
    }
}
