using System.Text.Json;
using AirQ.Api;
using AirQ.Api.Db;
using AirQ.Api.Http;
using AirQ.Model.Dto;
using AirQ.Model.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
namespace AirQMonitorTests
{
    [TestClass]
    public class TestRules
    {
        public TestRules()
        {
            ILoggerFactory factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestRules");
        }


        [TestMethod]
        [DynamicData(nameof(DateExclusion))]
        [DynamicData(nameof(DeletedExclusion))]
        [DynamicData(nameof(TimeExclusion))]
        [DynamicData(nameof(LevelExclusion))]
        public async Task TestStoreNoiseLevels_WithAlertRuleExclusion_Success(List<RvtAlertRuleDto> rules)
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/latestData\\?userID=foo&token=bar&instrumentID=*"), It.IsAny<CancellationToken>())).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.SamplesResponseJson()));
            List<NoiseMonitorDto> monitors = AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE);
            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(monitors);

            dbClient.Setup(c => c.ReadRules("Device1")).
                Returns(rules);

            await testObj.StoreNoiseLevelsAsync("foo", "bar");

            httpClient.Verify(c => c.GetAsync("/latestData?userID=foo&token=bar&instrumentID=Device1", It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestData?userID=foo&token=bar&instrumentID=Device2", It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestData?userID=foo&token=bar&instrumentID=Device3", It.IsAny<CancellationToken>()), Times.Exactly(1));
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

            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreNoiseLevels_AlertRuleActivatedThenDeactivatedByActivityWindow_Success()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            DateTime startTime = DateTime.Parse("2023-10-03T13:10:00+00:00");

            double alertLevel = 10.0;
            string serialId = "MyDevice";
            List<SampleResponse> measurements1 = [
                        AirQFixture.CreateSampleResponse(startTime, serialId, alertLevel + 1) ];
            List<SampleResponse> measurements2 = [
                        AirQFixture.CreateSampleResponse(startTime.AddMinutes(15), serialId, alertLevel + 1) ];

            List<NoiseMonitorDto> monitors = AirQFixture.SingleActiveMonitorDto(serialId, startTime.AddMinutes(-1).ToUniversalTime());
            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(monitors);

            Guid ruleId = Guid.NewGuid();

            httpClient.SetupSequence(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId), It.IsAny<CancellationToken>())).
                                 Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(measurements1))).
                                 Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(measurements2)));

            int durationSeconds = 15 * 60;
            RvtAlertRuleDto ruleOn = new(ruleId, serialId, "LAeq", alertLevel, 1.0, durationSeconds,
                                                    AirQFixture.CreateActiveRuleActivity(startTime, startTime.AddSeconds(30)),
                                                    AlertType.Alert, false, false, DateTime.UtcNow, null);
            dbClient.SetupSequence(c => c.ReadRules(serialId)).
                                Returns([ruleOn]).
                                Returns([ new(ruleId, serialId, "LAeq", alertLevel, 1.0, durationSeconds,
                                                            AirQFixture.CreateActiveRuleActivity(startTime, startTime.AddSeconds(30)),
                                                            AlertType.Alert, true, false, DateTime.UtcNow,
                                                            DateTime.UtcNow.AddMinutes(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES+1) )]);

            // first store noise levels should trigger an alert second should cancel it
            await testObj.StoreNoiseLevelsAsync("foo", "bar");
            await testObj.StoreNoiseLevelsAsync("foo", "bar");

            httpClient.Verify(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(2));
            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(2));
            dbClient.Verify(c => c.ReadRules(serialId), Times.Exactly(2));
            dbClient.Verify(c => c.InsertNoiseDtos(serialId, It.IsAny<List<NoiseDto>>()), Times.Exactly(2));

            dbClient.Verify(c => c.WriteLatestTimestamp(serialId, startTime.ToUniversalTime()), Times.Exactly(1));
            dbClient.Verify(c => c.WriteLatestTimestamp(serialId, startTime.AddMinutes(15).ToUniversalTime()), Times.Exactly(1));

            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, serialId, "LAeq", true))),
                Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            // The durable stack owns notification persistence, contact fan-out,
            // audits, and MQTT delivery; the breach contract is one signal.
            messageClient.Verify(ingress => ingress.AcceptAsync(
                It.Is<AlertSignal>(signal =>
                    signal.Source == "airq.rules" &&
                    signal.SerialId == serialId &&
                    signal.AlertType == AlertType.Alert &&
                    signal.Field == "LAeq" &&
                    signal.Level == alertLevel + 1 &&
                    signal.Limit == alertLevel &&
                    signal.AveragingPeriod == durationSeconds &&
                    signal.DeliveryChannels == (AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms) &&
                    signal.SuppressionWindow == TimeSpan.Zero),
                It.IsAny<CancellationToken>()), Times.Once);
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreNoiseLevels_AlertRuleActivatedThenDeactivatedByNoiseLimitOnOff_Success()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            // In this test we get 3 noise levels first 2 should trigger an alert third should remove the trigger
            DateTime startTime = DateTime.Parse("2023-10-03T13:10:00+00:00");

            double limitOn = 10.0;
            double limitOff = 8.0;
            string serialId = "MyDev1";
            List<SampleResponse> alertingMeasurements =
                [AirQFixture.CreateSampleResponse(startTime, serialId, limitOn)];
            List<SampleResponse> nonAlertingMeasurements =
                [AirQFixture.CreateSampleResponse(startTime.AddMinutes(15), serialId, limitOff)];

            List<NoiseMonitorDto> monitors = AirQFixture.SingleActiveMonitorDto(serialId, startTime.AddMinutes(-1).ToUniversalTime());
            dbClient.Setup(c => c.ReadMonitorList(null)).
                   Returns(monitors);

            Guid ruleId = Guid.NewGuid();

            int durationSeconds = 15 * 60;
            RvtAlertRuleDto rule = new(ruleId, serialId, "LAeq", limitOn, limitOff, durationSeconds,
                                                    AirQFixture.CreateActiveRuleActivity(startTime.AddHours(-1), startTime.AddHours(1)),
                                                    AlertType.Alert, false, false, DateTime.UtcNow, null);

            dbClient.SetupSequence(c => c.ReadRules(serialId)).
                                Returns([rule]).
                                Returns([ new(ruleId, serialId, "LAeq", limitOn, limitOff,  durationSeconds,
                                                            AirQFixture.CreateActiveRuleActivity(startTime.AddHours(-1), startTime.AddHours(1)),
                                                            AlertType.Alert, false, false, DateTime.UtcNow, null) ]).
                                Returns([ new(ruleId, serialId, "LAeq", limitOn, limitOff,  durationSeconds,
                                                            AirQFixture.CreateActiveRuleActivity(startTime.AddHours(-1), startTime.AddHours(1)),
                                                            AlertType.Alert, true, false, DateTime.UtcNow, null) ]);



            httpClient.SetupSequence(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId), It.IsAny<CancellationToken>())).
                                 Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(alertingMeasurements))).
                                 Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(alertingMeasurements))).
                                 Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(nonAlertingMeasurements)));

            await testObj.StoreNoiseLevelsAsync("foo", "bar");
            await testObj.StoreNoiseLevelsAsync("foo", "bar");
            await testObj.StoreNoiseLevelsAsync("foo", "bar");

            httpClient.Verify(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId), It.IsAny<CancellationToken>()),
                Times.Exactly(3));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(3));
            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(3));
            dbClient.Verify(c => c.ReadRules(serialId), Times.Exactly(3));
            dbClient.Verify(c => c.InsertNoiseDtos(serialId, It.IsAny<List<NoiseDto>>()), Times.Exactly(3));

            dbClient.Verify(c => c.WriteLatestTimestamp(serialId, startTime.ToUniversalTime()), Times.Exactly(2));
            dbClient.Verify(c => c.WriteLatestTimestamp(serialId, startTime.AddMinutes(15).ToUniversalTime()), Times.Exactly(1));

            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, serialId, "LAeq", true))),
                Times.Exactly(2));
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, serialId, "LAeq", false))),
                Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            // One durable signal per breach; delivery fan-out is downstream.
            messageClient.Verify(ingress => ingress.AcceptAsync(
                It.Is<AlertSignal>(signal =>
                    signal.Source == "airq.rules" &&
                    signal.SerialId == serialId &&
                    signal.AlertType == AlertType.Alert &&
                    signal.Field == "LAeq" &&
                    signal.Level == limitOn &&
                    signal.Limit == limitOn &&
                    signal.AveragingPeriod == durationSeconds &&
                    signal.DeliveryChannels == (AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms) &&
                    signal.SuppressionWindow == TimeSpan.Zero),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreNoiseLevels_AlertRuleActiveWritesAlertAccordingToAlertDelay_Success()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            string serialId = "MyDev1XXX";
            DateTime startTime = DateTime.Parse("2023-10-03T13:10:00+00:00");

            double alertLevel = 10.0;
            List<SampleResponse> measurements = [AirQFixture.CreateSampleResponse(startTime, serialId, alertLevel + 1)];
            List<NoiseMonitorDto> monitors = AirQFixture.SingleActiveMonitorDto(serialId, startTime.AddMinutes(-1).ToUniversalTime());
            dbClient.Setup(c => c.ReadMonitorList(null)).
                               Returns(monitors);

            Guid ruleId = Guid.NewGuid();

            httpClient.Setup(c => c.GetAsync(string.Format("/latestData?userID=blah&token=blahh&instrumentID={0}", serialId), It.IsAny<CancellationToken>())).
                                    Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(measurements)));

            AlertActivityTimeDto ruleActivity = AirQFixture.CreateActiveRuleActivity(null, null);
            int durationSeconds = 15 * 60;
            DateTime created = DateTime.UtcNow;
            RvtAlertRuleDto rule = new(ruleId, serialId, "LAeq", alertLevel, 1.0, durationSeconds,
                                                   ruleActivity,
                                                   AlertType.Alert, false, false, created, null);
            dbClient.SetupSequence(c => c.ReadRules(serialId)).
                                Returns([rule]).
                                Returns([ new(ruleId, serialId, "LAeq", alertLevel, 1.0, durationSeconds,
                                                            ruleActivity,
                                                            AlertType.Alert, true, false, created, created.AddMinutes(-(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES-1)) )]).
                                Returns([ new(ruleId, serialId, "LAeq", alertLevel, 1.0, durationSeconds,
                                                            ruleActivity,
                                                            AlertType.Alert, true, false, created, created.AddMinutes(-(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES+1)) )]);

            // first store noise levels should trigger an alert; while the rule stays latched
            // the later runs emit nothing (repeat-delivery timing lives in the durable stack)
            await testObj.StoreNoiseLevelsAsync("blah", "blahh");
            await testObj.StoreNoiseLevelsAsync("blah", "blahh");
            await testObj.StoreNoiseLevelsAsync("blah", "blahh");

            httpClient.Verify(c => c.GetAsync(string.Format("/latestData?userID=blah&token=blahh&instrumentID={0}", serialId), It.IsAny<CancellationToken>()),
                Times.Exactly(3));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(3));
            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(3));
            dbClient.Verify(c => c.ReadRules(serialId), Times.Exactly(3));
            dbClient.Verify(c => c.InsertNoiseDtos(serialId, It.IsAny<List<NoiseDto>>()), Times.Exactly(3));
            dbClient.Verify(c => c.WriteLatestTimestamp(serialId, startTime.ToUniversalTime()), Times.Exactly(3));
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, serialId, "LAeq", true))),
                Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            messageClient.Verify(ingress => ingress.AcceptAsync(
                It.Is<AlertSignal>(signal =>
                    signal.Source == "airq.rules" &&
                    signal.SerialId == serialId &&
                    signal.AlertType == AlertType.Alert &&
                    signal.Field == "LAeq" &&
                    signal.Level == alertLevel + 1 &&
                    signal.Limit == alertLevel &&
                    signal.AveragingPeriod == durationSeconds &&
                    signal.DeliveryChannels == (AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms) &&
                    signal.SuppressionWindow == TimeSpan.Zero),
                It.IsAny<CancellationToken>()), Times.Once);
            messageClient.VerifyNoOtherCalls();

        }

        [TestMethod]
        public async Task TestStoreNoiseLevels_AlertRuleBreach_EmitsSingleSignalRegardlessOfContacts()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                         out Mock<IDBClient> dbClient,
                                         out Mock<IMqttClient> mqttClient,
                                         out Mock<IAlertIngressPort> messageClient);

            DateTime startTime = DateTime.Parse("2023-10-03T13:10:00+00:00");

            double alertLevel = 10.0;
            string serialId = "MyDev1AbC";
            List<SampleResponse> measurements = [AirQFixture.CreateSampleResponse(startTime, serialId, alertLevel + 1)];
            List<NoiseMonitorDto> monitors = AirQFixture.SingleActiveMonitorDto(serialId, startTime.AddMinutes(-1).ToUniversalTime());
            dbClient.Setup(c => c.ReadMonitorList(null)).
                   Returns(monitors);

            Guid ruleId = Guid.NewGuid();

            httpClient.Setup(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId), It.IsAny<CancellationToken>())).
                                    Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(measurements)));

            AlertActivityTimeDto ruleActivity = AirQFixture.CreateActiveRuleActivity(null, null);
            int durationSeconds = 15 * 60;
            DateTime created = DateTime.UtcNow;

            RvtAlertRuleDto rule = new(ruleId, serialId, "LAeq", alertLevel, 1.0, durationSeconds,
                                                    ruleActivity,
                                                    AlertType.Alert, false, false, created, null);
            dbClient.Setup(c => c.ReadRules(serialId)).
                                Returns([rule]);

            await testObj.StoreNoiseLevelsAsync("foo", "bar");

            httpClient.Verify(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId), It.IsAny<CancellationToken>()),
                Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.ReadRules(serialId), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos(serialId, It.IsAny<List<NoiseDto>>()), Times.Exactly(1));

            dbClient.Verify(c => c.WriteLatestTimestamp(serialId, startTime.ToUniversalTime()),
                Times.Exactly(1));

            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, serialId, "LAeq", true))),
                Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            // Contact fan-out moved into the durable stack: however many contacts
            // are configured, one breach emits exactly one signal.
            messageClient.Verify(ingress => ingress.AcceptAsync(
                It.Is<AlertSignal>(signal =>
                    signal.Source == "airq.rules" &&
                    signal.SerialId == serialId &&
                    signal.AlertType == AlertType.Alert &&
                    signal.Field == "LAeq" &&
                    signal.Level == alertLevel + 1 &&
                    signal.Limit == alertLevel &&
                    signal.AveragingPeriod == durationSeconds &&
                    signal.DeliveryChannels == (AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms) &&
                    signal.SuppressionWindow == TimeSpan.Zero),
                It.IsAny<CancellationToken>()), Times.Once);
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreNoiseLevels_AlertRuleActivatedDeliveryFailure_SignalStillEmitted()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            DateTime startTime = DateTime.Parse("2023-10-03T13:10:00+00:00");

            double limitOn = 10.0;
            double limitOff = 8.0;
            string serialId = "MyDevice123";
            List<SampleResponse> measurements =
                          [AirQFixture.CreateSampleResponse(startTime, serialId, limitOn)];

            List<NoiseMonitorDto> monitors = AirQFixture.SingleActiveMonitorDto(serialId, startTime.AddMinutes(-1).ToUniversalTime());
            dbClient.Setup(c => c.ReadMonitorList(null)).
                   Returns(monitors);

            httpClient.Setup(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId), It.IsAny<CancellationToken>())).
                                    Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(measurements)));

            Guid ruleId = Guid.NewGuid();
            int durationSeconds = 15 * 60;
            RvtAlertRuleDto rule = new(ruleId, serialId, "LAeq", limitOn, limitOff, durationSeconds,
                                                    AirQFixture.CreateActiveRuleActivity(startTime.AddHours(-1), startTime.AddHours(1)),
                                                    AlertType.Alert, false, false, DateTime.UtcNow, null);
            dbClient.Setup(c => c.ReadRules(serialId)).
                                Returns([rule]);

            // Send failures and their audits now live in the durable dispatcher;
            // in-process the breach still emits its signal and latches the rule.
            await testObj.StoreNoiseLevelsAsync("foo", "bar");

            httpClient.Verify(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId), It.IsAny<CancellationToken>()),
                Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.ReadRules(serialId), Times.Exactly(1));

            dbClient.Verify(c => c.InsertNoiseDtos(serialId, It.IsAny<List<NoiseDto>>()), Times.Exactly(1));

            dbClient.Verify(c => c.WriteLatestTimestamp(serialId, startTime.ToUniversalTime()), Times.Exactly(1));

            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, serialId, "LAeq", true))),
                Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            messageClient.Verify(ingress => ingress.AcceptAsync(
                It.Is<AlertSignal>(signal =>
                    signal.Source == "airq.rules" &&
                    signal.SerialId == serialId &&
                    signal.AlertType == AlertType.Alert &&
                    signal.Field == "LAeq" &&
                    signal.Level == limitOn &&
                    signal.Limit == limitOn &&
                    signal.AveragingPeriod == durationSeconds &&
                    signal.DeliveryChannels == (AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms) &&
                    signal.SuppressionWindow == TimeSpan.Zero),
                It.IsAny<CancellationToken>()), Times.Once);
            messageClient.VerifyNoOtherCalls();
        }


        [DataRow("Fri, 10 Mar 2023 14:10:00Z")]
        [DataRow("Thu, 09 Mar 2023 14:10:00Z")]
        [DataRow("Sat, 17 Jun 2023 14:10:00Z")]
        [DataRow("Sun, 18 Jun 2023 14:10:00Z")]
        [TestMethod]
        public async Task TestStoreNoiseLevels_AlertRuleActivatedOutsideContactSendWindow_SignalStillEmitted(
            string dataTimeStr)
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            DateTime dataTime = DateTime.Parse(dataTimeStr).ToUniversalTime();

            double limitOn = 10.0;
            double limitOff = 8.0;
            string serialId = "MyDevice123";
            List<SampleResponse> measurements =
                          [AirQFixture.CreateSampleResponse(dataTime, serialId, limitOn)];

            List<NoiseMonitorDto> monitors = AirQFixture.SingleActiveMonitorDto(serialId, dataTime.AddMinutes(-1).ToUniversalTime());
            dbClient.Setup(c => c.ReadMonitorList(null)).
                   Returns(monitors);

            httpClient.Setup(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId), It.IsAny<CancellationToken>())).
                                    Returns(Task<string>.Factory.StartNew(() => JsonSerializer.Serialize(measurements)));

            Guid ruleId = Guid.NewGuid();
            int durationSeconds = 15 * 60;
            RvtAlertRuleDto rule = new(ruleId, serialId, "LAeq", limitOn, limitOff, durationSeconds,
                                                    AirQFixture.CreateActiveRuleActivity(dataTime.AddHours(-1), dataTime.AddHours(1)),
                                                    AlertType.Alert, false, false, DateTime.UtcNow, null);
            dbClient.Setup(c => c.ReadRules(serialId)).
                                Returns([rule]);

            // Per-contact send windows are applied by the durable dispatcher at
            // delivery time; whatever the clock says, the breach emits its signal.
            await testObj.StoreNoiseLevelsAsync("foo", "bar");

            httpClient.Verify(c => c.GetAsync(string.Format("/latestData?userID=foo&token=bar&instrumentID={0}", serialId), It.IsAny<CancellationToken>()),
                Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>()), Times.Exactly(1));
            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.ReadRules(serialId), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos(serialId, It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.Verify(c => c.WriteLatestTimestamp(serialId, dataTime.ToUniversalTime()),
                Times.Exactly(1));
            dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, serialId, "LAeq", true))),
                Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            messageClient.Verify(ingress => ingress.AcceptAsync(
                It.Is<AlertSignal>(signal =>
                    signal.Source == "airq.rules" &&
                    signal.SerialId == serialId &&
                    signal.AlertType == AlertType.Alert &&
                    signal.Field == "LAeq" &&
                    signal.Level == limitOn &&
                    signal.Limit == limitOn &&
                    signal.AveragingPeriod == durationSeconds &&
                    signal.DeliveryChannels == (AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms) &&
                    signal.SuppressionWindow == TimeSpan.Zero),
                It.IsAny<CancellationToken>()), Times.Once);
            messageClient.VerifyNoOtherCalls();
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
                                                AirQFixture.CreateActiveRuleActivity(DateTime.Parse("2023-09-25T00:01:02"),DateTime.Parse("2023-09-25T00:01:03")),
                                                AlertType.Alert, false, false,
                                                DateTime.UtcNow, DateTime.UtcNow.AddMinutes(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES+1))
                }
            };
        }

        private static IEnumerable<object[]> LevelExclusion()
        {
            yield return new object[] {
                new List<RvtAlertRuleDto> { new(Guid.NewGuid(), "Device1", "LAeq", 50.0,19.0, 15 * 60,
                                                AirQFixture.CreateActiveRuleActivity(null,null),
                                                AlertType.Alert, false, false,
                                                DateTime.UtcNow, DateTime.UtcNow.AddMinutes(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES+1))
                }
            };
        }
    }
}
