using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;

using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;

namespace Omnidots.Api.Db;

public interface IOmnidotsOperationalCommands
{
    void HandleException(string message, Exception exception);

    void WriteNotification(NotificationDto dto);

    void WriteNotificationAudit(Guid notificationId, string address, string message);

    void UpdateAlertRule(RvtAlertRuleDto dto);

    void ClearErrorMessages(DateTime before);
}
