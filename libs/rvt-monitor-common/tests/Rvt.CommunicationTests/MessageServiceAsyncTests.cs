using Moq;
using Rvt.Communication;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.CommunicationTests;

[TestClass]
public sealed class MessageServiceAsyncTests
{
    [TestMethod]
    public async Task SendMessageAsync_PassesTheCallerCancellationTokenToEmailDelivery()
    {
        using CancellationTokenSource cancellationSource = new CancellationTokenSource();
        Mock<INotificationDeliveryService> delivery = new Mock<INotificationDeliveryService>(MockBehavior.Strict);
        delivery.Setup(x => x.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                cancellationSource.Token))
            .Returns(Task.CompletedTask);
        MessageService service = new MessageService(delivery.Object);

        await service.SendMessageAsync(
            LegacyMessageKind.Alert,
            LegacyMessageChannel.Email,
            new RvtContactDto(true, false, "alerts@example.test", null, null, null),
            "fleet-1",
            cancellationToken: cancellationSource.Token);

        delivery.VerifyAll();
    }

    [TestMethod]
    public async Task SendMessageAsync_PassesTheCallerCancellationTokenToSmsDelivery()
    {
        using CancellationTokenSource cancellationSource = new CancellationTokenSource();
        Mock<INotificationDeliveryService> delivery = new Mock<INotificationDeliveryService>(MockBehavior.Strict);
        delivery.Setup(x => x.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                cancellationSource.Token))
            .Returns(Task.CompletedTask);
        MessageService service = new MessageService(delivery.Object);

        await service.SendMessageAsync(
            LegacyMessageKind.Alert,
            LegacyMessageChannel.SMS,
            new RvtContactDto(false, true, string.Empty, "447700900000", null, null),
            "fleet-1",
            cancellationToken: cancellationSource.Token);

        delivery.VerifyAll();
    }

    [TestMethod]
    public async Task SendMessageAsync_RequestedCancellationIsNotTranslated()
    {
        using CancellationTokenSource cancellationSource = new CancellationTokenSource();
        Mock<INotificationDeliveryService> delivery = new Mock<INotificationDeliveryService>(MockBehavior.Strict);
        delivery.Setup(x => x.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                cancellationSource.Token))
            .ThrowsAsync(new OperationCanceledException(cancellationSource.Token));
        MessageService service = new MessageService(delivery.Object);

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            service.SendMessageAsync(
                LegacyMessageKind.Alert,
                LegacyMessageChannel.Email,
                new RvtContactDto(true, false, "alerts@example.test", null, null, null),
                "fleet-1",
                cancellationToken: cancellationSource.Token));

        Assert.AreEqual(cancellationSource.Token, exception.CancellationToken);
    }
}
