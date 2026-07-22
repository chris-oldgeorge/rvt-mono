using System.Text.Json;
using Microsoft.Extensions.Options;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.CommonTests.Alerts;

[TestClass]
public sealed class AlertDeliveryAdapterTests
{
    private static readonly DateTime SentAt = new(2026, 7, 15, 12, 30, 0, DateTimeKind.Utc);

    [TestMethod]
    public async Task MqttAdapter_DeliversVersionOneEnvelopeAndReturnsNoAudit()
    {
        var publisher = new Mock<IMonitorEventPublisher>();
        var envelope = CreateEnvelope();
        var delivery = CreateDelivery("MqttAlert", "alert", envelope);
        var adapter = new MqttAlertDeliveryAdapter(publisher.Object);

        var audit = await adapter.DeliverAsync(delivery, CancellationToken.None);

        Assert.IsNull(audit);
        publisher.Verify(x => x.PublishAlertAsync(
            envelope.Timestamp,
            envelope.SerialId,
            envelope.Message,
            envelope.CustomerId), Times.Once);
    }

    [TestMethod]
    public async Task EmailAdapter_DeliversVersionOneEnvelopeAndReturnsSuccessAudit()
    {
        var notificationDelivery = new Mock<INotificationDeliveryService>();
        var envelope = CreateEnvelope();
        var delivery = CreateDelivery("Email", "ops@example.test", envelope);
        var adapter = new EmailAlertDeliveryAdapter(
            notificationDelivery.Object,
            Options.Create(new DurableAlertOptions { PortalBaseUrl = "https://portal.example/" }),
            CreateTimeProvider());

        var audit = await adapter.DeliverAsync(delivery, CancellationToken.None);

        notificationDelivery.Verify(x => x.SendAsync(
            It.Is<NotificationDeliveryRequest>(request =>
                request.Kind == NotificationMessageKind.Alert &&
                request.Channel == NotificationChannel.Email &&
                request.Destination == "ops@example.test" &&
                request.MonitorName == envelope.FleetNr &&
                request.CallbackUrl == $"https://portal.example/Notification/View/{envelope.NotificationId}"),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.AreEqual(
            new AlertDeliveryAudit(
                envelope.NotificationId,
                delivery.Destination,
                NotificationConstants.SENT_OK,
                SentAt),
            audit);
    }

    [TestMethod]
    public async Task SmsAdapter_DeliversVersionOneEnvelopeAndReturnsSuccessAudit()
    {
        var notificationDelivery = new Mock<INotificationDeliveryService>();
        var envelope = CreateEnvelope() with { AlertType = AlertType.Caution };
        var delivery = CreateDelivery("Sms", "+441234567890", envelope);
        var adapter = new SmsAlertDeliveryAdapter(
            notificationDelivery.Object,
            Options.Create(new DurableAlertOptions { PortalBaseUrl = "https://portal.example" }),
            CreateTimeProvider());

        var audit = await adapter.DeliverAsync(delivery, CancellationToken.None);

        notificationDelivery.Verify(x => x.SendAsync(
            It.Is<NotificationDeliveryRequest>(request =>
                request.Kind == NotificationMessageKind.Caution &&
                request.Channel == NotificationChannel.Sms &&
                request.Destination == "+441234567890" &&
                request.MonitorName == envelope.FleetNr &&
                request.CallbackUrl == $"https://portal.example/Notification/View/{envelope.NotificationId}"),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.AreEqual(delivery.Destination, audit!.Address);
        Assert.AreEqual(NotificationConstants.SENT_OK, audit.Message);
    }

    [DataTestMethod]
    [DataRow("mqtt")]
    [DataRow("email")]
    [DataRow("sms")]
    public async Task Adapters_RejectEnvelopeVersionsOtherThanOneBeforeExternalDelivery(string adapterKind)
    {
        var publisher = new Mock<IMonitorEventPublisher>();
        var notificationDelivery = new Mock<INotificationDeliveryService>();
        var adapter = CreateAdapter(adapterKind, publisher.Object, notificationDelivery.Object);
        var (kind, destination) = DeliveryIdentity(adapterKind);
        foreach (var version in new[] { 0, 2 })
        {
            var delivery = CreateDelivery(kind, destination, CreateEnvelope() with { Version = version });

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => adapter.DeliverAsync(delivery, CancellationToken.None));
        }

        publisher.VerifyNoOtherCalls();
        notificationDelivery.VerifyNoOtherCalls();
    }

    [DataTestMethod]
    [DataRow("mqtt")]
    [DataRow("email")]
    [DataRow("sms")]
    public async Task Adapters_RejectEmptyNotificationIdBeforeExternalDelivery(string adapterKind)
    {
        var publisher = new Mock<IMonitorEventPublisher>();
        var notificationDelivery = new Mock<INotificationDeliveryService>();
        var adapter = CreateAdapter(adapterKind, publisher.Object, notificationDelivery.Object);
        var (kind, destination) = DeliveryIdentity(adapterKind);
        var delivery = CreateDelivery(
            kind,
            destination,
            CreateEnvelope() with { NotificationId = Guid.Empty });

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => adapter.DeliverAsync(delivery, CancellationToken.None));

        publisher.VerifyNoOtherCalls();
        notificationDelivery.VerifyNoOtherCalls();
    }

    [DataTestMethod]
    [DataRow("mqtt")]
    [DataRow("email")]
    [DataRow("sms")]
    public async Task Adapters_RejectEnvelopeNotificationIdThatDiffersFromAuthoritativeClaim(
        string adapterKind)
    {
        var publisher = new Mock<IMonitorEventPublisher>();
        var notificationDelivery = new Mock<INotificationDeliveryService>();
        var adapter = CreateAdapter(adapterKind, publisher.Object, notificationDelivery.Object);
        var (kind, destination) = DeliveryIdentity(adapterKind);
        var delivery = CreateDelivery(kind, destination, CreateEnvelope()) with
        {
            NotificationId = Guid.NewGuid()
        };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => adapter.DeliverAsync(delivery, CancellationToken.None));

        publisher.VerifyNoOtherCalls();
        notificationDelivery.VerifyNoOtherCalls();
    }

    [DataTestMethod]
    [DataRow("mqtt", "Email", "alert")]
    [DataRow("email", "Sms", "ops@example.test")]
    [DataRow("sms", "Email", "+441234567890")]
    public async Task Adapters_RejectMismatchedKindBeforeExternalDelivery(
        string adapterKind,
        string deliveryKind,
        string destination)
    {
        var publisher = new Mock<IMonitorEventPublisher>();
        var notificationDelivery = new Mock<INotificationDeliveryService>();
        var adapter = CreateAdapter(adapterKind, publisher.Object, notificationDelivery.Object);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => adapter.DeliverAsync(
            CreateDelivery(deliveryKind, destination, CreateEnvelope()),
            CancellationToken.None));

        publisher.VerifyNoOtherCalls();
        notificationDelivery.VerifyNoOtherCalls();
    }

    [DataTestMethod]
    [DataRow("mqtt", "MqttAlert", "wrong-topic")]
    [DataRow("email", "Email", " ")]
    [DataRow("sms", "Sms", "")]
    public async Task Adapters_RejectInvalidDestinationBeforeExternalDelivery(
        string adapterKind,
        string deliveryKind,
        string destination)
    {
        var publisher = new Mock<IMonitorEventPublisher>();
        var notificationDelivery = new Mock<INotificationDeliveryService>();
        var adapter = CreateAdapter(adapterKind, publisher.Object, notificationDelivery.Object);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => adapter.DeliverAsync(
            CreateDelivery(deliveryKind, destination, CreateEnvelope()),
            CancellationToken.None));

        publisher.VerifyNoOtherCalls();
        notificationDelivery.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task MqttAdapter_WithRealPublisher_PreservesConfiguredTopicAndWireContract()
    {
        string? capturedTopic = null;
        string? capturedJson = null;
        var mqttClient = new Mock<IMqttClient>();
        mqttClient.Setup(x => x.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback((string topic, string json, CancellationToken _) =>
            {
                capturedTopic = topic;
                capturedJson = json;
            })
            .Returns(Task.CompletedTask);
        var publisher = new MonitorEventPublisher(mqttClient.Object, "insert/topic", "configured/alert/topic");
        var adapter = new MqttAlertDeliveryAdapter(publisher);
        var envelope = CreateEnvelope();

        await adapter.DeliverAsync(
            CreateDelivery("MqttAlert", "alert", envelope),
            CancellationToken.None);

        Assert.AreEqual("configured/alert/topic", capturedTopic);
        Assert.IsNotNull(capturedJson);
        var mqttMessage = JsonSerializer.Deserialize<CapturedMqttWireMessage>(capturedJson);
        Assert.IsNotNull(mqttMessage);
        Assert.AreEqual(envelope.Timestamp, mqttMessage.Timestamp);
        Assert.AreEqual(envelope.CustomerId, mqttMessage.CustomerId);
        Assert.AreEqual(envelope.SerialId, mqttMessage.SerialNumber);
        Assert.AreEqual(envelope.Message, mqttMessage.Message);
    }

    private static IAlertDeliveryAdapter CreateAdapter(
        string adapterKind,
        IMonitorEventPublisher publisher,
        INotificationDeliveryService notificationDelivery) => adapterKind switch
        {
            "mqtt" => new MqttAlertDeliveryAdapter(publisher),
            "email" => new EmailAlertDeliveryAdapter(
                notificationDelivery,
                Options.Create(new DurableAlertOptions()),
                CreateTimeProvider()),
            "sms" => new SmsAlertDeliveryAdapter(
                notificationDelivery,
                Options.Create(new DurableAlertOptions()),
                CreateTimeProvider()),
            _ => throw new ArgumentOutOfRangeException(nameof(adapterKind))
        };

    private static (string Kind, string Destination) DeliveryIdentity(string adapterKind) =>
        adapterKind switch
        {
            "mqtt" => ("MqttAlert", "alert"),
            "email" => ("Email", "ops@example.test"),
            "sms" => ("Sms", "+441234567890"),
            _ => throw new ArgumentOutOfRangeException(nameof(adapterKind))
        };

    private static AlertDeliveryEnvelope CreateEnvelope() => new(
        Version: 1,
        NotificationId: Guid.Parse("22222222-2222-8222-8222-222222222222"),
        Timestamp: new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc),
        AlertType: AlertType.Alert,
        SerialId: "serial-1",
        CustomerId: 9,
        FleetNr: "fleet-1",
        Message: "Vibration alert");

    private static ClaimedAlertDelivery CreateDelivery(
        string kind,
        string destination,
        AlertDeliveryEnvelope envelope) => new(
            Id: Guid.NewGuid(),
            OccurrenceId: Guid.NewGuid(),
            NotificationId: envelope.NotificationId,
            DeliveryKey: Guid.NewGuid().ToString("N"),
            Kind: kind,
            Destination: destination,
            Payload: JsonSerializer.Serialize(envelope),
            Status: "Leased",
            AttemptCount: 1,
            NextAttemptAt: SentAt,
            LeaseId: Guid.NewGuid(),
            LeaseUntil: SentAt.AddMinutes(2),
            CompletedAt: null,
            LastError: null,
            CreatedAt: SentAt.AddMinutes(-1));

    private static TimeProvider CreateTimeProvider()
    {
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(SentAt));
        return timeProvider.Object;
    }

    private sealed record CapturedMqttWireMessage(
        DateTime Timestamp,
        int? CustomerId,
        string SerialNumber,
        string Message);
}
