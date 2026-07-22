using Moq;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.CommonTests.Communications;

[TestClass]
public sealed class MessageServiceCompatibilityTests
{
    [DataTestMethod]
    [DataRow(MessageService.MessageContent.MessageEnum.Alert, NotificationMessageKind.Alert)]
    [DataRow(MessageService.MessageContent.MessageEnum.Caution, NotificationMessageKind.Caution)]
    [DataRow(MessageService.MessageContent.MessageEnum.Offline, NotificationMessageKind.Offline)]
    [DataRow(MessageService.MessageContent.MessageEnum.Battery_Caution, NotificationMessageKind.BatteryCaution)]
    [DataRow(MessageService.MessageContent.MessageEnum.Battery_Alert, NotificationMessageKind.BatteryAlert)]
    public async Task SendMessageAsync_Email_MapsLegacyMessageKind(
        MessageService.MessageContent.MessageEnum legacyKind,
        NotificationMessageKind expectedKind)
    {
        using var cancellationSource = new CancellationTokenSource();
        var delivery = new Mock<INotificationDeliveryService>(MockBehavior.Strict);
        delivery.Setup(x => x.SendAsync(
                It.Is<NotificationDeliveryRequest>(request =>
                    request.Kind == expectedKind &&
                    request.Channel == NotificationChannel.Email &&
                    request.Destination == "ops@example.test" &&
                    request.MonitorName == "fleet-1" &&
                    request.CallbackUrl == "https://portal.example/1"),
                cancellationSource.Token))
            .Returns(Task.CompletedTask);
        var service = new MessageService(delivery.Object);

        await service.SendMessageAsync(
            legacyKind,
            MessageService.MessageContent.MessageTypeEnum.Email,
            new RvtContactDto(true, false, "ops@example.test", null, null, null),
            "fleet-1",
            "https://portal.example/1",
            cancellationSource.Token);

        delivery.VerifyAll();
    }

    [TestMethod]
    public async Task SendMessageAsync_Sms_MapsPhoneDestination()
    {
        var delivery = new Mock<INotificationDeliveryService>(MockBehavior.Strict);
        delivery.Setup(x => x.SendAsync(
                It.Is<NotificationDeliveryRequest>(request =>
                    request.Kind == NotificationMessageKind.Alert &&
                    request.Channel == NotificationChannel.Sms &&
                    request.Destination == "+441234567890"),
                CancellationToken.None))
            .Returns(Task.CompletedTask);
        var service = new MessageService(delivery.Object);

        await service.SendMessageAsync(
            MessageService.MessageContent.MessageEnum.Alert,
            MessageService.MessageContent.MessageTypeEnum.SMS,
            new RvtContactDto(false, true, string.Empty, "+441234567890", null, null),
            "fleet-1");

        delivery.VerifyAll();
    }

    [TestMethod]
    public async Task SendMessageAsync_DeliveryFailure_TranslatesToCommsException()
    {
        var delivery = new Mock<INotificationDeliveryService>(MockBehavior.Strict);
        delivery.Setup(x => x.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                CancellationToken.None))
            .ThrowsAsync(new EmailDeliveryException(
                "SendGrid",
                DeliveryFailureKind.Permanent,
                "400"));
        var service = new MessageService(delivery.Object);

        var exception = await Assert.ThrowsExactlyAsync<CommsException>(() =>
            service.SendMessageAsync(
                MessageService.MessageContent.MessageEnum.Alert,
                MessageService.MessageContent.MessageTypeEnum.Email,
                new RvtContactDto(true, false, "ops@example.test", null, null, null),
                "fleet-1"));

        Assert.AreEqual("ops@example.test", exception.Address);
        Assert.AreEqual("SendGrid email delivery failed (Permanent, code 400).", exception.Message);
    }

    [TestMethod]
    public async Task SendMessageAsync_BothChannel_IsRejected()
    {
        var delivery = new Mock<INotificationDeliveryService>(MockBehavior.Strict);
        var service = new MessageService(delivery.Object);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
            service.SendMessageAsync(
                MessageService.MessageContent.MessageEnum.Alert,
                MessageService.MessageContent.MessageTypeEnum.Both,
                new RvtContactDto(true, true, "ops@example.test", "+441234567890", null, null),
                "fleet-1"));

        delivery.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task SendMessageAsync_UnsupportedLegacyMessage_IsRejected()
    {
        var delivery = new Mock<INotificationDeliveryService>(MockBehavior.Strict);
        var service = new MessageService(delivery.Object);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
            service.SendMessageAsync(
                MessageService.MessageContent.MessageEnum.Password_Set,
                MessageService.MessageContent.MessageTypeEnum.Email,
                new RvtContactDto(true, false, "ops@example.test", null, null, null),
                "fleet-1"));

        delivery.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void SendMessage_SynchronousCompatibilityWrapper_WaitsForDelivery()
    {
        var delivered = false;
        var delivery = new Mock<INotificationDeliveryService>(MockBehavior.Strict);
        delivery.Setup(x => x.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                CancellationToken.None))
            .Callback(() => delivered = true)
            .Returns(Task.CompletedTask);
        var service = new MessageService(delivery.Object);

#pragma warning disable CS0618
        service.SendMessage(
            MessageService.MessageContent.MessageEnum.Alert,
            MessageService.MessageContent.MessageTypeEnum.Email,
            new RvtContactDto(true, false, "ops@example.test", null, null, null),
            "fleet-1");
#pragma warning restore CS0618

        Assert.IsTrue(delivered);
    }
}
