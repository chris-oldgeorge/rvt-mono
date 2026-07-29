using MyAtm.Api.Db;
using MyAtm.Api.Rules;
using MyAtm.Model.Dto;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;

namespace MyAtm.Api;

// Builds pure scheduled alert commits; delivery is carried by the durable outbox.
public sealed class MyAtmRuleProcessor
{
    private readonly IMyAtmRuleQueries ruleQueries;
    private readonly MyAtmAlertTransitionEvaluator transitionEvaluator = new();
    private readonly RuleAlertDeliveryPlanner deliveryPlanner;

    public MyAtmRuleProcessor(
        IMyAtmRuleQueries ruleQueries,
        RuleAlertDeliveryPlanner? deliveryPlanner = null)
    {
        this.ruleQueries = ruleQueries;
        this.deliveryPlanner = deliveryPlanner ?? new RuleAlertDeliveryPlanner();
    }

    public MyAtmAlertCommit CreateAggregateCommit(
        DustMonitorDto monitor,
        RvtAlertRuleDto rule,
        double? level,
        DateTime end,
        bool alertForFieldIsActive,
        DateTime utcNow)
    {
        DustDto sample = AggregateSample(monitor.SerialId, end, rule.Field, level);
        MyAtmAlertTransition transition = transitionEvaluator.Evaluate(rule, rule.IsActive, sample, alertForFieldIsActive);
        RuleStateMutation[] mutations =
        [
            new RuleStateMutation(rule.RuleId, rule.IsActive, rule.Accessed, transition.IsActive, end)
        ];
        MyAtmAlertOccurrenceInput[] occurrences = transition.Activated
            ? [CreateOccurrence(monitor, rule, transition.Level!.Value, end, rule.AlertType, includeMqtt: true, utcNow)]
            : Array.Empty<MyAtmAlertOccurrenceInput>();
        return new MyAtmAlertCommit(mutations, null, occurrences, utcNow);
    }

    public MyAtmAlertCommit CreateOfflineCommit(
        DustMonitorDto monitor,
        RvtAlertRuleDto rule,
        double secondsOffline,
        DateTime lastDataTime,
        DateTime utcNow)
    {
        string occurrenceKey = $"{monitor.Id:N}:offline:{DateTimeUtil.AsUtc(lastDataTime):O}";
        MyAtmAlertOccurrenceInput occurrence = CreateOccurrence(
            monitor,
            rule,
            secondsOffline,
            utcNow,
            AlertType.Offline,
            includeMqtt: false,
            utcNow,
            occurrenceKey);
        return new MyAtmAlertCommit(
            Array.Empty<RuleStateMutation>(),
            new MyAtmMonitorStateMutation(monitor.Id, ExpectedOffline: false, Offline: true),
            [occurrence],
            utcNow);
    }

    public MyAtmAlertCommit CreateOnlineRecoveryCommit(DustMonitorDto monitor, DateTime utcNow) =>
        new(
            Array.Empty<RuleStateMutation>(),
            new MyAtmMonitorStateMutation(monitor.Id, ExpectedOffline: true, Offline: false),
            Array.Empty<MyAtmAlertOccurrenceInput>(),
            utcNow);

    public MyAtmAlertCommit CreateDeletedRuleDeactivationCommit(RvtAlertRuleDto rule, DateTime utcNow) =>
        new(
            [new RuleStateMutation(rule.RuleId, rule.IsActive, rule.Accessed, false, rule.Accessed)],
            null,
            Array.Empty<MyAtmAlertOccurrenceInput>(),
            utcNow);

    private MyAtmAlertOccurrenceInput CreateOccurrence(
        DustMonitorDto monitor,
        RvtAlertRuleDto rule,
        double level,
        DateTime triggeredAt,
        AlertType alertType,
        bool includeMqtt,
        DateTime createdAt,
        string? occurrenceKey = null)
    {
        string key = occurrenceKey ?? $"{monitor.Id:N}:{rule.RuleId:N}:{DateTimeUtil.AsUtc(triggeredAt):O}:{alertType}";
        string normalizedField = MyAtmAlertTransitionEvaluator.NormalizeField(rule.Field);
        RuleAlertDeliveryPlan deliveryPlan = deliveryPlanner.Plan(
            new RuleNotificationRequest(
                monitor.FleetNr ?? string.Empty,
                monitor.SerialId,
                DateTimeUtil.AsUtc(triggeredAt),
                rule.LimitOn,
                rule.AveragingPeriod,
                level,
                alertType,
                normalizedField,
                monitor.Id),
            ruleQueries.ReadAlertContacts(monitor.Id) ?? [],
            MonitorDeliveryProducers.MyAtm,
            monitor.CustomerId,
            key,
            DateTimeUtil.AsUtc(createdAt));
        if (!includeMqtt)
        {
            deliveryPlan = deliveryPlan with
            {
                Deliveries = [.. deliveryPlan.Deliveries.Where(delivery => delivery.Kind != MonitorDeliveryKind.MqttAlert)]
            };
        }

        return new MyAtmAlertOccurrenceInput(
            key,
            monitor.Id,
            rule.RuleId,
            ToPeriod(rule.AveragingPeriod),
            alertType,
            rule.Field,
            rule.LimitOn,
            level,
            triggeredAt,
            deliveryPlan);
    }

    private static DustDto AggregateSample(string serialId, DateTime end, string field, double? level) =>
        MyAtmAlertTransitionEvaluator.NormalizeField(field) switch
        {
            "pm1" => new DustDto(serialId, 60, end, level, null, null, null, null, null, null),
            "pm2.5" => new DustDto(serialId, 60, end, null, level, null, null, null, null, null),
            "pm10" => new DustDto(serialId, 60, end, null, null, level, null, null, null, null),
            "pmtotal" => new DustDto(serialId, 60, end, null, null, null, level, null, null, null),
            _ => new DustDto(serialId, 60, end, null, null, null, null, null, null, null)
        };

    private static Period ToPeriod(int seconds) => seconds switch
    {
        60 => Period.Minutes1,
        900 => Period.Minutes15,
        3600 => Period.Hours1,
        28800 => Period.Hours8,
        86400 => Period.Hours24,
        _ => throw new InvalidOperationException($"Unsupported MyATM rule averaging period {seconds}.")
    };

}
