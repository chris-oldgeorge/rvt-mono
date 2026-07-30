using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;
using Svantek.Api.Db;
using SvantekMonitor.model.dto;

namespace Svantek.Api.UseCases;

// Summary: Marks monitors offline/online from rule cutoffs and alerts contacts on transitions.
public sealed class CheckForOfflineMonitorsHandler
{
    private readonly ISvantekRuleQueries _ruleQueries;
    private readonly SvantekMonitorReader _monitorReader;
    private readonly ISvantekMonitorCommands _monitorCommands;
    private readonly ISvantekOperationalCommands _operationalCommands;
    private readonly SvantekRuleProcessor _ruleProcessor;

    public CheckForOfflineMonitorsHandler(
        ISvantekRuleQueries ruleQueries,
        SvantekMonitorReader monitorReader,
        ISvantekMonitorCommands monitorCommands,
        ISvantekOperationalCommands operationalCommands,
        SvantekRuleProcessor ruleProcessor)
    {
        _ruleQueries = ruleQueries;
        _monitorReader = monitorReader;
        _monitorCommands = monitorCommands;
        _operationalCommands = operationalCommands;
        _ruleProcessor = ruleProcessor;
    }

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
                        if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
                        {
                            RvtLogger.Logger.LogInformation(
                                "Device serialId={SerialId} has not received data; marking offline",
                                monitor.SerialId);
                        }
                        await _ruleProcessor.SignalAlertAsync(
                            monitor.SerialId,
                            utcNow,
                            0,
                            rule.AveragingPeriod,
                            diffInSeconds,
                            AlertType.Offline,
                            rule.Field,
                            cancellationToken).ConfigureAwait(false);
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
                failures.Capture($"CheckForOfflineMonitors monitor {monitor.SerialId}", exception, cancellationToken);
            }
        }

        failures.ThrowIfAny("CheckForOfflineMonitors");
    }
}
