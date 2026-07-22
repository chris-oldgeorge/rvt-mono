using Rvt.Monitor.Common.Notifications;
using Svantek.Api.Db;

namespace Svantek.Api.UseCases;

// Summary: Writes daily site noise averages and alerts contacts on site-hours rule breaches.
public sealed class NotifySiteAveragesHandler
{
    private readonly ISvantekMonitorQueries monitorQueries;
    private readonly ISvantekRuleQueries ruleQueries;
    private readonly ISvantekMeasurementCommands measurementCommands;
    private readonly ISvantekOperationalCommands operationalCommands;
    private readonly SvantekRuleProcessor ruleProcessor;

    public NotifySiteAveragesHandler(
        ISvantekMonitorQueries monitorQueries,
        ISvantekRuleQueries ruleQueries,
        ISvantekMeasurementCommands measurementCommands,
        ISvantekOperationalCommands operationalCommands,
        SvantekRuleProcessor ruleProcessor)
    {
        this.monitorQueries = monitorQueries;
        this.ruleQueries = ruleQueries;
        this.measurementCommands = measurementCommands;
        this.operationalCommands = operationalCommands;
        this.ruleProcessor = ruleProcessor;
    }

    public async Task RunAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var monitors = await monitorQueries
            .ReadSiteMonitorsWithSiteHoursAsync(date, cancellationToken)
            .ConfigureAwait(false);
        var failures = new SvantekFailureCollector(operationalCommands);

        foreach (var monitor in monitors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!monitor.StartTime.HasValue || !monitor.EndTime.HasValue)
                {
                    continue;
                }

                var periodStart = date + monitor.StartTime.Value;
                var periodEnd = date + monitor.EndTime.Value;
                var level = ruleQueries.GetAverageNoiseLevel(
                    monitor.SerialId,
                    "LAeq",
                    periodStart,
                    periodEnd);

                await measurementCommands.WriteDailyAverageAsync(
                    monitor.SiteId,
                    monitor.Id,
                    "lAeq",
                    level,
                    date,
                    cancellationToken).ConfigureAwait(false);
                ProcessRules(monitor, level, periodEnd, cancellationToken);
            }
            catch (Exception exception)
            {
                failures.Capture($"NotifySiteAverages monitor {monitor.SerialId}", exception);
            }
        }

        failures.ThrowIfAny("NotifySiteAverages");
    }

    private void ProcessRules(
        SvantekMonitor.model.dto.SiteMonitorsWithSiteHoursDto monitor,
        double level,
        DateTime periodEnd,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rules = ruleQueries.ReadRules(monitor.SerialId)
            .Where(rule => rule.AveragingPeriod == 0 && rule.Field == "LAeq")
            .OrderBy(rule => rule.AlertType)
            .ToList();
        var previousAlert = AlertType.Ignore;

        foreach (var rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (rule.LimitOn <= level && !rule.IsActive && !rule.IsDeleted)
            {
                if (rule.AlertType == AlertType.Alert ||
                    (previousAlert != AlertType.Alert && rule.AlertType == AlertType.Caution))
                {
                    var contacts = ruleQueries.ReadAlertContacts(monitor.Id, out Guid _);
                    ruleProcessor.ProcessAlertForContacts(
                        monitor.FleetNr,
                        monitor.SerialId,
                        periodEnd,
                        rule.LimitOn,
                        0,
                        level,
                        rule.AlertType,
                        rule.Field,
                        monitor.Id,
                        contacts);
                    rule.IsActive = true;
                    operationalCommands.UpdateAlertRule(rule);
                    previousAlert = rule.AlertType;
                }
            }
            else if (rule.LimitOff >= level && rule.IsActive)
            {
                rule.IsActive = false;
                operationalCommands.UpdateAlertRule(rule);
            }
            else if (rule.IsActive)
            {
                previousAlert = rule.AlertType;
            }
        }
    }
}
