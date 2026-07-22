using AirQ.Api.Db;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;

namespace AirQ.Api.UseCases
{
    // Summary: Marks monitors offline from rule cutoffs and alerts contacts on transitions.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiMonitors).
    public class CheckForOfflineMonitorsHandler
    {
        private readonly IAirQRuleQueries ruleQueries;
        private readonly AirQMonitorReader monitorReader;
        private readonly IAirQMonitorCommands monitorCommands;
        private readonly AirQRuleProcessor ruleProcessor;

        public CheckForOfflineMonitorsHandler(
            IAirQRuleQueries ruleQueries,
            AirQMonitorReader monitorReader,
            IAirQMonitorCommands monitorCommands,
            AirQRuleProcessor ruleProcessor)
        {
            this.ruleQueries = ruleQueries;
            this.monitorReader = monitorReader;
            this.monitorCommands = monitorCommands;
            this.ruleProcessor = ruleProcessor;
        }

        public void Run()
        {
            var rules = ruleQueries.ReadRules(null);

            var utcNow = DateTime.UtcNow;
            foreach (var rule in rules)
            {
                if (RuleConstants.OFFLINE_RULE.Equals(rule.Field))
                {
                    var cutOff = utcNow.Subtract(new TimeSpan(hours: 0, minutes: 0, seconds: rule.AveragingPeriod));
                    var offlineDateTime = DateTimeUtil.TruncateMillis(utcNow.AddSeconds(-rule.AveragingPeriod));
                    var monitors = monitorReader.ReadMonitors(null);

                    foreach (var monitor in monitors!)
                    {
                        if (!monitor.Offline)
                        {
                            var lastDataTime = monitor.LastDataTime != null ? DateTimeUtil.TruncateMillis((DateTime)monitor.LastDataTime!).ToUniversalTime() : AirQApi.JAN1_1970;
                            double diffInSeconds = monitor.LastDataTime != null ? offlineDateTime.Subtract(lastDataTime).TotalSeconds : 0;

                            if (lastDataTime < cutOff)
                            {
                                RvtLogger.Logger.LogInformation("Device serialId = {Value1} Data has not been recieved marking as offline", monitor.SerialId);
                                var contacts = ruleQueries.ReadAlertContacts(monitor.Id, out Guid _);
                                ruleProcessor.ProcessAlertForContactsV2(fleetNr: monitor.FleetNr,
                                                        serialId: monitor.SerialId!,
                                                        alertTime: DateTime.UtcNow,
                                                        limitOn: 0,
                                                        averagingPeriod: rule.AveragingPeriod,
                                                        level: diffInSeconds,
                                                        alertType: AlertType.Offline,
                                                        field: rule.Field,
                                                        monitorId: monitor.Id,
                                                        contacts: contacts);
                                monitor.Offline = true;
                            }
                            else
                            {
                                RvtLogger.Logger.LogDebug("Device serialId = {Value1} Data has been recieved marking as online", monitor.SerialId);
                                monitor.Offline = false;
                            }
                            monitorCommands.SetMonitorOffline(monitor.Id, monitor.Offline);
                        }
                        else
                        {
                            RvtLogger.Logger.LogDebug("Monitor serialId = {Value1} is already offline lastDataTime={Value2}",
                                monitor.SerialId, monitor.LastDataTime);
                        }
                    }
                }
            }
        }
    }
}
