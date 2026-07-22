using Moq;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.CommonTests.Communications;

[TestClass]
public sealed class MessageServiceAsyncTests
{
    [TestMethod]
    public async Task SendMessageAsync_PassesTheCallerCancellationTokenToEmailDelivery()
    {
        using var cancellationSource = new CancellationTokenSource();
        var delivery = new Mock<INotificationDeliveryService>(MockBehavior.Strict);
        delivery.Setup(x => x.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                cancellationSource.Token))
            .Returns(Task.CompletedTask);
        var service = new MessageService(delivery.Object);

        await service.SendMessageAsync(
            MessageService.MessageContent.MessageEnum.Alert,
            MessageService.MessageContent.MessageTypeEnum.Email,
            new RvtContactDto(true, false, "alerts@example.test", null, null, null),
            "fleet-1",
            cancellationToken: cancellationSource.Token);

        delivery.VerifyAll();
    }

    [TestMethod]
    public async Task SendMessageAsync_PassesTheCallerCancellationTokenToSmsDelivery()
    {
        using var cancellationSource = new CancellationTokenSource();
        var delivery = new Mock<INotificationDeliveryService>(MockBehavior.Strict);
        delivery.Setup(x => x.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                cancellationSource.Token))
            .Returns(Task.CompletedTask);
        var service = new MessageService(delivery.Object);

        await service.SendMessageAsync(
            MessageService.MessageContent.MessageEnum.Alert,
            MessageService.MessageContent.MessageTypeEnum.SMS,
            new RvtContactDto(false, true, string.Empty, "447700900000", null, null),
            "fleet-1",
            cancellationToken: cancellationSource.Token);

        delivery.VerifyAll();
    }

    [TestMethod]
    public async Task SendMessageAsync_RequestedCancellationIsNotTranslated()
    {
        using var cancellationSource = new CancellationTokenSource();
        var delivery = new Mock<INotificationDeliveryService>(MockBehavior.Strict);
        delivery.Setup(x => x.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                cancellationSource.Token))
            .ThrowsAsync(new OperationCanceledException(cancellationSource.Token));
        var service = new MessageService(delivery.Object);

        var exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            service.SendMessageAsync(
                MessageService.MessageContent.MessageEnum.Alert,
                MessageService.MessageContent.MessageTypeEnum.Email,
                new RvtContactDto(true, false, "alerts@example.test", null, null, null),
                "fleet-1",
                cancellationToken: cancellationSource.Token));

        Assert.AreEqual(cancellationSource.Token, exception.CancellationToken);
    }
}
