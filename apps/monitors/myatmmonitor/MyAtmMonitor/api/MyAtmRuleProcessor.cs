using MyAtm.Api.Db;
using MyAtm.Api.Rules;
using MyAtm.Model.Dto;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;

namespace MyAtm.Api;

// Keeps legacy synchronous notification APIs for unsupported callers and builds pure scheduled commits.
public sealed class MyAtmRuleProcessor
{
    private readonly IMyAtmRuleQueries ruleQueries;
    private readonly MyAtmAlertTransitionEvaluator transitionEvaluator = new();
    private readonly RuleAlertDeliveryPlanner deliveryPlanner;
    private readonly string portalBaseUrl;
    private readonly IMyAtmOperationalCommands? legacyOperationalCommands;
    private readonly IMessageService? legacyMessageService;
    private readonly IMonitorEventPublisher? legacyEventPublisher;

    public MyAtmRuleProcessor(
        IMyAtmRuleQueries ruleQueries,
        string portalBaseUrl,
        RuleAlertDeliveryPlanner? deliveryPlanner = null)
    {
        this.ruleQueries = ruleQueries;
        this.portalBaseUrl = portalBaseUrl;
        this.deliveryPlanner = deliveryPlanner ?? new RuleAlertDeliveryPlanner();
    }

    // Compatibility constructor for older in-process callers. Scheduled paths use the narrow constructor.
    public MyAtmRuleProcessor(
        IMyAtmRuleQueries ruleQueries,
        IMyAtmOperationalCommands operationalCommands,
        IMessageService messageService,
        IMonitorEventPublisher eventPublisher,
        string portalBaseUrl)
        : this(ruleQueries, portalBaseUrl)
    {
        legacyOperationalCommands = operationalCommands;
        legacyMessageService = messageService;
        legacyEventPublisher = eventPublisher;
    }

    public MyAtmAlertCommit CreateAggregateCommit(
        DustMonitorDto monitor,
        RvtAlertRuleDto rule,
        double? level,
        DateTime end,
        bool alertForFieldIsActive,
        DateTime utcNow)
    {
        var sample = AggregateSample(monitor.SerialId, end, rule.Field, level);
        var transition = transitionEvaluator.Evaluate(rule, rule.IsActive, sample, alertForFieldIsActive);
        var mutations = new[]
        {
            new RuleStateMutation(rule.RuleId, rule.IsActive, rule.Accessed, transition.IsActive, end)
        };
        var occurrences = transition.Activated
            ? new[] { CreateOccurrence(monitor, rule, transition.Level!.Value, end, rule.AlertType, includeMqtt: true, utcNow) }
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
        var occurrenceKey = $"{monitor.Id:N}:offline:{DateTimeUtil.AsUtc(lastDataTime):O}";
        var occurrence = CreateOccurrence(
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
            new[] { occurrence },
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
            legacyEventPublisher!.PublishAlertAsync(
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
        var notification = new NotificationDto(ruleDto, level, alertTime, monitor.Id);
        legacyOperationalCommands!.WriteNotification(notification);
        foreach (var contact in (ruleQueries.ReadAlertContacts(monitor.Id) ?? []).Where(contact => contact.ShouldSendAtTime(alertTime)))
        {
            if (contact.Email && !string.IsNullOrWhiteSpace(contact.EmailAddress))
            {
                legacyMessageService!.SendMessage(ToMessage(ruleDto.AlertType), MessageService.MessageContent.MessageTypeEnum.Email,
                    contact.ToNotificationDto(), monitor.FleetNr ?? string.Empty, NotificationUrl(notification.Id, ruleDto.AlertType));
                legacyOperationalCommands.WriteNotificationAudit(notification.Id, contact.EmailAddress, NotificationConstants.SENT_OK);
            }
            if (contact.SMS && !string.IsNullOrWhiteSpace(contact.PhoneNumber))
            {
                legacyMessageService!.SendMessage(ToMessage(ruleDto.AlertType), MessageService.MessageContent.MessageTypeEnum.SMS,
                    contact.ToNotificationDto(), monitor.FleetNr ?? string.Empty, NotificationUrl(notification.Id, ruleDto.AlertType));
                legacyOperationalCommands.WriteNotificationAudit(notification.Id, contact.PhoneNumber, NotificationConstants.SENT_OK);
            }
        }
    }

    // Compatibility API retained for callers that have not moved to a durable import/alert commit.
    public void ProcessRulesV2(DustMonitorDto monitorDto, List<RvtAlertRuleDto> allRules, DateTime end, List<DustDto> dtos)
    {
        RequireLegacyDependencies();
        foreach (var dust in dtos)
        {
            var previousAlert = new List<string>();
            foreach (var rule in allRules.OrderBy(rule => rule.AlertType))
            {
                if (rule.IsDeleted)
                {
                    if (rule.IsActive)
                    {
                        rule.IsActive = false;
                        legacyOperationalCommands!.UpdateAlertRule(rule);
                    }
                    continue;
                }

                var transition = transitionEvaluator.Evaluate(
                    rule,
                    rule.IsActive,
                    dust,
                    rule.AlertType == AlertType.Caution && previousAlert.Contains(rule.Field));
                if (!transition.Level.HasValue)
                {
                    continue;
                }

                var previousState = rule.IsActive;
                ProcessRule(monitorDto, rule, transition.Level.Value, dust.SampleTime, DateTime.UtcNow, previousAlert);
                if (previousState != rule.IsActive)
                {
                    legacyOperationalCommands!.UpdateAlertRule(rule);
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
        var key = occurrenceKey ?? $"{monitor.Id:N}:{rule.RuleId:N}:{DateTimeUtil.AsUtc(triggeredAt):O}:{alertType}";
        var normalizedField = MyAtmAlertTransitionEvaluator.NormalizeField(rule.Field);
        var deliveryPlan = deliveryPlanner.Plan(
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
                Deliveries = deliveryPlan.Deliveries
                    .Where(delivery => delivery.Kind != MonitorDeliveryKind.MqttAlert)
                    .ToList()
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
        alertType is AlertType.Alert or AlertType.Caution ? $"{portalBaseUrl}Notification/View/{notificationId}" : string.Empty;

    private static MessageService.MessageContent.MessageEnum ToMessage(AlertType alertType) => alertType switch
    {
        AlertType.Alert => MessageService.MessageContent.MessageEnum.Alert,
        AlertType.Caution => MessageService.MessageContent.MessageEnum.Caution,
        _ => MessageService.MessageContent.MessageEnum.Offline
    };

    private void RequireLegacyDependencies()
    {
        if (legacyOperationalCommands == null || legacyMessageService == null || legacyEventPublisher == null)
        {
            throw new InvalidOperationException("Legacy direct notification processing is not configured for scheduled MyATM jobs.");
        }
    }
}
