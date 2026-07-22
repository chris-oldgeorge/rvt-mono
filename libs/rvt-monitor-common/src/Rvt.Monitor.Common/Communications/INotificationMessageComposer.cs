namespace Rvt.Monitor.Common.Communications;

public interface INotificationMessageComposer
{
    ComposedNotification Compose(
        NotificationMessageKind kind,
        NotificationChannel channel,
        string monitorName,
        string callbackUrl);
}
