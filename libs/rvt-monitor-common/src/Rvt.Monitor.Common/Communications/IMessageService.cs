using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.Common.Communications
{
    public interface IMessageService
    {
        Task SendMessageAsync(
            MessageService.MessageContent.MessageEnum message,
            MessageService.MessageContent.MessageTypeEnum messsageType,
            RvtContactDto contact,
            string MonitorName,
            string url = "",
            CancellationToken cancellationToken = default);

        // Compatibility members for monitors that have not yet migrated to awaited delivery.
        void Sendmessage(MessageService.MessageContent.MessageEnum message, MessageService.MessageContent.MessageTypeEnum messsageType, RvtContactDto contact, string MonitorName, string url = "");
        void SendMessage(MessageService.MessageContent.MessageEnum message, MessageService.MessageContent.MessageTypeEnum messsageType, RvtContactDto contact, string MonitorName, string url = "");
    }
}
