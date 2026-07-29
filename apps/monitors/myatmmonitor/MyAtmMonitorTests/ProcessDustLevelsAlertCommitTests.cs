using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Model.Dto;
using MyAtm.Model.Json;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class ProcessDustLevelsAlertCommitTests
{
    [TestMethod]
    public async Task ActiveAlertWithAnEquivalentFieldAlias_SuppressesAggregateCaution()
    {
        Mock<IHttpClient> httpClient = new();
        Mock<IDBClient> dbClient = new();
        Mock<IMqttClient> mqttClient = new();
        Mock<INotificationDeliveryService> messageService = new();
        int customerId = 656;
        DateTime now = DateTime.UtcNow;
        DustMonitorDto monitor = MyAtmFixture.CustomerDeviceDtos(now.AddDays(1), singleItem: true).Single();
        AlertActivityTimeDto activity = new() { Weekdays = true, Saturdays = true, Sundays = true };
        RvtAlertRuleDto activeAlert = new(
            Guid.NewGuid(), monitor.SerialId, "Pm2_5", 10, 8, 8 * 60 * 60,
            activity, AlertType.Alert, true, false, now, null);
        RvtAlertRuleDto caution = new(
            Guid.NewGuid(), monitor.SerialId, "pm2.5", 10, 8, 8 * 60 * 60,
            activity, AlertType.Caution, false, false, now.AddDays(-1), null);
        List<MyAtmAlertCommit> commits = [];

        dbClient.Setup(client => client.ReadRules(Period.Hours8)).Returns([activeAlert, caution]);
        dbClient.Setup(client => client.ReadMonitor(customerId, monitor.SerialId)).Returns(monitor);
        dbClient.Setup(client => client.GetAverageDustLevel(
                monitor.SerialId, "pm2.5", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(12);
        dbClient.Setup(client => client.CommitAlertAsync(It.IsAny<MyAtmAlertCommit>(), It.IsAny<CancellationToken>()))
            .Callback<MyAtmAlertCommit, CancellationToken>((commit, _) => commits.Add(commit))
            .ReturnsAsync(new MyAtmAlertCommitResult(true, Array.Empty<MonitorDeliveryRequest>()));

        MyAtmApi api = new(httpClient.Object, dbClient.Object, mqttClient.Object, messageService.Object, false);

        await api.ProcessDustLevelsAsync<AvgDeviceMeasurement>(customerId, Period.Hours8, TestContext.CancellationToken);

        Assert.IsNotEmpty(commits);
        Assert.IsTrue(commits.All(commit => commit.Occurrences.Count == 0));
    }

    private static readonly string[] _expected =
    [
        "ReadRules",
        "ReadMonitor",
        "GetAverageDustLevel",
        "ReadAlertContacts",
        "CommitAlertAsync"
    ];

    [TestMethod]
    public async Task CompletedAggregatePeriod_CommitsStateOccurrenceAndAllDurableDeliveriesOnce()
    {
        Mock<IHttpClient> httpClient = new();
        Mock<IDBClient> dbClient = new();
        Mock<IMqttClient> mqttClient = new();
        Mock<INotificationDeliveryService> messageService = new();
        int customerId = 656;
        DustMonitorDto monitor = MyAtmFixture.CustomerDeviceDtos(DateTime.UtcNow.AddDays(1), singleItem: true).Single();
        RvtAlertRuleDto rule = new(
            Guid.NewGuid(), monitor.SerialId, "Pm10", 10, 8, 8 * 60 * 60,
            new AlertActivityTimeDto { Weekdays = true, Saturdays = true, Sundays = true },
            AlertType.Alert, false, false, DateTime.UtcNow.AddHours(-12), null);
        List<RvtContactDto> contacts =
        [
            new(ContactMethod.Email, "aggregate@example.test", (string?)null, true, false, null, null),
            new(ContactMethod.SMS, string.Empty, "15551234567", false, true, null, null)
        ];
        MyAtmAlertCommit? commit = null;

        dbClient.Setup(client => client.ReadRules(Period.Hours8)).Returns([rule]);
        dbClient.Setup(client => client.ReadMonitor(customerId, monitor.SerialId)).Returns(monitor);
        dbClient.Setup(client => client.GetAverageDustLevel(monitor.SerialId, "Pm10", It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(12);
        dbClient.Setup(client => client.ReadAlertContacts(monitor.Id)).Returns(contacts);
        dbClient.Setup(client => client.CommitAlertAsync(It.IsAny<MyAtmAlertCommit>(), It.IsAny<CancellationToken>()))
            .Callback<MyAtmAlertCommit, CancellationToken>((value, _) => commit = value)
            .ReturnsAsync(new MyAtmAlertCommitResult(true, Array.Empty<MonitorDeliveryRequest>()));

        MyAtmApi api = new(httpClient.Object, dbClient.Object, mqttClient.Object, messageService.Object, false);

        await api.ProcessDustLevelsAsync<AvgDeviceMeasurement>(customerId, Period.Hours8, TestContext.CancellationToken);

        Assert.IsNotNull(commit);
        Assert.HasCount(1, commit.RuleStateMutations);
        Assert.AreEqual(rule.RuleId, commit.RuleStateMutations[0].RuleId);
        Assert.IsFalse(commit.RuleStateMutations[0].ExpectedIsActive);
        Assert.IsTrue(commit.RuleStateMutations[0].IsActive);
        Assert.HasCount(1, commit.Occurrences);
        MyAtmAlertOccurrenceInput occurrence = commit.Occurrences[0];
        Assert.AreEqual(AlertType.Alert, occurrence.AlertType);
        Assert.AreEqual(12d, occurrence.Level);
        Assert.IsNotNull(occurrence.DeliveryPlan);
        Guid expectedNotificationId = MonitorDeliveryIdentity.CreateGuid($"notification:{occurrence.Key}");
        Assert.AreEqual(expectedNotificationId, occurrence.DeliveryPlan.Notification.Id);
        CollectionAssert.AreEquivalent(
            new[] { MonitorDeliveryKind.Email, MonitorDeliveryKind.Sms, MonitorDeliveryKind.MqttAlert },
            occurrence.DeliveryPlan.Deliveries.Select(delivery => delivery.Kind).ToArray());
        foreach (MonitorDeliveryRequest delivery in occurrence.DeliveryPlan.Deliveries)
        {
            string expectedKey = $"{occurrence.Key}:{delivery.Kind}:{delivery.Destination}";
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
            Assert.AreEqual(AlertType.Alert, payload.AlertType);
            Assert.AreEqual("pm10", payload.Field);
            Assert.AreEqual(12d, payload.Level);
            Assert.IsNull(payload.PortalBaseUrl);
        }
        string[] databaseCalls = [.. dbClient.Invocations.Select(invocation => invocation.Method.Name)];
        CollectionAssert.AreEqual(
            _expected,
            databaseCalls,
            $"Unexpected active aggregate DB call sequence: {string.Join(", ", databaseCalls)}");
        messageService.VerifyNoOtherCalls();
        mqttClient.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DeletedActiveRule_CommitsDeactivationWithoutWaitingForACompletedPeriod()
    {
        Mock<IHttpClient> httpClient = new();
        Mock<IDBClient> dbClient = new();
        Mock<IMqttClient> mqttClient = new();
        Mock<INotificationDeliveryService> messageService = new();
        int customerId = 656;
        DustMonitorDto monitor = MyAtmFixture.CustomerDeviceDtos(null, singleItem: true).Single();
        RvtAlertRuleDto rule = new(
            Guid.NewGuid(), monitor.SerialId, "Pm10", 10, 8, 8 * 60 * 60,
            new AlertActivityTimeDto { Weekdays = true, Saturdays = true, Sundays = true },
            AlertType.Alert, true, true, DateTime.UtcNow, null);
        MyAtmAlertCommit? commit = null;

        dbClient.Setup(client => client.ReadRules(Period.Hours8)).Returns([rule]);
        dbClient.Setup(client => client.CommitAlertAsync(It.IsAny<MyAtmAlertCommit>(), It.IsAny<CancellationToken>()))
            .Callback<MyAtmAlertCommit, CancellationToken>((value, _) => commit = value)
            .ReturnsAsync(new MyAtmAlertCommitResult(true, Array.Empty<MonitorDeliveryRequest>()));

        MyAtmApi api = new(httpClient.Object, dbClient.Object, mqttClient.Object, messageService.Object, false);

        await api.ProcessDustLevelsAsync<AvgDeviceMeasurement>(customerId, Period.Hours8, TestContext.CancellationToken);

        Assert.IsNotNull(commit);
        Assert.HasCount(1, commit.RuleStateMutations);
        Assert.IsTrue(commit.RuleStateMutations[0].ExpectedIsActive);
        Assert.IsFalse(commit.RuleStateMutations[0].IsActive);
        Assert.IsEmpty(commit.Occurrences);
        dbClient.Verify(client => client.ReadMonitor(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        dbClient.Verify(client => client.GetAverageDustLevel(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
        dbClient.Verify(client => client.UpdateAlertRule(It.IsAny<RvtAlertRuleDto>()), Times.Never);
        dbClient.Verify(client => client.WriteNotification(It.IsAny<NotificationDto>()), Times.Never);
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

    public TestContext TestContext { get; set; } = null!;
}
