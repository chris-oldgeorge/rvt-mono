namespace Rvt.Monitor.Common.Communications;

public enum NotificationMessageKind
{
    Alert,
    Caution,
    Offline,
    BatteryCaution,
    BatteryAlert
}

public enum NotificationChannel
{
    Email,
    Sms
}

public sealed record ComposedNotification(
    string Subject,
    string PlainTextBody,
    string HtmlBody);

public sealed record NotificationDeliveryRequest(
    NotificationMessageKind Kind,
    NotificationChannel Channel,
    string Destination,
    string MonitorName,
    string CallbackUrl);
