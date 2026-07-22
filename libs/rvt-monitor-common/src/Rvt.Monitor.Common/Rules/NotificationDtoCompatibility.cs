using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.Common.Rules;

public class NotificationDto : Rvt.Monitor.Common.Notifications.NotificationDto
{
    public NotificationDto(
        Guid id,
        DateTime notificationTime,
        double limitOn,
        int averagingPeriod,
        double level,
        DateTime? closedTime,
        Guid? closedByUser,
        AlertType alertType,
        string alertField,
        Guid monitorId)
        : base(id, notificationTime, limitOn, averagingPeriod, level, closedTime, closedByUser, alertType, alertField, monitorId)
    {
    }

    public NotificationDto(RvtAlertRuleDto ruleDto, double level, DateTime notificationTime, Guid monitorId)
        : base(ruleDto, level, notificationTime, monitorId)
    {
    }
}
