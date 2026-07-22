using Moq;
using Rvt.Monitor.Common.Communications;

namespace Rvt.Monitor.CommonTests.Communications;

[TestClass]
public sealed class NotificationDeliveryServiceTests
{
    [TestMethod]
    public async Task SendAsync_Email_ComposesAndUsesOnlyEmailPort()
    {
        using var cancellationSource = new CancellationTokenSource();
        var email = new Mock<IEmailDeliveryPort>(MockBehavior.Strict);
        var sms = new Mock<ISmsDeliveryPort>(MockBehavior.Strict);
        var composer = new Mock<INotificationMessageComposer>(MockBehavior.Strict);
        var request = new NotificationDeliveryRequest(
            NotificationMessageKind.Alert,
            NotificationChannel.Email,
            "ops@example.test",
            "fleet-1",
            "https://portal.example/1");
        composer.Setup(x => x.Compose(
                request.Kind,
                request.Channel,
                request.MonitorName,
                request.CallbackUrl))
            .Returns(new ComposedNotification("subject", "plain", "<p>html</p>"));
        email.Setup(x => x.SendAsync(
                It.Is<EmailDeliveryRequest>(message =>
                    message.Recipient == request.Destination &&
                    message.Subject == "subject" &&
                    message.PlainTextBody == "plain" &&
                    message.HtmlBody == "<p>html</p>" &&
                    message.Attachments.Count == 0),
                cancellationSource.Token))
            .Returns(Task.CompletedTask);
        var service = new NotificationDeliveryService(composer.Object, email.Object, sms.Object);

        await service.SendAsync(request, cancellationSource.Token);

        composer.VerifyAll();
        email.VerifyAll();
        sms.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task SendAsync_Sms_ComposesAndUsesOnlySmsPort()
    {
        var email = new Mock<IEmailDeliveryPort>(MockBehavior.Strict);
        var sms = new Mock<ISmsDeliveryPort>(MockBehavior.Strict);
        var composer = new Mock<INotificationMessageComposer>(MockBehavior.Strict);
        var request = new NotificationDeliveryRequest(
            NotificationMessageKind.Caution,
            NotificationChannel.Sms,
            "+441234567890",
            "fleet-1",
            "https://portal.example/1");
        composer.Setup(x => x.Compose(
                request.Kind,
                request.Channel,
                request.MonitorName,
                request.CallbackUrl))
            .Returns(new ComposedNotification("ignored", "sms body", string.Empty));
        sms.Setup(x => x.SendAsync(
                It.Is<SmsDeliveryRequest>(message =>
                    message.Recipient == request.Destination &&
                    message.Content == "sms body"),
                CancellationToken.None))
            .Returns(Task.CompletedTask);
        var service = new NotificationDeliveryService(composer.Object, email.Object, sms.Object);

        await service.SendAsync(request);

        composer.VerifyAll();
        sms.VerifyAll();
        email.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task SendAsync_BlankDestination_RejectsBeforeComposition()
    {
        var email = new Mock<IEmailDeliveryPort>(MockBehavior.Strict);
        var sms = new Mock<ISmsDeliveryPort>(MockBehavior.Strict);
        var composer = new Mock<INotificationMessageComposer>(MockBehavior.Strict);
        var service = new NotificationDeliveryService(composer.Object, email.Object, sms.Object);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.SendAsync(
            new NotificationDeliveryRequest(
                NotificationMessageKind.Alert,
                NotificationChannel.Email,
                " ",
                "fleet-1",
                string.Empty)));

        composer.VerifyNoOtherCalls();
        email.VerifyNoOtherCalls();
        sms.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task SendAsync_RequestedCancellation_Propagates()
    {
        using var cancellationSource = new CancellationTokenSource();
        var email = new Mock<IEmailDeliveryPort>(MockBehavior.Strict);
        var sms = new Mock<ISmsDeliveryPort>(MockBehavior.Strict);
        var composer = new Mock<INotificationMessageComposer>(MockBehavior.Strict);
        var request = new NotificationDeliveryRequest(
            NotificationMessageKind.Alert,
            NotificationChannel.Email,
            "ops@example.test",
            "fleet-1",
            string.Empty);
        composer.Setup(x => x.Compose(
                request.Kind,
                request.Channel,
                request.MonitorName,
                request.CallbackUrl))
            .Returns(new ComposedNotification("subject", string.Empty, "<p>html</p>"));
        email.Setup(x => x.SendAsync(It.IsAny<EmailDeliveryRequest>(), cancellationSource.Token))
            .ThrowsAsync(new OperationCanceledException(cancellationSource.Token));
        var service = new NotificationDeliveryService(composer.Object, email.Object, sms.Object);

        var exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => service.SendAsync(request, cancellationSource.Token));

        Assert.AreEqual(cancellationSource.Token, exception.CancellationToken);
    }
}
