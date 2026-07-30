using System.Globalization;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Svantek.Api.Db;
using Svantek.Model.Dto;

namespace Svantek.Api
{
    // Summary: Evaluates Svantek noise readings against alert rules and emits durable alert signals.
    // Major updates:
    // - 2026-06-18: Added null guard for optional DTO batches during analyzer cleanup.
    // - 2026-07-12 God-class split: extracted from the SvantekApi partials (SvantekApiRuleProcessing).
    // - 2026-07-29 Legacy retirement step 4: breaches signal IAlertIngressPort;
    //   the inline RuleAlertNotificationDispatcher send loop is gone.
    public class SvantekRuleProcessor
    {
        internal const string SignalSource = "svantek.rules";

        private readonly ISvantekRuleQueries _ruleQueries;
        private readonly ISvantekOperationalCommands _operationalCommands;
        private readonly IAlertIngressPort _alertIngress;

        public SvantekRuleProcessor(
            ISvantekRuleQueries ruleQueries,
            ISvantekOperationalCommands operationalCommands,
            IAlertIngressPort alertIngress)
        {
            _ruleQueries = ruleQueries;
            _operationalCommands = operationalCommands;
            _alertIngress = alertIngress;
        }

        private static bool CrossesOverIntervalExist(DateTime start, DateTime end, int average)
        {
            if (average == 900)
            {
                // List of minute intervals to check
                int[] intervals = [0, 15, 30, 45];

                // Check each interval
                foreach (int interval in intervals)
                {
                    // Get the next occurrence of the interval after date1's hour
                    DateTime nextInterval = new(start.Year, start.Month, start.Day, start.Hour, interval, 0);

                    // If the next interval is before date1, move it to the next hour
                    if (nextInterval <= start)
                    {
                        nextInterval = nextInterval.AddHours(1);
                    }

                    // If the next interval is between date1 and date2, return true
                    if (nextInterval <= end)
                    {
                        return true;
                    }
                }

                // No interval was crossed
                return false;
            }
            else if (average == 3600)
            {
                return start.Hour != end.Hour;
            }
            else if (average == 86400)
            {
                return start.Day != end.Day;
            }
            else
            {
                return false;
            }
        }

        private static DateTime PeriodstartTime(DateTime start, int average)
        {
            if (average == 900)
            {
                int min = (start.Minute / 15) * 15; //to the closest earlier quarter
                return (new DateTime(start.Year, start.Month, start.Day, start.Hour, min, 0));
            }
            else if (average == 3600)
            {
                return (new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0));
            }
            else if (average == 86400)
            {
                return (new DateTime(start.Year, start.Month, start.Day, 0, 0, 0));
            }
            else
            {
                return DateTime.Now.AddDays(1); // this is the case where the average is the site hours and the value is 0. That case should not be chaecked in this code.
            }
        }

        //Using start and end here to determine the date range and if there is time in there for an average. Eg, if there is a 15 to check the 15 minute average.
        public async Task ProcessRulesAsync(NoiseMonitorReadDto monitorDto, List<RvtAlertRuleDto> allrules, DateTime start, DateTime end, CancellationToken cancellationToken = default)
        {
            if (allrules != null && allrules.Count > 0)
            {
                TimeSpan timeDiff = end - start;
                //iterate through every averaging period
                List<int> averagingPeriods = [.. allrules.Select(x => x.AveragingPeriod).Distinct()];
                foreach (int averagePeriod in averagingPeriods)
                {
                    if (timeDiff > TimeSpan.FromSeconds(averagePeriod) || CrossesOverIntervalExist(start, end, averagePeriod)) //is the end of period within the time range of the samples?
                    {
                        //iterate through each parameter
                        List<string> parameters = [.. allrules.Where(a => a.AveragingPeriod == averagePeriod).Select(x => x.Field).Distinct()];

                        foreach (string paramter in parameters)
                        {
                            List<RvtAlertRuleDto> rules = [.. allrules.Where(x => x.AveragingPeriod == averagePeriod && x.Field == paramter).OrderBy(x => x.AlertType)];
                            await ProcessRulesOneAverageOneParamterAsync(monitorDto, rules, start, end, averagePeriod, cancellationToken);
                        }
                    }
                }
            }
        }

        private async Task ProcessRulesOneAverageOneParamterAsync(NoiseMonitorReadDto monitorDto, List<RvtAlertRuleDto> rules, DateTime start, DateTime end, int averagingPeriod, CancellationToken cancellationToken)
        {
            NoiseRuleEvaluator ruleEvaluator = CreateNoiseRuleEvaluator();
            // first get all the periods to check, every quarter every hour or every day...
            DateTime StartTime = PeriodstartTime(start, averagingPeriod);
            while (StartTime < end && ((TimeSpan)(end - StartTime)) >= TimeSpan.FromSeconds(averagingPeriod)) // once for each period in the range
            {
                AlertType previousAlert = AlertType.Ignore;
                string serialId = monitorDto.SerialId!;
                foreach (RvtAlertRuleDto rule in rules)
                {
                    double? level = _ruleQueries.GetAverageNoiseLevel(serialId, rule.Field, StartTime, StartTime.AddSeconds(averagingPeriod));
                    if (!level.HasValue)
                    {
                        // No samples in the window is no reading, not 0.0 dB:
                        // evaluating it would clear a latched rule and re-fire
                        // the breach on the next populated window.
                        continue;
                    }

                    previousAlert = await ruleEvaluator.EvaluateAsync(
                        NewRuleEvaluationRequest(
                            monitorDto,
                            activityTime: end,
                            alertTime: StartTime.AddSeconds(averagingPeriod),
                            publishTime: end,
                            deactivateDeletedRules: false),
                        rule,
                        level.Value,
                        previousAlert,
                        cancellationToken);
                }
                StartTime = StartTime.AddSeconds(averagingPeriod);
            }
        }

        // Handler-driven alerts (offline, battery) carry no MQTT delivery,
        // matching the retired direct-send path.
        public Task SignalAlertAsync(
                         string serialId,
                         DateTime alertTime,
                         double limitOn,
                         int averagingPeriod,
                         double level,
                         AlertType alertType,
                         string field,
                         CancellationToken cancellationToken = default)
        {
            string message = string.Create(
                CultureInfo.InvariantCulture,
                $"{alertType} {field} level={level} limit={limitOn}");
            return _alertIngress.AcceptAsync(
                RuleAlertSignals.Create(
                    SignalSource,
                    serialId,
                    alertTime,
                    alertType,
                    field,
                    level,
                    limitOn,
                    averagingPeriod,
                    message,
                    AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms),
                cancellationToken);
        }

        private NoiseRuleEvaluator CreateNoiseRuleEvaluator() =>
            new(
                _operationalCommands.UpdateAlertRule,
                _alertIngress,
                SignalSource);

        private static RuleEvaluationRequest NewRuleEvaluationRequest(
            NoiseMonitorReadDto monitorDto,
            DateTime activityTime,
            DateTime alertTime,
            DateTime publishTime,
            bool deactivateDeletedRules = true) =>
            new(
                monitorDto.FleetNr,
                monitorDto.SerialId!,
                monitorDto.Id,
                activityTime,
                alertTime,
                publishTime,
                deactivateDeletedRules);
    }
}
