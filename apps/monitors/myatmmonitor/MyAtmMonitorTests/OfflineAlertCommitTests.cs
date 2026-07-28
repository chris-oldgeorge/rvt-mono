using Microsoft.Extensions.Logging;
using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Model.Config;
using MyAtm.Model.Dto;
using Rvt.Communication.Abstractions;
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
        Mock<IHttpClient> httpClient = new Mock<IHttpClient>();
        Mock<IDBClient> dbClient = new Mock<IDBClient>();
        Mock<IMqttClient> mqttClient = new Mock<IMqttClient>();
        Mock<IMessageService> messageService = new Mock<IMessageService>();
        int customerId = 765;
        DustMonitorDto monitor = MyAtmFixture.CustomerDeviceDtos(DateTime.UtcNow.AddHours(-25), singleItem: true).Single();
        Rvt.Monitor.Common.Rules.RvtAlertRuleDto rule = MyAtmFixture.OfflineRules().Single();
        Rvt.Monitor.Common.Rules.RvtContactDto contact = MyAtmFixture.AlertContacts().Single();
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

        MyAtmApi api = new MyAtmApi(httpClient.Object, dbClient.Object, mqttClient.Object, messageService.Object, false);

        await api.CheckForOfflineMonitorsAsync(customerId);

        Assert.IsNotNull(commit);
        Assert.AreEqual(monitor.Id, commit.MonitorStateMutation!.MonitorId);
        Assert.IsFalse(commit.MonitorStateMutation.ExpectedOffline);
        Assert.IsTrue(commit.MonitorStateMutation.Offline);
        Assert.HasCount(1, commit.Occurrences);
        MyAtmAlertOccurrenceInput occurrence = commit.Occurrences[0];
        Assert.AreEqual(AlertType.Offline, occurrence.AlertType);
        Assert.IsNotNull(occurrence.DeliveryPlan);
        Guid expectedNotificationId = MonitorDeliveryIdentity.CreateGuid($"notification:{occurrence.Key}");
        Assert.AreEqual(expectedNotificationId, occurrence.DeliveryPlan.Notification.Id);
        CollectionAssert.AreEquivalent(
            new[] { MonitorDeliveryKind.Email },
            occurrence.DeliveryPlan.Deliveries.Select(delivery => delivery.Kind).ToArray());
        MonitorDeliveryRequest delivery = occurrence.DeliveryPlan.Deliveries.Single();
        string expectedKey = $"{occurrence.Key}:Email:{contact.EmailAddress}";
        Assert.AreEqual(expectedKey, delivery.DeliveryKey);
        Assert.AreEqual(MonitorDeliveryIdentity.CreateGuid($"outbox:{expectedKey}"), delivery.Id);
        Assert.AreEqual(MonitorDeliveryProducers.MyAtm, delivery.Producer);
        Assert.AreEqual(expectedNotificationId, delivery.NotificationId);
        Assert.AreEqual(occurrence.Key, delivery.CorrelationKey);
        Assert.AreEqual(1, delivery.PayloadVersion);
        Assert.AreEqual(commit.UtcNow, delivery.CreatedAt);
        MonitorDeliveryPayloadV1 payload = Decode(delivery);
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
        Mock<IHttpClient> httpClient = new Mock<IHttpClient>();
        Mock<IDBClient> dbClient = new Mock<IDBClient>();
        Mock<IMqttClient> mqttClient = new Mock<IMqttClient>();
        Mock<IMessageService> messageService = new Mock<IMessageService>();
        int customerId = 765;
        DustMonitorDto monitor = MyAtmFixture.CustomerDeviceDtos(DateTime.UtcNow, singleItem: true).Single();
        monitor.Offline = true;
        Rvt.Monitor.Common.Rules.RvtAlertRuleDto rule = MyAtmFixture.OfflineRules().Single();
        MyAtmAlertCommit? commit = null;

        dbClient.Setup(client => client.ReadRules(null)).Returns([rule]);
        dbClient.Setup(client => client.ReadMonitorList(customerId, It.IsAny<DateTime?>())).Returns([monitor]);
        dbClient.Setup(client => client.CommitAlertAsync(It.IsAny<MyAtmAlertCommit>(), It.IsAny<CancellationToken>()))
            .Callback<MyAtmAlertCommit, CancellationToken>((value, _) => commit = value)
            .ReturnsAsync(new MyAtmAlertCommitResult(true, Array.Empty<MonitorDeliveryRequest>()));

        MyAtmApi api = new MyAtmApi(httpClient.Object, dbClient.Object, mqttClient.Object, messageService.Object, false);

        await api.CheckForOfflineMonitorsAsync(customerId);

        Assert.IsNotNull(commit);
        Assert.IsTrue(commit.MonitorStateMutation!.ExpectedOffline);
        Assert.IsFalse(commit.MonitorStateMutation.Offline);
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
