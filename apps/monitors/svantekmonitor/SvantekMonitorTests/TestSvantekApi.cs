using Svantek.Api.Db;
using Svantek.Api.Http;
using Svantek.Model.Dto;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;


using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Rules.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace SvantekMonitorTests
{
    [TestClass]
    public class TestSvantekApi
    {

        public TestSvantekApi()
        {
            var factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestSvantekApi");
        }

        [TestMethod]
        public void TestStoreMonitors_Success()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);


            httpClient.Setup(c => c.GetAsync("/instrumentList?userID=foo&token=bar")).
                    Returns(Task<string>.Factory.StartNew(() => SvantekFixture.InstrumentsResponseJson()));


            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/latestMetaData\\?userID=foo&token=bar&instrumentID=*"))).
                    Returns(Task<string>.Factory.StartNew(() => SvantekFixture.MetaDataResponseJson()));

            testObj.StoreMonitors("foo", "bar");

            httpClient.Verify(c => c.GetAsync("/instrumentList?userID=foo&token=bar"), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device1"), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device2"), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device3"), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            var expected = SvantekFixture.MonitorDtos(DateTime.UtcNow, NoiseMonitorStatus.ACTIVE);
            dbClient.Verify(c => c.WriteMonitorList(
                            It.Is<List<NoiseMonitorDto>>(
                                l => TestUtil.AreEqual(expected, l))), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestCheckForOfflineMonitors_MonitorsOfflineFor23Hours_Success()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                     out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageService);

            var rules = SvantekFixture.OfflineRules();
            dbClient.Setup(c => c.ReadRules(null)).Returns(rules);
            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<DateTime?>())).
                Returns(new List<NoiseMonitorDto>());

            testObj.CheckForOfflineMonitors();

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
        [DataTestMethod]
        public void TestCheckForOfflineMonitors_NotificationWrittenOk_Success(int minutesOffline, int offlineForSeconds)
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                     out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageService);

            var rules = SvantekFixture.OfflineRules();
            dbClient.Setup(c => c.ReadRules(null)).Returns(rules);

            var monitors = SvantekFixture.MonitorDtos(DateTime.UtcNow.AddMinutes(-minutesOffline), NoiseMonitorStatus.ACTIVE);
            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<DateTime?>())).
                Returns(monitors);

            var contacts = SvantekFixture.AlertContacts();
            dbClient.Setup(c => c.ReadAlertContacts(It.IsAny<Guid>(), out It.Ref<Guid>.IsAny)).Returns(contacts);

            testObj.CheckForOfflineMonitors();

            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadRules(null), Times.Exactly(1));
            dbClient.Verify(c => c.ReadMonitorList(It.IsAny<DateTime?>()), Times.Exactly(1));

            dbClient.Verify(c => c.WriteNotificationAudit(It.IsAny<Guid>(), "baz@bob.org", "Sent ok"),
                Times.Exactly(monitors.Count));

            foreach (var m in monitors)
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
