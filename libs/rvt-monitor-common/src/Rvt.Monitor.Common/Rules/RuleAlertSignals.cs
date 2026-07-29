using System.Globalization;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Utilities;

namespace Rvt.Monitor.Common.Rules;

// Summary: Builds durable alert signals for rule-driven monitor alerting.
// Major updates:
// - 2026-07-29 Legacy retirement step 4: introduced when AirQ and Svantek rule
//   alerting moved from the deleted RuleAlertNotificationDispatcher onto the
//   durable alert stack.

/// <summary>
/// Creates the <see cref="AlertSignal"/> a rule breach emits. Rule-driven
/// emissions are already latched by the rule's IsActive flag (a rule fires at
/// most once until it resets), so signals carry no suppression window; the
/// source event key makes retried emissions of the same breach idempotent.
/// </summary>
public static class RuleAlertSignals
{
    public static AlertSignal Create(
        string source,
        string serialId,
        DateTime alertTime,
        AlertType alertType,
        string field,
        double level,
        double limit,
        int averagingPeriod,
        string message,
        AlertDeliveryChannels deliveryChannels)
    {
        DateTime eventTime = DateTimeUtil.AsUtc(alertTime);
        string sourceEventKey = string.Create(
            CultureInfo.InvariantCulture,
            $"{serialId}:{field}:{averagingPeriod}:{alertType}:{eventTime:O}");

        return new AlertSignal(
            source,
            sourceEventKey,
            eventTime,
            serialId,
            alertType,
            field,
            level,
            limit,
            averagingPeriod,
            message,
            deliveryChannels,
            SuppressionWindow: TimeSpan.Zero);
    }
}
