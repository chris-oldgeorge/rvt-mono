using MyAtm.Api;
using Rvt.Monitor.Common.Delivery;

namespace MyAtm.Model.Dto;

public sealed record MyAtmAlertCommit(
    IReadOnlyList<RuleStateMutation> RuleStateMutations,
    MyAtmMonitorStateMutation? MonitorStateMutation,
    IReadOnlyList<MyAtmAlertOccurrenceInput> Occurrences,
    DateTime UtcNow);

public sealed record MyAtmAlertCommitResult(
    bool Applied,
    IReadOnlyList<MonitorDeliveryRequest> OutboxMessages);

public sealed record MyAtmMonitorStateMutation(
    Guid MonitorId,
    bool? ExpectedOffline,
    bool? Offline);

public sealed record MyAtmAlertOccurrenceInput(
    string Key,
    Guid MonitorId,
    Guid RuleId,
    Period Period,
    Rvt.Monitor.Common.Notifications.AlertType AlertType,
    string Field,
    double LimitOn,
    double Level,
    DateTime TriggeredAt,
    RuleAlertDeliveryPlan? DeliveryPlan);
