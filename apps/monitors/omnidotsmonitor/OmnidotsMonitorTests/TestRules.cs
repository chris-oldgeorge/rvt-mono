using System.Data;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using Moq;
using Omnidots.Api.Db;
using Omnidots.Api.Http;
using Omnidots.Model.Dto;
using OmnidotsAdapterTests;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using AlertActivityTimeDto = Rvt.Monitor.Common.Notifications.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Notifications.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Notifications.RvtContactDto;
namespace MyAtmMonitorTests
{

    //[TestClass]
    public class TestRules
    {
        public TestRules()
        {
            var factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestMyAtmApi");
        }

        [DataTestMethod]
        [DynamicData(nameof(OneAlertContact), DynamicDataSourceType.Method)]
        [DynamicData(nameof(TwoAlertContacts), DynamicDataSourceType.Method)]
        [DynamicData(nameof(ThreeAlertContacts), DynamicDataSourceType.Method)]
        public void TestStorePeakLevels_WithVaryingNumberOfContactsForAlertRule_Success(List<RvtContactDto> contacts)
        {

            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageClient);

            var token = "hghjadg";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)))).
                    Returns(OmnidotsFixture.AuthenticateTask(token));
            var peakRecordsUrl = string.Format("/api/v1/get_peak_records?token={0}", token);
            httpClient.Setup(c => c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl)))).
                Returns(OmnidotsFixture.StringTask(OmnidotsFixture.PeakRecordsJson()));

            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(OmnidotsFixture.MonitorsList(1));

            var startTime = DateTime.Parse("2023-09-25T10:29:00+00:00");
            var ruleActivity = OmnidotsFixture.CreateActiveRuleActivity(null, null);
            var durationSeconds = 10 * 60;
            var ruleId = Guid.NewGuid();
            var alertLevel = 10.0;
            var serialId = "1";
            var monitorId = new Guid();
            var created = DateTime.UtcNow;
            dbClient.Setup(c => c.ReadAlertContacts(monitorId)).Returns(contacts);
            var rule = new RvtAlertRuleDto(ruleId, serialId, "XFdom", alertLevel, 1.0, durationSeconds,
                                                    ruleActivity,
                                                    AlertType.Alert, false, false, created, null);

            dbClient.SetupSequence(c => c.ReadRules(serialId)).
                                Returns(new List<RvtAlertRuleDto> { rule }).
                                Returns(new List<RvtAlertRuleDto> { new(ruleId, serialId, "XFdom", alertLevel, 1.0, durationSeconds,
                                                    ruleActivity,
                                                    AlertType.Alert, true, false, created, created.AddMinutes(-(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES-1)) )}).
                                Returns(new List<RvtAlertRuleDto> { new(ruleId, serialId, "XFdom", alertLevel, 1.0, durationSeconds,
                                                    ruleActivity,
                                                    AlertType.Alert, true, false, created, created.AddMinutes(-(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES+1)) )});

            dbClient.Setup(c => c.ReadRules("2")).
                Returns(new List<RvtAlertRuleDto>());
            dbClient.Setup(c => c.GetAveragePeakLevels(serialId, "XFdom", It.IsAny<DateTime>(),
                                                        It.IsAny<DateTime>())).Returns(alertLevel + 1);

            testObj.StorePeakRecords(10);

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>()), Times.Exactly(1));
            httpClient.Verify(c =>
                c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl))), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            var expectedLatest = DateTime.Parse("2023-11-14T11:24:59");
            dbClient.As<IOmnidotsMeasurementImportCommands>().Verify(c => c.ImportPeakRecords(
                serialId, It.IsAny<DataTable>(),
                It.Is<DateTime>(dt => TestUtil.VerifyDateTime(expectedLatest, dt))), Times.Once);
            dbClient.Verify(c => c.ReadRules(serialId), Times.Exactly(1));
            var expectedStartTime = expectedLatest.AddSeconds(-durationSeconds);
            dbClient.Verify(c => c.GetAveragePeakLevels("1", "XFdom", expectedStartTime, expectedLatest), Times.Exactly(1));
            dbClient.Verify(c => c.ReadAlertContacts(monitorId), Times.Exactly(1));
            dbClient.Verify(c => c.WriteNotification(It.Is<NotificationDto>(
                dto => TestUtil.VerifyNotificationDto(dto, rule, alertLevel + 1, expectedLatest, durationSeconds, alertLevel))),
                Times.Exactly(1));
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, "1", "XFdom", true))),
                Times.Exactly(1));

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.Verify(c => c.PublishAsync(RvtConfig.ALERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.VerifyNoOtherCalls();

            messageClient.Verify(c => c.Sendmessage(
               MessageService.MessageContent.MessageEnum.Offline,
               MessageService.MessageContent.MessageTypeEnum.Email,
               contacts[0],
               It.IsAny<string>(),
               It.IsAny<string>()), Times.Exactly(contacts.Count));
            messageClient.VerifyNoOtherCalls();
            //if (contacts.Count == 1)
            //{
            //    commsClient.Verify(c => c.SendMessage(ContactMethod.Email, AlertType.Alert, "baz@bob.org", null, It.IsAny<string>()), Times.Exactly(1));
            //}
            //else if (contacts.Count == 2)
            //{
            //    commsClient.Verify(c => c.SendMessage(ContactMethod.Email, AlertType.Alert, "foo@bob.org", "999999999", It.IsAny<string>()), Times.Exactly(1));
            //    commsClient.Verify(c => c.SendMessage(ContactMethod.SMS, AlertType.Alert, "blah", "01234567890", It.IsAny<string>()), Times.Exactly(1));
            //}
            //else if (contacts.Count == 3)
            //{
            //    commsClient.Verify(c => c.SendMessage(ContactMethod.Email, AlertType.Alert, "XXX@bob.org", null, It.IsAny<string>()), Times.Exactly(1));
            //    commsClient.Verify(c => c.SendMessage(ContactMethod.SMS, AlertType.Alert, "bbbb@cccc.ddd", "01234567890", It.IsAny<string>()), Times.Exactly(1));
            //    commsClient.Verify(c => c.SendMessage(ContactMethod.SMSAndEmail, AlertType.Alert, "bar@bazbaz.org", "9988776655", It.IsAny<string>()), Times.Exactly(1));
            //}
            //else
            //{
            //    Assert.Fail();
            //}
        }

        [TestMethod]
        public void TestStorePeakLevels_WithAlertRuleDateExclusion_Success()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageClient);

            var token = "hghjadg";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)))).
                    Returns(OmnidotsFixture.AuthenticateTask(token));
            var peakRecordsUrl = string.Format("/api/v1/get_peak_records?token={0}", token);
            httpClient.Setup(c => c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl)))).
                Returns(OmnidotsFixture.StringTask(OmnidotsFixture.PeakRecordsJson()));

            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(OmnidotsFixture.MonitorsList(1));
            dbClient.Setup(c => c.ReadRules("1")).
                Returns(new List<RvtAlertRuleDto> { new(Guid.NewGuid(), "1", "YVtop", 19.0,19.0, 10,
                                                    new AlertActivityTimeDto { Weekdays = false, Sundays = true, Saturdays = true,
                                                                       StartTime = null, EndTime = null}, AlertType.Alert, false, false,
                                                                       DateTime.UtcNow, DateTime.UtcNow.AddMinutes(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES+1)) }); ;

            dbClient.Setup(c => c.GetAveragePeakLevels("1", "YVtop", It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(20.0);

            testObj.StorePeakRecords(OmnidotsFixture.MINUTES_ELAPSED);

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>()), Times.Exactly(1));
            httpClient.Verify(c =>
                c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl))), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.ReadRules("1"), Times.Exactly(1));
            var expectedLatest = DateTime.Parse("2023-11-14T11:24:59");
            dbClient.As<IOmnidotsMeasurementImportCommands>().Verify(c => c.ImportPeakRecords(
                "1", It.IsAny<DataTable>(), expectedLatest), Times.Once);
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, "1", "YVtop", false))),
                Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();

        }

        [TestMethod]
        public void TestStorePeakLevels_WithAlertRuleDeleted_Success()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageClient);
            var token = "hgslkfslkjhjadg";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)))).
                Returns(OmnidotsFixture.AuthenticateTask(token));
            var peakRecordsUrl = string.Format("/api/v1/get_peak_records?token={0}", token);
            httpClient.Setup(c => c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl)))).
                Returns(OmnidotsFixture.StringTask(OmnidotsFixture.PeakRecordsJson()));

            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(OmnidotsFixture.MonitorsList(1));
            dbClient.Setup(c => c.ReadRules("1")).
                Returns(new List<RvtAlertRuleDto> { new(Guid.NewGuid(), "1", "YVtop", 19.0,19.0, 10,
                                                    new AlertActivityTimeDto { Weekdays = true, Sundays = true, Saturdays = true,
                                                                       StartTime = null, EndTime = null}, AlertType.Alert, false, true,
                                                                       DateTime.UtcNow, null) });
            dbClient.Setup(c => c.GetAveragePeakLevels("1", "YVtop", It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(20.0);

            testObj.StorePeakRecords(OmnidotsFixture.MINUTES_ELAPSED);

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>()), Times.Exactly(1));
            httpClient.Verify(c =>
                c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl))), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.ReadRules("1"), Times.Exactly(1));
            var expectedLatest = DateTime.Parse("2023-11-14T11:24:59");
            dbClient.As<IOmnidotsMeasurementImportCommands>().Verify(c => c.ImportPeakRecords(
                "1", It.IsAny<DataTable>(), expectedLatest), Times.Once);
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, "1", "YVtop", false))),
                Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();

        }

        [TestMethod]
        public void TestStorePeakLevels_WithAlertRuleLevelExclusion_Success()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                  out Mock<IMessageService> messageClient);
            var token = "hgslkfslkjhjadg";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)))).
                Returns(OmnidotsFixture.AuthenticateTask(token));

            var peakRecordsUrl = string.Format("/api/v1/get_peak_records?token={0}", token);
            httpClient.Setup(c => c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl)))).
                Returns(OmnidotsFixture.StringTask(OmnidotsFixture.PeakRecordsJson()));

            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(OmnidotsFixture.MonitorsList(1));
            var durationSeconds = 60 * 15;
            dbClient.Setup(c => c.ReadRules("1")).
                Returns(new List<RvtAlertRuleDto> { new(Guid.NewGuid(), "1", "YVtop", 1.0,1.0, durationSeconds,
                                                    new AlertActivityTimeDto { Weekdays = true, Sundays = true, Saturdays = true,
                                                                       StartTime = null, EndTime = null}, AlertType.Alert, false, false,
                                                                       DateTime.UtcNow, DateTime.UtcNow.AddMinutes(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES+1)) });

            testObj.StorePeakRecords(OmnidotsFixture.MINUTES_ELAPSED);

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>()), Times.Exactly(1));
            httpClient.Verify(c =>
                c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl))), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.ReadRules("1"), Times.Exactly(1));
            var expectedLatest = DateTime.Parse("2023-11-14T11:24:59");
            dbClient.As<IOmnidotsMeasurementImportCommands>().Verify(c => c.ImportPeakRecords(
                "1", It.IsAny<DataTable>(), expectedLatest), Times.Once);
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, "1", "YVtop", false))),
                Times.Exactly(1));
            var expectedStartTime = expectedLatest.AddSeconds(-durationSeconds);
            dbClient.Verify(c => c.GetAveragePeakLevels("1", "YVtop", expectedStartTime, expectedLatest), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();

        }

        [TestMethod]
        public void TestStorePeakLevels_WithAlertRuleTimeExclusion_Success()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                  out Mock<IMessageService> messageClient);

            var token = "hgslkfslkjhjadg";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)))).
                Returns(OmnidotsFixture.AuthenticateTask(token));
            var peakRecordsUrl = string.Format("/api/v1/get_peak_records?token={0}", token);
            httpClient.Setup(c => c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl)))).
                Returns(OmnidotsFixture.StringTask(OmnidotsFixture.PeakRecordsJson()));

            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(OmnidotsFixture.MonitorsList(1));
            var durationSeconds = 60 * 15;
            dbClient.Setup(c => c.ReadRules("1")).
                Returns(new List<RvtAlertRuleDto> { new(Guid.NewGuid(), "1", "YVtop", 19.0, 19.0, durationSeconds,
                                                    OmnidotsFixture.CreateActiveRuleActivity(DateTime.Parse("2023-09-25T00:01:02"),DateTime.Parse("2023-09-25T00:01:03")),
                                                    AlertType.Alert, false, false, DateTime.UtcNow, null) }); ;
            testObj.StorePeakRecords(OmnidotsFixture.MINUTES_ELAPSED);

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>()), Times.Exactly(1));
            httpClient.Verify(c =>
                c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl))), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.ReadRules("1"), Times.Exactly(1));
            var expectedLatest = DateTime.Parse("2023-11-14T11:24:59");
            dbClient.As<IOmnidotsMeasurementImportCommands>().Verify(c => c.ImportPeakRecords(
                "1", It.IsAny<DataTable>(), expectedLatest), Times.Once);
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, "1", "YVtop", false))),
                Times.Exactly(1));
            var expectedStartTime = expectedLatest.AddSeconds(-durationSeconds);
            dbClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStorePeakLevels_AlertRuleActivatedThenDeactivatedByDustLimitOnOff_Success()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                  out Mock<IMessageService> messageClient);

            var startTime = DateTime.Parse("2023-10-03T13:10:00Z").ToUniversalTime();
            var monitorId = new Guid();
            var fdomLimitOn = 10.0;
            var fdomLimitOff = 8.0;
            var vtop = .12;
            var vtopOverflow = .23;
            var alertingMeasurements =
                OmnidotsFixture.CreateDeviceMeasurement(startTime, fdomLimitOn, vtop, vtopOverflow);
            var nonAlertingMeasurements =
                OmnidotsFixture.CreateDeviceMeasurement(startTime.AddMinutes(1), fdomLimitOff, vtop, vtopOverflow);
            var token = "hgslkfslkjhjadg";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)))).
                Returns(OmnidotsFixture.AuthenticateTask(token));
            var peakRecordsUrl = string.Format("/api/v1/get_peak_records?token={0}", token);
            httpClient.SetupSequence(c => c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl)))).
                Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(alertingMeasurements))).
                Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(alertingMeasurements))).
                Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(nonAlertingMeasurements)));

            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(OmnidotsFixture.MonitorsList(1));
            var ruleId = Guid.NewGuid(); ;
            var contacts = OmnidotsFixture.AlertContacts();
            dbClient.Setup(c => c.ReadAlertContacts(monitorId)).Returns(contacts);
            var durationSeconds = 60;
            var rule = new RvtAlertRuleDto(ruleId, "1", "XFdom", fdomLimitOn, fdomLimitOff, durationSeconds,
                                                    OmnidotsFixture.CreateActiveRuleActivity(startTime.AddHours(-1), startTime.AddHours(1)),
                                                    AlertType.Alert, false, false, DateTime.UtcNow, null);
            dbClient.SetupSequence(c => c.ReadRules("1")).
                                Returns(new List<RvtAlertRuleDto> { rule }).
                                Returns(new List<RvtAlertRuleDto> { new(ruleId, "1", "XFdom", fdomLimitOn, fdomLimitOff,  durationSeconds,
                                                    OmnidotsFixture.CreateActiveRuleActivity(startTime.AddHours(-1), startTime.AddHours(1)),
                                                    AlertType.Alert, false, false, DateTime.UtcNow, null) }).
                                Returns(new List<RvtAlertRuleDto> { new(ruleId, "1", "XFdom", fdomLimitOn, fdomLimitOff,  durationSeconds,
                                                    OmnidotsFixture.CreateActiveRuleActivity(startTime.AddHours(-1), startTime.AddHours(1)),
                                                    AlertType.Alert, true, false, DateTime.UtcNow, null) });
            dbClient.SetupSequence(c => c.GetAveragePeakLevels("1", "XFdom", It.IsAny<DateTime>(), It.IsAny<DateTime>())).
                Returns(fdomLimitOn).
                Returns(fdomLimitOn).
                Returns(fdomLimitOff);

            testObj.StorePeakRecords(OmnidotsFixture.MINUTES_ELAPSED);
            testObj.StorePeakRecords(OmnidotsFixture.MINUTES_ELAPSED);
            testObj.StorePeakRecords(OmnidotsFixture.MINUTES_ELAPSED);

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>()), Times.Exactly(3));
            httpClient.Verify(c =>
                c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl))), Times.Exactly(3));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(3));
            mqttClient.Verify(c => c.PublishAsync(RvtConfig.ALERT_TOPIC, It.IsAny<string>()), Times.Exactly(2));
            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(3));
            dbClient.Verify(c => c.ReadRules("1"), Times.Exactly(3));
            var importCommands = dbClient.As<IOmnidotsMeasurementImportCommands>();
            importCommands.Verify(c => c.ImportPeakRecords("1", It.Is<DataTable>(table =>
                    TestUtil.VerifyPeakRecordTable(table, startTime, fdomLimitOn, vtop, vtopOverflow)), startTime),
                Times.Exactly(2));
            importCommands.Verify(c => c.ImportPeakRecords("1", It.Is<DataTable>(table =>
                    TestUtil.VerifyPeakRecordTable(table, startTime.AddMinutes(1), fdomLimitOff, vtop, vtopOverflow)),
                    startTime.AddMinutes(1)), Times.Once);
            var expectedDateTime = DateTime.Parse("2023-10-03T13:10:00");
            var expectedStartTime = expectedDateTime.AddSeconds(-durationSeconds);
            dbClient.Verify(c => c.GetAveragePeakLevels("1", "XFdom", expectedStartTime, expectedDateTime), Times.Exactly(2));
            dbClient.Verify(c => c.GetAveragePeakLevels("1", "XFdom", expectedStartTime.AddMinutes(1), expectedDateTime.AddMinutes(1)), Times.Exactly(1));
            dbClient.Verify(c => c.ReadAlertContacts(monitorId), Times.Exactly(2));
            dbClient.Verify(c =>
                c.WriteNotification(It.Is<NotificationDto>(dto => TestUtil.VerifyNotificationDto(dto, rule, fdomLimitOn, startTime.ToUniversalTime(), durationSeconds, fdomLimitOn))),
                Times.Exactly(2));
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, "1", "XFdom", true))),
                Times.Exactly(2));
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, "1", "XFdom", false))),
                Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            //commsClient.Verify(c => c.SendMessage(ContactMethod.Email, AlertType.Alert, "baz@bob.org", null, It.IsAny<string>()), Times.Exactly(2));
            messageClient.Verify(c => c.Sendmessage(
               MessageService.MessageContent.MessageEnum.Offline,
               MessageService.MessageContent.MessageTypeEnum.Email,
               contacts[0],
               It.IsAny<string>(),
               It.IsAny<string>()), Times.Exactly(2));
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStorePeakLevels_AlertRuleActivatedThenDeactivatedByActivityWindow_Success()
        {

            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                  out Mock<IMessageService> messageClient);
            var alertLevel = 10.0;
            var vtop = .12;
            var vtopOverflow = .23;
            var monitorId = new Guid();
            var startTime = DateTime.Parse("2023-10-03T13:10:00Z").ToUniversalTime();
            var measurements1 =
                OmnidotsFixture.CreateDeviceMeasurement(startTime, alertLevel + 1, vtop, vtopOverflow);
            var measurements2 =
                OmnidotsFixture.CreateDeviceMeasurement(startTime.AddMinutes(1), alertLevel + 1, vtop, vtopOverflow);
            var token = "hgslkfslkjhjadg";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)))).
                Returns(OmnidotsFixture.AuthenticateTask(token));
            var peakRecordsUrl = string.Format("/api/v1/get_peak_records?token={0}", token);
            httpClient.SetupSequence(c => c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl)))).
                Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(measurements1))).
                Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(measurements2)));

            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(OmnidotsFixture.MonitorsList(1));
            var ruleId = Guid.NewGuid(); ;
            var contacts = OmnidotsFixture.AlertContacts();
            dbClient.Setup(c => c.ReadAlertContacts(monitorId)).Returns(contacts);

            var durationSeconds = 60;
            var rule = new RvtAlertRuleDto(ruleId, "1", "XFdom", alertLevel, 1.0, durationSeconds,
                                                    OmnidotsFixture.CreateActiveRuleActivity(startTime, startTime.AddSeconds(30)),
                                                    AlertType.Alert, false, false, DateTime.UtcNow, null);
            dbClient.SetupSequence(c => c.ReadRules("1")).
                                Returns(new List<RvtAlertRuleDto> { rule }).
                                Returns(new List<RvtAlertRuleDto> { new(ruleId, "1", "XFdom", alertLevel, 1.0, durationSeconds,
                                                    OmnidotsFixture.CreateActiveRuleActivity(startTime, startTime.AddSeconds(30)),
                                                    AlertType.Alert, true, false, DateTime.UtcNow,
                                                    DateTime.UtcNow.AddMinutes(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES+1) )});
            dbClient.Setup(c => c.GetAveragePeakLevels("1", "XFdom", It.IsAny<DateTime>(), It.IsAny<DateTime>())).
                Returns(alertLevel + 1);

            // first store dust levels should trigger an alert second should cancel it
            testObj.StorePeakRecords(OmnidotsFixture.MINUTES_ELAPSED);
            testObj.StorePeakRecords(OmnidotsFixture.MINUTES_ELAPSED);

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>()), Times.Exactly(2));
            httpClient.Verify(c =>
                c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl))), Times.Exactly(2));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(2));
            mqttClient.Verify(c => c.PublishAsync(RvtConfig.ALERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.VerifyNoOtherCalls();

            var expectedDateTime = DateTime.Parse("2023-10-03T13:10:00");
            var expectedStartTime = expectedDateTime.AddSeconds(-durationSeconds);
            dbClient.Verify(c => c.ReadRules("1"), Times.Exactly(2));
            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(2));
            var importCommands = dbClient.As<IOmnidotsMeasurementImportCommands>();
            importCommands.Verify(c => c.ImportPeakRecords("1", It.Is<DataTable>(table =>
                    TestUtil.VerifyPeakRecordTable(table, startTime, alertLevel + 1, vtop, vtopOverflow)), startTime),
                Times.Once);
            importCommands.Verify(c => c.ImportPeakRecords("1", It.Is<DataTable>(table =>
                    TestUtil.VerifyPeakRecordTable(table, startTime.AddMinutes(1), alertLevel + 1, vtop, vtopOverflow)),
                    startTime.AddMinutes(1)), Times.Once);

            dbClient.Verify(c => c.GetAveragePeakLevels("1", "XFdom", expectedStartTime, expectedDateTime), Times.Exactly(1));

            dbClient.Verify(c => c.ReadAlertContacts(monitorId), Times.Exactly(1));
            dbClient.Verify(c => c.WriteNotification(It.Is<NotificationDto>(
                    dto => TestUtil.VerifyNotificationDto(dto, rule, alertLevel + 1, startTime, durationSeconds, alertLevel))),
                Times.Exactly(1));
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, "1", "XFdom", true))),
                Times.Exactly(1));
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, "1", "XFdom", false))),
                Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            //commsClient.Verify(c => c.SendMessage(ContactMethod.Email, AlertType.Alert, "baz@bob.org", null, It.IsAny<string>()), Times.Exactly(1));
            messageClient.Verify(c => c.Sendmessage(
               MessageService.MessageContent.MessageEnum.Offline,
               MessageService.MessageContent.MessageTypeEnum.Email,
               contacts[0],
               It.IsAny<string>(),
               It.IsAny<string>()), Times.Exactly(1));
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStorePeakLevels_AlertRuleActiveWritesAlertAccordingToAlertDelay_Success()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                  out Mock<IMessageService> messageClient);
            var alertLevel = 10.0;
            var vtop = .12;
            var vtopOverflow = .23;
            var monitorId = new Guid();
            var startTime = DateTime.Parse("2023-10-03T13:10:00Z").ToUniversalTime();
            var measurements = OmnidotsFixture.CreateDeviceMeasurement(startTime, alertLevel + 1, vtop, vtopOverflow);

            var token = "hgslkfslkjhjadg";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)))).
                Returns(OmnidotsFixture.AuthenticateTask(token));
            var peakRecordsUrl = string.Format("/api/v1/get_peak_records?token={0}", token);
            httpClient.Setup(c => c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl)))).
                Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(measurements)));

            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(OmnidotsFixture.MonitorsList(1));
            var ruleId = Guid.NewGuid(); ;
            var contacts = OmnidotsFixture.AlertContacts();
            dbClient.Setup(c => c.ReadAlertContacts(monitorId)).Returns(contacts);
            var durationSeconds = 60;
            var rule = new RvtAlertRuleDto(ruleId, "1", "XFdom", alertLevel, 1.0, durationSeconds,
                                                    OmnidotsFixture.CreateActiveRuleActivity(startTime, startTime.AddSeconds(30)),
                                                    AlertType.Alert, false, false, DateTime.UtcNow, null);
            var ruleActivity = OmnidotsFixture.CreateActiveRuleActivity(null, null);
            var created = DateTime.UtcNow;
            dbClient.SetupSequence(c => c.ReadRules("1")).
                                Returns(new List<RvtAlertRuleDto> { new(ruleId, "1", "XFdom", alertLevel, 1.0, durationSeconds,
                                                    ruleActivity,
                                                    AlertType.Alert, false, false, created, null) }).
                                Returns(new List<RvtAlertRuleDto> { new(ruleId, "1", "XFdom", alertLevel, 1.0, durationSeconds,
                                                    ruleActivity,
                                                    AlertType.Alert, true, false, created, created.AddMinutes(-(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES-1)) )}).
                                Returns(new List<RvtAlertRuleDto> { new(ruleId, "1", "XFdom", alertLevel, 1.0, durationSeconds,
                                                    ruleActivity,
                                                    AlertType.Alert, true, false, created, created.AddMinutes(-(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES+1)) )});
            dbClient.Setup(c => c.GetAveragePeakLevels("1", "XFdom", It.IsAny<DateTime>(), It.IsAny<DateTime>())).
                Returns(alertLevel + 1);

            testObj.StorePeakRecords(OmnidotsFixture.MINUTES_ELAPSED);
            testObj.StorePeakRecords(OmnidotsFixture.MINUTES_ELAPSED);
            testObj.StorePeakRecords(OmnidotsFixture.MINUTES_ELAPSED);

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>()), Times.Exactly(3));
            httpClient.Verify(c =>
                c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl))), Times.Exactly(3));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(3));
            mqttClient.Verify(c => c.PublishAsync(RvtConfig.ALERT_TOPIC, It.IsAny<string>()), Times.Exactly(2));
            mqttClient.VerifyNoOtherCalls();

            var expectedDateTime = DateTime.Parse("2023-10-03T13:10:00");
            var expectedStartTime = expectedDateTime.AddSeconds(-durationSeconds);
            dbClient.Verify(c => c.ReadRules("1"), Times.Exactly(3));
            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(3));
            dbClient.As<IOmnidotsMeasurementImportCommands>().Verify(c => c.ImportPeakRecords(
                "1",
                It.Is<DataTable>(table => TestUtil.VerifyPeakRecordTable(
                    table, startTime, alertLevel + 1, vtop, vtopOverflow)),
                startTime), Times.Exactly(3));
            dbClient.Verify(c => c.GetAveragePeakLevels("1", "XFdom", expectedStartTime, expectedDateTime), Times.Exactly(3));
            dbClient.Verify(c => c.ReadAlertContacts(monitorId), Times.Exactly(2));
            dbClient.Verify(c => c.WriteNotification(It.Is<NotificationDto>(
                    dto => TestUtil.VerifyNotificationDto(dto, rule, alertLevel + 1, startTime, durationSeconds, alertLevel))),
                Times.Exactly(2));
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, "1", "XFdom", true))),
                Times.Exactly(2));
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, "1", "XFdom", false))),
                Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            messageClient.Verify(c => c.Sendmessage(
               MessageService.MessageContent.MessageEnum.Offline,
               MessageService.MessageContent.MessageTypeEnum.Email,
               contacts[0],
               It.IsAny<string>(),
               It.IsAny<string>()), Times.Exactly(2));
            //commsClient.Verify(c => c.SendMessage(ContactMethod.Email, AlertType.Alert, "baz@bob.org", null, It.IsAny<string>()), Times.Exactly(2));
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStorePeakLevels_AlertRuleActivatedButSendMessageFails_Success()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageClient);
            var limitOn = 10.0;
            var limitOff = 8.0;
            var vtop = .12;
            var vtopOverflow = .23;
            var monitorId = new Guid();
            var startTime = DateTime.Parse("2023-10-03T13:10:00Z").ToUniversalTime();
            var alertingMeasurements = OmnidotsFixture.CreateDeviceMeasurement(startTime, limitOn, vtop, vtopOverflow);
            var token = "hgslkfslkjhjadg";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)))).
                Returns(OmnidotsFixture.AuthenticateTask(token));
            var peakRecordsUrl = string.Format("/api/v1/get_peak_records?token={0}", token);
            httpClient.Setup(c => c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl)))).
                Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(alertingMeasurements)));

            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(OmnidotsFixture.MonitorsList(1));
            var ruleId = Guid.NewGuid(); ;
            var contacts = OmnidotsFixture.AlertContacts();
            dbClient.Setup(c => c.ReadAlertContacts(monitorId)).Returns(contacts);
            var durationSeconds = 60;
            var rule = new RvtAlertRuleDto(ruleId, "1", "XFdom", limitOn, limitOff, durationSeconds,
                                                    OmnidotsFixture.CreateActiveRuleActivity(startTime.AddHours(-1), startTime.AddHours(1)),
                                                    AlertType.Alert, false, false, DateTime.UtcNow, null);
            var created = DateTime.UtcNow;
            dbClient.Setup(c => c.ReadRules("1")).
                                Returns(new List<RvtAlertRuleDto> { rule });
            dbClient.Setup(c => c.GetAveragePeakLevels("1", "XFdom", It.IsAny<DateTime>(), It.IsAny<DateTime>())).
                Returns(limitOn);

            //commsClient.Setup(c => c.SendMessage(ContactMethod.Email, AlertType.Alert, "baz@bob.org", null, It.IsAny<string>())).
            //    Throws(CommsException.Of("test-address", "test-message"));

            messageClient.Setup(c => c.Sendmessage(
               MessageService.MessageContent.MessageEnum.Offline,
               MessageService.MessageContent.MessageTypeEnum.Email,
               contacts[0],
               It.IsAny<string>(),
               It.IsAny<string>())).
               Throws(CommsException.Of("test-address", "test-message"));

            testObj.StorePeakRecords(OmnidotsFixture.MINUTES_ELAPSED);

            httpClient.Verify(c =>
                c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>()), Times.Exactly(1));
            httpClient.Verify(c =>
                c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl))), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.Verify(c => c.PublishAsync(RvtConfig.ALERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.VerifyNoOtherCalls();

            var expectedDateTime = DateTime.Parse("2023-10-03T13:10:00");
            var expectedStartTime = expectedDateTime.AddSeconds(-durationSeconds);
            dbClient.Verify(c => c.ReadRules("1"), Times.Exactly(1));
            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.As<IOmnidotsMeasurementImportCommands>().Verify(c => c.ImportPeakRecords(
                "1",
                It.Is<DataTable>(table => TestUtil.VerifyPeakRecordTable(
                    table, startTime, limitOn, vtop, vtopOverflow)),
                startTime), Times.Once);
            dbClient.Verify(c => c.GetAveragePeakLevels("1", "XFdom", expectedStartTime, expectedDateTime), Times.Exactly(1));
            dbClient.Verify(c => c.ReadAlertContacts(monitorId), Times.Exactly(1));
            dbClient.Verify(c => c.WriteNotification(It.Is<NotificationDto>(
                    dto => TestUtil.VerifyNotificationDto(dto, rule, limitOn, startTime, durationSeconds, limitOn))),
                Times.Exactly(1));
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, "1", "XFdom", true))),
                Times.Exactly(1));
            dbClient.Verify(c => c.WriteNotificationAudit(It.IsAny<Guid>(), "test-address", "test-message"),
                Times.Exactly(1));

            dbClient.VerifyNoOtherCalls();

            //commsClient.Verify(c => c.SendMessage(ContactMethod.Email, AlertType.Alert, "baz@bob.org", null, It.IsAny<string>()), Times.Exactly(1));
            messageClient.Verify(c => c.Sendmessage(
                 MessageService.MessageContent.MessageEnum.Offline,
                 MessageService.MessageContent.MessageTypeEnum.Email,
                 contacts[0],
                 It.IsAny<string>(),
                 It.IsAny<string>()), Times.Exactly(1));
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStorePeakLevels_AlertRuleActivatedButSendMessageExcludedBySendTime_Success()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageClient);
            var limitOn = 10.0;
            var limitOff = 8.0;
            var vtop = .12;
            var vtopOverflow = .23;
            var monitorId = new Guid();
            var startTime = DateTime.Parse("2023-10-03T13:10:00Z").ToUniversalTime();
            var alertingMeasurements = OmnidotsFixture.CreateDeviceMeasurement(startTime, limitOn, vtop, vtopOverflow);
            var token = "hgslkfslkjhjadg";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)))).
                Returns(OmnidotsFixture.AuthenticateTask(token));
            var peakRecordsUrl = string.Format("/api/v1/get_peak_records?token={0}", token);
            httpClient.Setup(c => c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl)))).
                Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(alertingMeasurements)));

            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(OmnidotsFixture.MonitorsList(1));
            var ruleId = Guid.NewGuid(); ;
            var contacts = OmnidotsFixture.AlertContacts(new TimeSpan(0, 0, 0), new TimeSpan(0, 0, 1));
            dbClient.Setup(c => c.ReadAlertContacts(monitorId)).Returns(contacts);
            var durationSeconds = 60;
            var rule = new RvtAlertRuleDto(ruleId, "1", "XFdom", limitOn, limitOff, durationSeconds,
                                                    OmnidotsFixture.CreateActiveRuleActivity(startTime.AddHours(-1), startTime.AddHours(1)),
                                                    AlertType.Alert, false, false, DateTime.UtcNow, null);
            var created = DateTime.UtcNow;
            dbClient.Setup(c => c.ReadRules("1")).
                                Returns(new List<RvtAlertRuleDto> { rule });
            dbClient.Setup(c => c.GetAveragePeakLevels("1", "XFdom", It.IsAny<DateTime>(), It.IsAny<DateTime>())).
                Returns(limitOn);

            testObj.StorePeakRecords(OmnidotsFixture.MINUTES_ELAPSED);

            httpClient.Verify(c =>
                c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>()), Times.Exactly(1));
            httpClient.Verify(c =>
                c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl))), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.Verify(c => c.PublishAsync(RvtConfig.ALERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.VerifyNoOtherCalls();

            var expectedDateTime = DateTime.Parse("2023-10-03T13:10:00");
            var expectedStartTime = expectedDateTime.AddSeconds(-durationSeconds);
            dbClient.Verify(c => c.ReadRules("1"), Times.Exactly(1));
            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.As<IOmnidotsMeasurementImportCommands>().Verify(c => c.ImportPeakRecords(
                "1",
                It.Is<DataTable>(table => TestUtil.VerifyPeakRecordTable(
                    table, startTime, limitOn, vtop, vtopOverflow)),
                startTime), Times.Once);
            dbClient.Verify(c => c.GetAveragePeakLevels("1", "XFdom", expectedStartTime, expectedDateTime), Times.Exactly(1));
            dbClient.Verify(c => c.ReadAlertContacts(monitorId), Times.Exactly(1));
            dbClient.Verify(c => c.WriteNotification(It.Is<NotificationDto>(
                    dto => TestUtil.VerifyNotificationDto(dto, rule, limitOn, startTime, durationSeconds, limitOn))),
                Times.Exactly(1));
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, "1", "XFdom", true))),
                Times.Exactly(1));

            dbClient.VerifyNoOtherCalls();
        }


        private static IEnumerable<object[]> OneAlertContact()
        {
            yield return new object[] { new List<RvtContactDto>()
                {
                    new RvtContactDto(contactMethod:ContactMethod.Email,
                                      emailAddress: "baz@bob.org",
                                      phoneNumber: null,
                                      sendStartTime: null,
                                      sendEndTime: null)
                }
            };
        }

        private static IEnumerable<object[]> TwoAlertContacts()
        {
            yield return new object[] { new List<RvtContactDto>()
                {
                    new RvtContactDto(contactMethod: ContactMethod.Email,
                                      emailAddress: "foo@bob.org",
                                      phoneNumber:"999999999",
                                      sendStartTime: null,
                                      sendEndTime: null),
                    new RvtContactDto(contactMethod: ContactMethod.SMS,
                                      emailAddress: "blah",
                                      phoneNumber: "01234567890",
                                      sendStartTime: null,
                                      sendEndTime: null)
                }
            };
        }

        private static IEnumerable<object[]> ThreeAlertContacts()
        {
            yield return new object[] { new List<RvtContactDto>()
                {
                    new RvtContactDto(contactMethod: ContactMethod.Email,
                                      emailAddress: "XXX@bob.org",
                                      phoneNumber: null,
                                      sendStartTime: null,
                                      sendEndTime: null),
                    new RvtContactDto(contactMethod: ContactMethod.SMS,
                                      emailAddress: "bbbb@cccc.ddd",
                                      phoneNumber: "01234567890",
                                      sendStartTime: null,
                                      sendEndTime: null),
                    new RvtContactDto(contactMethod:ContactMethod.SMSAndEmail,
                                      emailAddress: "bar@bazbaz.org",
                                      phoneNumber: "9988776655",
                                      sendStartTime: null,
                                      sendEndTime: null)
                }
            };
        }
    }
}
