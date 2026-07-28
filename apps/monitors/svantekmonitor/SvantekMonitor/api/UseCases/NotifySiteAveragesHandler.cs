using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Svantek.Api.Db;
using SvantekMonitor.model.dto;

namespace Svantek.Api.UseCases;

// Summary: Writes daily site noise averages and alerts contacts on site-hours rule breaches.
public sealed class NotifySiteAveragesHandler(
    ISvantekMonitorQueries monitorQueries,
    ISvantekRuleQueries ruleQueries,
    ISvantekMeasurementCommands measurementCommands,
    ISvantekOperationalCommands operationalCommands,
    SvantekRuleProcessor ruleProcessor)
{
    private readonly ISvantekMonitorQueries monitorQueries = monitorQueries;
    private readonly ISvantekRuleQueries ruleQueries = ruleQueries;
    private readonly ISvantekMeasurementCommands measurementCommands = measurementCommands;
    private readonly ISvantekOperationalCommands operationalCommands = operationalCommands;
    private readonly SvantekRuleProcessor ruleProcessor = ruleProcessor;

    public async Task RunAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        List<SiteMonitorsWithSiteHoursDto> monitors = await monitorQueries
            .ReadSiteMonitorsWithSiteHoursAsync(date, cancellationToken)
            .ConfigureAwait(false);
        SvantekFailureCollector failures = new(operationalCommands);

        foreach (SiteMonitorsWithSiteHoursDto monitor in monitors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!monitor.StartTime.HasValue || !monitor.EndTime.HasValue)
                {
                    continue;
                }

                DateTime periodStart = date + monitor.StartTime.Value;
                DateTime periodEnd = date + monitor.EndTime.Value;
                double level = ruleQueries.GetAverageNoiseLevel(
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
        List<RvtAlertRuleDto> rules = [.. ruleQueries.ReadRules(monitor.SerialId)
            .Where(rule => rule.AveragingPeriod == 0 && rule.Field == "LAeq")
            .OrderBy(rule => rule.AlertType)];
        AlertType previousAlert = AlertType.Ignore;

        foreach (RvtAlertRuleDto rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (rule.LimitOn <= level && !rule.IsActive && !rule.IsDeleted)
            {
                if (rule.AlertType == AlertType.Alert ||
                    (previousAlert != AlertType.Alert && rule.AlertType == AlertType.Caution))
                {
                    List<Rvt.Monitor.Common.Rules.RvtContactDto> contacts = ruleQueries.ReadAlertContacts(monitor.Id, out Guid _);
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
