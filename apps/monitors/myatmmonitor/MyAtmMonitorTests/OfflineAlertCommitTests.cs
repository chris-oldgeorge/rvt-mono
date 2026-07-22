using Microsoft.Extensions.Logging;
using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Model.Config;
using MyAtm.Model.Dto;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class OfflineAlertCommitTests
{
    public OfflineAlertCommitTests()
    {
        RvtLogger.CreateLogger(
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug)),
            nameof(OfflineAlertCommitTests));
    }

    [TestMethod]
    public async Task ScheduledOfflineTransition_CommitsMonitorStateOccurrenceAndEmailDeliveryAtomically()
    {
        var httpClient = new Mock<IHttpClient>();
        var dbClient = new Mock<IDBClient>();
        var mqttClient = new Mock<IMqttClient>();
        var messageService = new Mock<IMessageService>();
        var customerId = 765;
        var monitor = MyAtmFixture.CustomerDeviceDtos(DateTime.UtcNow.AddHours(-25), singleItem: true).Single();
        var rule = MyAtmFixture.OfflineRules().Single();
        var contact = MyAtmFixture.AlertContacts().Single();
        MyAtmAlertCommit? commit = null;

        dbClient.Setup(client => client.ReadRules(null)).Returns([rule]);
        dbClient.Setup(client => client.ReadMonitorList(customerId, It.IsAny<DateTime?>())).Returns([monitor]);
        dbClient.Setup(client => client.ReadSiteSchedule(monitor.Id)).Returns(new MyAtmSiteSchedule
        {
            WeekdayStart = TimeSpan.Zero,
            WeekdayEnd = TimeSpan.FromHours(24),
            SaturdayStart = TimeSpan.Zero,
            SaturdayEnd = TimeSpan.FromHours(24),
            SundayStart = TimeSpan.Zero,
            SundayEnd = TimeSpan.FromHours(24)
        });
        dbClient.Setup(client => client.ReadAlertContacts(monitor.Id)).Returns([contact]);
        dbClient.Setup(client => client.CommitAlertAsync(It.IsAny<MyAtmAlertCommit>(), It.IsAny<CancellationToken>()))
            .Callback<MyAtmAlertCommit, CancellationToken>((value, _) => commit = value)
            .ReturnsAsync(new MyAtmAlertCommitResult(true, Array.Empty<MonitorDeliveryRequest>()));

        var api = new MyAtmApi(httpClient.Object, dbClient.Object, mqttClient.Object, messageService.Object, false);

        await api.CheckForOfflineMonitorsAsync(customerId);

        Assert.IsNotNull(commit);
        Assert.AreEqual(monitor.Id, commit.MonitorStateMutation!.MonitorId);
        Assert.AreEqual(false, commit.MonitorStateMutation.ExpectedOffline);
        Assert.AreEqual(true, commit.MonitorStateMutation.Offline);
        Assert.HasCount(1, commit.Occurrences);
        var occurrence = commit.Occurrences[0];
        Assert.AreEqual(AlertType.Offline, occurrence.AlertType);
        Assert.IsNotNull(occurrence.DeliveryPlan);
        var expectedNotificationId = MonitorDeliveryIdentity.CreateGuid($"notification:{occurrence.Key}");
        Assert.AreEqual(expectedNotificationId, occurrence.DeliveryPlan.Notification.Id);
        CollectionAssert.AreEquivalent(
            new[] { MonitorDeliveryKind.Email },
            occurrence.DeliveryPlan.Deliveries.Select(delivery => delivery.Kind).ToArray());
        var delivery = occurrence.DeliveryPlan.Deliveries.Single();
        var expectedKey = $"{occurrence.Key}:Email:{contact.EmailAddress}";
        Assert.AreEqual(expectedKey, delivery.DeliveryKey);
        Assert.AreEqual(MonitorDeliveryIdentity.CreateGuid($"outbox:{expectedKey}"), delivery.Id);
        Assert.AreEqual(MonitorDeliveryProducers.MyAtm, delivery.Producer);
        Assert.AreEqual(expectedNotificationId, delivery.NotificationId);
        Assert.AreEqual(occurrence.Key, delivery.CorrelationKey);
        Assert.AreEqual(1, delivery.PayloadVersion);
        Assert.AreEqual(commit.UtcNow, delivery.CreatedAt);
        var payload = Decode(delivery);
        Assert.AreEqual(expectedNotificationId, payload.NotificationId);
        Assert.AreEqual(occurrence.TriggeredAt.ToUniversalTime(), payload.Timestamp);
        Assert.AreEqual(monitor.SerialId, payload.SerialId);
        Assert.AreEqual(monitor.CustomerId, payload.CustomerId);
        Assert.AreEqual(monitor.FleetNr, payload.FleetNr);
        Assert.AreEqual(AlertType.Offline, payload.AlertType);
        Assert.AreEqual(Rvt.Monitor.Common.Rules.RuleConstants.OFFLINE_RULE, payload.Field);
        Assert.AreEqual(occurrence.Level, payload.Level);
        Assert.IsNull(payload.PortalBaseUrl);

        dbClient.Verify(client => client.SetMonitorOffline(It.IsAny<Guid>(), It.IsAny<bool>()), Times.Never);
        dbClient.Verify(client => client.WriteNotification(It.IsAny<NotificationDto>()), Times.Never);
        messageService.VerifyNoOtherCalls();
        mqttClient.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ScheduledOnlineRecovery_CommitsOnlyTheExpectedOfflineStateChange()
    {
        var httpClient = new Mock<IHttpClient>();
        var dbClient = new Mock<IDBClient>();
        var mqttClient = new Mock<IMqttClient>();
        var messageService = new Mock<IMessageService>();
        var customerId = 765;
        var monitor = MyAtmFixture.CustomerDeviceDtos(DateTime.UtcNow, singleItem: true).Single();
        monitor.Offline = true;
        var rule = MyAtmFixture.OfflineRules().Single();
        MyAtmAlertCommit? commit = null;

        dbClient.Setup(client => client.ReadRules(null)).Returns([rule]);
        dbClient.Setup(client => client.ReadMonitorList(customerId, It.IsAny<DateTime?>())).Returns([monitor]);
        dbClient.Setup(client => client.CommitAlertAsync(It.IsAny<MyAtmAlertCommit>(), It.IsAny<CancellationToken>()))
            .Callback<MyAtmAlertCommit, CancellationToken>((value, _) => commit = value)
            .ReturnsAsync(new MyAtmAlertCommitResult(true, Array.Empty<MonitorDeliveryRequest>()));

        var api = new MyAtmApi(httpClient.Object, dbClient.Object, mqttClient.Object, messageService.Object, false);

        await api.CheckForOfflineMonitorsAsync(customerId);

        Assert.IsNotNull(commit);
        Assert.AreEqual(true, commit.MonitorStateMutation!.ExpectedOffline);
        Assert.AreEqual(false, commit.MonitorStateMutation.Offline);
        Assert.IsEmpty(commit.Occurrences);
        dbClient.Verify(client => client.ReadAlertContacts(It.IsAny<Guid>()), Times.Never);
        messageService.VerifyNoOtherCalls();
        mqttClient.VerifyNoOtherCalls();
    }

    private static MonitorDeliveryPayloadV1 Decode(MonitorDeliveryRequest request) =>
        MonitorDeliveryPayloadCodec.Decode(new MonitorDeliveryMessage(
            request.Id,
            request.Producer,
            request.NotificationId,
            request.CorrelationKey,
            request.DeliveryKey,
            request.Kind,
            request.Destination,
            request.PayloadVersion,
            request.Payload,
            AttemptCount: 1,
            LeaseId: Guid.NewGuid()));
}
