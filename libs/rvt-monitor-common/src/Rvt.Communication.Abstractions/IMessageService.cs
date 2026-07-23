using Rvt.Monitor.Common.Notifications;

namespace Rvt.Communication.Abstractions;

public interface IMessageService
{
    Task SendMessageAsync(LegacyMessageKind message, LegacyMessageChannel messsageType, RvtContactDto contact, string MonitorName, string url = "", CancellationToken cancellationToken = default);

    void Sendmessage(LegacyMessageKind message, LegacyMessageChannel messsageType, RvtContactDto contact, string MonitorName, string url = "");
    void SendMessage(LegacyMessageKind message, LegacyMessageChannel messsageType, RvtContactDto contact, string MonitorName, string url = "");
}
