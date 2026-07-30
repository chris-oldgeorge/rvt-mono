// File summary: Verifies Omnidots API orchestration, vibration ingestion, configuration, offline, webhook, and battery flows.
// Major updates:
// - 2026-06-18: Realigned expectations with site-hours-aware offline checks, bulk peak inserts, deploy-date guarded timestamps, and battery-specific messages.
using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Omnidots.Api;
using Omnidots.Api.Db;
using Omnidots.Api.Http;
using Omnidots.Api.UseCases;
using Omnidots.Model.Config;
using Omnidots.Model.Dto;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;
using static Omnidots.Api.OmnidotsApi;
namespace OmnidotsAdapterTests
{


    [TestClass]
    public class TestOmnidotsApi
    {
        public TestOmnidotsApi()
        {
            ILoggerFactory factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestOmnidotsApi");
        }

        [TestMethod]
        public async Task TestAuthenticate_Success()
        {
            OmnidotsApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)), It.IsAny<CancellationToken>())).
                Returns(OmnidotsFixture.AuthenticateTask());

            OmnidotsHttpGateway gateway = new(httpClient.Object, RvtConfig.USER_ID, RvtConfig.USER_AUTH);
            TokenResponse response = await gateway.AuthenticateAsync(TestContext.CancellationToken);
            AssertTokenResponse(response);

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate",
                It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.VerifyNoOtherCalls();
            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreMonitors_Success()
        {

            OmnidotsApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                    out Mock<IDBClient> dbClient,
                                                    out Mock<IMqttClient> mqttClient,
                                                    out Mock<IAlertIngressPort> messageClient);
            string token = "sometesttoken123";

            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)), It.IsAny<CancellationToken>())).
            Returns(OmnidotsFixture.AuthenticateTask(token));

            string measuringPointsUrl = string.Format("/api/v1/list_measuring_points?token={0}", token);
            httpClient.Setup(c => c.GetAsync(measuringPointsUrl, It.IsAny<CancellationToken>())).
                Returns(OmnidotsFixture.StringTask(OmnidotsFixture.MeasuringPointsJson()));

            await testObj.StoreMonitorsAsync(TestContext.CancellationToken);

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate",
             It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync(measuringPointsUrl, It.IsAny<CancellationToken>()), Times.Exactly(1));

            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c =>
                c.WriteMonitorList(It.Is<List<VibrationMonitorDto>>(c => TestUtil.VerifyMonitorList(c, 5))), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreMonitors_TestLocal_WritesOnlyDemoVibrationMonitor()
        {
            OmnidotsApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                    out Mock<IDBClient> dbClient,
                                                    out Mock<IMqttClient> mqttClient,
                                                    out Mock<IAlertIngressPort> messageClient,
                                                    testLocal: true);
            string token = "sometesttoken123";

            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)), It.IsAny<CancellationToken>())).
            Returns(OmnidotsFixture.AuthenticateTask(token));

            string measuringPointsUrl = string.Format("/api/v1/list_measuring_points?token={0}", token);
            httpClient.Setup(c => c.GetAsync(measuringPointsUrl, It.IsAny<CancellationToken>())).
                Returns(OmnidotsFixture.StringTask(TestLocalMeasuringPointsJson()));

            await testObj.StoreMonitorsAsync(TestContext.CancellationToken);

            dbClient.Verify(c => c.WriteMonitorList(It.Is<List<VibrationMonitorDto>>(
                monitors => monitors.Count == 1 && monitors[0].SerialId == "14768")), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync(measuringPointsUrl, It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        private static string TestLocalMeasuringPointsJson() =>
            """
        {
          "ok": true,
          "measuring_points": [
            {
              "name": "Vibration - Other - 99999",
              "id": 99999,
              "active": true,
              "disable_led": false,
              "log_flush_interval": 5,
              "timezone": "Europe/London",
              "vtop_enabled": "Off",
              "atop_enabled": "Off",
              "vector_enabled": "Off",
              "guide_line": "DIN4150_3_80Hz",
              "building_level": "unspecified",
              "category": "CAT3",
              "measurement_duration": 60,
              "data_save_level": 1,
              "noise_saving_enabled": "Off",
              "vdv_enabled": "Off",
              "vdv_period": 0,
              "trace_save_level": 1,
              "trace_pre_trigger": 1,
              "trace_post_trigger": 1,
              "schedule_enable_1": "00:00:00",
              "schedule_disable_1": "24:00:00",
              "schedule_enable_2": "00:00:00",
              "schedule_disable_2": "24:00:00",
              "schedule_enable_3": "00:00:00",
              "schedule_disable_3": "24:00:00",
              "schedule_enable_4": "00:00:00",
              "schedule_disable_4": "24:00:00",
              "schedule_enable_5": "00:00:00",
              "schedule_disable_5": "24:00:00",
              "schedule_enable_6": "00:00:00",
              "schedule_disable_6": "24:00:00",
              "schedule_enable_0": "00:00:00",
              "schedule_disable_0": "24:00:00",
              "alarm_value": 50
            },
            {
              "name": "Vibration - R17222V-QUCILO - 14768",
              "id": 14768,
              "active": true,
              "disable_led": false,
              "log_flush_interval": 5,
              "timezone": "Europe/London",
              "vtop_enabled": "Off",
              "atop_enabled": "Off",
              "vector_enabled": "Off",
              "guide_line": "DIN4150_3_80Hz",
              "building_level": "unspecified",
              "category": "CAT3",
              "measurement_duration": 60,
              "data_save_level": 1,
              "noise_saving_enabled": "Off",
              "vdv_enabled": "Off",
              "vdv_period": 0,
              "trace_save_level": 1,
              "trace_pre_trigger": 1,
              "trace_post_trigger": 1,
              "schedule_enable_1": "00:00:00",
              "schedule_disable_1": "24:00:00",
              "schedule_enable_2": "00:00:00",
              "schedule_disable_2": "24:00:00",
              "schedule_enable_3": "00:00:00",
              "schedule_disable_3": "24:00:00",
              "schedule_enable_4": "00:00:00",
              "schedule_disable_4": "24:00:00",
              "schedule_enable_5": "00:00:00",
              "schedule_disable_5": "24:00:00",
              "schedule_enable_6": "00:00:00",
              "schedule_disable_6": "24:00:00",
              "schedule_enable_0": "00:00:00",
              "schedule_disable_0": "24:00:00",
              "alarm_value": 50
            }
          ]
        }
        """;


        [TestMethod]
        public async Task TestCheckForOfflineMonitors_MonitorsOfflineFor23Hours_Success()
        {
            OmnidotsApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                     out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

            List<RvtAlertRuleDto> rules = OmnidotsFixture.OfflineRules();
            dbClient.Setup(c => c.ReadRules(null)).Returns(rules);
            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<DateTime?>())).
                Returns([]);

            await testObj.CheckForOfflineMonitorsAsync(TestContext.CancellationToken);

            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadRules(null), Times.Exactly(1));
            dbClient.Verify(c => c.ReadMonitorList(It.IsAny<DateTime?>()), Times.Exactly(1));

            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
        }

        [DataRow(25 * 60, 3600)]
        [DataRow(24 * 60, 0)]
        [DataRow((24 * 60) + 1, 60)]
        [TestMethod]
        public async Task TestCheckForOfflineMonitors_NotificationWrittenOk_Success(int minutesOffline, int offlineForSeconds)
        {
            OmnidotsApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                     out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

            List<RvtAlertRuleDto> rules = OmnidotsFixture.OfflineRules();
            dbClient.Setup(c => c.ReadRules(null)).Returns(rules);
            List<VibrationMonitorDto> monitors = OmnidotsFixture.MonitorsList(
                2,
                DateTime.UtcNow.AddMinutes(-minutesOffline),
                timeZone: "Europe/London");

            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<DateTime?>())).
                Returns(monitors);
            dbClient.Setup(c => c.ReadSiteTimes(It.IsAny<Guid>())).Returns(OmnidotsFixture.AlwaysOpenSiteTimes());

            await testObj.CheckForOfflineMonitorsAsync(TestContext.CancellationToken);

            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadRules(null), Times.Exactly(1));
            dbClient.Verify(c => c.ReadMonitorList(It.IsAny<DateTime?>()), Times.Exactly(1));

            foreach (VibrationMonitorDto m in monitors)
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
                dbClient.Verify(c => c.ReadSiteTimes(m.Id), Times.Exactly(1));
            }

            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [DataRow(null)]
        [DataRow("Invalid/secret-timezone")]
        [TestMethod]
        public async Task TestCheckForOfflineMonitors_InvalidTimeZone_RecordsFailureAndContinues(
            string? invalidTimeZone)
        {
            OmnidotsApi testObj = TestUtil.CreateApiAndMocks(
                out Mock<IHttpClient> httpClient,
                out Mock<IDBClient> dbClient,
                out Mock<IMqttClient> mqttClient,
                out Mock<IAlertIngressPort> messageClient);
            List<RvtAlertRuleDto> rules = OmnidotsFixture.OfflineRules();
            VibrationMonitorDto invalidMonitor = OmnidotsFixture.MonitorsList(
                1,
                DateTime.UtcNow.AddHours(-25),
                timeZone: invalidTimeZone)[0];
            VibrationMonitorDto validMonitor = OmnidotsFixture.MonitorsList(
                1,
                DateTime.UtcNow.AddHours(-25),
                serialIdIn: 1,
                timeZone: "Europe/London")[0];

            dbClient.Setup(c => c.ReadRules(null)).Returns(rules);
            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<DateTime?>()))
                .Returns([invalidMonitor, validMonitor]);
            dbClient.Setup(c => c.ReadSiteTimes(It.IsAny<Guid>()))
                .Returns(OmnidotsFixture.AlwaysOpenSiteTimes());

            OmnidotsImportException exception = await Assert.ThrowsExactlyAsync<OmnidotsImportException>(() => testObj.CheckForOfflineMonitorsAsync(TestContext.CancellationToken));

            Assert.AreEqual("CheckForOfflineMonitors", exception.Operation);
            Assert.HasCount(1, exception.Failures);
            Assert.AreEqual(invalidMonitor.SerialId, exception.Failures[0].SerialId);
            Assert.IsFalse(exception.ToString().Contains(invalidTimeZone ?? "missing", StringComparison.Ordinal));
            dbClient.Verify(c => c.HandleException(
                $"CheckForOfflineMonitors serialId={invalidMonitor.SerialId}",
                It.Is<InvalidOperationException>(e =>
                    e.Message == "Monitor timezone is missing or invalid.")), Times.Once);
            dbClient.Verify(c => c.ReadSiteTimes(invalidMonitor.Id), Times.Never);
            dbClient.Verify(c => c.ReadSiteTimes(validMonitor.Id), Times.Once);
            dbClient.Verify(c => c.SetMonitorOffline(validMonitor.Id, true), Times.Once);

            httpClient.VerifyNoOtherCalls();
            mqttClient.VerifyNoOtherCalls();
            messageClient.Verify(c => c.AcceptAsync(
                It.Is<AlertSignal>(signal =>
                    signal.SerialId == validMonitor.SerialId &&
                    signal.AlertType == AlertType.Offline),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [DataRow("2026-03-29T00:00:00Z")]
        [DataRow("2025-10-26T00:00:00Z")]
        [TestMethod]
        public async Task TestCheckForOfflineMonitors_InvalidOrAmbiguousScheduleBoundary_RecordsAndContinues(
            string lastDataTimeUtc)
        {
            OmnidotsApi testObj = TestUtil.CreateApiAndMocks(
                out Mock<IHttpClient> httpClient,
                out Mock<IDBClient> dbClient,
                out Mock<IMqttClient> mqttClient,
                out Mock<IAlertIngressPort> messageClient);
            VibrationMonitorDto invalidMonitor = OmnidotsFixture.MonitorsList(
                1,
                DateTimeOffset.Parse(lastDataTimeUtc).UtcDateTime,
                timeZone: "Europe/London")[0];
            VibrationMonitorDto validMonitor = OmnidotsFixture.MonitorsList(
                1,
                DateTime.UtcNow.AddHours(-25),
                serialIdIn: 1,
                timeZone: "Europe/London")[0];
            InvalidOperationException recordingException = new("secret recorder detail");
            SiteTimes invalidSchedule = new()
            {
                SundayStart = TimeSpan.FromMinutes(90),
                SundayEnd = TimeSpan.FromHours(4)
            };

            dbClient.Setup(c => c.ReadRules(null)).Returns(OmnidotsFixture.OfflineRules());
            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<DateTime?>()))
                .Returns([invalidMonitor, validMonitor]);
            dbClient.Setup(c => c.ReadSiteTimes(invalidMonitor.Id)).Returns(invalidSchedule);
            dbClient.Setup(c => c.ReadSiteTimes(validMonitor.Id))
                .Returns(OmnidotsFixture.AlwaysOpenSiteTimes());
            dbClient.Setup(c => c.HandleException(
                    $"CheckForOfflineMonitors serialId={invalidMonitor.SerialId}",
                    It.IsAny<Exception>()))
                .Throws(recordingException);

            OmnidotsImportException exception = await Assert.ThrowsExactlyAsync<OmnidotsImportException>(() => testObj.CheckForOfflineMonitorsAsync(TestContext.CancellationToken));

            Assert.AreEqual("CheckForOfflineMonitors", exception.Operation);
            Assert.HasCount(1, exception.Failures);
            Assert.AreEqual(invalidMonitor.SerialId, exception.Failures[0].SerialId);
            Assert.IsInstanceOfType<SiteScheduleConfigurationException>(
                exception.Failures[0].Exception);
            Assert.AreSame(recordingException, exception.Failures[0].RecordingException);
            Assert.IsFalse(exception.ToString().Contains("secret recorder detail", StringComparison.Ordinal));
            dbClient.Verify(c => c.ReadSiteTimes(validMonitor.Id), Times.Once);
            dbClient.Verify(c => c.SetMonitorOffline(validMonitor.Id, true), Times.Once);

            httpClient.VerifyNoOtherCalls();
            mqttClient.VerifyNoOtherCalls();
            messageClient.Verify(c => c.AcceptAsync(
                It.Is<AlertSignal>(signal =>
                    signal.SerialId == validMonitor.SerialId &&
                    signal.AlertType == AlertType.Offline),
                It.IsAny<CancellationToken>()), Times.Once);
        }



        [TestMethod]
        public async Task TestStorePeakRecords_Success()
        {
            OmnidotsApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IAlertIngressPort> messageClient);
            string token = "hghjadg";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)), It.IsAny<CancellationToken>())).
                    Returns(OmnidotsFixture.AuthenticateTask(token));

            string peakRecordsUrl = string.Format("/api/v1/get_peak_records?token={0}", token);
            httpClient.Setup(c => c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl)), It.IsAny<CancellationToken>())).
                Returns(OmnidotsFixture.StringTask(OmnidotsFixture.PeakRecordsJson()));

            List<VibrationMonitorDto> monitors = OmnidotsFixture.MonitorsList(2);
            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(monitors);
            dbClient.Setup(c => c.ReadRules(It.IsAny<string>())).
                Returns([]);

            await testObj.StorePeakRecordsLastDataTimeAsync(TestContext.CancellationToken);

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()),
                Times.Exactly(1));
            httpClient.Verify(c =>
                c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl)), It.IsAny<CancellationToken>()), Times.Exactly(2));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            DateTime expectedLatest = DateTime.Parse("2023-11-14T11:24:59");
            Mock<IOmnidotsMeasurementImportCommands> importCommands = dbClient.As<IOmnidotsMeasurementImportCommands>();
            importCommands.Verify(c => c.ImportPeakRecords("1",
                It.Is<DataTable>(t => t.Rows.Count == 2),
                It.Is<DateTime>(dt => TestUtil.VerifyDateTime(expectedLatest, dt))), Times.Once);
            importCommands.Verify(c => c.ImportPeakRecords("2",
                It.Is<DataTable>(t => t.Rows.Count == 2),
                It.Is<DateTime>(dt => TestUtil.VerifyDateTime(expectedLatest, dt))), Times.Once);
            Mock<IOmnidotsImportCursorQueries> cursorQueries = dbClient.As<IOmnidotsImportCursorQueries>();
            cursorQueries.Verify(
                c => c.ReadImportCursor(It.IsAny<string>(), OmnidotsMeasurementSeries.Peak), Times.Exactly(2));
            cursorQueries.Verify(
                c => c.ReadLatestMeasurementTime(It.IsAny<string>(), OmnidotsMeasurementSeries.Peak), Times.Exactly(2));

            dbClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>(), TestContext.CancellationToken), Times.Exactly(2));

            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStorePeakRecords_UsesPeakCursorOverlapAndAtomicImport()
        {
            OmnidotsApi testObj = TestUtil.CreateApiAndMocks(
                out Mock<IHttpClient> httpClient,
                out Mock<IDBClient> dbClient,
                out Mock<IMqttClient> mqttClient,
                out Mock<IAlertIngressPort> messageClient,
                out Mock<IOmnidotsImportCursorQueries> cursorQueries,
                out Mock<IOmnidotsMeasurementImportCommands> importCommands);
            DateTime cursor = new(2026, 7, 11, 8, 30, 0, DateTimeKind.Utc);
            List<VibrationMonitorDto> monitors = OmnidotsFixture.MonitorsList(1);
            string? requestedUrl = null;

            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.AuthenticateTask("peak-token"));
            httpClient.Setup(c => c.GetAsync(It.Is<string>(url =>
                    url.StartsWith("/api/v1/get_peak_records", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((url, _) => requestedUrl = url)
                .Returns(OmnidotsFixture.StringTask(OmnidotsFixture.PeakRecordsJson()));
            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(monitors);
            cursorQueries.Setup(c => c.ReadImportCursor("1", OmnidotsMeasurementSeries.Peak))
                .Returns(cursor);

            await testObj.StorePeakRecordsLastDataTimeAsync(TestContext.CancellationToken);

            Assert.IsNotNull(requestedUrl);
            Assert.AreEqual(DateTimeUtil.GetMillis(cursor.AddMinutes(-5)), RequestTime(requestedUrl, "start_time"));
            importCommands.Verify(c => c.ImportPeakRecords(
                "1",
                It.Is<DataTable>(table =>
                    table.Rows.Count == 2 &&
                    table.Rows.Cast<DataRow>().Select(row => (DateTime)row["SampleTime"])
                        .SequenceEqual(table.Rows.Cast<DataRow>().Select(row => (DateTime)row["SampleTime"]).OrderBy(time => time))),
                It.Is<DateTime>(time => time == DateTimeOffset.FromUnixTimeMilliseconds(1699961099999).DateTime)),
                Times.Once);
            cursorQueries.Verify(c => c.ReadImportCursor("1", OmnidotsMeasurementSeries.Peak), Times.Once);
            cursorQueries.Verify(c => c.ReadLatestMeasurementTime(It.IsAny<string>(), It.IsAny<OmnidotsMeasurementSeries>()), Times.Never);
            dbClient.Verify(c => c.InsertPeakRecordsTable(It.IsAny<DataTable>()), Times.Never);
            dbClient.Verify(c => c.WriteLatestTimestamp(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>(), TestContext.CancellationToken), Times.Once);
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreVeffRecords_NoCursorOrStoredMeasurementUsesUtcLookbackAndSkipsEmptyImport()
        {
            OmnidotsApi testObj = TestUtil.CreateApiAndMocks(
                out Mock<IHttpClient> httpClient,
                out Mock<IDBClient> dbClient,
                out Mock<IMqttClient> mqttClient,
                out Mock<IAlertIngressPort> messageClient,
                out Mock<IOmnidotsImportCursorQueries> cursorQueries,
                out Mock<IOmnidotsMeasurementImportCommands> importCommands);
            VibrationMonitorDto monitor = OmnidotsFixture.MonitorsList(1).Single();
            string? requestedUrl = null;
            DateTime before = DateTime.UtcNow;

            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.AuthenticateTask("veff-token"));
            httpClient.Setup(c => c.GetAsync(It.Is<string>(url =>
                    url.StartsWith("/api/v1/get_veff_records", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((url, _) => requestedUrl = url)
                .Returns(OmnidotsFixture.StringTask("{\"ok\":true,\"samples\":[]}"));
            dbClient.Setup(c => c.ReadMonitorList(null)).Returns([monitor]);

            await testObj.StoreVeffRecordsAsync(TimeSpan.FromHours(2), TestContext.CancellationToken);
            DateTime after = DateTime.UtcNow;

            Assert.IsNotNull(requestedUrl);
            DateTime start = DateTimeUtil.JAN1_1970.AddMilliseconds(RequestTime(requestedUrl, "start_time"));
            DateTime end = DateTimeUtil.JAN1_1970.AddMilliseconds(RequestTime(requestedUrl, "end_time"));
            Assert.IsTrue(start >= before.AddHours(-2).AddMinutes(-5).AddSeconds(-1));
            Assert.IsTrue(start <= after.AddHours(-2).AddMinutes(-5));
            Assert.IsTrue(end >= before.AddSeconds(-1) && end <= after);
            cursorQueries.Verify(c => c.ReadImportCursor("1", OmnidotsMeasurementSeries.Veff), Times.Once);
            cursorQueries.Verify(c => c.ReadLatestMeasurementTime("1", OmnidotsMeasurementSeries.Veff), Times.Once);
            importCommands.Verify(c => c.ImportVeffRecords(
                It.IsAny<string>(), It.IsAny<IReadOnlyCollection<VeffRecordDto>>(), It.IsAny<DateTime>()), Times.Never);
            dbClient.Verify(c => c.WriteLatestTimestamp(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
            dbClient.Verify(c => c.SetMonitorOffline(It.IsAny<Guid>(), It.IsAny<bool>()), Times.Never);
            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreVdvRecords_UsesStoredMeasurementFallbackAndOrdersAtomicBatch()
        {
            OmnidotsApi testObj = TestUtil.CreateApiAndMocks(
                out Mock<IHttpClient> httpClient,
                out Mock<IDBClient> dbClient,
                out Mock<IMqttClient> mqttClient,
                out Mock<IAlertIngressPort> messageClient,
                out Mock<IOmnidotsImportCursorQueries> cursorQueries,
                out Mock<IOmnidotsMeasurementImportCommands> importCommands);
            DateTime storedMeasurement = new(2026, 7, 9, 4, 15, 0, DateTimeKind.Utc);
            VibrationMonitorDto monitor = OmnidotsFixture.MonitorsList(1).Single();
            string? requestedUrl = null;

            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.AuthenticateTask("vdv-token"));
            httpClient.Setup(c => c.GetAsync(It.Is<string>(url =>
                    url.StartsWith("/api/v1/get_vdv_records", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((url, _) => requestedUrl = url)
                .Returns(OmnidotsFixture.StringTask(OmnidotsFixture.VdvRecordsJson()));
            dbClient.Setup(c => c.ReadMonitorList(null)).Returns([monitor]);
            cursorQueries.Setup(c => c.ReadLatestMeasurementTime("1", OmnidotsMeasurementSeries.Vdv))
                .Returns(storedMeasurement);

            await testObj.StoreVdvRecordsAsync(TimeSpan.FromHours(2), TestContext.CancellationToken);

            Assert.IsNotNull(requestedUrl);
            Assert.AreEqual(DateTimeUtil.GetMillis(storedMeasurement.AddMinutes(-5)), RequestTime(requestedUrl, "start_time"));
            cursorQueries.Verify(c => c.ReadImportCursor("1", OmnidotsMeasurementSeries.Vdv), Times.Once);
            cursorQueries.Verify(c => c.ReadLatestMeasurementTime("1", OmnidotsMeasurementSeries.Vdv), Times.Once);
            cursorQueries.Verify(c => c.ReadImportCursor("1", It.Is<OmnidotsMeasurementSeries>(series => series != OmnidotsMeasurementSeries.Vdv)), Times.Never);
            importCommands.Verify(c => c.ImportVdvRecords(
                "1",
                It.Is<IReadOnlyCollection<VdvRecordDto>>(records =>
                    records.Select(record => record.SampleTime).SequenceEqual(records.Select(record => record.SampleTime).OrderBy(time => time))),
                It.Is<DateTime>(time => time == DateTimeOffset.FromUnixTimeMilliseconds(1692282419999).DateTime)),
                Times.Once);
            dbClient.Verify(c => c.InsertVdvRecords(It.IsAny<string>(), It.IsAny<List<VdvRecordDto>>()), Times.Never);
            dbClient.Verify(c => c.WriteLatestTimestamp(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
            dbClient.Verify(c => c.SetMonitorOffline(monitor.Id, false), Times.Once);
            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>(), TestContext.CancellationToken), Times.Once);
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreVeffRecords_WhenAtomicImportFailsContinuesLaterMonitorThenFaults()
        {
            OmnidotsApi testObj = TestUtil.CreateApiAndMocks(
                out Mock<IHttpClient> httpClient,
                out Mock<IDBClient> dbClient,
                out Mock<IMqttClient> mqttClient,
                out Mock<IAlertIngressPort> messageClient,
                out Mock<IOmnidotsImportCursorQueries> cursorQueries,
                out Mock<IOmnidotsMeasurementImportCommands> importCommands);
            List<VibrationMonitorDto> monitors = OmnidotsFixture.MonitorsList(2);
            InvalidOperationException importFailure = new("atomic import failed");

            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.AuthenticateTask("veff-token"));
            httpClient.Setup(c => c.GetAsync(It.Is<string>(url =>
                    url.StartsWith("/api/v1/get_veff_records", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.StringTask(OmnidotsFixture.VeffRecordsJson()));
            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(monitors);
            importCommands.Setup(c => c.ImportVeffRecords(
                    "1", It.IsAny<IReadOnlyCollection<VeffRecordDto>>(), It.IsAny<DateTime>()))
                .Throws(importFailure);

            OmnidotsImportException exception = await Assert.ThrowsExactlyAsync<OmnidotsImportException>(() => testObj.StoreVeffRecordsAsync(TimeSpan.FromHours(2), TestContext.CancellationToken));

            Assert.AreEqual("StoreVeffRecords", exception.Operation);
            Assert.AreEqual("1", exception.Failures.Single().SerialId);
            Assert.AreSame(importFailure, exception.Failures.Single().Exception);
            importCommands.Verify(c => c.ImportVeffRecords(
                "2", It.IsAny<IReadOnlyCollection<VeffRecordDto>>(), It.IsAny<DateTime>()), Times.Once);
            dbClient.Verify(c => c.SetMonitorOffline(monitors[0].Id, false), Times.Never);
            dbClient.Verify(c => c.SetMonitorOffline(monitors[1].Id, false), Times.Once);
            dbClient.Verify(c => c.HandleException("StoreVeffRecords serialId=1", importFailure), Times.Once);
            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>(), TestContext.CancellationToken), Times.Once);
            cursorQueries.Verify(c => c.ReadImportCursor(It.IsAny<string>(), OmnidotsMeasurementSeries.Veff), Times.Exactly(2));
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStorePeakRecordsLastDataTime_Success()
        {
            OmnidotsApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IAlertIngressPort> messageClient);
            string token = "hghjadg";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)), It.IsAny<CancellationToken>())).
                    Returns(OmnidotsFixture.AuthenticateTask(token));

            string peakRecordsUrl = string.Format("/api/v1/get_peak_records?token={0}", token);

            DateTime latestTime = DateTime.UtcNow;
            List<VibrationMonitorDto> monitors = OmnidotsFixture.MonitorsList(2);
            // mark first monitor as having recent data, second has not reported for delay period
            monitors[0].LastDataTime = latestTime;
            monitors[1].LastDataTime = latestTime.AddHours(-13);
            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(monitors);

            // mock sample data to have latestTime in Timestamp
            string json = OmnidotsFixture.PeakRecordsJson();
            PeakRecords? peakRecords = JsonSerializer.Deserialize<PeakRecords>(json!);
            peakRecords!.Samples![peakRecords.Samples.Count - 1].Timestamp = DateTimeUtil.GetMillis(latestTime);
            string modJson = JsonSerializer.Serialize(peakRecords);
            httpClient.Setup(c => c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl)), It.IsAny<CancellationToken>())).
                Returns(OmnidotsFixture.StringTask(modJson));

            dbClient.Setup(c => c.ReadRules(It.IsAny<string>())).
                Returns([]);

            await testObj.StorePeakRecordsLastDataTimeAsync(TestContext.CancellationToken);

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()),
                Times.Exactly(1));
            httpClient.Verify(c =>
                c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl)), It.IsAny<CancellationToken>()), Times.Exactly(2));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));

            Mock<IOmnidotsMeasurementImportCommands> importCommands = dbClient.As<IOmnidotsMeasurementImportCommands>();
            importCommands.Verify(c => c.ImportPeakRecords(
                monitors[0].SerialId,
                It.Is<DataTable>(t => t.Rows.Count == 2),
                It.Is<DateTime>(dt => TestUtil.VerifyDateTime(latestTime, dt))), Times.Once);
            importCommands.Verify(c => c.ImportPeakRecords(
                monitors[1].SerialId,
                It.Is<DataTable>(t => t.Rows.Count == 2),
                It.Is<DateTime>(dt => TestUtil.VerifyDateTime(latestTime, dt))), Times.Once);
            Mock<IOmnidotsImportCursorQueries> cursorQueries = dbClient.As<IOmnidotsImportCursorQueries>();
            cursorQueries.Verify(c => c.ReadImportCursor(
                It.IsAny<string>(), OmnidotsMeasurementSeries.Peak), Times.Exactly(2));
            cursorQueries.Verify(c => c.ReadLatestMeasurementTime(
                It.IsAny<string>(), OmnidotsMeasurementSeries.Peak), Times.Exactly(2));

            dbClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>(), TestContext.CancellationToken), Times.Exactly(2));

            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreVdvRecords_Success()
        {
            OmnidotsApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IAlertIngressPort> messageClient);
            string token = "hghjadg";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)), It.IsAny<CancellationToken>())).
                    Returns(OmnidotsFixture.AuthenticateTask(token));

            string vdvRecordsUrl = string.Format("/api/v1/get_vdv_records?token={0}", token);
            httpClient.Setup(c => c.GetAsync(It.Is<string>(s => s.StartsWith(vdvRecordsUrl)), It.IsAny<CancellationToken>())).
                Returns(OmnidotsFixture.StringTask(OmnidotsFixture.VdvRecordsJson()));

            List<VibrationMonitorDto> monitors = OmnidotsFixture.MonitorsList(2);
            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(monitors);
            dbClient.Setup(c => c.ReadRules(It.IsAny<string>())).
                Returns([]);

            await testObj.StoreVdvRecordsAsync(TimeSpan.FromMinutes(10), TestContext.CancellationToken);

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()),
                Times.Exactly(1));
            httpClient.Verify(c =>
                c.GetAsync(It.Is<string>(s => s.StartsWith(vdvRecordsUrl)), It.IsAny<CancellationToken>()), Times.Exactly(2));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            DateTime expectedLatest = DateTime.Parse("2023-8-17T14:26:59");
            Mock<IOmnidotsMeasurementImportCommands> importCommands = dbClient.As<IOmnidotsMeasurementImportCommands>();
            importCommands.Verify(c => c.ImportVdvRecords("1", It.IsAny<IReadOnlyCollection<VdvRecordDto>>(),
                It.Is<DateTime>(dt => TestUtil.VerifyDateTime(expectedLatest, dt))), Times.Once);
            importCommands.Verify(c => c.ImportVdvRecords("2", It.IsAny<IReadOnlyCollection<VdvRecordDto>>(),
                It.Is<DateTime>(dt => TestUtil.VerifyDateTime(expectedLatest, dt))), Times.Once);
            Mock<IOmnidotsImportCursorQueries> cursorQueries = dbClient.As<IOmnidotsImportCursorQueries>();
            cursorQueries.Verify(c => c.ReadImportCursor(It.IsAny<string>(), OmnidotsMeasurementSeries.Vdv), Times.Exactly(2));
            cursorQueries.Verify(c => c.ReadLatestMeasurementTime(It.IsAny<string>(), OmnidotsMeasurementSeries.Vdv), Times.Exactly(2));


            foreach (VibrationMonitorDto m in monitors)
            {
                dbClient.Verify(c => c.SetMonitorOffline(m.Id, false), Times.Exactly(1));
            }
            dbClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>(), TestContext.CancellationToken), Times.Exactly(2));

            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();

        }

        [TestMethod]
        public async Task TestStoreVeffRecords_Success()
        {
            OmnidotsApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                   out Mock<IDBClient> dbClient,
                                                   out Mock<IMqttClient> mqttClient,
                                                    out Mock<IAlertIngressPort> messageClient);
            string token = "hghjadg";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)), It.IsAny<CancellationToken>())).
                    Returns(OmnidotsFixture.AuthenticateTask(token));

            string veffRecordsUrl = string.Format("/api/v1/get_veff_records?token={0}", token);
            httpClient.Setup(c => c.GetAsync(It.Is<string>(s => s.StartsWith(veffRecordsUrl)), It.IsAny<CancellationToken>())).
                Returns(OmnidotsFixture.StringTask(OmnidotsFixture.VeffRecordsJson()));

            List<VibrationMonitorDto> monitors = OmnidotsFixture.MonitorsList(2);
            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(monitors);
            dbClient.Setup(c => c.ReadRules(It.IsAny<string>())).
                Returns([]);

            await testObj.StoreVeffRecordsAsync(TimeSpan.FromMinutes(10), TestContext.CancellationToken);

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()),
                Times.Exactly(1));
            httpClient.Verify(c =>
                c.GetAsync(It.Is<string>(s => s.StartsWith(veffRecordsUrl)), It.IsAny<CancellationToken>()), Times.Exactly(2));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            DateTime expectedLatest = DateTime.Parse("2023-11-14T11:19:59");
            Mock<IOmnidotsMeasurementImportCommands> importCommands = dbClient.As<IOmnidotsMeasurementImportCommands>();
            importCommands.Verify(c => c.ImportVeffRecords("1", It.IsAny<IReadOnlyCollection<VeffRecordDto>>(),
                It.Is<DateTime>(dt => TestUtil.VerifyDateTime(expectedLatest, dt))), Times.Once);
            importCommands.Verify(c => c.ImportVeffRecords("2", It.IsAny<IReadOnlyCollection<VeffRecordDto>>(),
                It.Is<DateTime>(dt => TestUtil.VerifyDateTime(expectedLatest, dt))), Times.Once);
            Mock<IOmnidotsImportCursorQueries> cursorQueries = dbClient.As<IOmnidotsImportCursorQueries>();
            cursorQueries.Verify(c => c.ReadImportCursor(It.IsAny<string>(), OmnidotsMeasurementSeries.Veff), Times.Exactly(2));
            cursorQueries.Verify(c => c.ReadLatestMeasurementTime(It.IsAny<string>(), OmnidotsMeasurementSeries.Veff), Times.Exactly(2));


            foreach (VibrationMonitorDto m in monitors)
            {
                dbClient.Verify(c => c.SetMonitorOffline(m.Id, false), Times.Exactly(1));
            }
            dbClient.VerifyNoOtherCalls();

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>(), TestContext.CancellationToken), Times.Exactly(2));

            mqttClient.VerifyNoOtherCalls();
        }


        [TestMethod]
        public async Task StoreTraces_NonLegacySerial_IsImportedWhenEligible()
        {
            OmnidotsApi testObj = TestUtil.CreateApiAndMocks(
                out Mock<IHttpClient> httpClient,
                out Mock<IDBClient> dbClient,
                out Mock<IMqttClient> mqttClient,
                out Mock<IAlertIngressPort> messageClient,
                traceCollectionOptions: new OmnidotsTraceCollectionOptions
                {
                    AllowedSerialIds = [],
                    MaxMonitorsPerRun = 1
                });
            dbClient.Setup(client => client.ReadMonitorList(It.IsAny<DateTime?>()))
                .Returns(OmnidotsFixture.MonitorsList(1));
            httpClient.Setup(client => client.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.AuthenticateTask("trace-token"));
            httpClient.Setup(client => client.GetAsync(It.Is<string>(url =>
                    url.StartsWith("/api/v1/get_traces_list", StringComparison.Ordinal) &&
                    url.Contains("measuring_point_id=1", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.StringTask("{\"ok\":true,\"traces\":[]}"));

            await testObj.StoreTracesAsync(DateTime.UtcNow.AddMinutes(-5), TestContext.CancellationToken);

            httpClient.Verify(client => client.GetAsync(It.Is<string>(url =>
                url.StartsWith("/api/v1/get_traces_list", StringComparison.Ordinal) &&
                url.Contains("measuring_point_id=1", StringComparison.Ordinal)), It.IsAny<CancellationToken>()), Times.Once);
            dbClient.Verify(client => client.WriteTraces(It.IsAny<string>(), It.IsAny<IReadOnlyList<TraceData>>()), Times.Never);
            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task StoreTraces_CompatibilityDefaults_DoNotRestrictFleetSerials()
        {
            Mock<IHttpClient> httpClient = new();
            Mock<IDBClient> dbClient = new();
            dbClient.As<IOmnidotsImportCursorQueries>();
            dbClient.As<IOmnidotsMeasurementImportCommands>();
            dbClient.As<IOmnidotsTraceQueries>();
            Mock<IMqttClient> mqttClient = new();
            Mock<IAlertIngressPort> messageClient = new();
            OmnidotsApi subject = new(
                httpClient.Object,
                dbClient.Object,
                mqttClient.Object,
                messageClient.Object);
            dbClient
                .Setup(client => client.ReadMonitorList(It.IsAny<DateTime?>()))
                .Returns(OmnidotsFixture.MonitorsList(1));
            httpClient
                .Setup(client => client.PostAsync(
                    "/api/v1/user/authenticate",
                    It.IsAny<HttpContent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.AuthenticateTask("trace-token"));
            httpClient
                .Setup(client => client.GetAsync(
                    It.Is<string>(url =>
                        url.StartsWith(
                            "/api/v1/get_traces_list",
                            StringComparison.Ordinal)
                        && url.Contains(
                            "measuring_point_id=1",
                            StringComparison.Ordinal)),
                    It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.StringTask(
                    "{\"ok\":true,\"traces\":[]}"));

            await subject.StoreTracesAsync(
                DateTime.UtcNow.AddMinutes(-5),
                TestContext.CancellationToken);

            httpClient.Verify(
                client => client.GetAsync(
                    It.Is<string>(url => url.Contains(
                        "measuring_point_id=1",
                        StringComparison.Ordinal)),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task StoreTraces_DisabledCollection_MakesNoVendorCalls()
        {
            OmnidotsApi testObj = TestUtil.CreateApiAndMocks(
                out Mock<IHttpClient> httpClient,
                out Mock<IDBClient> dbClient,
                out Mock<IMqttClient> mqttClient,
                out Mock<IAlertIngressPort> messageClient,
                traceCollectionOptions: new OmnidotsTraceCollectionOptions
                {
                    Enabled = false,
                    MaxMonitorsPerRun = 1
                });
            dbClient.Setup(client => client.ReadMonitorList(It.IsAny<DateTime?>()))
                .Returns(OmnidotsFixture.MonitorsList(1));

            await testObj.StoreTracesAsync(DateTime.UtcNow.AddMinutes(-5), TestContext.CancellationToken);

            httpClient.VerifyNoOtherCalls();
            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [DataRow(1, BatteryAlertType.Off, BatteryAlertType.BatteryAlert, AlertType.BatteryAlert, true)]
        [DataRow(3, BatteryAlertType.BatteryCaution, BatteryAlertType.BatteryAlert, AlertType.BatteryAlert, true)]
        [DataRow(5, BatteryAlertType.Off, BatteryAlertType.BatteryAlert, AlertType.BatteryAlert, true)]
        [DataRow(10, BatteryAlertType.Off, BatteryAlertType.BatteryAlert, AlertType.BatteryAlert, true)]
        [DataRow(11, BatteryAlertType.Off, BatteryAlertType.BatteryCaution, AlertType.BatteryCaution, true)]
        [DataRow(20, BatteryAlertType.Off, BatteryAlertType.BatteryCaution, AlertType.BatteryCaution, true)]
        [DataRow(98, BatteryAlertType.BatteryCaution, BatteryAlertType.Off, AlertType.Ignore, false)]
        [DataRow(99, BatteryAlertType.Off, BatteryAlertType.Off, AlertType.Ignore, false)]
        [TestMethod]
        public async Task TestNotifyBatteryLevels_Success(int batteryLevel, BatteryAlertType initialBatteryStatus, BatteryAlertType expectedBatteryStatus,
                                               AlertType expectedAlertType, bool expectNotification)
        {
            OmnidotsApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IAlertIngressPort> messageClient);

            List<VibrationMonitorDto> monitors = OmnidotsFixture.MonitorsList(numMonitors: 1,
                                                        lastDataTime: null,
                                                        alwaysMakeSensor: true,
                                                        serialIdIn: 23422,
                                                        batteryLevel: batteryLevel,
                                                        batteryStatus: initialBatteryStatus);
            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(monitors);
            await testObj.NotifyBatteryLevelsAsync(TestContext.CancellationToken);
            httpClient.VerifyNoOtherCalls();
            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            if (expectNotification)
            {
                dbClient.Verify(c => c.SetMonitorBatteryStatus(monitors[0].Id, (byte)expectedBatteryStatus),
                    Times.Exactly(1));

                // The durable stack owns notification persistence, contact
                // fan-out, and audits; the handler's contract is the signal.
                messageClient.Verify(c => c.AcceptAsync(
                    It.Is<AlertSignal>(signal =>
                        signal.SerialId == monitors[0].SerialId &&
                        signal.AlertType == expectedAlertType &&
                        signal.Level == batteryLevel &&
                        signal.DeliveryChannels == (AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms)),
                    It.IsAny<CancellationToken>()), Times.Exactly(1));
            }
            else
            {
                if (initialBatteryStatus != BatteryAlertType.Off)
                {
                    dbClient.Verify(c => c.SetMonitorBatteryStatus(monitors[0].Id, (byte)expectedBatteryStatus), Times.Exactly(1));
                }
            }
            dbClient.VerifyNoOtherCalls();
            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        //public void TestGetTracesList_Success()
        //{
        //    var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
        //                                           out Mock<IDBClient> dbClient,
        //                                           out Mock<IMqttClient> mqttClient,
        //                                           out Mock<ICommsClient> commsClient);

        //        "http://blah?user_id=foo&user_auth=bar&measuring_point_id=123");
        //    var res = testObj.GetTracesList(req, null, out DateTime endTime, out int measuringPointId);

        //    Assert.IsNotNull(res);
        //    Assert.AreEqual(123, measuringPointId);
        //    Assert.IsTrue(res.Ok);
        //    Assert.IsNotNull(res.Traces);
        //    Assert.IsTrue(res.Traces!.Count == 2);
        //    Assert.AreEqual(88888888L, res.Traces[0].StartTime);
        //    Assert.AreEqual(999999999L, res.Traces[0].EndTime);
        //    Assert.AreEqual(1234567890L, res.Traces[0].StartTime);
        //    Assert.AreEqual(987654321, res.Traces[0].EndTime);

        //    httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate",
        //     It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
        //    httpClient.VerifyNoOtherCalls();

        //    dbClient.VerifyNoOtherCalls();

        //    mqttClient.Verify(c => c.ConnectAsync(), Times.Exactly(1));
        //    mqttClient.VerifyNoOtherCalls();
        //    commsClient.VerifyNoOtherCalls();


        //}


        //public void TestGetTraces_Success()
        //{
        //    var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
        //                                            out Mock<IDBClient> dbClient,
        //                                            out Mock<IMqttClient> mqttClient,
        //                                            out Mock<ICommsClient> commsClient);

        //        "http://blah?user_id=foo&user_auth=bar&measuring_point_id=123");
        //    var res = testObj.GetTraces(req, null, out DateTime _, out int measuringPointId);

        //    Assert.IsNotNull(res);
        //    Assert.AreEqual(123, measuringPointId);

        //    Assert.IsTrue(res.Ok);
        //    Assert.IsNotNull(res.Traces);
        //    Assert.IsTrue(res.Traces!.Count == 2);
        //    Assert.AreEqual(11111111, res.Traces[0].StartTime);
        //    Assert.AreEqual(22222222, res.Traces[0].EndTime);
        //    Assert.AreEqual(33333333, res.Traces[0].StartTime);
        //    Assert.AreEqual(44444444, res.Traces[0].EndTime);
        //    Assert.AreEqual(2, res.Traces[0].X!.Count);
        //    Assert.AreEqual(3, res.Traces[0].Y!.Count);
        //    Assert.AreEqual(4, res.Traces[0].Z!.Count);
        //    Assert.AreEqual(2, res.Traces[1].X!.Count);
        //    Assert.AreEqual(3, res.Traces[1].Y!.Count);
        //    Assert.AreEqual(4, res.Traces[1].Z!.Count);

        //    httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate",
        //     It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
        //    httpClient.VerifyNoOtherCalls();

        //    dbClient.VerifyNoOtherCalls();

        //    mqttClient.Verify(c => c.ConnectAsync(), Times.Exactly(1));
        //    mqttClient.VerifyNoOtherCalls();
        //    commsClient.VerifyNoOtherCalls();
        //}



        private static void AssertTokenResponse(TokenResponse response)
        {
            Assert.IsNotNull(response);
            Assert.IsInstanceOfType<TokenResponse>(response);
            Assert.IsTrue(response.Ok);
            Assert.AreEqual("702811da14ff4225973c4054ed52bb9f", response.Token);
        }

        private static long RequestTime(string url, string name)
        {
            string value = url.Split('?', 2)[1]
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2))
                .Single(part => part[0] == name)[1];
            return long.Parse(value);
        }

        public TestContext TestContext { get; set; } = null!;
    }
}
