using Microsoft.Extensions.Logging;
using Omnidots.Api.Db;
using Omnidots.Model.Config;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Utilities;

namespace Omnidots.Api.UseCases
{
    // Summary: Marks monitors offline/online from offline-rule cutoffs and site hours, alerting contacts on transitions.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiMonitors).
    public class CheckForOfflineMonitorsHandler
    {
        private readonly IOmnidotsRuleQueries ruleQueries;
        private readonly OmnidotsMonitorReader monitorReader;
        private readonly IOmnidotsMonitorQueries monitorQueries;
        private readonly IOmnidotsMonitorCommands monitorCommands;
        private readonly IOmnidotsOperationalCommands operationalCommands;
        private readonly OmnidotsRuleProcessor ruleProcessor;

        public CheckForOfflineMonitorsHandler(
            IOmnidotsRuleQueries ruleQueries,
            OmnidotsMonitorReader monitorReader,
            IOmnidotsMonitorQueries monitorQueries,
            IOmnidotsMonitorCommands monitorCommands,
            IOmnidotsOperationalCommands operationalCommands,
            OmnidotsRuleProcessor ruleProcessor)
        {
            this.ruleQueries = ruleQueries;
            this.monitorReader = monitorReader;
            this.monitorQueries = monitorQueries;
            this.monitorCommands = monitorCommands;
            this.operationalCommands = operationalCommands;
            this.ruleProcessor = ruleProcessor;
        }

        public void Run()
        {
            var rules = ruleQueries.ReadRules(null);

            var utcNow = DateTime.UtcNow;
            var failures = new List<OmnidotsMonitorFailure>();
            foreach (var rule in rules)
            {
                if (rule.Field == "offline-rule")
                {
                    var cutOff = utcNow.Subtract(new TimeSpan(hours: 0, minutes: 0, seconds: rule.AveragingPeriod));
                    var offlineDateTime = DateTimeUtil.TruncateMillis(utcNow.AddSeconds(-rule.AveragingPeriod));
                    var monitors = monitorReader.ReadMonitors(offlineDateTime);

                    foreach (var monitor in monitors!)
                    {
                        var lastDataTime = monitor.LastDataTime != null
                            ? AsUtc(DateTimeUtil.TruncateMillis((DateTime)monitor.LastDataTime))
                            : OmnidotsApi.JAN1_1970;
                        double diffInSeconds = offlineDateTime.Subtract(lastDataTime).TotalSeconds;
                        if (lastDataTime < cutOff) // remove all with less 24 hours already
                        {
                            if (!TryResolveTimeZone(monitor.TimeZone, out var siteTimeZone))
                            {
                                var failure = new InvalidOperationException(
                                    "Monitor timezone is missing or invalid.");
                                var message = $"CheckForOfflineMonitors serialId={monitor.SerialId}";
                                failures.Add(OmnidotsMonitorFailure.Record(
                                    monitor.SerialId,
                                    failure,
                                    () => operationalCommands.HandleException(message, failure)));
                                continue;
                            }

                            TimeSpan activeDuration;
                            try
                            {
                                var siteTimes = monitorQueries.ReadSiteTimes(monitor.Id);
                                activeDuration = SiteActiveDurationCalculator.Between(
                                    siteTimes,
                                    lastDataTime,
                                    utcNow,
                                    siteTimeZone);
                            }
                            catch (SiteScheduleConfigurationException failure)
                            {
                                var message = $"CheckForOfflineMonitors serialId={monitor.SerialId}";
                                failures.Add(OmnidotsMonitorFailure.Record(
                                    monitor.SerialId,
                                    failure,
                                    () => operationalCommands.HandleException(message, failure)));
                                continue;
                            }

                            if (activeDuration > TimeSpan.FromSeconds(rule.AveragingPeriod))
                            {
                                if (!monitor.Offline)
                                {
                                    RvtLogger.Logger.LogInformation("Device serialId = {Value1} Data has not been recieved marking as offline", monitor.SerialId);

                                    var notification = new NotificationDto(id: Guid.NewGuid(),
                                                                                   notificationTime: DateTime.UtcNow,
                                                                                   limitOn: 0,
                                                                                   averagingPeriod: rule.AveragingPeriod,
                                                                                   level: diffInSeconds,
                                                                                   closedTime: null,
                                                                                   closedByUser: null,
                                                                                   alertField: rule.Field,
                                                                                   alertType: AlertType.Offline,
                                                                                   monitorId: monitor.Id);

                                    ruleProcessor.ProcessAlertForContacts(monitor, notification);

                                    monitor.Offline = true;
                                    monitorCommands.SetMonitorOffline(monitor.Id, monitor.Offline);
                                }
                                else
                                    RvtLogger.Logger.LogInformation("Device serialId = {Value1} was already offline", monitor.SerialId);

                            }
                            else
                            {
                                if (monitor.Offline)
                                {
                                    monitor.Offline = false;
                                    monitorCommands.SetMonitorOffline(monitor.Id, monitor.Offline);
                                }
                                RvtLogger.Logger.LogDebug("Device serialId = {Value1} Data not offline (considering site hours) marking as online", monitor.SerialId);
                            }
                        }
                        else
                        {
                            if (monitor.Offline)
                            {
                                monitor.Offline = false;
                                monitorCommands.SetMonitorOffline(monitor.Id, monitor.Offline);
                            }
                            RvtLogger.Logger.LogDebug("Device serialId = {Value1} Data not offline (less than 24 hours)  marking as online", monitor.SerialId);
                        }
                    }
                }
            }

            if (failures.Count > 0)
            {
                throw new OmnidotsImportException("CheckForOfflineMonitors", failures);
            }
        }

        private static DateTime AsUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        private static bool TryResolveTimeZone(string? timeZoneId, out TimeZoneInfo timeZone)
        {
            if (!string.IsNullOrWhiteSpace(timeZoneId))
            {
                try
                {
                    timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                    return true;
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            timeZone = null!;
            return false;
        }
    }
}
