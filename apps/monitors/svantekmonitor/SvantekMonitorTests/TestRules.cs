using System.Data;
using System.Text.Json;
using Svantek.Api.Db;
using Svantek.Api.Http;
using Svantek.Model.Dto;
using Svantek.Model.Http;
using SvantekMonitorTests;
using Microsoft.Extensions.Logging;
using Moq;
using Org.BouncyCastle.Tls;
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
    public class TestRules
    {
        public TestRules()
        {
            var factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestRules");
        }

        private static Rvt.Monitor.Common.Notifications.RvtContactDto ContactEquivalentTo(RvtContactDto expected) =>
            It.Is<Rvt.Monitor.Common.Notifications.RvtContactDto>(actual =>
                actual.ContactMethod == (Rvt.Monitor.Common.Notifications.ContactMethod)(int)expected.ContactMethod &&
                actual.EmailAddress == expected.EmailAddress &&
                actual.PhoneNumber == expected.PhoneNumber &&
                actual.Email == expected.Email &&
                actual.SMS == expected.SMS &&
                actual.SendStartTime == expected.SendStartTime &&
                actual.SendEndTime == expected.SendEndTime);


        [DataTestMethod]
        [DynamicData(nameof(DateExclusion), DynamicDataSourceType.Method)]
        [DynamicData(nameof(DeletedExclusion), DynamicDataSourceType.Method)]
        [DynamicData(nameof(TimeExclusion), DynamicDataSourceType.Method)]
        [DynamicData(nameof(LevelExclusion), DynamicDataSourceType.Method)]
        public void TestStoreNoiseLevels_WithAlertRuleExclusion_Success(List<RvtAlertRuleDto> rules)
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/latestData\\?userID=foo&token=bar&instrumentID=*"))).
                                Returns(Task<string>.Factory.StartNew(() => SvantekFixture.SamplesResponseJson()));
            var monitors = SvantekFixture.MonitorDtos(null, NoiseMonitorStatus.ACTIVE);
            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(monitors);

            dbClient.Setup(c => c.ReadRules("Device1")).
                Returns(rules);

            testObj.StoreNoiseLevels("foo", "bar");

            httpClient.Verify(c => c.GetAsync("/latestData?userID=foo&token=bar&instrumentID=Device1"), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestData?userID=foo&token=bar&instrumentID=Device2"), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestData?userID=foo&token=bar&instrumentID=Device3"), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos("Device1", It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos("Device2", It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos("Device3", It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.Verify(c => c.WriteLatestTimestamp(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Exactly(3));
            dbClient.Verify(c => c.ReadRules("Device1"), Times.Exactly(1));
            dbClient.Verify(c => c.ReadRules("Device2"), Times.Exactly(1));
            dbClient.Verify(c => c.ReadRules("Device3"), Times.Exactly(1));

            //dbClient.Verify(c => c.UpdateMonitorStatus(monitors[1].SerialId, monitors[1].MonitorStatus));
            //dbClient.Verify(c => c.UpdateMonitorStatus(monitors[2].SerialId, monitors[2].MonitorStatus));
            //dbClient.Verify(c => c.ReadAlertContacts(monitors[0].Id, out It.Ref<Guid>.IsAny), Times.Exactly(1));
            //dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, monitors[0].SerialId, "LAeq", true))), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(3));
            mqttClient.VerifyNoOtherCalls();

            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreNoiseLevels_AlertRuleActivatedThenDeactivatedByActivityWindow_Success()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            var startTime = DateTime.Parse("2023-10-03T13:10:00+00:00");

            var alertLevel = 10.0;
            var serialId = "MyDevice";
            var measurements1 = new List<SampleResponse> {
                        SvantekFixture.CreateSampleResponse(startTime, serialId, alertLevel + 1) };
            var measurements2 = new List<SampleResponse> {
                        SvantekFixture.CreateSampleResponse(startTime.AddMinutes(15), serialId, alertLevel + 1) };

            var monitors = SvantekFixture.SingleActiveMonitorDto(serialId, null);
            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(monitors);

            var ruleId = Guid.NewGuid();
            var contacts = SvantekFixture.AlertContacts();
            dbClient.Setup(c => c.ReadAlertContacts(monitors[0].Id, out It.Ref<Guid>.IsAny)).Returns(contacts);


            httpClient.SetupSequence(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId))).
                                 Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(measurements1))).
                                 Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(measurements2)));

            var durationSeconds = 15 * 60;
            var ruleOn = new RvtAlertRuleDto(ruleId, serialId, "LAeq", alertLevel, 1.0, durationSeconds,
                                                    SvantekFixture.CreateActiveRuleActivity(startTime, startTime.AddSeconds(30)),
                                                    AlertType.Alert, false, false, DateTime.UtcNow, null);
            dbClient.SetupSequence(c => c.ReadRules(serialId)).
                                Returns(new List<RvtAlertRuleDto> { ruleOn }).
                                Returns(new List<RvtAlertRuleDto> { new(ruleId, serialId, "LAeq", alertLevel, 1.0, durationSeconds,
                                                            SvantekFixture.CreateActiveRuleActivity(startTime, startTime.AddSeconds(30)),
                                                            AlertType.Alert, true, false, DateTime.UtcNow,
                                                            DateTime.UtcNow.AddMinutes(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES+1) )});

            dbClient.Setup(c => c.HasOpenNotification(monitors[0].Id, "LAeq", ruleOn.AlertType)).
                                Returns(false);

            // first store noise levels should trigger an alert second should cancel it
            testObj.StoreNoiseLevels("foo", "bar");
            testObj.StoreNoiseLevels("foo", "bar");

            httpClient.Verify(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId)),
                Times.Exactly(2));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(2));
            mqttClient.Verify(c => c.PublishAsync(RvtConfig.ALERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(2));
            dbClient.Verify(c => c.ReadRules(serialId), Times.Exactly(2));
            dbClient.Verify(c => c.InsertNoiseDtos(serialId, It.IsAny<List<NoiseDto>>()), Times.Exactly(2));

            dbClient.Verify(c => c.WriteLatestTimestamp(serialId, startTime.ToUniversalTime()), Times.Exactly(1));
            dbClient.Verify(c => c.WriteLatestTimestamp(serialId, startTime.AddMinutes(15).ToUniversalTime()), Times.Exactly(1));

            dbClient.Verify(c => c.ReadAlertContacts(monitors[0].Id, out It.Ref<Guid>.IsAny), Times.Exactly(1));
            dbClient.Verify(c => c.WriteNotification(It.Is<NotificationDto>(dto => TestUtil.VerifyNotificationDto(dto, ruleOn, alertLevel + 1, startTime.ToUniversalTime(), durationSeconds, alertLevel))),
                Times.Exactly(1));
            dbClient.Verify(c => c.HasOpenNotification(monitors[0].Id, "LAeq", ruleOn.AlertType), Times.Exactly(0));
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, serialId, "LAeq", true))),
                Times.Exactly(1));

            dbClient.Verify(c => c.WriteNotificationAudit(It.IsAny<Guid>(), "baz@bob.org", NotificationConstants.SENT_OK));
            dbClient.VerifyNoOtherCalls();

            messageService.Verify(c => c.Sendmessage(MessageService.MessageContent.MessageEnum.Alert, MessageService.MessageContent.MessageTypeEnum.Email, ContactEquivalentTo(contacts[0]), monitors[0].FleetNr!, It.IsAny<string>()), Times.Exactly(1));
            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreNoiseLevels_AlertRuleActivatedThenDeactivatedByNoiseLimitOnOff_Success()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            // In this test we get 3 noise levels first 2 should trigger an alert third should remove the trigger
            var startTime = DateTime.Parse("2023-10-03T13:10:00+00:00");

            var limitOn = 10.0;
            var limitOff = 8.0;
            var serialId = "MyDev1";
            var alertingMeasurements =
                new List<SampleResponse> { SvantekFixture.CreateSampleResponse(startTime, serialId, limitOn) };
            var nonAlertingMeasurements =
                new List<SampleResponse> { SvantekFixture.CreateSampleResponse(startTime.AddMinutes(15), serialId, limitOff) };

            var monitors = SvantekFixture.SingleActiveMonitorDto(serialId, startTime.AddMinutes(-1).ToUniversalTime());
            dbClient.Setup(c => c.ReadMonitorList(null)).
                   Returns(monitors);

            var ruleId = Guid.NewGuid();
            var contacts = SvantekFixture.AlertContacts();
            dbClient.Setup(c => c.ReadAlertContacts(monitors[0].Id, out It.Ref<Guid>.IsAny)).Returns(contacts);

            var durationSeconds = 15 * 60;
            var rule = new RvtAlertRuleDto(ruleId, serialId, "LAeq", limitOn, limitOff, durationSeconds,
                                                    SvantekFixture.CreateActiveRuleActivity(startTime.AddHours(-1), startTime.AddHours(1)),
                                                    AlertType.Alert, false, false, DateTime.UtcNow, null);

            dbClient.SetupSequence(c => c.ReadRules(serialId)).
                                Returns(new List<RvtAlertRuleDto> { rule }).
                                Returns(new List<RvtAlertRuleDto> { new(ruleId, serialId, "LAeq", limitOn, limitOff,  durationSeconds,
                                                            SvantekFixture.CreateActiveRuleActivity(startTime.AddHours(-1), startTime.AddHours(1)),
                                                            AlertType.Alert, false, false, DateTime.UtcNow, null) }).
                                Returns(new List<RvtAlertRuleDto> { new(ruleId, serialId, "LAeq", limitOn, limitOff,  durationSeconds,
                                                            SvantekFixture.CreateActiveRuleActivity(startTime.AddHours(-1), startTime.AddHours(1)),
                                                            AlertType.Alert, true, false, DateTime.UtcNow, null) });



            httpClient.SetupSequence(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId))).
                                 Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(alertingMeasurements))).
                                 Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(alertingMeasurements))).
                                 Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(nonAlertingMeasurements)));

            dbClient.Setup(c => c.HasOpenNotification(monitors[0].Id, "LAeq", rule.AlertType)).
                Returns(false);

            testObj.StoreNoiseLevels("foo", "bar");
            testObj.StoreNoiseLevels("foo", "bar");
            testObj.StoreNoiseLevels("foo", "bar");

            httpClient.Verify(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId)),
                Times.Exactly(3));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(3));
            mqttClient.Verify(c => c.PublishAsync(RvtConfig.ALERT_TOPIC, It.IsAny<string>()), Times.Exactly(2));
            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(3));
            dbClient.Verify(c => c.ReadRules(serialId), Times.Exactly(3));
            dbClient.Verify(c => c.InsertNoiseDtos(serialId, It.IsAny<List<NoiseDto>>()), Times.Exactly(3));

            dbClient.Verify(c => c.WriteLatestTimestamp(serialId, startTime.ToUniversalTime()), Times.Exactly(2));
            dbClient.Verify(c => c.WriteLatestTimestamp(serialId, startTime.AddMinutes(15).ToUniversalTime()), Times.Exactly(1));

            dbClient.Verify(c => c.ReadAlertContacts(monitors[0].Id, out It.Ref<Guid>.IsAny), Times.Exactly(2));
            dbClient.Verify(c => c.WriteNotification(It.Is<NotificationDto>(dto => TestUtil.VerifyNotificationDto(dto, rule, limitOn, startTime.ToUniversalTime(), durationSeconds, limitOn))),
                Times.Exactly(2));
            dbClient.Verify(c => c.HasOpenNotification(monitors[0].Id, "LAeq", rule.AlertType), Times.Exactly(0));
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, serialId, "LAeq", true))),
                Times.Exactly(2));
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, serialId, "LAeq", false))),
                Times.Exactly(1));
            dbClient.Verify(c => c.WriteNotificationAudit(It.IsAny<Guid>(), "baz@bob.org", NotificationConstants.SENT_OK));
            dbClient.VerifyNoOtherCalls();

            messageService.Verify(c => c.Sendmessage(MessageService.MessageContent.MessageEnum.Alert, MessageService.MessageContent.MessageTypeEnum.Email, ContactEquivalentTo(contacts[0]), monitors[0].FleetNr!, It.IsAny<string>()), Times.Exactly(2));
            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreNoiseLevels_AlertRuleActiveWritesAlertAccordingToAlertDelay_Success()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            var serialId = "MyDev1XXX";
            var startTime = DateTime.Parse("2023-10-03T13:10:00+00:00");

            var alertLevel = 10.0;
            var measurements = new List<SampleResponse> { SvantekFixture.CreateSampleResponse(startTime, serialId, alertLevel + 1) };
            var monitors = SvantekFixture.SingleActiveMonitorDto(serialId, startTime.AddMinutes(-1).ToUniversalTime());
            dbClient.Setup(c => c.ReadMonitorList(null)).
                               Returns(monitors);

            var ruleId = Guid.NewGuid();
            var contacts = SvantekFixture.AlertContacts();
            dbClient.Setup(c => c.ReadAlertContacts(monitors[0].Id, out It.Ref<Guid>.IsAny)).Returns(contacts);

            httpClient.Setup(c => c.GetAsync(string.Format("/latestData?userID=blah&token=blahh&instrumentID={0}", serialId))).
                                    Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(measurements)));

            var ruleActivity = SvantekFixture.CreateActiveRuleActivity(null, null);
            var durationSeconds = 15 * 60;
            var created = DateTime.UtcNow;
            var rule = new RvtAlertRuleDto(ruleId, serialId, "LAeq", alertLevel, 1.0, durationSeconds,
                                                   ruleActivity,
                                                   AlertType.Alert, false, false, created, null);
            dbClient.SetupSequence(c => c.ReadRules(serialId)).
                                Returns(new List<RvtAlertRuleDto> { rule }).
                                Returns(new List<RvtAlertRuleDto> { new(ruleId, serialId, "LAeq", alertLevel, 1.0, durationSeconds,
                                                            ruleActivity,
                                                            AlertType.Alert, true, false, created, created.AddMinutes(-(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES-1)) )}).
                                Returns(new List<RvtAlertRuleDto> { new(ruleId, serialId, "LAeq", alertLevel, 1.0, durationSeconds,
                                                            ruleActivity,
                                                            AlertType.Alert, true, false, created, created.AddMinutes(-(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES+1)) )});

            dbClient.Setup(c => c.HasOpenNotification(monitors[0].Id, "Pm1", rule.AlertType)).
                   Returns(false);

            // first store noise levels should trigger an alert, second should not as it occurred before RULE_ALERT_DELAY_MINUTES but 3rd should as it's after RULE_ALERT_DELAY_MINUTES
            testObj.StoreNoiseLevels("blah", "blahh");
            testObj.StoreNoiseLevels("blah", "blahh");
            testObj.StoreNoiseLevels("blah", "blahh");

            httpClient.Verify(c => c.GetAsync(string.Format("/latestData?userID=blah&token=blahh&instrumentID={0}", serialId)),
                Times.Exactly(3));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(3));
            mqttClient.Verify(c => c.PublishAsync(RvtConfig.ALERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(3));
            dbClient.Verify(c => c.ReadRules(serialId), Times.Exactly(3));
            dbClient.Verify(c => c.InsertNoiseDtos(serialId, It.IsAny<List<NoiseDto>>()), Times.Exactly(3));
            dbClient.Verify(c => c.WriteLatestTimestamp(serialId, startTime.ToUniversalTime()), Times.Exactly(3));
            var expectedDateTime = DateTime.Parse("2023-10-03T13:10:00");
            var expectedStartTime = expectedDateTime.AddSeconds(-durationSeconds);
            dbClient.Verify(c => c.ReadAlertContacts(monitors[0].Id, out It.Ref<Guid>.IsAny), Times.Exactly(1));
            dbClient.Verify(c => c.WriteNotification(It.Is<NotificationDto>(
                dto => TestUtil.VerifyNotificationDto(dto, rule, alertLevel + 1, expectedDateTime, durationSeconds, alertLevel))),
                Times.Exactly(1));
            dbClient.Verify(c => c.HasOpenNotification(monitors[0].Id, "LAeq", rule.AlertType),
                Times.Exactly(0));
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, serialId, "LAeq", true))),
                Times.Exactly(1));

            dbClient.Verify(c => c.WriteNotificationAudit(It.IsAny<Guid>(), "baz@bob.org", NotificationConstants.SENT_OK));
            dbClient.VerifyNoOtherCalls();

            messageService.Verify(c => c.Sendmessage(MessageService.MessageContent.MessageEnum.Alert, MessageService.MessageContent.MessageTypeEnum.Email, ContactEquivalentTo(contacts[0]), monitors[0].FleetNr!, It.IsAny<string>()), Times.Exactly(1));

            messageService.VerifyNoOtherCalls();

        }

        [DataTestMethod]
        [DynamicData(nameof(OneAlertContact), DynamicDataSourceType.Method)]
        [DynamicData(nameof(TwoAlertContacts), DynamicDataSourceType.Method)]
        [DynamicData(nameof(ThreeAlertContacts), DynamicDataSourceType.Method)]
        public void TestStoreNoiseLevels_WithVaryingNumberOfContactsForAlertRule_Success(List<RvtContactDto> contacts)
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                         out Mock<IDBClient> dbClient,
                                         out Mock<IMqttClient> mqttClient,
                                         out Mock<IMessageService> messageService);

            var startTime = DateTime.Parse("2023-10-03T13:10:00+00:00");

            var alertLevel = 10.0;
            var serialId = "MyDev1AbC";
            var measurements = new List<SampleResponse> { SvantekFixture.CreateSampleResponse(startTime, serialId, alertLevel + 1) };
            var monitors = SvantekFixture.SingleActiveMonitorDto(serialId, startTime.AddMinutes(-1).ToUniversalTime());
            dbClient.Setup(c => c.ReadMonitorList(null)).
                   Returns(monitors);

            var ruleId = Guid.NewGuid();

            dbClient.Setup(c => c.ReadAlertContacts(monitors[0].Id, out It.Ref<Guid>.IsAny)).Returns(contacts);

            httpClient.Setup(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId))).
                                    Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(measurements)));

            var ruleActivity = SvantekFixture.CreateActiveRuleActivity(null, null);
            var durationSeconds = 15 * 60;
            var created = DateTime.UtcNow;

            var rule = new RvtAlertRuleDto(ruleId, serialId, "LAeq", alertLevel, 1.0, durationSeconds,
                                                    ruleActivity,
                                                    AlertType.Alert, false, false, created, null);
            dbClient.Setup(c => c.ReadRules(serialId)).
                                Returns(new List<RvtAlertRuleDto> { rule });
            dbClient.Setup(c => c.HasOpenNotification(monitors[0].Id, "LAeq", rule.AlertType)).
                Returns(false);

            // first store noise levels should trigger an alert, second should not as it occurred before RULE_ALERT_DELAY_MINUTES but 3rd should as it's after RULE_ALERT_DELAY_MINUTES
            testObj.StoreNoiseLevels("foo", "bar");

            httpClient.Verify(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId)),
                Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));


            mqttClient.Verify(c => c.PublishAsync(RvtConfig.ALERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.ReadRules(serialId), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos(serialId, It.IsAny<List<NoiseDto>>()), Times.Exactly(1));

            dbClient.Verify(c => c.WriteLatestTimestamp(serialId, startTime.ToUniversalTime()),
                Times.Exactly(1));

            var expectedDateTime = DateTime.Parse("2023-10-03T13:10:00");
            var expectedStartTime = expectedDateTime.AddSeconds(-durationSeconds);

            dbClient.Verify(c => c.ReadAlertContacts(monitors[0].Id, out It.Ref<Guid>.IsAny), Times.Exactly(1));
            dbClient.Verify(c => c.WriteNotification(It.Is<NotificationDto>(
                dto => TestUtil.VerifyNotificationDto(dto, rule, alertLevel + 1, expectedDateTime, durationSeconds, alertLevel))),
                Times.Exactly(1));
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, serialId, "LAeq", true))),
                Times.Exactly(1));

            dbClient.Verify(c => c.HasOpenNotification(monitors[0].Id, "LAeq", rule.AlertType), Times.Exactly(0));
            if (contacts.Count == 1)
            {
                messageService.Verify(c => c.Sendmessage(MessageService.MessageContent.MessageEnum.Alert, MessageService.MessageContent.MessageTypeEnum.Email, ContactEquivalentTo(contacts[0]), monitors[0].FleetNr!, It.IsAny<string>()), Times.Exactly(1));
                dbClient.Verify(c => c.WriteNotificationAudit(It.IsAny<Guid>(), "baz@bob.org", NotificationConstants.SENT_OK));
            }
            else if (contacts.Count == 2)
            {
                messageService.Verify(c => c.Sendmessage(MessageService.MessageContent.MessageEnum.Alert, MessageService.MessageContent.MessageTypeEnum.Email, ContactEquivalentTo(contacts[0]), monitors[0].FleetNr!, It.IsAny<string>()), Times.Exactly(1));
                messageService.Verify(c => c.Sendmessage(MessageService.MessageContent.MessageEnum.Alert, MessageService.MessageContent.MessageTypeEnum.SMS, ContactEquivalentTo(contacts[1]), monitors[0].FleetNr!, It.IsAny<string>()), Times.Exactly(1));
                dbClient.Verify(c => c.WriteNotificationAudit(It.IsAny<Guid>(), "foo@bob.org", NotificationConstants.SENT_OK));
                dbClient.Verify(c => c.WriteNotificationAudit(It.IsAny<Guid>(), "01234567890", NotificationConstants.SENT_OK));
            }
            else if (contacts.Count == 3)
            {
                messageService.Verify(c => c.Sendmessage(MessageService.MessageContent.MessageEnum.Alert, MessageService.MessageContent.MessageTypeEnum.Email, ContactEquivalentTo(contacts[0]), monitors[0].FleetNr!, It.IsAny<string>()), Times.Exactly(1));
                messageService.Verify(c => c.Sendmessage(MessageService.MessageContent.MessageEnum.Alert, MessageService.MessageContent.MessageTypeEnum.SMS, ContactEquivalentTo(contacts[1]), monitors[0].FleetNr!, It.IsAny<string>()), Times.Exactly(1));
                messageService.Verify(c => c.Sendmessage(MessageService.MessageContent.MessageEnum.Alert, MessageService.MessageContent.MessageTypeEnum.Email, ContactEquivalentTo(contacts[2]), monitors[0].FleetNr!, It.IsAny<string>()), Times.Exactly(1));
                messageService.Verify(c => c.Sendmessage(MessageService.MessageContent.MessageEnum.Alert, MessageService.MessageContent.MessageTypeEnum.SMS, ContactEquivalentTo(contacts[2]), monitors[0].FleetNr!, It.IsAny<string>()), Times.Exactly(1));
                dbClient.Verify(c => c.WriteNotificationAudit(It.IsAny<Guid>(), "XXX@bob.org", NotificationConstants.SENT_OK));
                dbClient.Verify(c => c.WriteNotificationAudit(It.IsAny<Guid>(), "01234567890", NotificationConstants.SENT_OK));
                dbClient.Verify(c => c.WriteNotificationAudit(It.IsAny<Guid>(), "bar@bazbaz.org", NotificationConstants.SENT_OK));
                dbClient.Verify(c => c.WriteNotificationAudit(It.IsAny<Guid>(), "9988776655", NotificationConstants.SENT_OK));
            }
            else
            {
                Assert.Fail();
            }
            dbClient.VerifyNoOtherCalls();
            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreNoiseLevels_AlertRuleActivatedButSendMessageFails_Success()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            var startTime = DateTime.Parse("2023-10-03T13:10:00+00:00");

            var limitOn = 10.0;
            var limitOff = 8.0;
            var serialId = "MyDevice123";
            var measurements =
                          new List<SampleResponse> { SvantekFixture.CreateSampleResponse(startTime, serialId, limitOn) };

            var monitors = SvantekFixture.SingleActiveMonitorDto(serialId, startTime.AddMinutes(-1).ToUniversalTime());
            dbClient.Setup(c => c.ReadMonitorList(null)).
                   Returns(monitors);

            httpClient.Setup(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId))).
                                    Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(measurements)));

            var ruleId = Guid.NewGuid(); ;
            var contacts = SvantekFixture.AlertContacts();
            dbClient.Setup(c => c.ReadAlertContacts(monitors[0].Id, out It.Ref<Guid>.IsAny)).Returns(contacts);
            var durationSeconds = 15 * 60;
            var rule = new RvtAlertRuleDto(ruleId, serialId, "LAeq", limitOn, limitOff, durationSeconds,
                                                    SvantekFixture.CreateActiveRuleActivity(startTime.AddHours(-1), startTime.AddHours(1)),
                                                    AlertType.Alert, false, false, DateTime.UtcNow, null);
            dbClient.Setup(c => c.ReadRules(serialId)).
                                Returns(new List<RvtAlertRuleDto> { rule });

            dbClient.Setup(c => c.HasOpenNotification(monitors[0].Id, It.IsAny<string>(), rule.AlertType)).
                Returns(false);

            messageService.Setup(c => c.Sendmessage(MessageService.MessageContent.MessageEnum.Alert, MessageService.MessageContent.MessageTypeEnum.Email, ContactEquivalentTo(contacts[0]), monitors[0].FleetNr!, It.IsAny<string>())).
                Throws(CommsException.Of("test-address", "test-message"));

            testObj.StoreNoiseLevels("foo", "bar");

            httpClient.Verify(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId)),
                Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.Verify(c => c.PublishAsync(RvtConfig.ALERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.ReadRules(serialId), Times.Exactly(1));

            dbClient.Verify(c => c.InsertNoiseDtos(serialId, It.IsAny<List<NoiseDto>>()), Times.Exactly(1));

            dbClient.Verify(c => c.WriteLatestTimestamp(serialId, startTime.ToUniversalTime()), Times.Exactly(1));

            var expectedDateTime = DateTime.Parse("2023-10-03T13:10:00");
            var expectedStartTime = expectedDateTime.AddSeconds(-durationSeconds);
            dbClient.Verify(c => c.ReadAlertContacts(monitors[0].Id, out It.Ref<Guid>.IsAny), Times.Exactly(1));


            dbClient.Verify(c => c.WriteNotification(It.Is<NotificationDto>(
                dto => TestUtil.VerifyNotificationDto(dto, rule, limitOn, expectedDateTime, durationSeconds, limitOn))),
                Times.Exactly(1));
            dbClient.Verify(c => c.HasOpenNotification(monitors[0].Id, It.IsAny<string>(), rule.AlertType), Times.Exactly(0));

            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, serialId, "LAeq", true))),
                Times.Exactly(1));
            dbClient.Verify(c => c.WriteNotificationAudit(It.IsAny<Guid>(), "test-address", "test-message"), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            messageService.Verify(c => c.Sendmessage(MessageService.MessageContent.MessageEnum.Alert, MessageService.MessageContent.MessageTypeEnum.Email, ContactEquivalentTo(contacts[0]), monitors[0].FleetNr!, It.IsAny<string>()), Times.Exactly(1));
            messageService.VerifyNoOtherCalls();
        }


        [DataRow("Fri, 10 Mar 2023 14:10:00Z", null, null, 1)]
        [DataRow("Thu, 09 Mar 2023 14:10:00Z", "09:00:00", "10:00:00", 0)]
        [DataRow("Wed, 08 Mar 2023 14:10:00Z", "09:00:00", "15:00:00", 1)]
        [DataRow("Sat, 16 Dec 2023 14:10:00Z", null, null, 1)]
        [DataRow("Sat, 16 Dec 2023 14:10:00Z", "14:00:00", "15:00:00", 1)]
        [DataRow("Sat, 17 Jun 2023 14:10:00Z", "14:00:00", "14:09:00", 0)]
        [DataRow("Sun, 17 Dec 2023 14:10:00Z", null, null, 1)]
        [DataRow("Sun, 17 Dec 2023 14:10:00Z", "14:09:00", "15:00:00", 1)]
        [DataRow("Sun, 18 Jun 2023 14:10:00Z", "08:00:00", "14:09:00", 0)]
        [DataTestMethod]
        public void TestStoreNoiseLevels_AlertRuleActivatedButSendMessageExcludedBySendTime_Success(
            string dataTimeStr, string? sendStartTimeStr, string? sendEndTimeStr, int numExpectedMessages)
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            var dataTime = DateTime.Parse(dataTimeStr).ToUniversalTime();

            TimeSpan? sendStartTime = sendStartTimeStr == null ? null : TimeSpan.Parse(sendStartTimeStr);
            TimeSpan? sendEndTime = sendEndTimeStr == null ? null : TimeSpan.Parse(sendEndTimeStr);

            var limitOn = 10.0;
            var limitOff = 8.0;
            var serialId = "MyDevice123";
            var measurements =
                          new List<SampleResponse> { SvantekFixture.CreateSampleResponse(dataTime, serialId, limitOn) };

            var monitors = SvantekFixture.SingleActiveMonitorDto(serialId, dataTime.AddMinutes(-1).ToUniversalTime());
            dbClient.Setup(c => c.ReadMonitorList(null)).
                   Returns(monitors);

            httpClient.Setup(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId))).
                                    Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(measurements)));

            var ruleId = Guid.NewGuid();
            var contacts = SvantekFixture.AlertContacts(sendStartTime, sendEndTime);
            dbClient.Setup(c => c.ReadAlertContacts(monitors[0].Id, out It.Ref<Guid>.IsAny)).Returns(contacts);
            var durationSeconds = 15 * 60;
            var rule = new RvtAlertRuleDto(ruleId, serialId, "LAeq", limitOn, limitOff, durationSeconds,
                                                    SvantekFixture.CreateActiveRuleActivity(dataTime.AddHours(-1), dataTime.AddHours(1)),
                                                    AlertType.Alert, false, false, DateTime.UtcNow, null);
            dbClient.Setup(c => c.ReadRules(serialId)).
                                Returns(new List<RvtAlertRuleDto> { rule });

            dbClient.Setup(c => c.HasOpenNotification(monitors[0].Id, It.IsAny<string>(), It.IsAny<AlertType>())).
                    Returns(false);

            testObj.StoreNoiseLevels("foo", "bar"); //Runs the StoreNoiseLevels function so that we can verify that all required functions have been triggered

            httpClient.Verify(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId)),
                Times.Exactly(1)); //Checks that the GetAsync function has been ran with the correct parameters only 1 time (as shown above)
            httpClient.VerifyNoOtherCalls();// checks that no other calls have been made by the httpClient

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(1)); //Checks that the PublishAsync function has been ran with the RVTConfig.INSERT_TOPIC 1 time
            mqttClient.Verify(c => c.PublishAsync(RvtConfig.ALERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.ReadRules(serialId), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos(serialId, It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.Verify(c => c.WriteLatestTimestamp(serialId, dataTime.ToUniversalTime()),
                Times.Exactly(1));
            var expectedEndTime = dataTime;
            var expectedStartTime = expectedEndTime.AddSeconds(-durationSeconds);
            dbClient.Verify(c => c.ReadAlertContacts(monitors[0].Id, out It.Ref<Guid>.IsAny), Times.Exactly(1));
            dbClient.Verify(c => c.WriteNotification(It.Is<NotificationDto>(
                dto => TestUtil.VerifyNotificationDto(dto, rule, limitOn, expectedEndTime, durationSeconds, limitOn))),
                Times.Exactly(1));
            dbClient.Verify(c => c.WriteNotificationAudit(It.IsAny<Guid>(), "baz@bob.org", NotificationConstants.SENT_OK),
                Times.Exactly(numExpectedMessages));
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, serialId, "LAeq", true))),
                Times.Exactly(1));
            foreach (var monitor in monitors)
            {
                dbClient.Verify(c => c.HasOpenNotification(monitor.Id, It.IsAny<string>(), rule.AlertType),
                    Times.Exactly(0));
            }
            dbClient.VerifyNoOtherCalls();

            messageService.Verify(c => c.Sendmessage(MessageService.MessageContent.MessageEnum.Alert, MessageService.MessageContent.MessageTypeEnum.Email, ContactEquivalentTo(contacts[0]), monitors[0].FleetNr!, It.IsAny<string>()),
                Times.Exactly(numExpectedMessages));
            messageService.VerifyNoOtherCalls();
        }


        private static IEnumerable<object[]> DateExclusion()
        {
            yield return new object[] {
                new List<RvtAlertRuleDto> { new(Guid.NewGuid(), "Device1", "LAeq", 19.0,19.0, 15 * 60,
                                                new AlertActivityTimeDto { Weekdays = false, Sundays = true,Saturdays = true,
                                                StartTime = null, EndTime = null}, AlertType.Alert, false, false,
                                                DateTime.UtcNow, DateTime.UtcNow.AddMinutes(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES+1))
                }
            };
        }

        private static IEnumerable<object[]> DeletedExclusion()
        {
            yield return new object[] {
                new List<RvtAlertRuleDto> { new(Guid.NewGuid(), "Device1", "LAeq", 19.0,19.0, 15 * 60,
                                                new AlertActivityTimeDto { Weekdays = true, Sundays = true,Saturdays = true,
                                                StartTime = null, EndTime = null}, AlertType.Alert, false, true,
                                                DateTime.UtcNow, DateTime.UtcNow.AddMinutes(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES+1))
                }
            };
        }

        private static IEnumerable<object[]> TimeExclusion()
        {
            yield return new object[] {
                new List<RvtAlertRuleDto> { new(Guid.NewGuid(), "Device1", "LAeq", 19.0,19.0, 15 * 60,
                                                SvantekFixture.CreateActiveRuleActivity(DateTime.Parse("2023-09-25T00:01:02"),DateTime.Parse("2023-09-25T00:01:03")),
                                                AlertType.Alert, false, false,
                                                DateTime.UtcNow, DateTime.UtcNow.AddMinutes(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES+1))
                }
            };
        }

        private static IEnumerable<object[]> LevelExclusion()
        {
            yield return new object[] {
                new List<RvtAlertRuleDto> { new(Guid.NewGuid(), "Device1", "LAeq", 50.0,19.0, 15 * 60,
                                                SvantekFixture.CreateActiveRuleActivity(null,null),
                                                AlertType.Alert, false, false,
                                                DateTime.UtcNow, DateTime.UtcNow.AddMinutes(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES+1))
                }
            };
        }

        private static IEnumerable<object[]> OneAlertContact()
        {
            yield return new object[] { new List<RvtContactDto>()
                {
                    new RvtContactDto(contactMethod:ContactMethod.Email,
                                      emailAddress: "baz@bob.org",
                                      phoneNumber: null,
                                      email: true,
                                      sms: false,
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
                                      email: true,
                                      sms: false,
                                      sendStartTime: null,
                                      sendEndTime: null),
                    new RvtContactDto(contactMethod: ContactMethod.SMS,
                                      emailAddress: "blah",
                                      phoneNumber: "01234567890",
                                      email: false,
                                      sms: true,
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
                                      email: true,
                                      sms: false,
                                      sendStartTime: null,
                                      sendEndTime: null),
                    new RvtContactDto(contactMethod: ContactMethod.SMS,
                                      emailAddress: "bbbb@cccc.ddd",
                                      phoneNumber: "01234567890",
                                      email: false,
                                      sms: true,
                                      sendStartTime: null,
                                      sendEndTime: null),
                    new RvtContactDto(contactMethod:ContactMethod.SMSAndEmail,
                                      emailAddress: "bar@bazbaz.org",
                                      phoneNumber: "9988776655",
                                      email: true,
                                      sms: true,
                                      sendStartTime: null,
                                      sendEndTime: null)
                }
            };
        }
    }
}
