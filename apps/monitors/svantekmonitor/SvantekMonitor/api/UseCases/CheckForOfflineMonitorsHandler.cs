using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;
using Svantek.Api.Db;

namespace Svantek.Api.UseCases;

// Summary: Marks monitors offline/online from rule cutoffs and alerts contacts on transitions.
public sealed class CheckForOfflineMonitorsHandler
{
    private readonly ISvantekRuleQueries ruleQueries;
    private readonly SvantekMonitorReader monitorReader;
    private readonly ISvantekMonitorCommands monitorCommands;
    private readonly ISvantekOperationalCommands operationalCommands;
    private readonly SvantekRuleProcessor ruleProcessor;

    public CheckForOfflineMonitorsHandler(
        ISvantekRuleQueries ruleQueries,
        SvantekMonitorReader monitorReader,
        ISvantekMonitorCommands monitorCommands,
        ISvantekOperationalCommands operationalCommands,
        SvantekRuleProcessor ruleProcessor)
    {
        this.ruleQueries = ruleQueries;
        this.monitorReader = monitorReader;
        this.monitorCommands = monitorCommands;
        this.operationalCommands = operationalCommands;
        this.ruleProcessor = ruleProcessor;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rules = ruleQueries.ReadRules(null)
            .Where(rule => RuleConstants.OFFLINE_RULE.Equals(rule.Field))
            .ToList();
        var monitors = await monitorReader.ReadMonitorsAsync(
            lastDataTime: null,
            cancellationToken).ConfigureAwait(false);
        var utcNow = DateTime.UtcNow;
        var failures = new SvantekFailureCollector(operationalCommands);

        foreach (var monitor in monitors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                foreach (var rule in rules)
                {
                    var cutOff = utcNow.Subtract(TimeSpan.FromSeconds(rule.AveragingPeriod));
                    var offlineDateTime = DateTimeUtil.TruncateMillis(utcNow.AddSeconds(-rule.AveragingPeriod));
                    var lastDataTime = monitor.LastDataTime.HasValue
                        ? DateTimeUtil.TruncateMillis(monitor.LastDataTime.Value).ToUniversalTime()
                        : SvantekApi.JAN1_1970;
                    var diffInSeconds = monitor.LastDataTime.HasValue
                        ? offlineDateTime.Subtract(lastDataTime).TotalSeconds
                        : 0;

                    if (lastDataTime < cutOff && !monitor.Offline)
                    {
                        RvtLogger.Logger.LogInformation(
                            "Device serialId={SerialId} has not received data; marking offline",
                            monitor.SerialId);
                        var contacts = ruleQueries.ReadAlertContacts(monitor.Id, out Guid _);
                        ruleProcessor.ProcessAlertForContacts(
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
                        await monitorCommands.SetMonitorOfflineAsync(
                            monitor.Id,
                            offline: true,
                            cancellationToken).ConfigureAwait(false);
                    }
                    else if (lastDataTime >= cutOff && monitor.Offline)
                    {
                        monitor.Offline = false;
                        await monitorCommands.SetMonitorOfflineAsync(
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
