using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;

using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;

namespace MyAtm.Api.Db
{
    public interface IMyAtmOperationalCommands
    {
        void HandleException(string message, Exception exception);

        void WriteNotification(NotificationDto dto);

        void WriteNotificationAudit(Guid notificationId, string address, string message);

        void UpdateAlertRule(RvtAlertRuleDto dto);

        void ClearErrorMessages(DateTime before);
    }
}
