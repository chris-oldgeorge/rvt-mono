using Rvt.Monitor.Common.Notifications;
using static Rvt.Monitor.Common.Communications.MessageService.MessageContent;

namespace Rvt.Monitor.Common.Communications;

public sealed class MessageService(INotificationDeliveryService notificationDelivery) : IMessageService
{
    [Obsolete("Use SendMessageAsync. Synchronous delivery remains only for legacy callers.")]
    public void Sendmessage(
        MessageEnum message,
        MessageTypeEnum messsageType,
        RvtContactDto contact,
        string MonitorName,
        string url = "") => SendMessage(message, messsageType, contact, MonitorName, url);

    [Obsolete("Use SendMessageAsync. Synchronous delivery remains only for legacy callers.")]
    public void SendMessage(
        MessageEnum message,
        MessageTypeEnum messsageType,
        RvtContactDto contact,
        string MonitorName,
        string url = "") => SendMessageAsync(
            message,
            messsageType,
            contact,
            MonitorName,
            url).GetAwaiter().GetResult();

    public async Task SendMessageAsync(
        MessageEnum message,
        MessageTypeEnum messsageType,
        RvtContactDto contact,
        string MonitorName,
        string url = "",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contact);
        var channel = ToChannel(messsageType);
        var destination = channel == NotificationChannel.Email
            ? contact.EmailAddress
            : contact.PhoneNumber;

        try
        {
            await notificationDelivery.SendAsync(
                new NotificationDeliveryRequest(
                    ToMessageKind(message),
                    channel,
                    destination ?? string.Empty,
                    MonitorName,
                    url),
                cancellationToken).ConfigureAwait(false);
        }
        catch (DeliveryException exception)
        {
            throw CommsException.Of(destination ?? string.Empty, exception.Message);
        }
    }

    private static NotificationMessageKind ToMessageKind(MessageEnum message) => message switch
    {
        MessageEnum.Alert => NotificationMessageKind.Alert,
        MessageEnum.Caution => NotificationMessageKind.Caution,
        MessageEnum.Offline => NotificationMessageKind.Offline,
        MessageEnum.Battery_Caution => NotificationMessageKind.BatteryCaution,
        MessageEnum.Battery_Alert => NotificationMessageKind.BatteryAlert,
        _ => throw new ArgumentOutOfRangeException(nameof(message), message, "Unsupported legacy message.")
    };

    private static NotificationChannel ToChannel(MessageTypeEnum messageType) => messageType switch
    {
        MessageTypeEnum.Email => NotificationChannel.Email,
        MessageTypeEnum.SMS => NotificationChannel.Sms,
        _ => throw new ArgumentOutOfRangeException(nameof(messageType), messageType, "Unsupported delivery channel.")
    };

    public sealed class MessageContent
    {
        public enum MessageEnum
        {
            Password_Set,
            Password_Forgotten,
            Alert,
            Caution,
            Offline,
            Battery_Caution,
            Battery_Alert,
            Report_Weekly,
            Report_Monthly
        }

        public enum MonitorMessageTypeEnum
        {
            Dust = 0,
            Noise = 1,
            Vibration = 2,
            Other = 3,
            All = 4
        }

        public enum MessageTypeEnum
        {
            Email = 0,
            SMS = 1,
            Both = 2
        }

        public MessageEnum Message { get; set; }

        public MessageTypeEnum MessageType { get; set; }

        public MonitorMessageTypeEnum MonitorType { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Subject { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }
}
