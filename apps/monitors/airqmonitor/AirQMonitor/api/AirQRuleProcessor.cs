using AirQ.Api.Db;
using AirQ.Model.Dto;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;

namespace AirQ.Api
{
    // Summary: Evaluates AirQ noise readings against alert rules and dispatches notifications.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiRuleProcessing).
    public class AirQRuleProcessor
    {
        private readonly IAirQRuleQueries ruleQueries;
        private readonly IAirQOperationalCommands operationalCommands;
        private readonly IMessageService messageService;
        private readonly IMonitorEventPublisher eventPublisher;

        public AirQRuleProcessor(
            IAirQRuleQueries ruleQueries,
            IAirQOperationalCommands operationalCommands,
            IMessageService messageService,
            IMonitorEventPublisher eventPublisher)
        {
            this.ruleQueries = ruleQueries;
            this.operationalCommands = operationalCommands;
            this.messageService = messageService;
            this.eventPublisher = eventPublisher;
        }

        //Using start and end here to determine the date range and if there is time in there for an average. Eg, if there is a 15 to check the 15 minute average.
        public void ProcessRulesV2(NoiseMonitorDto monitorDto, List<RvtAlertRuleDto> allrules, DateTime start, DateTime end, List<NoiseDto>? dtos)
        {
            if (allrules != null && allrules.Count > 0)
            {
                var ruleEvaluator = CreateNoiseRuleEvaluator();
                if (dtos != null && allrules.Any(x => x.AveragingPeriod == 900)) //15 min same as the data process from DTOs
                {
                    var rules = allrules.Where(x => x.AveragingPeriod == 900).OrderBy(x => x.AlertType).ToList();
                    foreach (var sound in dtos)
                    {
                        //ensure alerts are first.
                        //Below to keep track of previous alerts for filed type. Somewhat overengineered, a boolean would have been  enough?
                        AlertType previousAlert = AlertType.Ignore;
                        foreach (var rule in rules)
                        {
                            double level = (double)0;
                            switch (rule.Field.ToLower()) //There must be a slicker way to do this?
                            {
                                case ("laeq"):
                                    level = sound.LAeq;
                                    break;
                                case ("lamax"):
                                    level = sound.LAmax;
                                    break;
                                case ("la90"):
                                    level = sound.LA90;
                                    break;
                                case ("la10"):
                                    level = sound.LA10;
                                    break;
                                case ("lceq"):
                                    level = sound.LCeq;
                                    break;
                                case ("lcmax"):
                                    level = sound.LCmax;
                                    break;
                                case ("lc90"):
                                    level = sound.LC90;
                                    break;
                                case ("lc10"):
                                    level = sound.LC10;
                                    break;
                                default:
                                    break;
                            }
                            ;
                            previousAlert = ruleEvaluator.Evaluate(
                                NewRuleEvaluationRequest(monitorDto, end, end),
                                rule,
                                level,
                                previousAlert);
                        }
                    }
                }


                //<option value = "0" > Site hours</option>
                //<option value = "900" > Instantaneous </ option >
                //< option value="3600">1 hour</option>
                //<option value = "86400" > 1 day</option>
                TimeSpan timeDifference = end - start;
                if (allrules.Where(x => x.AveragingPeriod == 3600).Count() > 0 && (start.Hour != end.Hour || timeDifference.TotalHours > 1))   //to do on the hour so has there been hour value change. The second test is for if the processing has been delayed 24 hours..
                {
                    var rules = allrules.Where(x => x.AveragingPeriod == 3600).OrderBy(x => x.AlertType).ToList();
                    DateTime Starthour = (new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0));
                    while (Starthour < end) // once for hour change in the period
                    {
                        AlertType previousAlert = AlertType.Ignore;
                        var serialId = monitorDto.SerialId!;
                        foreach (var rule in rules)
                        {
                            double level = ruleQueries.GetAverageNoiseLevel(serialId, rule.Field, Starthour, Starthour.AddHours(1));
                            previousAlert = ruleEvaluator.Evaluate(
                                NewRuleEvaluationRequest(monitorDto, end, end),
                                rule,
                                level,
                                previousAlert);

                        }
                        Starthour = Starthour.AddHours(1);
                    }
                }


                if (allrules.Where(x => x.AveragingPeriod == 86400).Count() > 0 && (start.Day != end.Day || timeDifference.TotalDays > 1))   //to do on the day change so has there been day value change. The second test is for if the processing has been delayed mopre than a month..
                {
                    var rules = allrules.Where(x => x.AveragingPeriod == 86400).OrderBy(x => x.AlertType).ToList();
                    DateTime Startday = (new DateTime(start.Year, start.Month, start.Day, 0, 0, 0));
                    while (Startday < end) // once for every day change in the period
                    {
                        AlertType previousAlert = AlertType.Ignore;
                        var serialId = monitorDto.SerialId!;
                        foreach (var rule in rules)
                        {
                            double level = ruleQueries.GetAverageNoiseLevel(serialId, rule.Field, Startday, Startday.AddDays(1));
                            previousAlert = ruleEvaluator.Evaluate(
                                NewRuleEvaluationRequest(monitorDto, end, end),
                                rule,
                                level,
                                previousAlert);
                        }
                        Startday = Startday.AddDays(1);
                    }
                }
            }
        }

        private NoiseRuleEvaluator CreateNoiseRuleEvaluator() =>
            new(
                operationalCommands.UpdateAlertRule,
                monitorId => ruleQueries.ReadAlertContacts(monitorId, out Guid _),
                (request, contacts) => ProcessAlertForContactsV2(
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
            NoiseMonitorDto monitorDto,
            DateTime alertTime,
            DateTime publishTime) =>
            new(
                monitorDto.FleetNr,
                monitorDto.SerialId!,
                monitorDto.Id,
                alertTime,
                alertTime,
                publishTime);

        public void ProcessAlertForContactsV2(
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
    }
}
