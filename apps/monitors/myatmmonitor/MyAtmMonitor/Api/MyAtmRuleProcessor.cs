using MyAtm.Api.Db;
using MyAtm.Api.Rules;
using MyAtm.Model.Dto;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;

namespace MyAtm.Api;

// Keeps legacy synchronous notification APIs for unsupported callers and builds pure scheduled commits.
public sealed class MyAtmRuleProcessor(
    IMyAtmRuleQueries ruleQueries,
    string portalBaseUrl,
    RuleAlertDeliveryPlanner? deliveryPlanner = null)
{
    private readonly IMyAtmRuleQueries _ruleQueries = ruleQueries;
    private readonly MyAtmAlertTransitionEvaluator _transitionEvaluator = new();
    private readonly RuleAlertDeliveryPlanner _deliveryPlanner = deliveryPlanner ?? new RuleAlertDeliveryPlanner();
    private readonly string _portalBaseUrl = portalBaseUrl;
    private readonly IMyAtmOperationalCommands? _legacyOperationalCommands;
    private readonly IMessageService? _legacyMessageService;
    private readonly IMonitorEventPublisher? _legacyEventPublisher;

    // Compatibility constructor for older in-process callers. Scheduled paths use the narrow constructor.
    public MyAtmRuleProcessor(
        IMyAtmRuleQueries ruleQueries,
        IMyAtmOperationalCommands operationalCommands,
        IMessageService messageService,
        IMonitorEventPublisher eventPublisher,
        string portalBaseUrl)
        : this(ruleQueries, portalBaseUrl)
    {
        _legacyOperationalCommands = operationalCommands;
        _legacyMessageService = messageService;
        _legacyEventPublisher = eventPublisher;
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
        MyAtmAlertTransition transition = _transitionEvaluator.Evaluate(rule, rule.IsActive, sample, alertForFieldIsActive);
        RuleStateMutation[] mutations =
        [
            new RuleStateMutation(rule.RuleId, rule.IsActive, rule.Accessed, transition.IsActive, end)
        ];
        MyAtmAlertOccurrenceInput[] occurrences = transition.Activated
            ? [CreateOccurrence(monitor, rule, transition.Level!.Value, end, rule.AlertType, includeMqtt: true, utcNow)]
            : [];
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
            [],
            new MyAtmMonitorStateMutation(monitor.Id, ExpectedOffline: false, Offline: true),
            [occurrence],
            utcNow);
    }

    public MyAtmAlertCommit CreateOnlineRecoveryCommit(DustMonitorDto monitor, DateTime utcNow) =>
        new(
            [],
            new MyAtmMonitorStateMutation(monitor.Id, ExpectedOffline: true, Offline: false),
            [],
            utcNow);

    public MyAtmAlertCommit CreateDeletedRuleDeactivationCommit(RvtAlertRuleDto rule, DateTime utcNow) =>
        new(
            [new RuleStateMutation(rule.RuleId, rule.IsActive, rule.Accessed, false, rule.Accessed)],
            null,
            [],
            utcNow);

    // Compatibility API: no scheduled handler calls this direct-delivery route.
    public void ProcessRule(DustMonitorDto monitorDto, RvtAlertRuleDto rule, double level, DateTime end, DateTime utcNow, List<string> previousAlert)
    {
        RequireLegacyDependencies();
        if (level >= rule.LimitOn && !rule.IsActive &&
            (rule.AlertType == AlertType.Alert || !previousAlert.Contains(rule.Field)))
        {
            if (rule.AlertType == AlertType.Alert)
            {
                previousAlert.Add(rule.Field);
            }

            rule.IsActive = true;
            rule.Accessed = utcNow;
            ProcessAlertForContacts(rule, level, end, monitorDto);
            _legacyEventPublisher!.PublishAlertAsync(
                end,
                monitorDto.SerialId,
                $"Dust Alert {rule.Field} level={level} exceeds limitOn/Off={rule.LimitOn}/{rule.LimitOff}",
                monitorDto.CustomerId).GetAwaiter().GetResult();
        }
        else if (level <= rule.LimitOff)
        {
            rule.IsActive = false;
        }
    }

    // Compatibility API: no scheduled handler calls this direct-delivery route.
    public void ProcessAlertForContacts(RvtAlertRuleDto ruleDto, double level, DateTime alertTime, DustMonitorDto monitor)
    {
        RequireLegacyDependencies();
        NotificationDto notification = new(ruleDto, level, alertTime, monitor.Id);
        _legacyOperationalCommands!.WriteNotification(notification);
        foreach (Rvt.Monitor.Common.Rules.RvtContactDto? contact in (_ruleQueries.ReadAlertContacts(monitor.Id) ?? []).Where(contact => contact.ShouldSendAtTime(alertTime)))
        {
            if (contact.Email && !string.IsNullOrWhiteSpace(contact.EmailAddress))
            {
                _legacyMessageService!.SendMessage(ToMessage(ruleDto.AlertType), LegacyMessageChannel.Email,
                    contact.ToNotificationDto(), monitor.FleetNr ?? string.Empty, NotificationUrl(notification.Id, ruleDto.AlertType));
                _legacyOperationalCommands.WriteNotificationAudit(notification.Id, contact.EmailAddress, NotificationConstants.SENT_OK);
            }
            if (contact.SMS && !string.IsNullOrWhiteSpace(contact.PhoneNumber))
            {
                _legacyMessageService!.SendMessage(ToMessage(ruleDto.AlertType), LegacyMessageChannel.SMS,
                    contact.ToNotificationDto(), monitor.FleetNr ?? string.Empty, NotificationUrl(notification.Id, ruleDto.AlertType));
                _legacyOperationalCommands.WriteNotificationAudit(notification.Id, contact.PhoneNumber, NotificationConstants.SENT_OK);
            }
        }
    }

    // Compatibility API retained for callers that have not moved to a durable import/alert commit.
    public void ProcessRulesV2(DustMonitorDto monitorDto, List<RvtAlertRuleDto> allRules, List<DustDto> dtos)
    {
        RequireLegacyDependencies();
        foreach (DustDto dust in dtos)
        {
            List<string> previousAlert = [];
            foreach (RvtAlertRuleDto? rule in allRules.OrderBy(rule => rule.AlertType))
            {
                if (rule.IsDeleted)
                {
                    if (rule.IsActive)
                    {
                        rule.IsActive = false;
                        _legacyOperationalCommands!.UpdateAlertRule(rule);
                    }
                    continue;
                }

                MyAtmAlertTransition transition = _transitionEvaluator.Evaluate(
                    rule,
                    rule.IsActive,
                    dust,
                    rule.AlertType == AlertType.Caution && previousAlert.Contains(rule.Field));
                if (!transition.Level.HasValue)
                {
                    continue;
                }

                bool previousState = rule.IsActive;
                ProcessRule(monitorDto, rule, transition.Level.Value, dust.SampleTime, DateTime.UtcNow, previousAlert);
                if (previousState != rule.IsActive)
                {
                    _legacyOperationalCommands!.UpdateAlertRule(rule);
                }
            }
        }
    }

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
        RuleAlertDeliveryPlan deliveryPlan = _deliveryPlanner.Plan(
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
            _ruleQueries.ReadAlertContacts(monitor.Id) ?? [],
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

    private string NotificationUrl(Guid notificationId, AlertType alertType) =>
        alertType is AlertType.Alert or AlertType.Caution ? $"{_portalBaseUrl}Notification/View/{notificationId}" : string.Empty;

    private static LegacyMessageKind ToMessage(AlertType alertType) => alertType switch
    {
        AlertType.Alert => LegacyMessageKind.Alert,
        AlertType.Caution => LegacyMessageKind.Caution,
        _ => LegacyMessageKind.Offline
    };

    private void RequireLegacyDependencies()
    {
        if (_legacyOperationalCommands == null || _legacyMessageService == null || _legacyEventPublisher == null)
        {
            throw new InvalidOperationException("Legacy direct notification processing is not configured for scheduled MyATM jobs.");
        }
    }
}
