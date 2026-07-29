using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;
using Svantek.Api.Db;
using Svantek.Model.Dto;

namespace Svantek.Api.UseCases;

// Summary: Marks monitors offline/online from rule cutoffs and alerts contacts on transitions.
public sealed class CheckForOfflineMonitorsHandler(
    ISvantekRuleQueries ruleQueries,
    SvantekMonitorReader monitorReader,
    ISvantekMonitorCommands monitorCommands,
    ISvantekOperationalCommands operationalCommands,
    SvantekRuleProcessor ruleProcessor)
{
    private readonly ISvantekRuleQueries _ruleQueries = ruleQueries;
    private readonly SvantekMonitorReader _monitorReader = monitorReader;
    private readonly ISvantekMonitorCommands _monitorCommands = monitorCommands;
    private readonly ISvantekOperationalCommands _operationalCommands = operationalCommands;
    private readonly SvantekRuleProcessor _ruleProcessor = ruleProcessor;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<RvtAlertRuleDto> rules = [.. _ruleQueries.ReadRules(null).Where(rule => RuleConstants.OFFLINE_RULE.Equals(rule.Field))];
        List<NoiseMonitorReadDto> monitors = await _monitorReader.ReadMonitorsAsync(
            lastDataTime: null,
            cancellationToken).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        SvantekFailureCollector failures = new(_operationalCommands);

        foreach (NoiseMonitorReadDto monitor in monitors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                foreach (RvtAlertRuleDto rule in rules)
                {
                    DateTime cutOff = utcNow.Subtract(TimeSpan.FromSeconds(rule.AveragingPeriod));
                    DateTime offlineDateTime = DateTimeUtil.TruncateMillis(utcNow.AddSeconds(-rule.AveragingPeriod));
                    DateTime lastDataTime = monitor.LastDataTime.HasValue
                        ? DateTimeUtil.TruncateMillis(monitor.LastDataTime.Value).ToUniversalTime()
                        : SvantekApi.JAN1_1970;
                    double diffInSeconds = monitor.LastDataTime.HasValue
                        ? offlineDateTime.Subtract(lastDataTime).TotalSeconds
                        : 0;

                    if (lastDataTime < cutOff && !monitor.Offline)
                    {
                        RvtLogger.Logger.LogInformation(
                            "Device serialId={SerialId} has not received data; marking offline",
                            monitor.SerialId);
                        List<Rvt.Monitor.Common.Rules.RvtContactDto> contacts = _ruleQueries.ReadAlertContacts(monitor.Id, out Guid _);
                        _ruleProcessor.ProcessAlertForContacts(
                            monitor.FleetNr,
                            monitor.SerialId,
                            utcNow,
                            0,
                            rule.AveragingPeriod,
                            diffInSeconds,
                            AlertType.Offline,
                            rule.Field,
                            monitor.Id,
                            contacts);
                        monitor.Offline = true;
                        await _monitorCommands.SetMonitorOfflineAsync(
                            monitor.Id,
                            offline: true,
                            cancellationToken).ConfigureAwait(false);
                    }
                    else if (lastDataTime >= cutOff && monitor.Offline)
                    {
                        monitor.Offline = false;
                        await _monitorCommands.SetMonitorOfflineAsync(
                            monitor.Id,
                            offline: false,
                            cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exception)
            {
                failures.Capture($"CheckForOfflineMonitors monitor {monitor.SerialId}", exception);
            }
        }

        failures.ThrowIfAny("CheckForOfflineMonitors");
    }
}
