using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Communication;

public sealed class MessageService(INotificationDeliveryService notificationDelivery) : IMessageService
{
    [Obsolete("Use SendMessageAsync. Synchronous delivery remains only for legacy callers.")]
    public void Sendmessage(
        LegacyMessageKind message,
        LegacyMessageChannel messsageType,
        RvtContactDto contact,
        string MonitorName,
        string url = "") => SendMessage(message, messsageType, contact, MonitorName, url);

    [Obsolete("Use SendMessageAsync. Synchronous delivery remains only for legacy callers.")]
    public void SendMessage(
        LegacyMessageKind message,
        LegacyMessageChannel messsageType,
        RvtContactDto contact,
        string MonitorName,
        string url = "") => SendMessageAsync(
            message,
            messsageType,
            contact,
            MonitorName,
            url).GetAwaiter().GetResult();

    public async Task SendMessageAsync(
        LegacyMessageKind message,
        LegacyMessageChannel messsageType,
        RvtContactDto contact,
        string MonitorName,
        string url = "",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contact);
        NotificationChannel channel = ToChannel(messsageType);
        var destination = channel == NotificationChannel.Email
            ? contact.EmailAddress
            : contact.PhoneNumber;

        // A contact opted into a channel it has no address for is a data
        // condition, not a programming error. Report it through the same
        // CommsException contract callers already handle so the failure is
        // audited against this contact and the remaining contacts still run.
        if (string.IsNullOrWhiteSpace(destination))
        {
            throw CommsException.Of(
                string.Empty,
                $"Contact has no {(channel == NotificationChannel.Email ? "email address" : "phone number")} for {channel} delivery.");
        }

        try
        {
            await notificationDelivery.SendAsync(
                new NotificationDeliveryRequest(
                    ToMessageKind(message),
                    channel,
                    destination,
                    MonitorName,
                    url),
                cancellationToken).ConfigureAwait(false);
        }
        catch (DeliveryException exception)
        {
            throw CommsException.Of(destination, exception.Message);
        }
    }

    private static NotificationMessageKind ToMessageKind(LegacyMessageKind message) => message switch
    {
        LegacyMessageKind.Alert => NotificationMessageKind.Alert,
        LegacyMessageKind.Caution => NotificationMessageKind.Caution,
        LegacyMessageKind.Offline => NotificationMessageKind.Offline,
        LegacyMessageKind.Battery_Caution => NotificationMessageKind.BatteryCaution,
        LegacyMessageKind.Battery_Alert => NotificationMessageKind.BatteryAlert,
        _ => throw new ArgumentOutOfRangeException(nameof(message), message, "Unsupported legacy message.")
    };

    private static NotificationChannel ToChannel(LegacyMessageChannel messageType) => messageType switch
    {
        LegacyMessageChannel.Email => NotificationChannel.Email,
        LegacyMessageChannel.SMS => NotificationChannel.Sms,
        _ => throw new ArgumentOutOfRangeException(nameof(messageType), messageType, "Unsupported delivery channel.")
    };

    public sealed class MessageContent
    {
        public enum MonitorMessageTypeEnum
        {
            Dust = 0,
            Noise = 1,
            Vibration = 2,
            Other = 3,
            All = 4
        }

        public LegacyMessageKind Message { get; set; }

        public LegacyMessageChannel MessageType { get; set; }

        public MonitorMessageTypeEnum MonitorType { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Subject { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }
}
