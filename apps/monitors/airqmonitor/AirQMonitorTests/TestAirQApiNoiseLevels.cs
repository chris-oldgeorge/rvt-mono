using System.Globalization;
using AirQ.Api.Db;
using AirQ.Api.Http;
using AirQ.Model.Dto;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Communications;
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
    public class TestAirQApiNoiseLevels
    {
        public TestAirQApiNoiseLevels()
        {
            var factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestAirQApiNoiseLevels");
        }

        [TestMethod]
        public void TestStoreNoiseLevels_EmptyRules_Success()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageService);

            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/latestData\\?userID=foo&token=bar&instrumentID=*"))).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.SamplesResponseJson()));

            var monitors = AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE);
            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(monitors);
            dbClient.Setup(c => c.ReadRules(It.IsAny<string>())).
                Returns(new List<RvtAlertRuleDto>());

            testObj.StoreNoiseLevels("foo", "bar");

            httpClient.Verify(c => c.GetAsync(It.Is<string>(s => s.StartsWith("/latestData?userID=foo"))), Times.Exactly(3));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos("Device1", It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos("Device2", It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos("Device3", It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.Verify(c => c.WriteLatestTimestamp(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Exactly(3));
            dbClient.Verify(c => c.ReadRules("Device1"), Times.Exactly(1));
            dbClient.Verify(c => c.ReadRules("Device2"), Times.Exactly(1));
            dbClient.Verify(c => c.ReadRules("Device3"), Times.Exactly(1));

            dbClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(3));
            mqttClient.VerifyNoOtherCalls();

            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreNoiseLevels_TruncatedByTimestamp_Success()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageService);

            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/latestData\\?userID=foo&token=bar&instrumentID=*"))).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.SamplesResponseJson()));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(DateTime.UtcNow, NoiseMonitorStatus.ACTIVE));

            testObj.StoreNoiseLevels("foo", "bar");

            httpClient.Verify(c => c.GetAsync(It.Is<string>(s => s.StartsWith("/latestData?userID=foo"))), Times.Exactly(3));
            httpClient.VerifyNoOtherCalls();
            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();

            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreNoiseLevelsForYesterday_Success()
        {

            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageService);

            var yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/dataForDate\\?userID=foo&date=" + yesterday + "&token=bar&instrumentID=*"))).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.DateSamplesResponseJson()));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            testObj.StoreAllNoiseLevelsForYesterday("foo", "bar");

            httpClient.Verify(c => c.GetAsync(It.Is<string>(s => s.StartsWith("/dataForDate?userID=foo"))), Times.Exactly(3));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos("Device1", It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos("Device2", It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos("Device3", It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();

            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreNoiseLevelsForDate_Success()
        {

            var dateStr = "2023-09-11";
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageService);

            var regex =
            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/dataForDate\\?userID=foo&date=" + dateStr + "&token=bar&instrumentID=*"))).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.DateSamplesResponseJson()));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            testObj.StoreNoiseLevelsForDate("foo", "bar", dateStr);
            httpClient.Verify(c => c.GetAsync(It.Is<string>(s => s.StartsWith("/dataForDate?userID="))), Times.Exactly(3));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos("Device1", It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos("Device2", It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos("Device3", It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();

            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void GetSamplesForDate_EncodesEveryDynamicQueryParameter()
        {
            var httpClient = new Mock<IHttpClient>(MockBehavior.Strict);
            httpClient.Setup(client => client.GetAsync(It.IsAny<string>())).ReturnsAsync("[]");
            var gateway = new AirQHttpGateway(httpClient.Object);

            gateway.GetSamplesForDate(
                "vendor user&admin=true",
                "vendor token&role=admin",
                "serial&override=true",
                "2026-07-14&token=attacker");

            httpClient.Verify(client => client.GetAsync(
                "/dataForDate?userID=vendor%20user%26admin%3Dtrue&date=2026-07-14%26token%3Dattacker&token=vendor%20token%26role%3Dadmin&instrumentID=serial%26override%3Dtrue"),
                Times.Once);
            httpClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreNoiseLevelsInactiveMonitor_Success()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageService);

            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/latestData\\?userID=foo&token=bar&instrumentID=*"))).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.SamplesResponseJson()));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(null, "Inactive"));

            testObj.StoreNoiseLevels("foo", "bar");

            httpClient.Verify(c => c.GetAsync(It.Is<string>(s => s.StartsWith("/dataForDate?userID="))), Times.Exactly(0));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();

            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreNoiseLevelsForDateInactiveMonitor_Success()
        {

            var dateStr = "2023-09-11";
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageService);

            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/dataForDate\\?userID=foo&date=" + dateStr + "&token=bar&instrumentID=*"))).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.SamplesResponseJson()));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(null, "Inactive"));

            testObj.StoreNoiseLevelsForDate("foo", "bar", dateStr);

            httpClient.Verify(c => c.GetAsync(It.Is<string>(s => s.StartsWith("/dataForDate?userID="))), Times.Exactly(0));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();

            messageService.VerifyNoOtherCalls();
        }

        // don't have time to remock this test
        //[DataRow(10.0, 11.0, 1)]
        //[DataRow(11.0, 10.0, 0)]
        //[DataTestMethod]
        //public void TestNotifySiteAverages_Success(double limit, double level, int numExpectedNotifications)
        //{

        //    var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
        //                                 out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageService);

        //    var monitors = AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE);
        //    dbClient.Setup(c => c.ReadMonitorList(null)).Returns(monitors);

        //    var rules = AirQFixture.NotifyRules(monitors[0].SerialId, "LA90",
        //                                                  limit);
        //    dbClient.Setup(c => c.ReadRules(monitors[0].SerialId)).Returns(rules);
        //    dbClient.Setup(c => c.ReadRules(monitors[1].SerialId)).Returns(new List<RvtAlertRuleDto>());
        //    dbClient.Setup(c => c.ReadRules(monitors[2].SerialId)).Returns(new List<RvtAlertRuleDto>());

        //    dbClient.Setup(c => c.GetAverageNoiseLevel(It.IsAny<string>(), It.IsAny<string>(),
        //        It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(level);

        //    var contacts = AirQFixture.AlertContacts();

        //    dbClient.Setup(c => c.ReadAlertContacts(It.IsAny<string>(), out It.Ref<Guid>.IsAny)).
        //        Returns(contacts);

        //    var siteInfo = new SiteInfoDto(siteId: Guid.NewGuid(),
        //                                   startTime: TimeSpan.Parse("09:00:00"),
        //                                   endTime: TimeSpan.Parse("17:00:00"),
        //                                   satStartTime: TimeSpan.Parse("09:40:12"),
        //                                   satEndTime: TimeSpan.Parse("16:00:00"),
        //                                   sunStartTime: TimeSpan.Parse("09:59:48"),
        //                                   sunEndTime: TimeSpan.Parse("15:00:00"));

        //    dbClient.Setup(c => c.ReadSiteInfo(It.IsAny<Guid>())).Returns(siteInfo);
        //    testObj.NotifySiteAverages(DateTime.Today.AddDays(-1));

        //    dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
        //    dbClient.Verify(c => c.ReadRules(monitors[0].SerialId), Times.Exactly(1));
        //    dbClient.Verify(c => c.ReadRules(monitors[1].SerialId), Times.Exactly(1));
        //    dbClient.Verify(c => c.ReadRules(monitors[2].SerialId), Times.Exactly(1));
        //    dbClient.Verify(c => c.ReadAlertContacts(monitors[0].SerialId, out It.Ref<Guid>.IsAny),
        //        Times.Exactly(1));
        //    dbClient.Verify(c => c.ReadSiteInfo(It.IsAny<Guid>()), Times.Exactly(1));
        //    dbClient.Verify(c => c.GetAverageNoiseLevel(monitors[0].SerialId, "LA90",
        //                                                It.IsAny<DateTime>(), It.IsAny<DateTime>()),
        //                                                Times.Exactly(1));
        //    dbClient.Verify(c => c.WriteNotification(It.IsAny<NotificationDto>()),
        //        Times.Exactly(numExpectedNotifications));
        //    dbClient.Verify(c => c.WriteNotificationAudit(It.IsAny<Guid>(), contacts[0].EmailAddress, NotificationConstants.SENT_OK),
        //        Times.Exactly(numExpectedNotifications));
        //    dbClient.Verify(c => c.WriteDailyAverage(siteInfo.SiteId, monitors[0].Id, "LA90", level, It.IsAny<DateTime>()),
        //        Times.Exactly(1));
        //    dbClient.VerifyNoOtherCalls();

        //    httpClient.VerifyNoOtherCalls();

        //    mqttClient.Verify(c => c.ConnectAsync(), Times.Exactly(1));
        //    mqttClient.VerifyNoOtherCalls();

        //    commsClient.Verify(c => c.SendMessage(ContactMethod.Email, AlertType.Caution, contacts[0].EmailAddress, null, It.IsAny<string>()),
        //        Times.Exactly(numExpectedNotifications));
        //    commsClient.VerifyNoOtherCalls();

        //}
    }
}
