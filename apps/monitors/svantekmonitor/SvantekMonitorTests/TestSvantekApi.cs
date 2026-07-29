using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Svantek.Api;
using Svantek.Api.Db;
using Svantek.Api.Http;
using Svantek.Model.Dto;
using SvantekMonitor.model.dto;
namespace SvantekMonitorTests;

// Summary: Facade-level tests for the scheduled Svantek jobs; handler-driven alerts are
// pinned as durable AlertSignals (email + SMS channels) instead of in-process sends.
// Major updates:
// - 2026-07-29 Legacy retirement step 4: rewritten from the synchronous StoreMonitors /
//   CheckForOfflineMonitors suite onto the async facade and IAlertIngressPort contract.
[TestClass]
public class TestSvantekApi
{
    private const string _projectsJson = """
        {
          "status": "ok",
          "projects": [
            {
              "id": "7",
              "project_name": "first",
              "stations": [
                { "point_id": "1", "serial": "1001", "short_name": "one" },
                { "point_id": "2", "serial": "1002", "short_name": "two" },
                { "point_id": "3", "serial": "1003", "short_name": "three" }
              ]
            }
          ]
        }
        """;

    private const string _stationsJson = """
        {
          "status": "ok",
          "stations": [
            { "serial": 1001, "type": "SV307" },
            { "serial": 1002, "type": "SV307" },
            { "serial": 1003, "type": "SV307" }
          ]
        }
        """;

    public TestSvantekApi()
    {
        ILoggerFactory factory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
        });
        RvtLogger.CreateLogger(factory, "TestSvantekApi");
    }

    [TestMethod]
    public async Task TestStoreMonitors_Success()
    {
        SvantekApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IAlertIngressPort> messageClient);

        httpClient.Setup(c => c.PostAsync("projects-get-data.php", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())).
                ReturnsAsync(_projectsJson);
        httpClient.Setup(c => c.PostAsync("stations-get-list.php", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())).
                ReturnsAsync(_stationsJson);

        await testObj.StoreMonitorsAsync();

        httpClient.Verify(c => c.PostAsync("projects-get-data.php", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
        httpClient.Verify(c => c.PostAsync("stations-get-list.php", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
        httpClient.VerifyNoOtherCalls();

        dbClient.Verify(c => c.WriteMonitorListAsync(
                        It.Is<IReadOnlyList<NoiseMonitorDto>>(l =>
                            l.Count == 3 &&
                            l[0].SerialId == "1001" &&
                            l[1].SerialId == "1002" &&
                            l[2].SerialId == "1003" &&
                            l.All(m => m.ProjectId == 7 && m.Model == "SV307")),
                        It.IsAny<CancellationToken>()), Times.Exactly(1));
        dbClient.VerifyNoOtherCalls();

        mqttClient.VerifyNoOtherCalls();
        messageClient.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestCheckForOfflineMonitors_NoMonitors_Success()
    {
        SvantekApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                 out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

        List<RvtAlertRuleDto> rules = SvantekFixture.OfflineRules();
        dbClient.Setup(c => c.ReadRules(null)).Returns(rules);
        dbClient.Setup(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>())).
            ReturnsAsync([]);

        await testObj.CheckForOfflineMonitorsAsync();

        httpClient.VerifyNoOtherCalls();

        dbClient.Verify(c => c.ReadRules(null), Times.Exactly(1));
        dbClient.Verify(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>()), Times.Exactly(1));
        dbClient.VerifyNoOtherCalls();

        mqttClient.VerifyNoOtherCalls();
        messageClient.VerifyNoOtherCalls();
    }

    [DataRow(25 * 60, 3600)]
    [DataRow(24 * 60, 0)]
    [DataRow((24 * 60) + 1, 60)]
    [TestMethod]
    public async Task TestCheckForOfflineMonitors_SignalsOfflineAlertAndMarksOffline_Success(int minutesOffline, int offlineForSeconds)
    {
        SvantekApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                 out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

        List<RvtAlertRuleDto> rules = SvantekFixture.OfflineRules();
        dbClient.Setup(c => c.ReadRules(null)).Returns(rules);

        List<NoiseMonitorReadDto> monitors = SvantekFixture.ReadMonitorDtos(DateTime.UtcNow.AddMinutes(-minutesOffline));
        dbClient.Setup(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>())).
            ReturnsAsync(monitors);

        await testObj.CheckForOfflineMonitorsAsync();

        httpClient.VerifyNoOtherCalls();

        dbClient.Verify(c => c.ReadRules(null), Times.Exactly(1));
        dbClient.Verify(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>()), Times.Exactly(1));

        foreach (NoiseMonitorReadDto m in monitors)
        {
            // The durable stack owns notification persistence, contact
            // fan-out, and audits; the handler's contract is the signal.
            messageClient.Verify(c => c.AcceptAsync(
                It.Is<AlertSignal>(signal =>
                    signal.SerialId == m.SerialId &&
                    signal.AlertType == AlertType.Offline &&
                    signal.AveragingPeriod == 60 * 60 * 24 &&
                    signal.Level >= offlineForSeconds &&
                    signal.Level <= offlineForSeconds + 1 &&
                    signal.Field == rules[0].Field &&
                    signal.DeliveryChannels == (AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms)),
                It.IsAny<CancellationToken>()), Times.Exactly(1));
            dbClient.Verify(c => c.SetMonitorOfflineAsync(m.Id, true, It.IsAny<CancellationToken>()), Times.Exactly(1));
        }
        dbClient.VerifyNoOtherCalls();

        mqttClient.VerifyNoOtherCalls();
        messageClient.VerifyNoOtherCalls();
    }

    [DataRow(5, 10.0, (byte)1, AlertType.BatteryAlert)]
    [DataRow(15, 20.0, (byte)2, AlertType.BatteryCaution)]
    [TestMethod]
    public async Task TestNotifyBatteryLevels_SignalsBatteryAlertAndStoresStatus_Success(
        int batteryCharge, double threshold, byte expectedStatus, AlertType expectedAlertType)
    {
        SvantekApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                 out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

        NoiseMonitorReadDto monitor = SvantekFixture.ReadMonitorDto("Device1", batteryCharge: batteryCharge);
        dbClient.Setup(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>())).
            ReturnsAsync([monitor]);

        await testObj.NotifyBatteryLevelsAsync();

        httpClient.VerifyNoOtherCalls();

        dbClient.Verify(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>()), Times.Exactly(1));
        dbClient.Verify(c => c.SetMonitorBatteryStatusAsync(monitor.Id, expectedStatus, It.IsAny<CancellationToken>()), Times.Exactly(1));
        dbClient.VerifyNoOtherCalls();

        messageClient.Verify(c => c.AcceptAsync(
            It.Is<AlertSignal>(signal =>
                signal.SerialId == monitor.SerialId &&
                signal.AlertType == expectedAlertType &&
                signal.Field == "Battery level" &&
                signal.Level == batteryCharge &&
                signal.Limit == threshold &&
                signal.AveragingPeriod == 0 &&
                signal.DeliveryChannels == (AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms)),
            It.IsAny<CancellationToken>()), Times.Exactly(1));
        messageClient.VerifyNoOtherCalls();

        mqttClient.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestNotifyBatteryLevels_HealthyBatteryResetsStatusWithoutSignal_Success()
    {
        SvantekApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                 out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

        NoiseMonitorReadDto monitor = SvantekFixture.ReadMonitorDto(
            "Device1",
            batteryCharge: 80,
            batteryStatus: SvantekApi.BatteryAlertType.BatteryAlert);
        dbClient.Setup(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>())).
            ReturnsAsync([monitor]);

        await testObj.NotifyBatteryLevelsAsync();

        httpClient.VerifyNoOtherCalls();

        dbClient.Verify(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>()), Times.Exactly(1));
        dbClient.Verify(c => c.SetMonitorBatteryStatusAsync(monitor.Id, 0, It.IsAny<CancellationToken>()), Times.Exactly(1));
        dbClient.VerifyNoOtherCalls();

        mqttClient.VerifyNoOtherCalls();
        messageClient.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestNotifyBatteryLevels_UnchangedStatus_EmitsNothing_Success()
    {
        SvantekApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                 out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

        NoiseMonitorReadDto monitor = SvantekFixture.ReadMonitorDto(
            "Device1",
            batteryCharge: 5,
            batteryStatus: SvantekApi.BatteryAlertType.BatteryAlert);
        dbClient.Setup(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>())).
            ReturnsAsync([monitor]);

        await testObj.NotifyBatteryLevelsAsync();

        httpClient.VerifyNoOtherCalls();

        dbClient.Verify(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>()), Times.Exactly(1));
        dbClient.VerifyNoOtherCalls();

        mqttClient.VerifyNoOtherCalls();
        messageClient.VerifyNoOtherCalls();
    }

    [DataRow(10.0, 11.0, 1)]
    [DataRow(11.0, 10.0, 0)]
    [TestMethod]
    public async Task TestNotifySiteAverages_Success(double limit, double level, int numExpectedSignals)
    {
        SvantekApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                 out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

        DateTime date = new(2023, 10, 2);
        Guid siteId = Guid.NewGuid();
        SiteMonitorsWithSiteHoursDto monitor = new(
            Guid.NewGuid(),
            "123",
            "Device1",
            0,
            false,
            siteId,
            "Site",
            TimeSpan.Parse("09:00:00"),
            TimeSpan.Parse("17:00:00"));
        dbClient.Setup(c => c.ReadSiteMonitorsWithSiteHoursAsync(date, It.IsAny<CancellationToken>())).
            ReturnsAsync([monitor]);

        RvtAlertRuleDto rule = new(Guid.NewGuid(), "Device1", "LAeq", limit, limit - 1, 0,
                                   SvantekFixture.CreateActiveRuleActivity(null, null),
                                   AlertType.Caution, false, false, DateTime.UtcNow, null);
        dbClient.Setup(c => c.ReadRules("Device1")).Returns([rule]);
        dbClient.Setup(c => c.GetAverageNoiseLevel("Device1", "LAeq", date + monitor.StartTime!.Value, date + monitor.EndTime!.Value)).
            Returns(level);

        await testObj.NotifySiteAveragesAsync(date);

        httpClient.VerifyNoOtherCalls();

        dbClient.Verify(c => c.ReadSiteMonitorsWithSiteHoursAsync(date, It.IsAny<CancellationToken>()), Times.Exactly(1));
        dbClient.Verify(c => c.GetAverageNoiseLevel("Device1", "LAeq", date + monitor.StartTime!.Value, date + monitor.EndTime!.Value),
            Times.Exactly(1));
        dbClient.Verify(c => c.WriteDailyAverageAsync(siteId, monitor.Id, "lAeq", level, date, It.IsAny<CancellationToken>()),
            Times.Exactly(1));
        dbClient.Verify(c => c.ReadRules("Device1"), Times.Exactly(1));
        dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, "Device1", "LAeq", true))),
            Times.Exactly(numExpectedSignals));
        dbClient.VerifyNoOtherCalls();

        messageClient.Verify(c => c.AcceptAsync(
            It.Is<AlertSignal>(signal =>
                signal.SerialId == "Device1" &&
                signal.AlertType == rule.AlertType &&
                signal.Field == "LAeq" &&
                signal.Level == level &&
                signal.Limit == limit &&
                signal.AveragingPeriod == 0 &&
                signal.DeliveryChannels == (AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms)),
            It.IsAny<CancellationToken>()), Times.Exactly(numExpectedSignals));
        messageClient.VerifyNoOtherCalls();

        mqttClient.VerifyNoOtherCalls();
    }
}
