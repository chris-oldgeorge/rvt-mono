using System.Globalization;
using AirQ.Api;
using AirQ.Api.Db;
using AirQ.Api.Http;
using AirQ.Model.Dto;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Language.Flow;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
namespace AirQMonitorTests
{
    [TestClass]
    public class TestAirQApiNoiseLevels
    {
        public TestAirQApiNoiseLevels()
        {
            ILoggerFactory factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestAirQApiNoiseLevels");
        }

        [TestMethod]
        public async Task TestStoreNoiseLevels_EmptyRules_Success()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/latestData\\?userID=foo&token=bar&instrumentID=*"), It.IsAny<CancellationToken>())).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.SamplesResponseJson(), TestContext.CancellationToken));

            List<NoiseMonitorDto> monitors = AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE);
            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(monitors);
            dbClient.Setup(c => c.ReadRules(It.IsAny<string>())).
                Returns([]);

            await testObj.StoreNoiseLevelsAsync("foo", "bar", TestContext.CancellationToken);

            httpClient.Verify(c => c.GetAsync(It.Is<string>(s => s.StartsWith("/latestData?userID=foo")), It.IsAny<CancellationToken>()), Times.Exactly(3));
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

            mqttClient.Verify(c => c.PublishAsync(RvtConfig.INSERT_TOPIC, It.IsAny<string>(), TestContext.CancellationToken), Times.Exactly(3));
            mqttClient.VerifyNoOtherCalls();

            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreNoiseLevels_TruncatedByTimestamp_Success()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/latestData\\?userID=foo&token=bar&instrumentID=*"), It.IsAny<CancellationToken>())).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.SamplesResponseJson(), TestContext.CancellationToken));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(DateTime.UtcNow, NoiseMonitorStatus.ACTIVE));

            await testObj.StoreNoiseLevelsAsync("foo", "bar", TestContext.CancellationToken);

            httpClient.Verify(c => c.GetAsync(It.Is<string>(s => s.StartsWith("/latestData?userID=foo")), It.IsAny<CancellationToken>()), Times.Exactly(3));
            httpClient.VerifyNoOtherCalls();
            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();

            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreNoiseLevelsForYesterday_Success()
        {

            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

            string yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/dataForDate\\?userID=foo&date=" + yesterday + "&token=bar&instrumentID=*"), It.IsAny<CancellationToken>())).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.DateSamplesResponseJson(), TestContext.CancellationToken));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            await testObj.StoreAllNoiseLevelsForYesterdayAsync("foo", "bar", TestContext.CancellationToken);

            httpClient.Verify(c => c.GetAsync(It.Is<string>(s => s.StartsWith("/dataForDate?userID=foo")), It.IsAny<CancellationToken>()), Times.Exactly(3));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos("Device1", It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos("Device2", It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos("Device3", It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();

            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreNoiseLevelsForDate_Success()
        {

            string dateStr = "2023-09-11";
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

            IReturnsResult<IHttpClient> regex =
            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/dataForDate\\?userID=foo&date=" + dateStr + "&token=bar&instrumentID=*"), It.IsAny<CancellationToken>())).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.DateSamplesResponseJson(), TestContext.CancellationToken));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            await testObj.StoreNoiseLevelsForDateAsync("foo", "bar", dateStr, TestContext.CancellationToken);
            httpClient.Verify(c => c.GetAsync(It.Is<string>(s => s.StartsWith("/dataForDate?userID=")), It.IsAny<CancellationToken>()), Times.Exactly(3));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos("Device1", It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos("Device2", It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.Verify(c => c.InsertNoiseDtos("Device3", It.IsAny<List<NoiseDto>>()), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();

            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task GetSamplesForDate_EncodesEveryDynamicQueryParameter()
        {
            Mock<IHttpClient> httpClient = new(MockBehavior.Strict);
            httpClient.Setup(client => client.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("[]");
            AirQHttpGateway gateway = new(httpClient.Object);

            await gateway.GetSamplesForDateAsync(
                "vendor user&admin=true",
                "vendor token&role=admin",
                "serial&override=true",
                "2026-07-14&token=attacker", TestContext.CancellationToken);

            httpClient.Verify(client => client.GetAsync(
                "/dataForDate?userID=vendor%20user%26admin%3Dtrue&date=2026-07-14%26token%3Dattacker&token=vendor%20token%26role%3Dadmin&instrumentID=serial%26override%3Dtrue", It.IsAny<CancellationToken>()),
                Times.Once);
            httpClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreNoiseLevelsInactiveMonitor_Success()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/latestData\\?userID=foo&token=bar&instrumentID=*"), It.IsAny<CancellationToken>())).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.SamplesResponseJson(), TestContext.CancellationToken));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(null, "Inactive"));

            await testObj.StoreNoiseLevelsAsync("foo", "bar", TestContext.CancellationToken);

            httpClient.Verify(c => c.GetAsync(It.Is<string>(s => s.StartsWith("/dataForDate?userID=")), It.IsAny<CancellationToken>()), Times.Exactly(0));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();

            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreNoiseLevelsForDateInactiveMonitor_Success()
        {

            string dateStr = "2023-09-11";
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/dataForDate\\?userID=foo&date=" + dateStr + "&token=bar&instrumentID=*"), It.IsAny<CancellationToken>())).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.SamplesResponseJson(), TestContext.CancellationToken));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(null, "Inactive"));

            await testObj.StoreNoiseLevelsForDateAsync("foo", "bar", dateStr, TestContext.CancellationToken);

            httpClient.Verify(c => c.GetAsync(It.Is<string>(s => s.StartsWith("/dataForDate?userID=")), It.IsAny<CancellationToken>()), Times.Exactly(0));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();

            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestNotifySiteAverages_SiteWithoutHoursIsSkipped_ValidSiteStillProcessed()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                             out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

            DateTime date = new(2026, 7, 29);
            SiteMonitorsWithSiteHoursDto hourless = SiteMonitor("Device1", startTime: null, endTime: null);
            SiteMonitorsWithSiteHoursDto valid = SiteMonitor(
                "Device2",
                startTime: TimeSpan.Parse("09:00:00", CultureInfo.InvariantCulture),
                endTime: TimeSpan.Parse("17:00:00", CultureInfo.InvariantCulture));
            dbClient.Setup(c => c.ReadSiteMonitorsWithSiteHours(date)).Returns([hourless, valid]);
            dbClient.Setup(c => c.GetAverageNoiseLevel("Device2", "LAeq", date + valid.StartTime!.Value, date + valid.EndTime!.Value)).
                Returns(42.0);
            dbClient.Setup(c => c.ReadRules("Device2")).Returns([]);

            await testObj.NotifySiteAveragesAsync(date, TestContext.CancellationToken);

            dbClient.Verify(c => c.ReadSiteMonitorsWithSiteHours(date), Times.Exactly(1));

            // The hour-less site has no averaging window and is skipped without faulting the run.
            dbClient.Verify(c => c.GetAverageNoiseLevel("Device1", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()),
                Times.Never);
            dbClient.Verify(c => c.WriteDailyAverage(hourless.SiteId, hourless.Id, It.IsAny<string>(), It.IsAny<double>(), It.IsAny<DateTime>()),
                Times.Never);

            // The valid site is still fully processed.
            dbClient.Verify(c => c.GetAverageNoiseLevel("Device2", "LAeq", date + valid.StartTime!.Value, date + valid.EndTime!.Value),
                Times.Exactly(1));
            dbClient.Verify(c => c.WriteDailyAverage(valid.SiteId, valid.Id, "lAeq", 42.0, date), Times.Exactly(1));
            dbClient.Verify(c => c.ReadRules("Device2"), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            httpClient.VerifyNoOtherCalls();
            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestNotifySiteAverages_MonitorFailureIsRecorded_NextMonitorStillProcessed()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                             out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

            DateTime date = new(2026, 7, 29);
            TimeSpan start = TimeSpan.Parse("09:00:00", CultureInfo.InvariantCulture);
            TimeSpan end = TimeSpan.Parse("17:00:00", CultureInfo.InvariantCulture);
            SiteMonitorsWithSiteHoursDto failing = SiteMonitor("Device1", start, end);
            SiteMonitorsWithSiteHoursDto next = SiteMonitor("Device2", start, end);
            IOException failure = new("hypertable unavailable");
            dbClient.Setup(c => c.ReadSiteMonitorsWithSiteHours(date)).Returns([failing, next]);
            dbClient.Setup(c => c.GetAverageNoiseLevel("Device1", "LAeq", date + start, date + end)).Throws(failure);
            dbClient.Setup(c => c.GetAverageNoiseLevel("Device2", "LAeq", date + start, date + end)).Returns(42.0);
            dbClient.Setup(c => c.ReadRules("Device2")).Returns([]);

            AggregateException aggregate = await Assert.ThrowsExactlyAsync<AggregateException>(
                () => testObj.NotifySiteAveragesAsync(date, TestContext.CancellationToken));

            Assert.HasCount(1, aggregate.InnerExceptions);
            Assert.AreSame(failure, aggregate.InnerExceptions[0]);
            dbClient.Verify(c => c.HandleException("NotifySiteAverages SerialId=Device1", failure), Times.Exactly(1));

            // The failure is isolated per monitor: the next site is still fully processed.
            dbClient.Verify(c => c.WriteDailyAverage(next.SiteId, next.Id, "lAeq", 42.0, date), Times.Exactly(1));
            dbClient.Verify(c => c.ReadRules("Device2"), Times.Exactly(1));

            httpClient.VerifyNoOtherCalls();
            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        private static SiteMonitorsWithSiteHoursDto SiteMonitor(
            string serialId,
            TimeSpan? startTime,
            TimeSpan? endTime) =>
            new(Guid.NewGuid(),
                "123",
                serialId,
                0,
                false,
                Guid.NewGuid(),
                "Site",
                startTime,
                endTime);

        public TestContext TestContext { get; set; } = null!;

    }
}
