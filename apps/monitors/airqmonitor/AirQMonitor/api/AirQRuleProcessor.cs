using System.Globalization;
using AirQ.Api.Db;
using AirQ.Model.Dto;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;

namespace AirQ.Api
{
    // Summary: Evaluates AirQ noise readings against alert rules and emits durable alert signals.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiRuleProcessing).
    // - 2026-07-29 Legacy retirement step 4: breaches signal IAlertIngressPort;
    //   the inline RuleAlertNotificationDispatcher send loop is gone.
    public class AirQRuleProcessor
    {
        internal const string SignalSource = "airq.rules";

        private readonly IAirQRuleQueries _ruleQueries;
        private readonly IAirQOperationalCommands _operationalCommands;
        private readonly IAlertIngressPort _alertIngress;

        public AirQRuleProcessor(
            IAirQRuleQueries ruleQueries,
            IAirQOperationalCommands operationalCommands,
            IAlertIngressPort alertIngress)
        {
            _ruleQueries = ruleQueries;
            _operationalCommands = operationalCommands;
            _alertIngress = alertIngress;
        }

        //Using start and end here to determine the date range and if there is time in there for an average. Eg, if there is a 15 to check the 15 minute average.
        public async Task ProcessRulesV2Async(NoiseMonitorDto monitorDto, List<RvtAlertRuleDto> allrules, DateTime start, DateTime end, List<NoiseDto>? dtos, CancellationToken cancellationToken = default)
        {
            if (allrules != null && allrules.Count > 0)
            {
                NoiseRuleEvaluator ruleEvaluator = CreateNoiseRuleEvaluator();
                if (dtos != null && allrules.Any(x => x.AveragingPeriod == 900)) //15 min same as the data process from DTOs
                {
                    List<RvtAlertRuleDto> rules = [.. allrules.Where(x => x.AveragingPeriod == 900).OrderBy(x => x.AlertType)];
                    foreach (NoiseDto sound in dtos)
                    {
                        //ensure alerts are first.
                        //Below to keep track of previous alerts for filed type. Somewhat overengineered, a boolean would have been  enough?
                        AlertType previousAlert = AlertType.Ignore;
                        foreach (RvtAlertRuleDto? rule in rules)
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
                            previousAlert = await ruleEvaluator.EvaluateAsync(
                                NewRuleEvaluationRequest(monitorDto, activityTime: end, alertTime: sound.SampleTime, publishTime: end),
                                rule,
                                level,
                                previousAlert,
                                cancellationToken);
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
                    List<RvtAlertRuleDto> rules = [.. allrules.Where(x => x.AveragingPeriod == 3600).OrderBy(x => x.AlertType)];
                    DateTime Starthour = (new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0));
                    // Only complete windows: a partial trailing hour averages
                    // partial data (an empty window scores 0.0), which would
                    // deactivate latched rules and re-fire them later.
                    while (Starthour.AddHours(1) <= end) // once for each complete hour in the period
                    {
                        AlertType previousAlert = AlertType.Ignore;
                        string serialId = monitorDto.SerialId!;
                        foreach (RvtAlertRuleDto? rule in rules)
                        {
                            double level = _ruleQueries.GetAverageNoiseLevel(serialId, rule.Field, Starthour, Starthour.AddHours(1));
                            previousAlert = await ruleEvaluator.EvaluateAsync(
                                NewRuleEvaluationRequest(monitorDto, activityTime: end, alertTime: Starthour.AddHours(1), publishTime: end),
                                rule,
                                level,
                                previousAlert,
                                cancellationToken);

                        }
                        Starthour = Starthour.AddHours(1);
                    }
                }


                if (allrules.Where(x => x.AveragingPeriod == 86400).Count() > 0 && (start.Day != end.Day || timeDifference.TotalDays > 1))   //to do on the day change so has there been day value change. The second test is for if the processing has been delayed mopre than a month..
                {
                    List<RvtAlertRuleDto> rules = [.. allrules.Where(x => x.AveragingPeriod == 86400).OrderBy(x => x.AlertType)];
                    DateTime Startday = (new DateTime(start.Year, start.Month, start.Day, 0, 0, 0));
                    while (Startday.AddDays(1) <= end) // once for each complete day in the period
                    {
                        AlertType previousAlert = AlertType.Ignore;
                        string serialId = monitorDto.SerialId!;
                        foreach (RvtAlertRuleDto? rule in rules)
                        {
                            double level = _ruleQueries.GetAverageNoiseLevel(serialId, rule.Field, Startday, Startday.AddDays(1));
                            previousAlert = await ruleEvaluator.EvaluateAsync(
                                NewRuleEvaluationRequest(monitorDto, activityTime: end, alertTime: Startday.AddDays(1), publishTime: end),
                                rule,
                                level,
                                previousAlert,
                                cancellationToken);
                        }
                        Startday = Startday.AddDays(1);
                    }
                }
            }
        }

        private NoiseRuleEvaluator CreateNoiseRuleEvaluator() =>
            new(
                _operationalCommands.UpdateAlertRule,
                _alertIngress,
                SignalSource);

        private static RuleEvaluationRequest NewRuleEvaluationRequest(
            NoiseMonitorDto monitorDto,
            DateTime activityTime,
            DateTime alertTime,
            DateTime publishTime) =>
            new(
                monitorDto.FleetNr,
                monitorDto.SerialId!,
                monitorDto.Id,
                activityTime,
                alertTime,
                publishTime);

        // Handler-driven alerts (offline, site averages) carry no MQTT
        // delivery, matching the retired direct-send path.
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
    }
}
