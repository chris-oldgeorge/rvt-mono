using System.Text.Json;
using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Model;
using MyAtm.Model.Dto;
using MyAtm.Model.Json;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;

namespace MyAtmMonitorTests;

// Summary: Verifies normal dust ingestion hands the complete, durable state transition to one command.
[TestClass]
public sealed class TestRules
{
    [TestMethod]
    public async Task StoreDustLevels_CommitIncludesMeasurementWatermarkRuleMutationAndOccurrence()
    {
        Mock<IHttpClient> httpClient = new();
        Mock<IDBClient> dbClient = new();
        Mock<IMqttClient> mqttClient = new();
        Mock<INotificationDeliveryService> messageService = new();
        DateTime sampleTime = new(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);
        DustMonitorDto monitor = MyAtmFixture.CustomerDeviceDtos(sampleTime.AddMinutes(-1), singleItem: true).Single();
        RvtAlertRuleDto rule = CreateRule(monitor, isActive: false);
        List<Rvt.Monitor.Common.Rules.RvtContactDto> contacts =
        [
            new(true, false, "alert@example.test", null, null, null)
        ];
        MyAtmDustImportCommit? commit = null;

        httpClient.Setup(client => client.GetAsync(It.IsRegex("/api/customers/656/devices/11111/measurements"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(new[]
            {
                MyAtmFixture.CreateDeviceMeasurement(sampleTime, pm1: 11, pm2_5: 0, pm10: 0)
            }));
        dbClient.Setup(client => client.ReadMonitorList(656, null)).Returns([monitor]);
        dbClient.Setup(client => client.ReadRules(monitor.SerialId, Period.Minutes1)).Returns([rule]);
        dbClient.Setup(client => client.ReadAlertContacts(monitor.Id)).Returns(contacts);
        dbClient.Setup(client => client.CommitDustImportAsync(It.IsAny<MyAtmDustImportCommit>(), It.IsAny<CancellationToken>()))
            .Callback<MyAtmDustImportCommit, CancellationToken>((value, _) => commit = value)
            .ReturnsAsync(new DustImportCommitResult(Array.Empty<MonitorDeliveryRequest>()));
        dbClient.Setup(client => client.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MonitorDeliveryMessage?)null);
        mqttClient.Setup(client => client.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), TestContext.CancellationToken)).Returns(Task.CompletedTask);

        MyAtmApi api = new(httpClient.Object, dbClient.Object, mqttClient.Object, messageService.Object, false);

        await api.StoreDustLevelsAsync<DeviceMeasurement>(656, Period.Minutes1, TestContext.CancellationToken);

        Assert.IsNotNull(commit);
        Assert.AreEqual(monitor.Id, commit.Monitor.Id);
        Assert.AreEqual(sampleTime, commit.Watermark);
        Assert.HasCount(1, commit.Measurements);
        Assert.HasCount(1, commit.RuleStateMutations);
        Assert.IsTrue(commit.RuleStateMutations[0].IsActive);
        Assert.HasCount(1, commit.AlertOccurrences);
        Assert.AreEqual(rule.RuleId, commit.AlertOccurrences[0].RuleId);
        Assert.HasCount(1, commit.AlertOccurrences[0].Contacts);

        mqttClient.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task StoreDustLevels_DeletedRuleCommitDeactivatesWithoutCreatingOccurrence()
    {
        Mock<IHttpClient> httpClient = new();
        Mock<IDBClient> dbClient = new();
        Mock<IMqttClient> mqttClient = new();
        Mock<INotificationDeliveryService> messageService = new();
        DateTime sampleTime = new(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);
        DustMonitorDto monitor = MyAtmFixture.CustomerDeviceDtos(sampleTime.AddMinutes(-1), singleItem: true).Single();
        RvtAlertRuleDto rule = CreateRule(monitor, isActive: true, isDeleted: true);
        MyAtmDustImportCommit? commit = null;

        httpClient.Setup(client => client.GetAsync(It.IsRegex("/api/customers/656/devices/11111/measurements"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(new[]
            {
                MyAtmFixture.CreateDeviceMeasurement(sampleTime, pm1: 11, pm2_5: 0, pm10: 0)
            }));
        dbClient.Setup(client => client.ReadMonitorList(656, null)).Returns([monitor]);
        dbClient.Setup(client => client.ReadRules(monitor.SerialId, Period.Minutes1)).Returns([rule]);
        dbClient.Setup(client => client.CommitDustImportAsync(It.IsAny<MyAtmDustImportCommit>(), It.IsAny<CancellationToken>()))
            .Callback<MyAtmDustImportCommit, CancellationToken>((value, _) => commit = value)
            .ReturnsAsync(new DustImportCommitResult(Array.Empty<MonitorDeliveryRequest>()));
        dbClient.Setup(client => client.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MonitorDeliveryMessage?)null);
        mqttClient.Setup(client => client.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), TestContext.CancellationToken)).Returns(Task.CompletedTask);

        MyAtmApi api = new(httpClient.Object, dbClient.Object, mqttClient.Object, messageService.Object, false);

        await api.StoreDustLevelsAsync<DeviceMeasurement>(656, Period.Minutes1, TestContext.CancellationToken);

        Assert.IsNotNull(commit);
        Assert.HasCount(1, commit.RuleStateMutations);
        Assert.IsFalse(commit.RuleStateMutations[0].IsActive);
        Assert.IsEmpty(commit.AlertOccurrences);
        dbClient.Verify(client => client.ReadAlertContacts(It.IsAny<Guid>()), Times.Never);
    }

    private static RvtAlertRuleDto CreateRule(DustMonitorDto monitor, bool isActive, bool isDeleted = false) =>
        new(
            Guid.NewGuid(),
            monitor.SerialId,
            "Pm1",
            limitOn: 10,
            limitOff: 8,
            averagingPeriod: 60,
            new Rvt.Monitor.Common.Rules.AlertActivityTimeDto { Weekdays = true, Saturdays = true, Sundays = true },
            AlertType.Alert,
            isActive,
            isDeleted,
            DateTime.UnixEpoch,
            null);

    public TestContext TestContext { get; set; } = null!;
}
