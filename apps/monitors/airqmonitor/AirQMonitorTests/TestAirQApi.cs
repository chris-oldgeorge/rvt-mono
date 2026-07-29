using AirQ.Api;
using AirQ.Api.Db;
using AirQ.Api.Http;
using AirQ.Model.Dto;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
namespace AirQMonitorTests
{
    [TestClass]
    public class TestAirQApi
    {

        public TestAirQApi()
        {
            ILoggerFactory factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestAirQApi");
        }

        [TestMethod]
        public async Task TestStoreMonitors_Success()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);


            httpClient.Setup(c => c.GetAsync("/instrumentList?userID=foo&token=bar", It.IsAny<CancellationToken>())).
                    Returns(Task<string>.Factory.StartNew(() => AirQFixture.InstrumentsResponseJson(), TestContext.CancellationToken));


            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/latestMetaData\\?userID=foo&token=bar&instrumentID=*"), It.IsAny<CancellationToken>())).
                    Returns(Task<string>.Factory.StartNew(() => AirQFixture.MetaDataResponseJson(), TestContext.CancellationToken));

            await testObj.StoreMonitorsAsync("foo", "bar", TestContext.CancellationToken);

            httpClient.Verify(c => c.GetAsync("/instrumentList?userID=foo&token=bar", It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device1", It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device2", It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device3", It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            List<NoiseMonitorDto> expected = AirQFixture.MonitorDtos(DateTime.UtcNow, NoiseMonitorStatus.ACTIVE);
            dbClient.Verify(c => c.WriteMonitorList(
                            It.Is<List<NoiseMonitorDto>>(
                                l => TestUtil.AreEqual(expected, l))), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreMonitors_EmptyMetadataStillWritesEveryMonitor()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            httpClient.Setup(c => c.GetAsync("/instrumentList?userID=foo&token=bar", It.IsAny<CancellationToken>()))
                .ReturnsAsync(AirQFixture.InstrumentsResponseJson());
            httpClient.Setup(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(AirQFixture.MetaDataResponseJson());
            httpClient.Setup(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device2", It.IsAny<CancellationToken>()))
                .ReturnsAsync("[]");
            httpClient.Setup(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device3", It.IsAny<CancellationToken>()))
                .ReturnsAsync(AirQFixture.MetaDataResponseJson());

            await testObj.StoreMonitorsAsync("foo", "bar", TestContext.CancellationToken);

            dbClient.Verify(client => client.WriteMonitorList(It.Is<List<NoiseMonitorDto>>(monitors =>
                monitors.Count == 3 &&
                monitors.Single(monitor => monitor.SerialId == "Device2").MonitorStatus.BatteryVoltage == null)), Times.Once);
            httpClient.Verify(client => client.GetAsync("/instrumentList?userID=foo&token=bar", It.IsAny<CancellationToken>()), Times.Once);
            httpClient.Verify(client => client.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device1", It.IsAny<CancellationToken>()), Times.Once);
            httpClient.Verify(client => client.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device2", It.IsAny<CancellationToken>()), Times.Once);
            httpClient.Verify(client => client.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device3", It.IsAny<CancellationToken>()), Times.Once);
            httpClient.VerifyNoOtherCalls();
            dbClient.VerifyNoOtherCalls();
            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestCheckForOfflineMonitors_MonitorsOfflineFor23Hours_Success()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                     out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

            List<RvtAlertRuleDto> rules = AirQFixture.OfflineRules();
            dbClient.Setup(c => c.ReadRules(null)).Returns(rules);
            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<DateTime?>())).
                Returns([]);

            await testObj.CheckForOfflineMonitorsAsync(TestContext.CancellationToken);

            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadRules(null), Times.Exactly(1));
            dbClient.Verify(c => c.ReadMonitorList(It.IsAny<DateTime?>()), Times.Exactly(1));

            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }



        [DataRow(25 * 60, 3600)]
        [DataRow(24 * 60, 0)]
        [DataRow((24 * 60) + 1, 60)]
        [TestMethod]
        public async Task TestCheckForOfflineMonitors_NotificationWrittenOk_Success(int minutesOffline, int offlineForSeconds)
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                     out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

            List<RvtAlertRuleDto> rules = AirQFixture.OfflineRules();
            dbClient.Setup(c => c.ReadRules(null)).Returns(rules);

            List<NoiseMonitorDto> monitors = AirQFixture.MonitorDtos(DateTime.UtcNow.AddMinutes(-minutesOffline), NoiseMonitorStatus.ACTIVE);
            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<DateTime?>())).
                Returns(monitors);

            await testObj.CheckForOfflineMonitorsAsync(TestContext.CancellationToken);

            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadRules(null), Times.Exactly(1));
            dbClient.Verify(c => c.ReadMonitorList(It.IsAny<DateTime?>()), Times.Exactly(1));

            foreach (NoiseMonitorDto m in monitors)
            {
                // The durable stack owns notification persistence, contact
                // fan-out, and audits; the handler's contract is the signal.
                messageClient.Verify(c => c.AcceptAsync(
                    It.Is<AlertSignal>(signal =>
                        signal.SerialId == m.SerialId &&
                        signal.AlertType == AlertType.Offline &&
                        signal.AveragingPeriod == 60 * 60 * 24 &&
                        signal.Level == offlineForSeconds &&
                        signal.Field == rules[0].Field &&
                        signal.DeliveryChannels == (AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms)),
                    It.IsAny<CancellationToken>()), Times.Exactly(1));
                dbClient.Verify(c => c.SetMonitorOffline(m.Id, true), Times.Exactly(1));
            }
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        public TestContext TestContext { get; set; } = null!;
    }
}
