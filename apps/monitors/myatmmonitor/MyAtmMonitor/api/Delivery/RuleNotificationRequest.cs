// The namespace is retained from the shared-kernel folder this file moved out
// of, so its consumers keep compiling; IDE0130 would force a rename ripple.
#pragma warning disable IDE0130
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.Common.Rules;

// Summary: The MyAtm outbox planning contract handed to RuleAlertDeliveryPlanner.
// Major updates:
// - 2026-07-30 Hexagonal convergence M3: moved out of NoiseRuleEvaluator.cs
//   (which never used it) next to its only consumer, the delivery planner.
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
