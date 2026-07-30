using Rvt.Monitor.Common.Notifications;

namespace MyAtm.Delivery;

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
