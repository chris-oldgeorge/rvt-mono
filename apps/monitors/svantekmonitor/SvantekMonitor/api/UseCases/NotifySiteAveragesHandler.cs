using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Svantek.Api.Db;
using SvantekMonitor.model.dto;

namespace Svantek.Api.UseCases;

// Summary: Writes daily site noise averages and alerts contacts on site-hours rule breaches.
public sealed class NotifySiteAveragesHandler
{
    private readonly ISvantekMonitorQueries _monitorQueries;
    private readonly ISvantekRuleQueries _ruleQueries;
    private readonly ISvantekMeasurementCommands _measurementCommands;
    private readonly ISvantekOperationalCommands _operationalCommands;
    private readonly SvantekRuleProcessor _ruleProcessor;

    public NotifySiteAveragesHandler(
        ISvantekMonitorQueries monitorQueries,
        ISvantekRuleQueries ruleQueries,
        ISvantekMeasurementCommands measurementCommands,
        ISvantekOperationalCommands operationalCommands,
        SvantekRuleProcessor ruleProcessor)
    {
        _monitorQueries = monitorQueries;
        _ruleQueries = ruleQueries;
        _measurementCommands = measurementCommands;
        _operationalCommands = operationalCommands;
        _ruleProcessor = ruleProcessor;
    }

    public async Task RunAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        List<SiteMonitorsWithSiteHoursDto> monitors = await _monitorQueries
            .ReadSiteMonitorsWithSiteHoursAsync(date, cancellationToken)
            .ConfigureAwait(false);
        SvantekFailureCollector failures = new(_operationalCommands);

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
                double level = _ruleQueries.GetAverageNoiseLevel(
                    monitor.SerialId,
                    "LAeq",
                    periodStart,
                    periodEnd);

                await _measurementCommands.WriteDailyAverageAsync(
                    monitor.SiteId,
                    monitor.Id,
                    "lAeq",
                    level,
                    date,
                    cancellationToken).ConfigureAwait(false);
                await ProcessRulesAsync(monitor, level, periodEnd, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Capture($"NotifySiteAverages monitor {monitor.SerialId}", exception, cancellationToken);
            }
        }

        failures.ThrowIfAny("NotifySiteAverages");
    }

    private async Task ProcessRulesAsync(
        SvantekMonitor.model.dto.SiteMonitorsWithSiteHoursDto monitor,
        double level,
        DateTime periodEnd,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<RvtAlertRuleDto> rules = [.. _ruleQueries.ReadRules(monitor.SerialId)
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
                    await _ruleProcessor.SignalAlertAsync(
                        monitor.SerialId,
                        periodEnd,
                        rule.LimitOn,
                        0,
                        level,
                        rule.AlertType,
                        rule.Field,
                        cancellationToken).ConfigureAwait(false);
                    rule.IsActive = true;
                    _operationalCommands.UpdateAlertRule(rule);
                    previousAlert = rule.AlertType;
                }
            }
            else if (rule.LimitOff >= level && rule.IsActive)
            {
                rule.IsActive = false;
                _operationalCommands.UpdateAlertRule(rule);
            }
            else if (rule.IsActive)
            {
                previousAlert = rule.AlertType;
            }
        }
    }
}
