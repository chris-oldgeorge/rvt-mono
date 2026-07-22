using Rvt.Monitor.Common.Rules;

using NotificationDto = Rvt.Monitor.Common.Rules.NotificationDto;

namespace Svantek.Api.Db;

public interface ISvantekOperationalCommands
{
    void HandleException(string message, Exception exception);

    void WriteNotification(NotificationDto dto);

    void UpdateAlertRule(RvtAlertRuleDto dto);

    void WriteNotificationAudit(Guid notificationId, string address, string message);

    bool WriteSoundFile(Guid notificationId, string fileName);

    Task<bool> WriteSoundFileAsync(
        Guid notificationId,
        string fileName,
        CancellationToken cancellationToken = default);
}
