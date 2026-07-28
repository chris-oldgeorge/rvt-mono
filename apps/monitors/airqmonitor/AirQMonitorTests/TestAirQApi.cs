using AirQ.Api;
using AirQ.Api.Db;
using AirQ.Api.Http;
using AirQ.Model.Dto;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Rules.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
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
                                                     out Mock<IMessageService> messageService);


            httpClient.Setup(c => c.GetAsync("/instrumentList?userID=foo&token=bar", It.IsAny<CancellationToken>())).
                    Returns(Task<string>.Factory.StartNew(() => AirQFixture.InstrumentsResponseJson()));


            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/latestMetaData\\?userID=foo&token=bar&instrumentID=*"), It.IsAny<CancellationToken>())).
                    Returns(Task<string>.Factory.StartNew(() => AirQFixture.MetaDataResponseJson()));

            await testObj.StoreMonitorsAsync("foo", "bar");

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
            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreMonitors_EmptyMetadataStillWritesEveryMonitor()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            httpClient.Setup(c => c.GetAsync("/instrumentList?userID=foo&token=bar", It.IsAny<CancellationToken>()))
                .ReturnsAsync(AirQFixture.InstrumentsResponseJson());
            httpClient.Setup(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(AirQFixture.MetaDataResponseJson());
            httpClient.Setup(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device2", It.IsAny<CancellationToken>()))
                .ReturnsAsync("[]");
            httpClient.Setup(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device3", It.IsAny<CancellationToken>()))
                .ReturnsAsync(AirQFixture.MetaDataResponseJson());

            await testObj.StoreMonitorsAsync("foo", "bar");

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
            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestCheckForOfflineMonitors_MonitorsOfflineFor23Hours_Success()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                     out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageService);

            List<RvtAlertRuleDto> rules = AirQFixture.OfflineRules();
            dbClient.Setup(c => c.ReadRules(null)).Returns(rules);
            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<DateTime?>())).
                Returns(new List<NoiseMonitorDto>());

            await testObj.CheckForOfflineMonitorsAsync();

            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadRules(null), Times.Exactly(1));
            dbClient.Verify(c => c.ReadMonitorList(It.IsAny<DateTime?>()), Times.Exactly(1));

            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageService.VerifyNoOtherCalls();
        }



        [DataRow(25 * 60, 3600)]
        [DataRow(24 * 60, 0)]
        [DataRow((24 * 60) + 1, 60)]
        [TestMethod]
        public async Task TestCheckForOfflineMonitors_NotificationWrittenOk_Success(int minutesOffline, int offlineForSeconds)
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                     out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageService);

            List<RvtAlertRuleDto> rules = AirQFixture.OfflineRules();
            dbClient.Setup(c => c.ReadRules(null)).Returns(rules);

            List<NoiseMonitorDto> monitors = AirQFixture.MonitorDtos(DateTime.UtcNow.AddMinutes(-minutesOffline), NoiseMonitorStatus.ACTIVE);
            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<DateTime?>())).
                Returns(monitors);

            List<RvtContactDto> contacts = AirQFixture.AlertContacts();
            dbClient.Setup(c => c.ReadAlertContacts(It.IsAny<Guid>(), out It.Ref<Guid>.IsAny)).Returns(contacts);

            await testObj.CheckForOfflineMonitorsAsync();

            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadRules(null), Times.Exactly(1));
            dbClient.Verify(c => c.ReadMonitorList(It.IsAny<DateTime?>()), Times.Exactly(1));

            dbClient.Verify(c => c.WriteNotificationAudit(It.IsAny<Guid>(), "baz@bob.org", "Sent ok"),
                Times.Exactly(monitors.Count));

            foreach (NoiseMonitorDto m in monitors)
            {
                dbClient.Verify(c => c.WriteNotification(It.Is<NotificationDto>(
                    n => n.MonitorId == m.Id &&
                         n.AveragingPeriod == 60 * 60 * 24 &&
                         n.Level == offlineForSeconds &&
                         n.AlertType == AlertType.Offline &&
                         n.AlertField.Equals(rules[0].Field)
                         )), Times.Exactly(1));
                dbClient.Verify(c => c.ReadAlertContacts(m.Id, out It.Ref<Guid>.IsAny), Times.Exactly(1));
                dbClient.Verify(c => c.SetMonitorOffline(m.Id, true), Times.Exactly(1));
                //dbClient.Verify(c=>c.WriteNotification)
            }
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            //Need to add new test here !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            //emailClient.Verify(c => c.SendMessage(ContactMethod.Email, AlertType.Offline,
            //             "baz@bob.org", It.IsAny<string?>(), It.IsAny<string>()), Times.Exactly(monitors.Count));

            //emailClient.VerifyNoOtherCalls();
        }

    }
}
