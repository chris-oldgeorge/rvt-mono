using AirQ.Api.Db;
using Rvt.Monitor.Common.Notifications;

namespace AirQ.Api.UseCases
{
    // Summary: Writes daily site noise averages and alerts contacts on site-hours rule breaches.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiMonitorsNoiseLevels).
    public class NotifySiteAveragesHandler
    {
        private readonly IAirQMonitorQueries monitorQueries;
        private readonly IAirQRuleQueries ruleQueries;
        private readonly IAirQMeasurementCommands measurementCommands;
        private readonly IAirQOperationalCommands operationalCommands;
        private readonly AirQRuleProcessor ruleProcessor;

        public NotifySiteAveragesHandler(
            IAirQMonitorQueries monitorQueries,
            IAirQRuleQueries ruleQueries,
            IAirQMeasurementCommands measurementCommands,
            IAirQOperationalCommands operationalCommands,
            AirQRuleProcessor ruleProcessor)
        {
            this.monitorQueries = monitorQueries;
            this.ruleQueries = ruleQueries;
            this.measurementCommands = measurementCommands;
            this.operationalCommands = operationalCommands;
            this.ruleProcessor = ruleProcessor;
        }

        public void Run(DateTime date)
        {
            var monitors = monitorQueries.ReadSiteMonitorsWithSiteHours(date);
            foreach (var monitor in monitors)
            {

                var level = ruleQueries.GetAverageNoiseLevel(serialNumber: monitor.SerialId,
                                              columnName: "LAeq", // Assuming that is enough for now.
                                              start: date + monitor.StartTime!.Value,
                                              end: date + monitor.EndTime!.Value);

                measurementCommands.WriteDailyAverage(siteId: monitor.SiteId,
                                           monitorId: monitor.Id,
                                           field: "lAeq",
                                           level: level,
                                           timestamp: date);
                var allRules = ruleQueries.ReadRules(monitor.SerialId);
                if (allRules != null && allRules.Count > 0)
                {
                    var rules = allRules.Where(x => x.AveragingPeriod == 0 && x.Field == "LAeq").OrderBy(x => x.AlertType).ToList();
                    AlertType previousAlert = AlertType.Ignore;
                    foreach (var rule in rules)
                    {
                        if (rule.LimitOn <= level && !rule.IsActive && !rule.IsDeleted)
                        {
                            //Either an alert or cautioon with no previous alert
                            if (rule.AlertType == AlertType.Alert || (previousAlert != AlertType.Alert && rule.AlertType == AlertType.Caution)) //Not to send cautions if we have sent alerts but if there are two alert rules lets go for it
                            {
                                //New breach generate notification
                                var contacts = ruleQueries.ReadAlertContacts(monitor.Id, out Guid siteId);
                                ruleProcessor.ProcessAlertForContactsV2(fleetNr: monitor.FleetNr,
                                serialId: monitor.SerialId,
                                alertTime: date + monitor.EndTime!.Value,
                                limitOn: rule.LimitOn,
                                averagingPeriod: 0,
                                level: level,
                                alertType: rule.AlertType,
                                field: rule.Field,
                                monitorId: monitor.Id,
                                contacts: contacts);

                                rule.IsActive = true;
                                operationalCommands.UpdateAlertRule(rule);
                                previousAlert = rule.AlertType;
                            }
                        }
                        else if (rule.LimitOff >= level && rule.IsActive)
                        {
                            //turn off active rule
                            rule.IsActive = false;
                            operationalCommands.UpdateAlertRule(rule);
                        }
                        else if (rule.IsActive)
                            previousAlert = rule.AlertType;

                    }
                }
            }
        }
    }
}
