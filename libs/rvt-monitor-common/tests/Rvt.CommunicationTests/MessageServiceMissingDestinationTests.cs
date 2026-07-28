using Moq;
using Rvt.Communication;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.CommunicationTests;

/// <summary>
/// A contact that opted into a channel it has no address for is a data
/// condition. It must surface as <see cref="CommsException"/> — the contract
/// callers catch — so the failure is audited against that one contact and the
/// remaining contacts of the notification are still attempted.
/// </summary>
[TestClass]
public sealed class MessageServiceMissingDestinationTests
{
    [TestMethod]
    public async Task SendMessageAsync_WhenSmsContactHasNoPhoneNumber_ThrowsCommsExceptionWithoutCallingDelivery()
    {
        var delivery = new Mock<INotificationDeliveryService>(MockBehavior.Strict);
        var service = new MessageService(delivery.Object);

        var exception = await Assert.ThrowsExactlyAsync<CommsException>(
            () => service.SendMessageAsync(
                LegacyMessageKind.Alert,
                LegacyMessageChannel.SMS,
                new RvtContactDto(false, true, "alerts@example.test", null, null, null),
                "fleet-1"));

        StringAssert.Contains(exception.Message, "phone number");
        delivery.Verify(
            x => x.SendAsync(It.IsAny<NotificationDeliveryRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task SendMessageAsync_WhenSmsContactHasBlankPhoneNumber_ThrowsCommsException()
    {
        var delivery = new Mock<INotificationDeliveryService>(MockBehavior.Strict);
        var service = new MessageService(delivery.Object);

        await Assert.ThrowsExactlyAsync<CommsException>(
            () => service.SendMessageAsync(
                LegacyMessageKind.Alert,
                LegacyMessageChannel.SMS,
                new RvtContactDto(false, true, "alerts@example.test", "   ", null, null),
                "fleet-1"));

        delivery.Verify(
            x => x.SendAsync(It.IsAny<NotificationDeliveryRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task SendMessageAsync_WhenEmailContactHasNoEmailAddress_ThrowsCommsException()
    {
        var delivery = new Mock<INotificationDeliveryService>(MockBehavior.Strict);
        var service = new MessageService(delivery.Object);

        var exception = await Assert.ThrowsExactlyAsync<CommsException>(
            () => service.SendMessageAsync(
                LegacyMessageKind.Alert,
                LegacyMessageChannel.Email,
                new RvtContactDto(true, false, string.Empty, "+15550001111", null, null),
                "fleet-1"));

        StringAssert.Contains(exception.Message, "email address");
        delivery.Verify(
            x => x.SendAsync(It.IsAny<NotificationDeliveryRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public void SendMessage_WhenSmsContactHasNoPhoneNumber_ThrowsCommsExceptionForLegacyCallers()
    {
        // The legacy synchronous entry point is what the AirQ and Svantek rule
        // processors still call; it must raise the same catchable contract.
        var delivery = new Mock<INotificationDeliveryService>(MockBehavior.Strict);
        var service = new MessageService(delivery.Object);

#pragma warning disable CS0618 // Legacy synchronous path retained for existing callers.
        Assert.ThrowsExactly<CommsException>(() => service.Sendmessage(
            LegacyMessageKind.Alert,
            LegacyMessageChannel.SMS,
            new RvtContactDto(false, true, "alerts@example.test", null, null, null),
            "fleet-1"));
#pragma warning restore CS0618
    }
}
