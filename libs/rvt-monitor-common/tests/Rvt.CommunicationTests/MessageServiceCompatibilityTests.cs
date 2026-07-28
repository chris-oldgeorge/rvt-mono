using Moq;
using Rvt.Communication;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.CommunicationTests;

[TestClass]
public sealed class MessageServiceCompatibilityTests
{
    [TestMethod]
    [DataRow(LegacyMessageKind.Alert, NotificationMessageKind.Alert)]
    [DataRow(LegacyMessageKind.Caution, NotificationMessageKind.Caution)]
    [DataRow(LegacyMessageKind.Offline, NotificationMessageKind.Offline)]
    [DataRow(LegacyMessageKind.Battery_Caution, NotificationMessageKind.BatteryCaution)]
    [DataRow(LegacyMessageKind.Battery_Alert, NotificationMessageKind.BatteryAlert)]
    public async Task SendMessageAsync_Email_MapsLegacyMessageKind(
        LegacyMessageKind legacyKind,
        NotificationMessageKind expectedKind)
    {
        using CancellationTokenSource cancellationSource = new();
        Mock<INotificationDeliveryService> delivery = new(MockBehavior.Strict);
        delivery.Setup(x => x.SendAsync(
                It.Is<NotificationDeliveryRequest>(request =>
                    request.Kind == expectedKind &&
                    request.Channel == NotificationChannel.Email &&
                    request.Destination == "ops@example.test" &&
                    request.MonitorName == "fleet-1" &&
                    request.CallbackUrl == "https://portal.example/1"),
                cancellationSource.Token))
            .Returns(Task.CompletedTask);
        MessageService service = new(delivery.Object);

        await service.SendMessageAsync(
            legacyKind,
            LegacyMessageChannel.Email,
            new RvtContactDto(true, false, "ops@example.test", null, null, null),
            "fleet-1",
            "https://portal.example/1",
            cancellationSource.Token);

        delivery.VerifyAll();
    }

    [TestMethod]
    public async Task SendMessageAsync_Sms_MapsPhoneDestination()
    {
        Mock<INotificationDeliveryService> delivery = new(MockBehavior.Strict);
        delivery.Setup(x => x.SendAsync(
                It.Is<NotificationDeliveryRequest>(request =>
                    request.Kind == NotificationMessageKind.Alert &&
                    request.Channel == NotificationChannel.Sms &&
                    request.Destination == "+441234567890"),
                CancellationToken.None))
            .Returns(Task.CompletedTask);
        MessageService service = new(delivery.Object);

        await service.SendMessageAsync(
            LegacyMessageKind.Alert,
            LegacyMessageChannel.SMS,
            new RvtContactDto(false, true, string.Empty, "+441234567890", null, null),
            "fleet-1");

        delivery.VerifyAll();
    }

    [TestMethod]
    public async Task SendMessageAsync_DeliveryFailure_TranslatesToCommsException()
    {
        Mock<INotificationDeliveryService> delivery = new(MockBehavior.Strict);
        delivery.Setup(x => x.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                CancellationToken.None))
            .ThrowsAsync(new EmailDeliveryException(
                "SendGrid",
                DeliveryFailureKind.Permanent,
                "400"));
        MessageService service = new(delivery.Object);

        CommsException exception = await Assert.ThrowsExactlyAsync<CommsException>(() =>
            service.SendMessageAsync(
                LegacyMessageKind.Alert,
                LegacyMessageChannel.Email,
                new RvtContactDto(true, false, "ops@example.test", null, null, null),
                "fleet-1"));

        Assert.AreEqual("ops@example.test", exception.Address);
        Assert.AreEqual("SendGrid email delivery failed (Permanent, code 400).", exception.Message);
    }

    [TestMethod]
    public async Task SendMessageAsync_BothChannel_IsRejected()
    {
        Mock<INotificationDeliveryService> delivery = new(MockBehavior.Strict);
        MessageService service = new(delivery.Object);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
            service.SendMessageAsync(
                LegacyMessageKind.Alert,
                LegacyMessageChannel.Both,
                new RvtContactDto(true, true, "ops@example.test", "+441234567890", null, null),
                "fleet-1"));

        delivery.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task SendMessageAsync_UnsupportedLegacyMessage_IsRejected()
    {
        Mock<INotificationDeliveryService> delivery = new(MockBehavior.Strict);
        MessageService service = new(delivery.Object);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
            service.SendMessageAsync(
                LegacyMessageKind.Password_Set,
                LegacyMessageChannel.Email,
                new RvtContactDto(true, false, "ops@example.test", null, null, null),
                "fleet-1"));

        delivery.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void SendMessage_SynchronousCompatibilityWrapper_WaitsForDelivery()
    {
        bool delivered = false;
        Mock<INotificationDeliveryService> delivery = new(MockBehavior.Strict);
        delivery.Setup(x => x.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                CancellationToken.None))
            .Callback(() => delivered = true)
            .Returns(Task.CompletedTask);
        MessageService service = new(delivery.Object);

#pragma warning disable CS0618
        service.SendMessage(
            LegacyMessageKind.Alert,
            LegacyMessageChannel.Email,
            new RvtContactDto(true, false, "ops@example.test", null, null, null),
            "fleet-1");
#pragma warning restore CS0618

        Assert.IsTrue(delivered);
    }
}
