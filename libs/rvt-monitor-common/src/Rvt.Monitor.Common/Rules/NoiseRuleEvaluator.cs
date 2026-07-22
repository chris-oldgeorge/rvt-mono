using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Rules;

namespace Rvt.Monitor.Common.Rules;

public sealed class NoiseRuleEvaluator
{
    private readonly Action<RvtAlertRuleDto> updateAlertRule;
    private readonly Func<Guid, List<RvtContactDto>> readAlertContacts;
    private readonly Action<RuleNotificationRequest, List<RvtContactDto>> processAlertForContacts;
    private readonly IMonitorEventPublisher eventPublisher;

    public NoiseRuleEvaluator(
        Action<RvtAlertRuleDto> updateAlertRule,
        Func<Guid, List<RvtContactDto>> readAlertContacts,
        Action<RuleNotificationRequest, List<RvtContactDto>> processAlertForContacts,
        IMonitorEventPublisher eventPublisher)
    {
        this.updateAlertRule = updateAlertRule;
        this.readAlertContacts = readAlertContacts;
        this.processAlertForContacts = processAlertForContacts;
        this.eventPublisher = eventPublisher;
    }

    public Rvt.Monitor.Common.Notifications.AlertType Evaluate(
        RuleEvaluationRequest request,
        RvtAlertRuleDto rule,
        double level,
        Rvt.Monitor.Common.Notifications.AlertType previousAlert)
    {
        if (request.DeactivateDeletedRules && rule.IsDeleted)
        {
            RvtLogger.Logger.LogInformation("PROCESS-RULES Ignoring deleted rule ={Value1}", rule.ToString());
            if (rule.IsActive)
            {
                rule.IsActive = false;
                updateAlertRule(rule);
            }

            return previousAlert;
        }

        if (!rule.RuleActiveTime.IsActive(request.ActivityTime))
        {
            RvtLogger.Logger.LogInformation("PROCESS-RULES Time is outside activity window ignoring rule ={Value1}", rule.ToString());
            return previousAlert;
        }

        RvtLogger.Logger.LogInformation(
            "PROCESS-RULES Processing rule serialId={Value1} level={Value2} rule={Value3}",
            request.MonitorSerialId,
            level,
            rule.ToString());

        if (level >= rule.LimitOn)
        {
            return ProcessLimitExceeded(request, rule, level, previousAlert);
        }

        if (level <= rule.LimitOff)
        {
            RvtLogger.Logger.LogInformation(
                "PROCESS-RULES Rule level={Value1} is below limit off={Value2} setting rule as inactive",
                level,
                rule.LimitOff);

            if (rule.IsActive)
            {
                rule.IsActive = false;
                updateAlertRule(rule);
            }

            return previousAlert;
        }

        if (rule.IsActive)
        {
            return rule.AlertType;
        }

        RvtLogger.Logger.LogInformation(
            "PROCESS-RULES Rule level={Value1} is within limits on={Value2} off={Value3}",
            level,
            rule.LimitOn,
            rule.LimitOff);

        return previousAlert;
    }

    private Rvt.Monitor.Common.Notifications.AlertType ProcessLimitExceeded(
        RuleEvaluationRequest request,
        RvtAlertRuleDto rule,
        double level,
        Rvt.Monitor.Common.Notifications.AlertType previousAlert)
    {
        if (rule.AlertType != Rvt.Monitor.Common.Notifications.AlertType.Alert
            && (previousAlert == Rvt.Monitor.Common.Notifications.AlertType.Alert || rule.AlertType != Rvt.Monitor.Common.Notifications.AlertType.Caution))
        {
            RvtLogger.Logger.LogInformation("PROCESS-RULES Already have an Alert for this monitor and field.");
            return previousAlert;
        }

        if (rule.IsActive)
        {
            RvtLogger.Logger.LogWarning("PROCESS-RULES Ignoring already active rule ={Value1}", rule.ToString());
            return rule.AlertType;
        }

        var contacts = readAlertContacts(request.MonitorId);
        processAlertForContacts(
            new RuleNotificationRequest(
                request.FleetNr,
                rule.SerialId!,
                request.AlertTime,
                rule.LimitOn,
                rule.AveragingPeriod,
                level,
                rule.AlertType,
                rule.Field,
                request.MonitorId),
            contacts);

        var text = string.Format(
            "Alert {0} level={1} exceeds limitOn/Off={2}/{3}",
            rule.Field,
            level,
            rule.LimitOn,
            rule.LimitOff);
        eventPublisher.PublishAlert(request.PublishTime, request.MonitorSerialId, text);

        rule.IsActive = true;
        updateAlertRule(rule);
        return rule.AlertType;
    }
}

public sealed record RuleEvaluationRequest(
    string FleetNr,
    string MonitorSerialId,
    Guid MonitorId,
    DateTime ActivityTime,
    DateTime AlertTime,
    DateTime PublishTime,
    bool DeactivateDeletedRules = true);

public sealed record RuleNotificationRequest(
    string FleetNr,
    string SerialId,
    DateTime AlertTime,
    double LimitOn,
    int AveragingPeriod,
    double Level,
    Rvt.Monitor.Common.Notifications.AlertType AlertType,
    string Field,
    Guid MonitorId);
