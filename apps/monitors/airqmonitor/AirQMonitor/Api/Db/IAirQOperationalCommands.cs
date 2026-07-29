using Rvt.Monitor.Common.Rules;

using NotificationDto = Rvt.Monitor.Common.Rules.NotificationDto;

namespace AirQ.Api.Db;

public interface IAirQOperationalCommands
{
    void HandleException(string message, Exception exception);

    void WriteNotification(NotificationDto dto);

    void WriteNotificationAudit(Guid notificationId, string address, string message);

    void UpdateAlertRule(RvtAlertRuleDto dto);

    void ClearErrorMessages(DateTime before);
}
