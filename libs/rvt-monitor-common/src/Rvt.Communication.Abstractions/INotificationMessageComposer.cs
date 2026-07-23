namespace Rvt.Communication.Abstractions;

public interface INotificationMessageComposer
{
    ComposedNotification Compose(NotificationMessageKind kind, NotificationChannel channel, string monitorName, string callbackUrl);
}
