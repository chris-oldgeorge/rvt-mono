using System.Globalization;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.Common.Rules;

// Summary: Shared noise-rule evaluation; breaches become durable alert signals.
// Major updates:
// - 2026-07-29 Legacy retirement step 4: breaches now signal IAlertIngressPort
//   (durable notification, contact fan-out, and MQTT delivery) instead of the
//   deleted RuleAlertNotificationDispatcher's inline synchronous sends.

public sealed class NoiseRuleEvaluator
{
    private readonly Action<RvtAlertRuleDto> _updateAlertRule;
    private readonly IAlertIngressPort _alertIngress;
    private readonly string _signalSource;

    public NoiseRuleEvaluator(
        Action<RvtAlertRuleDto> updateAlertRule,
        IAlertIngressPort alertIngress,
        string signalSource)
    {
        ArgumentNullException.ThrowIfNull(updateAlertRule);
        ArgumentNullException.ThrowIfNull(alertIngress);
        ArgumentException.ThrowIfNullOrWhiteSpace(signalSource);

        _updateAlertRule = updateAlertRule;
        _alertIngress = alertIngress;
        _signalSource = signalSource;
    }

    public async Task<AlertType> EvaluateAsync(
        RuleEvaluationRequest request,
        RvtAlertRuleDto rule,
        double level,
        AlertType previousAlert,
        CancellationToken cancellationToken = default)
    {
        if (request.DeactivateDeletedRules && rule.IsDeleted)
        {
            if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
            {
                RvtLogger.Logger.LogInformation("PROCESS-RULES Ignoring deleted rule ={Value1}", rule.ToString());
            }

            if (rule.IsActive)
            {
                rule.IsActive = false;
                _updateAlertRule(rule);
            }

            return previousAlert;
        }

        if (!rule.RuleActiveTime.IsActive(request.ActivityTime))
        {
            if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
            {
                RvtLogger.Logger.LogInformation("PROCESS-RULES Time is outside activity window ignoring rule ={Value1}", rule.ToString());
            }

            return previousAlert;
        }

        if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
        {
            RvtLogger.Logger.LogInformation(
                "PROCESS-RULES Processing rule serialId={Value1} level={Value2} rule={Value3}",
                request.MonitorSerialId,
                level,
                rule.ToString());
        }

        if (level >= rule.LimitOn)
        {
            return await ProcessLimitExceededAsync(request, rule, level, previousAlert, cancellationToken);
        }

        if (level <= rule.LimitOff)
        {
            if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
            {
                RvtLogger.Logger.LogInformation(
                    "PROCESS-RULES Rule level={Value1} is below limit off={Value2} setting rule as inactive",
                    level,
                    rule.LimitOff);
            }

            if (rule.IsActive)
            {
                rule.IsActive = false;
                _updateAlertRule(rule);
            }

            return previousAlert;
        }

        if (rule.IsActive)
        {
            return rule.AlertType;
        }

        if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
        {
            RvtLogger.Logger.LogInformation(
                "PROCESS-RULES Rule level={Value1} is within limits on={Value2} off={Value3}",
                level,
                rule.LimitOn,
                rule.LimitOff);
        }

        return previousAlert;
    }

    private async Task<AlertType> ProcessLimitExceededAsync(
        RuleEvaluationRequest request,
        RvtAlertRuleDto rule,
        double level,
        AlertType previousAlert,
        CancellationToken cancellationToken)
    {
        if (rule.AlertType != AlertType.Alert
            && (previousAlert == AlertType.Alert || rule.AlertType != AlertType.Caution))
        {
            RvtLogger.Logger.LogInformation("PROCESS-RULES Already have an Alert for this monitor and field.");
            return previousAlert;
        }

        if (rule.IsActive)
        {
            if (RvtLogger.Logger.IsEnabled(LogLevel.Warning))
            {
                RvtLogger.Logger.LogWarning("PROCESS-RULES Ignoring already active rule ={Value1}", rule.ToString());
            }

            return rule.AlertType;
        }

        string message = string.Create(
            CultureInfo.InvariantCulture,
            $"Alert {rule.Field} level={level} exceeds limitOn/Off={rule.LimitOn}/{rule.LimitOff}");
        await _alertIngress.AcceptAsync(
            RuleAlertSignals.Create(
                _signalSource,
                request.MonitorSerialId,
                request.AlertTime,
                rule.AlertType,
                rule.Field,
                level,
                rule.LimitOn,
                rule.AveragingPeriod,
                message,
                AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms),
            cancellationToken);

        rule.IsActive = true;
        _updateAlertRule(rule);
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
    AlertType AlertType,
    string Field,
    Guid MonitorId);
