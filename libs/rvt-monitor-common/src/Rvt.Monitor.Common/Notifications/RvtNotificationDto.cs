using System.Diagnostics.CodeAnalysis;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;

namespace Rvt.Monitor.Common.Notifications;

[method: SuppressMessage(
    "Maintainability",
    "S107:Methods should not have too many parameters",
    Justification = "The primary constructor is the explicit materialization contract for the immutable notification DTO.")]
public class NotificationDto(Guid id, DateTime notificationTime, double limitOn, int averagingPeriod, double level,
                       DateTime? closedTime, Guid? closedByUser, AlertType alertType, string alertField, Guid monitorId)
{
    public Guid Id { get; } = id;
    public DateTime NotificationTime { get; } = notificationTime;
    public double LimitOn { get; } = limitOn;
    public int AveragingPeriod { get; } = averagingPeriod;
    public double Level { get; } = level;
    public Guid? ClosedByUser { get; } = closedByUser;
    public DateTime? ClosedTime { get; } = closedTime;
    public AlertType AlertType { get; } = alertType;
    public string AlertField { get; } = alertField;
    public Guid MonitorId { get; } = monitorId;
    public string? ApiMessage { get; set; }

    public NotificationDto(RvtAlertRuleDto ruleDto, double level, DateTime notificationTime, Guid monitorId)
        : this(id: Guid.NewGuid(),
               notificationTime: notificationTime,
               limitOn: ruleDto.LimitOn,
               averagingPeriod: ruleDto.AveragingPeriod,
               level: level,
               closedTime: null,
               closedByUser: null,
               alertType: ruleDto.AlertType,
               alertField: ruleDto.Field,
               monitorId: monitorId)
    {
    }

    /// <summary>
    /// The monitor-specific notification wording. Defaults to the running
    /// monitor's policy, and can be set explicitly so the message can be
    /// rendered for a chosen monitor without global state.
    /// </summary>
    public MonitorRulePolicy Policy { get; init; } = RvtConfig.RulePolicy;

    public string GetMessage()
    {
        string notificationUrl = "";
        if (AlertType == AlertType.Alert || AlertType == AlertType.Caution)
        {
            notificationUrl = $"{RvtConfig.PORTAL_BASE_URL}Notification/View/{Id}";
        }

        if (Policy.NotificationStyle == MonitorNotificationStyle.Vibration)
        {
            return string.Format(@"Alert NotificationTime={0} AlertField={1} Level={2} LimitOn={3} AlertType={4}
                                   AveragingPeriod={5} ClosedDate={6} ClosedByUser={7}
                                   {8}",
                DateTimeUtil.FormatString(DateTimeUtil.UtcToLocal(NotificationTime)), AlertField, Level, LimitOn, AlertType,
                AveragingPeriod, DateTimeUtil.UtcToLocal(ClosedTime), ClosedByUser, notificationUrl);
        }

        string prefix = Policy.NotificationStyle == MonitorNotificationStyle.Noise
            ? AveragingPeriod == 0 ? "Noise site average" : "Noise notification"
            : "Alert";

        return string.Format(@"{0} NotificationTime={1} LimitOn={2} AlertField={3} AveragingPeriod={4}
                                   Level={5} ClosedDate={6} ClosedByUser={7} AlertType={8}
                                   {9}",
                        prefix, DateTimeUtil.UtcToLocal(NotificationTime), LimitOn, AlertField, AveragingPeriod,
                        Level, DateTimeUtil.UtcToLocal(ClosedTime), ClosedByUser, AlertType, notificationUrl);
    }
}
