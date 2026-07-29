using AirQ.Api.Db;
using AirQ.Model.Dto;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;

namespace AirQ.Api.UseCases;

// Summary: Marks monitors offline from rule cutoffs and alerts contacts on transitions.
// Major updates:
// - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiMonitors).
public class CheckForOfflineMonitorsHandler(
    IAirQRuleQueries ruleQueries,
    AirQMonitorReader monitorReader,
    IAirQMonitorCommands monitorCommands,
    AirQRuleProcessor ruleProcessor)
{
    private readonly IAirQRuleQueries _ruleQueries = ruleQueries;
    private readonly AirQMonitorReader _monitorReader = monitorReader;
    private readonly IAirQMonitorCommands _monitorCommands = monitorCommands;
    private readonly AirQRuleProcessor _ruleProcessor = ruleProcessor;

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<RvtAlertRuleDto> rules = _ruleQueries.ReadRules(null);

        DateTime utcNow = DateTime.UtcNow;
        foreach (RvtAlertRuleDto rule in rules)
        {
            if (RuleConstants.OFFLINE_RULE.Equals(rule.Field))
            {
                DateTime cutOff = utcNow.Subtract(new TimeSpan(hours: 0, minutes: 0, seconds: rule.AveragingPeriod));
                DateTime offlineDateTime = DateTimeUtil.TruncateMillis(utcNow.AddSeconds(-rule.AveragingPeriod));
                List<NoiseMonitorDto> monitors = _monitorReader.ReadMonitors(null);

                foreach (NoiseMonitorDto monitor in monitors!)
                {
                    if (!monitor.Offline)
                    {
                        DateTime lastDataTime = monitor.LastDataTime != null ? DateTimeUtil.TruncateMillis((DateTime)monitor.LastDataTime!).ToUniversalTime() : AirQApi.JAN1_1970;
                        double diffInSeconds = monitor.LastDataTime != null ? offlineDateTime.Subtract(lastDataTime).TotalSeconds : 0;

                        if (lastDataTime < cutOff)
                        {
                            if (RvtLogger.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information)) { RvtLogger.Logger.LogInformation("Device serialId = {Value1} Data has not been recieved marking as offline", monitor.SerialId); }
                            List<Rvt.Monitor.Common.Rules.RvtContactDto> contacts = _ruleQueries.ReadAlertContacts(monitor.Id, out Guid _);
                            _ruleProcessor.ProcessAlertForContactsV2(fleetNr: monitor.FleetNr,
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
                            if (RvtLogger.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug)) { RvtLogger.Logger.LogDebug("Device serialId = {Value1} Data has been recieved marking as online", monitor.SerialId); }
                            monitor.Offline = false;
                        }
                        _monitorCommands.SetMonitorOffline(monitor.Id, monitor.Offline);
                    }
                    else
                    {
                        if (RvtLogger.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
                        {
                            RvtLogger.Logger.LogDebug("Monitor serialId = {Value1} is already offline lastDataTime={Value2}",
                            monitor.SerialId, monitor.LastDataTime);
                        }
                    }
                }
            }
        }

        return Task.CompletedTask;
    }
}
