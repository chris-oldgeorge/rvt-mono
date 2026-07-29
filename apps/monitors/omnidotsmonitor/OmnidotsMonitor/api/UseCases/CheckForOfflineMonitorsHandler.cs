using Microsoft.Extensions.Logging;
using Omnidots.Api.Db;
using Omnidots.Model.Config;
using Omnidots.Model.Dto;
using Rvt.Monitor.Common.Alerts;
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
        private readonly IAlertIngressPort _alertIngress;

        public CheckForOfflineMonitorsHandler(
            IOmnidotsRuleQueries ruleQueries,
            OmnidotsMonitorReader monitorReader,
            IOmnidotsMonitorQueries monitorQueries,
            IOmnidotsMonitorCommands monitorCommands,
            IOmnidotsOperationalCommands operationalCommands,
            IAlertIngressPort alertIngress)
        {
            this.ruleQueries = ruleQueries;
            this.monitorReader = monitorReader;
            this.monitorQueries = monitorQueries;
            this.monitorCommands = monitorCommands;
            this.operationalCommands = operationalCommands;
            _alertIngress = alertIngress;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<Rvt.Monitor.Common.Rules.RvtAlertRuleDto> rules = ruleQueries.ReadRules(null);

            DateTime utcNow = DateTime.UtcNow;
            List<OmnidotsMonitorFailure> failures = [];
            foreach (Rvt.Monitor.Common.Rules.RvtAlertRuleDto rule in rules)
            {
                if (rule.Field == "offline-rule")
                {
                    DateTime cutOff = utcNow.Subtract(new TimeSpan(hours: 0, minutes: 0, seconds: rule.AveragingPeriod));
                    DateTime offlineDateTime = DateTimeUtil.TruncateMillis(utcNow.AddSeconds(-rule.AveragingPeriod));
                    List<VibrationMonitorDto> monitors = monitorReader.ReadMonitors(offlineDateTime);

                    foreach (VibrationMonitorDto monitor in monitors!)
                    {
                        DateTime lastDataTime = monitor.LastDataTime != null
                            ? AsUtc(DateTimeUtil.TruncateMillis((DateTime)monitor.LastDataTime))
                            : OmnidotsApi.JAN1_1970;
                        double diffInSeconds = offlineDateTime.Subtract(lastDataTime).TotalSeconds;
                        if (lastDataTime < cutOff) // remove all with less 24 hours already
                        {
                            if (!TryResolveTimeZone(monitor.TimeZone, out TimeZoneInfo? siteTimeZone))
                            {
                                InvalidOperationException failure = new(
                                    "Monitor timezone is missing or invalid.");
                                string message = $"CheckForOfflineMonitors serialId={monitor.SerialId}";
                                failures.Add(OmnidotsMonitorFailure.Record(
                                    monitor.SerialId,
                                    failure,
                                    () => operationalCommands.HandleException(message, failure)));
                                continue;
                            }

                            TimeSpan activeDuration;
                            try
                            {
                                SiteTimes siteTimes = monitorQueries.ReadSiteTimes(monitor.Id);
                                activeDuration = SiteActiveDurationCalculator.Between(
                                    siteTimes,
                                    lastDataTime,
                                    utcNow,
                                    siteTimeZone);
                            }
                            catch (SiteScheduleConfigurationException failure)
                            {
                                string message = $"CheckForOfflineMonitors serialId={monitor.SerialId}";
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

                                    // The durable stack writes the notification, plans
                                    // per-contact deliveries, and retries them; the
                                    // data-gap start keys idempotency across re-runs.
                                    await _alertIngress.AcceptAsync(
                                        new AlertSignal(
                                            Source: "omnidots.offline",
                                            SourceEventKey: $"{monitor.SerialId}:offline:{lastDataTime:O}",
                                            EventTime: utcNow,
                                            SerialId: monitor.SerialId!,
                                            AlertType: AlertType.Offline,
                                            Field: rule.Field,
                                            Level: diffInSeconds,
                                            Limit: 0,
                                            AveragingPeriod: rule.AveragingPeriod,
                                            Message: $"Monitor offline for {diffInSeconds:F0}s",
                                            DeliveryChannels: AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms,
                                            SuppressionWindow: TimeSpan.Zero),
                                        cancellationToken);

                                    monitor.Offline = true;
                                    monitorCommands.SetMonitorOffline(monitor.Id, monitor.Offline);
                                }
                                else
                                {
                                    RvtLogger.Logger.LogInformation("Device serialId = {Value1} was already offline", monitor.SerialId);
                                }
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
