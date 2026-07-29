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
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;

namespace MyAtmMonitorTests;

// Legacy aggregate tests migrated from direct notification/state assertions to the atomic commit contract.
[TestClass]
public sealed class TestRules2
{
    [TestMethod]
    public async Task DeletedActiveAggregateRule_UsesAtomicCommitWithoutReadingTheMonitor()
    {
        Mock<IHttpClient> httpClient = new();
        Mock<IDBClient> dbClient = new();
        Mock<IMqttClient> mqttClient = new();
        Mock<INotificationDeliveryService> messageService = new();
        RvtAlertRuleDto rule = new(
            Guid.NewGuid(), "11111", "Pm10", 19, 15, 8 * 60 * 60,
            new AlertActivityTimeDto { Weekdays = true, Saturdays = true, Sundays = true },
            AlertType.Alert, true, true, DateTime.UtcNow, null);
        MyAtmAlertCommit? commit = null;
        dbClient.Setup(client => client.ReadRules(Period.Hours8)).Returns([rule]);
        dbClient.Setup(client => client.CommitAlertAsync(It.IsAny<MyAtmAlertCommit>(), It.IsAny<CancellationToken>()))
            .Callback<MyAtmAlertCommit, CancellationToken>((value, _) => commit = value)
            .ReturnsAsync(new MyAtmAlertCommitResult(true, Array.Empty<MonitorDeliveryRequest>()));
        MyAtmApi api = new(httpClient.Object, dbClient.Object, mqttClient.Object, messageService.Object, false);

        await api.ProcessDustLevelsAsync<AvgDeviceMeasurement>(656, Period.Hours8);

        Assert.IsNotNull(commit);
        Assert.IsTrue(commit.RuleStateMutations.Single().ExpectedIsActive);
        Assert.IsFalse(commit.RuleStateMutations.Single().IsActive);
        Assert.IsEmpty(commit.Occurrences);
        dbClient.Verify(client => client.ReadMonitor(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        dbClient.Verify(client => client.UpdateAlertRule(It.IsAny<RvtAlertRuleDto>()), Times.Never);
        dbClient.Verify(client => client.WriteNotification(It.IsAny<NotificationDto>()), Times.Never);
        messageService.VerifyNoOtherCalls();
        mqttClient.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DeletedInactiveAggregateRule_DoesNotNeedACommit()
    {
        Mock<IHttpClient> httpClient = new();
        Mock<IDBClient> dbClient = new();
        Mock<IMqttClient> mqttClient = new();
        Mock<INotificationDeliveryService> messageService = new();
        RvtAlertRuleDto rule = new(
            Guid.NewGuid(), "11111", "Pm10", 19, 15, 8 * 60 * 60,
            new AlertActivityTimeDto { Weekdays = true, Saturdays = true, Sundays = true },
            AlertType.Alert, false, true, DateTime.UtcNow, null);
        dbClient.Setup(client => client.ReadRules(Period.Hours8)).Returns([rule]);
        MyAtmApi api = new(httpClient.Object, dbClient.Object, mqttClient.Object, messageService.Object, false);

        await api.ProcessDustLevelsAsync<AvgDeviceMeasurement>(656, Period.Hours8);

        dbClient.Verify(client => client.CommitAlertAsync(It.IsAny<MyAtmAlertCommit>(), It.IsAny<CancellationToken>()), Times.Never);
        dbClient.Verify(client => client.ReadMonitor(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        dbClient.Verify(client => client.UpdateAlertRule(It.IsAny<RvtAlertRuleDto>()), Times.Never);
        dbClient.Verify(client => client.WriteNotification(It.IsAny<NotificationDto>()), Times.Never);
        messageService.VerifyNoOtherCalls();
        mqttClient.VerifyNoOtherCalls();
    }
}
