using System.Diagnostics.CodeAnalysis;
using Rvt.Monitor.Common.Rules;

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
}
