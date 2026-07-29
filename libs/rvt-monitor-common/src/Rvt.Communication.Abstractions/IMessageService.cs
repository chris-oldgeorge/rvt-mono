using Rvt.Monitor.Common.Notifications;

namespace Rvt.Communication.Abstractions;

/// <summary>
/// The legacy synchronous messaging contract. New code uses
/// <see cref="INotificationDeliveryService"/>; the remaining callers are the
/// AirQ/Svantek rule processors and Omnidots' inline dispatcher, retired by
/// steps 4-5 of the legacy-retirement map. Projects that still consume it
/// carry a NoWarn for RVT0001 naming this paragraph.
/// </summary>
[Obsolete(
    "IMessageService is the legacy synchronous messaging path; use INotificationDeliveryService.",
    DiagnosticId = "RVT0001")]
public interface IMessageService
{
    Task SendMessageAsync(LegacyMessageKind message, LegacyMessageChannel messsageType, RvtContactDto contact, string MonitorName, string url = "", CancellationToken cancellationToken = default);

    void Sendmessage(LegacyMessageKind message, LegacyMessageChannel messsageType, RvtContactDto contact, string MonitorName, string url = "");
    void SendMessage(LegacyMessageKind message, LegacyMessageChannel messsageType, RvtContactDto contact, string MonitorName, string url = "");
}
