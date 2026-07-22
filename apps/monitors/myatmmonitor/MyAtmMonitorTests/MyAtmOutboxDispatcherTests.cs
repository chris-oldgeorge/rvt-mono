using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Model.Config;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmOutboxDispatcherTests
{
    private static readonly TimeSpan TimingTolerance = TimeSpan.FromSeconds(2);

    [TestMethod]
    public async Task DispatchDueAsync_UsesMappedMyAtmTopicsLeaseAndMqttFormatting()
    {
        var queries = new Mock<IMonitorDeliveryOutboxQueries>();
        var commands = new Mock<IMonitorDeliveryOutboxCommands>();
        var operationalCommands = new Mock<IMyAtmOperationalCommands>();
        var mqttClient = new Mock<IMqttClient>();
        var notificationDelivery = new Mock<INotificationDeliveryService>();
        var alert = CreateMessage(MonitorDeliveryKind.MqttAlert, "ignored", CreatePayload(), attemptCount: 1);
        var inserted = CreateMessage(
            MonitorDeliveryKind.MqttDataInserted,
            "ignored",
            CreatePayload() with { NotificationId = Guid.Empty },
            attemptCount: 1) with
        {
            NotificationId = null
        };
        queries.SetupSequence(query => query.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                It.IsAny<DateTime>(),
                TimeSpan.FromSeconds(120),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert)
            .ReturnsAsync(inserted)
            .ReturnsAsync((MonitorDeliveryMessage?)null);
        var publications = new List<(string Topic, string Payload)>();
        mqttClient.Setup(client => client.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string topic, string payload, CancellationToken _) => publications.Add((topic, payload)))
            .Returns(Task.CompletedTask);
        commands.Setup(command => command.CompleteAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var dispatcher = CreateDispatcher(
            queries.Object,
            commands.Object,
            operationalCommands.Object,
            mqttClient.Object,
            notificationDelivery.Object);

        await dispatcher.DispatchDueAsync();

        Assert.HasCount(2, publications);
        Assert.AreEqual("myatm/alerts", publications[0].Topic);
        using (var document = JsonDocument.Parse(publications[0].Payload))
        {
            Assert.AreEqual("serial-1", document.RootElement.GetProperty("SerialNumber").GetString());
            Assert.AreEqual(9, document.RootElement.GetProperty("CustomerId").GetInt32());
            Assert.AreEqual("Dust Alert pm10 level=42", document.RootElement.GetProperty("Message").GetString());
        }

        Assert.AreEqual("myatm/inserted", publications[1].Topic);
        using (var document = JsonDocument.Parse(publications[1].Payload))
        {
            Assert.AreEqual("Dto Inserted", document.RootElement.GetProperty("Message").GetString());
        }

        queries.Verify(query => query.ClaimNextDueAsync(
            MonitorDeliveryProducers.MyAtm,
            It.IsAny<DateTime>(),
            TimeSpan.FromSeconds(120),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
        commands.Verify(command => command.CompleteAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task DispatchDueAsync_PreservesMyAtmContactFormattingUrlsAndSuccessAudits()
    {
        var queries = new Mock<IMonitorDeliveryOutboxQueries>();
        var commands = new Mock<IMonitorDeliveryOutboxCommands>();
        var operationalCommands = new Mock<IMyAtmOperationalCommands>();
        var mqttClient = new Mock<IMqttClient>();
        var notificationDelivery = new Mock<INotificationDeliveryService>();
        var payload = CreatePayload();
        var email = CreateMessage(MonitorDeliveryKind.Email, "person@example.test", payload, attemptCount: 1);
        var sms = CreateMessage(MonitorDeliveryKind.Sms, "447700900000", payload, attemptCount: 1);
        queries.SetupSequence(query => query.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(email)
            .ReturnsAsync(sms)
            .ReturnsAsync((MonitorDeliveryMessage?)null);
        var requests = new List<NotificationDeliveryRequest>();
        notificationDelivery.Setup(service => service.SendAsync(
                It.IsAny<NotificationDeliveryRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback((NotificationDeliveryRequest request, CancellationToken _) => requests.Add(request))
            .Returns(Task.CompletedTask);
        var audits = new List<MonitorDeliveryAudit>();
        commands.Setup(command => command.CompleteAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<MonitorDeliveryAudit>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, Guid _, DateTime _, MonitorDeliveryAudit? audit, CancellationToken _) => audits.Add(audit!))
            .ReturnsAsync(true);
        var dispatcher = CreateDispatcher(
            queries.Object,
            commands.Object,
            operationalCommands.Object,
            mqttClient.Object,
            notificationDelivery.Object,
            new MyAtmMonitorOptions { PortalBaseUrl = "https://portal.example.test/root/" });

        await dispatcher.DispatchDueAsync();

        Assert.HasCount(2, requests);
        Assert.AreEqual(NotificationChannel.Email, requests[0].Channel);
        Assert.AreEqual("person@example.test", requests[0].Destination);
        Assert.AreEqual(NotificationChannel.Sms, requests[1].Channel);
        Assert.AreEqual("447700900000", requests[1].Destination);
        Assert.IsTrue(requests.All(request =>
            request.CallbackUrl == $"https://portal.example.test/root/Notification/View/{email.NotificationId}"));
        Assert.HasCount(2, audits);
        Assert.IsTrue(audits.All(audit => audit.Result == NotificationConstants.SENT_OK));
        CollectionAssert.AreEquivalent(
            new[] { "person@example.test", "447700900000" },
            audits.Select(audit => audit.Address).ToArray());
    }

    [TestMethod]
    public async Task DispatchDueAsync_RetriesExponentiallyWithoutFailingDeadLetterOnlyPassOrRecordingOperationalErrors()
    {
        var queries = new Mock<IMonitorDeliveryOutboxQueries>();
        var commands = new Mock<IMonitorDeliveryOutboxCommands>();
        var operationalCommands = new Mock<IMyAtmOperationalCommands>();
        var mqttClient = new Mock<IMqttClient>();
        var notificationDelivery = new Mock<INotificationDeliveryService>();
        queries.SetupSequence(query => query.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMessage(MonitorDeliveryKind.MqttAlert, "", CreatePayload(), attemptCount: 1))
            .ReturnsAsync(CreateMessage(MonitorDeliveryKind.MqttAlert, "", CreatePayload(), attemptCount: 2))
            .ReturnsAsync(CreateMessage(MonitorDeliveryKind.MqttAlert, "", CreatePayload(), attemptCount: 7))
            .ReturnsAsync((MonitorDeliveryMessage?)null);
        mqttClient.Setup(client => client.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("destination secret"));
        var retries = new List<(DateTime NextAttemptAt, string Error)>();
        commands.Setup(command => command.RetryAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, Guid _, DateTime nextAttemptAt, string error, CancellationToken _) =>
                retries.Add((nextAttemptAt, error)))
            .ReturnsAsync(true);
        var startedAt = DateTime.UtcNow;
        var dispatcher = CreateDispatcher(
            queries.Object,
            commands.Object,
            operationalCommands.Object,
            mqttClient.Object,
            notificationDelivery.Object);

        await dispatcher.DispatchDueAsync();

        Assert.HasCount(3, retries);
        AssertTimestampNear(startedAt.AddSeconds(30), retries[0].NextAttemptAt);
        AssertTimestampNear(startedAt.AddSeconds(60), retries[1].NextAttemptAt);
        AssertTimestampNear(startedAt.AddMinutes(30), retries[2].NextAttemptAt);
        Assert.IsTrue(retries.All(retry => retry.Error == "Delivery failed (TimeoutException)."));
        operationalCommands.Verify(command => command.HandleException(
            It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        commands.Verify(command => command.DeadLetterAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<string>(),
            It.IsAny<MonitorDeliveryAudit?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DispatchDueAsync_DeadLettersFinalContactFailureWithAuditAndRecordsTerminalFailure()
    {
        var queries = new Mock<IMonitorDeliveryOutboxQueries>();
        var commands = new Mock<IMonitorDeliveryOutboxCommands>();
        var operationalCommands = new Mock<IMyAtmOperationalCommands>();
        var mqttClient = new Mock<IMqttClient>();
        var notificationDelivery = new Mock<INotificationDeliveryService>();
        var message = CreateMessage(MonitorDeliveryKind.Email, "person@example.test", CreatePayload(), attemptCount: 8);
        queries.SetupSequence(query => query.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(message)
            .ReturnsAsync((MonitorDeliveryMessage?)null);
        notificationDelivery.Setup(service => service.SendAsync(
                It.Is<NotificationDeliveryRequest>(request => request.Channel == NotificationChannel.Email),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("provider included person@example.test"));
        MonitorDeliveryAudit? failureAudit = null;
        commands.Setup(command => command.DeadLetterAsync(
                message.Id,
                message.LeaseId,
                It.IsAny<DateTime>(),
                "Delivery failed (IOException).",
                It.IsAny<MonitorDeliveryAudit>(),
                It.IsAny<CancellationToken>()))
            .Callback((Guid _, Guid _, DateTime _, string _, MonitorDeliveryAudit? audit, CancellationToken _) => failureAudit = audit)
            .ReturnsAsync(true);
        var dispatcher = CreateDispatcher(
            queries.Object,
            commands.Object,
            operationalCommands.Object,
            mqttClient.Object,
            notificationDelivery.Object);

        var exception = await Assert.ThrowsExactlyAsync<MonitorDeliveryDispatchException>(
            () => dispatcher.DispatchDueAsync());

        Assert.HasCount(1, exception.Failures);
        Assert.IsNotNull(failureAudit);
        Assert.AreEqual(message.NotificationId, failureAudit.NotificationId);
        Assert.AreEqual("person@example.test", failureAudit.Address);
        Assert.AreEqual("Delivery failed (IOException).", failureAudit.Result);
        operationalCommands.Verify(command => command.HandleException(
            "Outbox delivery dead-lettered",
            It.Is<InvalidOperationException>(error => error.Message == "Delivery failed (IOException).")), Times.Once);
    }

    [TestMethod]
    public async Task DispatchDueAsync_DeadLettersMalformedPayloadWithoutContactAudit()
    {
        var queries = new Mock<IMonitorDeliveryOutboxQueries>();
        var commands = new Mock<IMonitorDeliveryOutboxCommands>();
        var operationalCommands = new Mock<IMyAtmOperationalCommands>();
        var mqttClient = new Mock<IMqttClient>();
        var notificationDelivery = new Mock<INotificationDeliveryService>();
        var message = CreateMessage(MonitorDeliveryKind.Email, "person@example.test", CreatePayload(), attemptCount: 8) with
        {
            Payload = "{ corrupt payload"
        };
        queries.SetupSequence(query => query.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(message)
            .ReturnsAsync((MonitorDeliveryMessage?)null);
        commands.Setup(command => command.DeadLetterAsync(
                message.Id,
                message.LeaseId,
                It.IsAny<DateTime>(),
                "Delivery failed (InvalidDataException).",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var dispatcher = CreateDispatcher(
            queries.Object,
            commands.Object,
            operationalCommands.Object,
            mqttClient.Object,
            notificationDelivery.Object);

        await Assert.ThrowsExactlyAsync<MonitorDeliveryDispatchException>(() => dispatcher.DispatchDueAsync());

        commands.Verify(command => command.DeadLetterAsync(
            message.Id,
            message.LeaseId,
            It.IsAny<DateTime>(),
            "Delivery failed (InvalidDataException).",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        notificationDelivery.VerifyNoOtherCalls();
        operationalCommands.Verify(command => command.HandleException(
            "Outbox delivery dead-lettered",
            It.Is<InvalidOperationException>(error => error.Message == "Delivery failed (InvalidDataException).")), Times.Once);
    }

    [TestMethod]
    public async Task MyAtmDeliveryFailureSink_RecordsOnlyTerminalFailures()
    {
        var operationalCommands = new Mock<IMyAtmOperationalCommands>();
        var sink = new MyAtmDeliveryFailureSink(operationalCommands.Object);
        var message = CreateMessage(MonitorDeliveryKind.MqttAlert, "", CreatePayload(), attemptCount: 1);

        await sink.RecordFailureAsync(message, "transient", terminal: false);
        await sink.RecordFailureAsync(message, "terminal", terminal: true);

        operationalCommands.Verify(command => command.HandleException(
            "Outbox delivery dead-lettered",
            It.Is<InvalidOperationException>(exception => exception.Message == "terminal")), Times.Once);
        operationalCommands.VerifyNoOtherCalls();
    }

    private static MonitorDeliveryDispatcher CreateDispatcher(
        IMonitorDeliveryOutboxQueries queries,
        IMonitorDeliveryOutboxCommands commands,
        IMyAtmOperationalCommands operationalCommands,
        IMqttClient mqttClient,
        INotificationDeliveryService notificationDelivery,
        MyAtmMonitorOptions? options = null) =>
        new(
            queries,
            commands,
            new MyAtmDeliveryFailureSink(operationalCommands),
            mqttClient,
            notificationDelivery,
            NullLogger<MonitorDeliveryDispatcher>.Instance,
            (options ?? new MyAtmMonitorOptions()).ToDeliveryOptions("myatm/inserted", "myatm/alerts"));

    private static void AssertTimestampNear(DateTime expected, DateTime actual)
    {
        var delta = (actual - expected).Duration();
        Assert.IsLessThanOrEqualTo(TimingTolerance, delta);
    }

    private static MonitorDeliveryPayloadV1 CreatePayload() => new(
        Guid.NewGuid(),
        new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc),
        "serial-1",
        9,
        "fleet-1",
        AlertType.Alert,
        "pm10",
        42);

    private static MonitorDeliveryMessage CreateMessage(
        MonitorDeliveryKind kind,
        string destination,
        MonitorDeliveryPayloadV1 payload,
        int attemptCount) =>
        new(
            Guid.NewGuid(),
            MonitorDeliveryProducers.MyAtm,
            payload.NotificationId == Guid.Empty ? null : payload.NotificationId,
            "occurrence-1",
            $"delivery-{Guid.NewGuid():N}",
            kind,
            destination,
            1,
            JsonSerializer.Serialize(payload),
            attemptCount,
            Guid.NewGuid());
}
