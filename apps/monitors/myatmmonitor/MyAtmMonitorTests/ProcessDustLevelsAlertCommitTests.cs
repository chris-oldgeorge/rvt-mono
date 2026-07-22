using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Model.Dto;
using MyAtm.Model.Json;
using Rvt.Monitor.Common.Communications;
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
        var httpClient = new Mock<IHttpClient>();
        var dbClient = new Mock<IDBClient>();
        var mqttClient = new Mock<IMqttClient>();
        var messageService = new Mock<IMessageService>();
        var customerId = 656;
        var now = DateTime.UtcNow;
        var monitor = MyAtmFixture.CustomerDeviceDtos(now.AddDays(1), singleItem: true).Single();
        var activity = new AlertActivityTimeDto { Weekdays = true, Saturdays = true, Sundays = true };
        var activeAlert = new RvtAlertRuleDto(
            Guid.NewGuid(), monitor.SerialId, "Pm2_5", 10, 8, 8 * 60 * 60,
            activity, AlertType.Alert, true, false, now, null);
        var caution = new RvtAlertRuleDto(
            Guid.NewGuid(), monitor.SerialId, "pm2.5", 10, 8, 8 * 60 * 60,
            activity, AlertType.Caution, false, false, now.AddDays(-1), null);
        var commits = new List<MyAtmAlertCommit>();

        dbClient.Setup(client => client.ReadRules(Period.Hours8)).Returns([activeAlert, caution]);
        dbClient.Setup(client => client.ReadMonitor(customerId, monitor.SerialId)).Returns(monitor);
        dbClient.Setup(client => client.GetAverageDustLevel(
                monitor.SerialId, "pm2.5", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(12);
        dbClient.Setup(client => client.CommitAlertAsync(It.IsAny<MyAtmAlertCommit>(), It.IsAny<CancellationToken>()))
            .Callback<MyAtmAlertCommit, CancellationToken>((commit, _) => commits.Add(commit))
            .ReturnsAsync(new MyAtmAlertCommitResult(true, Array.Empty<MonitorDeliveryRequest>()));

        var api = new MyAtmApi(httpClient.Object, dbClient.Object, mqttClient.Object, messageService.Object, false);

        await api.ProcessDustLevelsAsync<AvgDeviceMeasurement>(customerId, Period.Hours8);

        Assert.IsNotEmpty(commits);
        Assert.IsTrue(commits.All(commit => commit.Occurrences.Count == 0));
    }

    [TestMethod]
    public async Task CompletedAggregatePeriod_CommitsStateOccurrenceAndAllDurableDeliveriesOnce()
    {
        var httpClient = new Mock<IHttpClient>();
        var dbClient = new Mock<IDBClient>();
        var mqttClient = new Mock<IMqttClient>();
        var messageService = new Mock<IMessageService>();
        var customerId = 656;
        var monitor = MyAtmFixture.CustomerDeviceDtos(DateTime.UtcNow.AddDays(1), singleItem: true).Single();
        var rule = new RvtAlertRuleDto(
            Guid.NewGuid(), monitor.SerialId, "Pm10", 10, 8, 8 * 60 * 60,
            new AlertActivityTimeDto { Weekdays = true, Saturdays = true, Sundays = true },
            AlertType.Alert, false, false, DateTime.UtcNow.AddHours(-12), null);
        var contacts = new List<RvtContactDto>
        {
            new(ContactMethod.Email, "aggregate@example.test", (string?)null, true, false, null, null),
            new(ContactMethod.SMS, string.Empty, "15551234567", false, true, null, null)
        };
        MyAtmAlertCommit? commit = null;

        dbClient.Setup(client => client.ReadRules(Period.Hours8)).Returns([rule]);
        dbClient.Setup(client => client.ReadMonitor(customerId, monitor.SerialId)).Returns(monitor);
        dbClient.Setup(client => client.GetAverageDustLevel(monitor.SerialId, "Pm10", It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(12);
        dbClient.Setup(client => client.ReadAlertContacts(monitor.Id)).Returns(contacts);
        dbClient.Setup(client => client.CommitAlertAsync(It.IsAny<MyAtmAlertCommit>(), It.IsAny<CancellationToken>()))
            .Callback<MyAtmAlertCommit, CancellationToken>((value, _) => commit = value)
            .ReturnsAsync(new MyAtmAlertCommitResult(true, Array.Empty<MonitorDeliveryRequest>()));

        var api = new MyAtmApi(httpClient.Object, dbClient.Object, mqttClient.Object, messageService.Object, false);

        await api.ProcessDustLevelsAsync<AvgDeviceMeasurement>(customerId, Period.Hours8);

        Assert.IsNotNull(commit);
        Assert.HasCount(1, commit.RuleStateMutations);
        Assert.AreEqual(rule.RuleId, commit.RuleStateMutations[0].RuleId);
        Assert.IsFalse(commit.RuleStateMutations[0].ExpectedIsActive);
        Assert.IsTrue(commit.RuleStateMutations[0].IsActive);
        Assert.HasCount(1, commit.Occurrences);
        var occurrence = commit.Occurrences[0];
        Assert.AreEqual(AlertType.Alert, occurrence.AlertType);
        Assert.AreEqual(12d, occurrence.Level);
        Assert.IsNotNull(occurrence.DeliveryPlan);
        var expectedNotificationId = MonitorDeliveryIdentity.CreateGuid($"notification:{occurrence.Key}");
        Assert.AreEqual(expectedNotificationId, occurrence.DeliveryPlan.Notification.Id);
        CollectionAssert.AreEquivalent(
            new[] { MonitorDeliveryKind.Email, MonitorDeliveryKind.Sms, MonitorDeliveryKind.MqttAlert },
            occurrence.DeliveryPlan.Deliveries.Select(delivery => delivery.Kind).ToArray());
        foreach (var delivery in occurrence.DeliveryPlan.Deliveries)
        {
            var expectedKey = $"{occurrence.Key}:{delivery.Kind}:{delivery.Destination}";
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
            Assert.AreEqual(AlertType.Alert, payload.AlertType);
            Assert.AreEqual("pm10", payload.Field);
            Assert.AreEqual(12d, payload.Level);
            Assert.IsNull(payload.PortalBaseUrl);
        }
        var databaseCalls = dbClient.Invocations
            .Select(invocation => invocation.Method.Name)
            .ToArray();
        CollectionAssert.AreEqual(
            new[]
            {
                "ReadRules",
                "ReadMonitor",
                "GetAverageDustLevel",
                "ReadAlertContacts",
                "CommitAlertAsync"
            },
            databaseCalls,
            $"Unexpected active aggregate DB call sequence: {string.Join(", ", databaseCalls)}");
        messageService.VerifyNoOtherCalls();
        mqttClient.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DeletedActiveRule_CommitsDeactivationWithoutWaitingForACompletedPeriod()
    {
        var httpClient = new Mock<IHttpClient>();
        var dbClient = new Mock<IDBClient>();
        var mqttClient = new Mock<IMqttClient>();
        var messageService = new Mock<IMessageService>();
        var customerId = 656;
        var monitor = MyAtmFixture.CustomerDeviceDtos(null, singleItem: true).Single();
        var rule = new RvtAlertRuleDto(
            Guid.NewGuid(), monitor.SerialId, "Pm10", 10, 8, 8 * 60 * 60,
            new AlertActivityTimeDto { Weekdays = true, Saturdays = true, Sundays = true },
            AlertType.Alert, true, true, DateTime.UtcNow, null);
        MyAtmAlertCommit? commit = null;

        dbClient.Setup(client => client.ReadRules(Period.Hours8)).Returns([rule]);
        dbClient.Setup(client => client.CommitAlertAsync(It.IsAny<MyAtmAlertCommit>(), It.IsAny<CancellationToken>()))
            .Callback<MyAtmAlertCommit, CancellationToken>((value, _) => commit = value)
            .ReturnsAsync(new MyAtmAlertCommitResult(true, Array.Empty<MonitorDeliveryRequest>()));

        var api = new MyAtmApi(httpClient.Object, dbClient.Object, mqttClient.Object, messageService.Object, false);

        await api.ProcessDustLevelsAsync<AvgDeviceMeasurement>(customerId, Period.Hours8);

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
}
