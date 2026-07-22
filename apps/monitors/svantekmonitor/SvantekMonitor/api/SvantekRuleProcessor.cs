using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Svantek.Api.Db;
using SvantekMonitor.model.dto;
using NotificationDto = Rvt.Monitor.Common.Rules.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;

namespace Svantek.Api
{
    // Summary: Evaluates Svantek noise readings against alert rules and dispatches notifications.
    // Major updates:
    // - 2026-06-18: Added null guard for optional DTO batches during analyzer cleanup.
    // - 2026-07-12 God-class split: extracted from the SvantekApi partials (SvantekApiRuleProcessing).
    public class SvantekRuleProcessor
    {
        private readonly ISvantekRuleQueries ruleQueries;
        private readonly ISvantekOperationalCommands operationalCommands;
        private readonly IMessageService messageService;
        private readonly IMonitorEventPublisher eventPublisher;

        public SvantekRuleProcessor(
            ISvantekRuleQueries ruleQueries,
            ISvantekOperationalCommands operationalCommands,
            IMessageService messageService,
            IMonitorEventPublisher eventPublisher)
        {
            this.ruleQueries = ruleQueries;
            this.operationalCommands = operationalCommands;
            this.messageService = messageService;
            this.eventPublisher = eventPublisher;
        }

        private static bool CrossesOverIntervalExist(DateTime start, DateTime end, int average)
        {
            if (average == 900)
            {
                // List of minute intervals to check
                int[] intervals = { 0, 15, 30, 45 };

                // Check each interval
                foreach (int interval in intervals)
                {
                    // Get the next occurrence of the interval after date1's hour
                    DateTime nextInterval = new DateTime(start.Year, start.Month, start.Day, start.Hour, interval, 0);

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
                return false;
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
                return DateTime.Now.AddDays(1); // this is the case where the average is the site hours and the value is 0. That case should not be chaecked in this code.
        }

        //Using start and end here to determine the date range and if there is time in there for an average. Eg, if there is a 15 to check the 15 minute average.
        public void ProcessRules(NoiseMonitorReadDto monitorDto, List<RvtAlertRuleDto> allrules, DateTime start, DateTime end)
        {
            if (allrules != null && allrules.Count > 0)
            {
                TimeSpan timeDiff = end - start;
                //iterate through every averaging period
                List<int> averagingPeriods = allrules.Select(x => x.AveragingPeriod).Distinct().ToList();
                foreach (int averagePeriod in averagingPeriods)
                {
                    if (timeDiff > TimeSpan.FromSeconds(averagePeriod) || CrossesOverIntervalExist(start, end, averagePeriod)) //is the end of period within the time range of the samples?
                    {
                        //iterate through each parameter
                        List<string> parameters = allrules.Where(a => a.AveragingPeriod == averagePeriod).Select(x => x.Field).Distinct().ToList();

                        foreach (string paramter in parameters)
                        {
                            var rules = allrules.Where(x => x.AveragingPeriod == averagePeriod && x.Field == paramter).OrderBy(x => x.AlertType).ToList();
                            ProcessRulesOneAverageOneParamter(monitorDto, rules, start, end, averagePeriod, paramter);
                        }
                    }
                }
            }
        }

        private void ProcessRulesOneAverageOneParamter(NoiseMonitorReadDto monitorDto, List<RvtAlertRuleDto> rules, DateTime start, DateTime end, int averagingPeriod, string parameter)
        {
            var ruleEvaluator = CreateNoiseRuleEvaluator();
            // first get all the periods to check, every quarter every hour or every day...
            DateTime StartTime = PeriodstartTime(start, averagingPeriod);
            while (StartTime < end && ((TimeSpan)(end - StartTime)) >= TimeSpan.FromSeconds(averagingPeriod)) // once for each period in the range
            {
                AlertType previousAlert = AlertType.Ignore;
                var serialId = monitorDto.SerialId!;
                foreach (var rule in rules)
                {
                    double level = ruleQueries.GetAverageNoiseLevel(serialId, rule.Field, StartTime, StartTime.AddSeconds(averagingPeriod));
                    previousAlert = ruleEvaluator.Evaluate(
                        NewRuleEvaluationRequest(
                            monitorDto,
                            activityTime: end,
                            alertTime: StartTime.AddSeconds(averagingPeriod),
                            publishTime: end,
                            deactivateDeletedRules: false),
                        rule,
                        level,
                        previousAlert);
                }
                StartTime = StartTime.AddSeconds(averagingPeriod);
            }
        }

        public void ProcessAlertForContacts(
                         string fleetNr,
                         string serialId,
                         DateTime alertTime,
                         double limitOn,
                         int averagingPeriod,
                         double level,
                         AlertType alertType,
                         string field,
                         Guid monitorId,
                         List<RvtContactDto> contacts
        )
        {
            var dispatcher = new RuleAlertNotificationDispatcher(
                messageService,
                operationalCommands.WriteNotification,
                operationalCommands.WriteNotificationAudit);

            dispatcher.ProcessAlertForContacts(
                new RuleNotificationRequest(
                    fleetNr,
                    serialId,
                    alertTime,
                    limitOn,
                    averagingPeriod,
                    level,
                    alertType,
                    field,
                    monitorId),
                contacts: contacts);
        }

        private NoiseRuleEvaluator CreateNoiseRuleEvaluator() =>
            new(
                operationalCommands.UpdateAlertRule,
                monitorId => ruleQueries.ReadAlertContacts(monitorId, out Guid _),
                (request, contacts) => ProcessAlertForContacts(
                    fleetNr: request.FleetNr,
                    serialId: request.SerialId,
                    alertTime: request.AlertTime,
                    limitOn: request.LimitOn,
                    averagingPeriod: request.AveragingPeriod,
                    level: request.Level,
                    alertType: request.AlertType,
                    field: request.Field,
                    monitorId: request.MonitorId,
                    contacts: contacts),
                eventPublisher);

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
