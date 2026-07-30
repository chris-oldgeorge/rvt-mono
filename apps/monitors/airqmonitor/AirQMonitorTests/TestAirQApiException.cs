using System.Globalization;
using System.Text.Json;
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
namespace AirQMonitorTests
{
    [TestClass]
    public class TestAirQApiException
    {
        public TestAirQApiException()
        {
            ILoggerFactory factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestAirQApiException");
        }

        [TestMethod]
        public async Task TestStoreMonitors_HandlesJsonExceptionCorrectly()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            httpClient.Setup(c => c.GetAsync("/instrumentList?userID=foo&token=bar", It.IsAny<CancellationToken>())).
                Returns(Task<string>.Factory.StartNew(() => "Blah Blah Blah.", TestContext.CancellationToken));

            await Assert.ThrowsAsync<AdapterException>(() => testObj.StoreMonitorsAsync("foo", "bar", TestContext.CancellationToken));

            httpClient.Verify(c => c.GetAsync("/instrumentList?userID=foo&token=bar", It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.HandleException("StoreMonitors", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreMonitors_HandlesAdapterExceptionCorrectly()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            httpClient.Setup(c => c.GetAsync("/instrumentList?userID=foo&token=bar", It.IsAny<CancellationToken>())).
                    Returns(Task<string>.Factory.StartNew(() => AirQFixture.TooManyRequestsJson(), TestContext.CancellationToken));

            await Assert.ThrowsAsync<AdapterException>(() => testObj.StoreMonitorsAsync("foo", "bar", TestContext.CancellationToken));


            httpClient.Verify(c => c.GetAsync("/instrumentList?userID=foo&token=bar", It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.HandleException("StoreMonitors", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreMonitors_HandlesMetaDataAdapterExceptionCorrectly()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            httpClient.Setup(c => c.GetAsync("/instrumentList?userID=foo&token=bar", It.IsAny<CancellationToken>())).
                    Returns(Task<string>.Factory.StartNew(() => AirQFixture.InstrumentsResponseJson(), TestContext.CancellationToken));

            // 2nd monitor should fail to get metadata
            httpClient.Setup(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device1", It.IsAny<CancellationToken>())).
                Returns(Task<string>.Factory.StartNew(() => AirQFixture.MetaDataResponseJson(), TestContext.CancellationToken));

            httpClient.Setup(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device2", It.IsAny<CancellationToken>())).
                Returns(Task<string>.Factory.StartNew(() => AirQFixture.TooManyRequestsJson(), TestContext.CancellationToken));

            httpClient.Setup(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device3", It.IsAny<CancellationToken>())).
                Returns(Task<string>.Factory.StartNew(() => AirQFixture.MetaDataResponseJson(), TestContext.CancellationToken));

            await testObj.StoreMonitorsAsync("foo", "bar", TestContext.CancellationToken);

            httpClient.Verify(c => c.GetAsync("/instrumentList?userID=foo&token=bar", It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device1", It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device2", It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device3", It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.HandleException("GetMetaData", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
            dbClient.Verify(c => c.WriteMonitorList(It.Is<List<NoiseMonitorDto>>(l => l.Count == 3)));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreMonitors_HandlesExceptionCorrectly()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);


            httpClient.Setup(c => c.GetAsync("/instrumentList?userID=foo&token=bar", It.IsAny<CancellationToken>())).
                    Throws(new IOException());
            httpClient.VerifyNoOtherCalls();

            await Assert.ThrowsAsync<AdapterException>(() => testObj.StoreMonitorsAsync("foo", "bar", TestContext.CancellationToken));

            dbClient.Verify(c => c.HandleException("StoreMonitors",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is IOException)), Times.Exactly(1));

            dbClient.VerifyNoOtherCalls();


            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestReadMonitors_HandlesExceptionCorrectly()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Throws(new IOException());

            await Assert.ThrowsAsync<AdapterException>(() => testObj.StoreNoiseLevelsAsync("foo", "bar", TestContext.CancellationToken));

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreNoiseLevels",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is IOException)), Times.Exactly(1));

            httpClient.VerifyNoOtherCalls();
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }


        [TestMethod]
        public async Task TestStoreNoiseLevels_HandlesJsonExceptionCorrectly()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/latestData\\?userID=foo&token=bar&instrumentID=*"), It.IsAny<CancellationToken>())).
                                Returns(Task<string>.Factory.StartNew(() => "Blah Blah Blah", TestContext.CancellationToken));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            AggregateException exception = await Assert.ThrowsAsync<AggregateException>(() => testObj.StoreNoiseLevelsAsync("foo", "bar", TestContext.CancellationToken));
            Assert.HasCount(3, exception.InnerExceptions);

            httpClient.Verify(c => c.GetAsync("/latestData?userID=foo&token=bar&instrumentID=Device1", It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestData?userID=foo&token=bar&instrumentID=Device2", It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestData?userID=foo&token=bar&instrumentID=Device3", It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreNoiseLevels SerialId=Device1", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreNoiseLevels SerialId=Device2", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreNoiseLevels SerialId=Device3", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));
            dbClient.Verify(c => c.UpdateMonitorStatus("Device1", It.Is<NoiseMonitorStatus>(s => s.ErrorCount == 1)), Times.Exactly(1));
            dbClient.Verify(c => c.UpdateMonitorStatus("Device2", It.Is<NoiseMonitorStatus>(s => s.ErrorCount == 1)), Times.Exactly(1));
            dbClient.Verify(c => c.UpdateMonitorStatus("Device3", It.Is<NoiseMonitorStatus>(s => s.ErrorCount == 1)), Times.Exactly(1));


            dbClient.VerifyNoOtherCalls();


            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreNoiseLevels_HandlesAdapterExceptionCorrectly()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/latestData\\?userID=foo&token=bar&instrumentID=*"), It.IsAny<CancellationToken>())).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.TooManyRequestsJson(), TestContext.CancellationToken));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            AggregateException exception = await Assert.ThrowsAsync<AggregateException>(() => testObj.StoreNoiseLevelsAsync("foo", "bar", TestContext.CancellationToken));
            Assert.HasCount(3, exception.InnerExceptions);

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreNoiseLevels SerialId=Device1", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreNoiseLevels SerialId=Device2", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreNoiseLevels SerialId=Device3", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));

            dbClient.Verify(c => c.UpdateMonitorStatus("Device1", It.Is<NoiseMonitorStatus>(s => s.ErrorCount == 1)), Times.Exactly(1));
            dbClient.Verify(c => c.UpdateMonitorStatus("Device2", It.Is<NoiseMonitorStatus>(s => s.ErrorCount == 1)), Times.Exactly(1));
            dbClient.Verify(c => c.UpdateMonitorStatus("Device3", It.Is<NoiseMonitorStatus>(s => s.ErrorCount == 1)), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();


            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreNoiseLevels_HandlesExceptionCorrectly()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/latestData\\?userID=foo&token=bar&instrumentID=*"), It.IsAny<CancellationToken>())).
                    Throws(new IOException());

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));
            AggregateException exception = await Assert.ThrowsAsync<AggregateException>(() => testObj.StoreNoiseLevelsAsync("foo", "bar", TestContext.CancellationToken));
            Assert.HasCount(3, exception.InnerExceptions);

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreNoiseLevels SerialId=Device1",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is IOException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreNoiseLevels SerialId=Device2",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is IOException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreNoiseLevels SerialId=Device3",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is IOException)), Times.Exactly(1));

            dbClient.Verify(c => c.UpdateMonitorStatus("Device1", It.Is<NoiseMonitorStatus>(s => s.ErrorCount == 1)), Times.Exactly(1));
            dbClient.Verify(c => c.UpdateMonitorStatus("Device2", It.Is<NoiseMonitorStatus>(s => s.ErrorCount == 1)), Times.Exactly(1));
            dbClient.Verify(c => c.UpdateMonitorStatus("Device3", It.Is<NoiseMonitorStatus>(s => s.ErrorCount == 1)), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();


            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreNoiseLevelsForYesterday_HandlesJsonExceptionCorrectly()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            string yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            httpClient.Setup(c => c.GetAsync(It.IsRegex(string.Format("\\/dataForDate\\?userID=foo&date={0}&token=bar&instrumentID=*", yesterday)), It.IsAny<CancellationToken>())).
                                Returns(Task<string>.Factory.StartNew(() => "Blah Blah Blah", TestContext.CancellationToken));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            AggregateException exception = await Assert.ThrowsAsync<AggregateException>(() => testObj.StoreAllNoiseLevelsForYesterdayAsync("foo", "bar", TestContext.CancellationToken));
            Assert.HasCount(3, exception.InnerExceptions);

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device1", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device2", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device3", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));

            dbClient.VerifyNoOtherCalls();


            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreNoiseLevelsForYesterday_HandlesAdapterExceptionCorrectly()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            string yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            httpClient.Setup(c => c.GetAsync(It.IsRegex(string.Format("\\/dataForDate\\?userID=foo&date={0}&token=bar&instrumentID=*", yesterday)), It.IsAny<CancellationToken>())).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.TooManyRequestsJson(), TestContext.CancellationToken));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            AggregateException exception = await Assert.ThrowsAsync<AggregateException>(() => testObj.StoreAllNoiseLevelsForYesterdayAsync("foo", "bar", TestContext.CancellationToken));
            Assert.HasCount(3, exception.InnerExceptions);

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device1", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device2", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device3", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();


            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreNoiseLevelsForYesterday_HandlesExceptionCorrectly()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            string yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            httpClient.Setup(c => c.GetAsync(It.IsRegex(string.Format("\\/dataForDate\\?userID=foo&date={0}&token=bar&instrumentID=*", yesterday)), It.IsAny<CancellationToken>())).
                    Throws(new IOException());
            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            AggregateException exception = await Assert.ThrowsAsync<AggregateException>(() => testObj.StoreAllNoiseLevelsForYesterdayAsync("foo", "bar", TestContext.CancellationToken));
            Assert.HasCount(3, exception.InnerExceptions);

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device1",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is IOException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device2",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is IOException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device3",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is IOException)), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();


            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }


        [TestMethod]
        public async Task TestStoreNoiseLevelsForDate_HandlesJsonExceptionCorrectly()
        {
            string dateStr = "2023-09-11";
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            httpClient.Setup(c => c.GetAsync(It.IsRegex(string.Format("\\/dataForDate\\?userID=foo&date={0}&token=bar&instrumentID=*", dateStr)), It.IsAny<CancellationToken>())).
                                Returns(Task<string>.Factory.StartNew(() => "Blah!!!", TestContext.CancellationToken));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            AggregateException exception = await Assert.ThrowsAsync<AggregateException>(() => testObj.StoreNoiseLevelsForDateAsync("foo", "bar", dateStr, TestContext.CancellationToken));
            Assert.HasCount(3, exception.InnerExceptions);

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device1", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device2", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device3", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));

            dbClient.VerifyNoOtherCalls();


            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreNoiseLevelsForDate_HandlesAdapterExceptionCorrectly()
        {
            string dateStr = "2023-09-11";
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            httpClient.Setup(c => c.GetAsync(It.IsRegex(string.Format("\\/dataForDate\\?userID=foo&date={0}&token=bar&instrumentID=*", dateStr)), It.IsAny<CancellationToken>())).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.TooManyRequestsJson(), TestContext.CancellationToken));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            AggregateException exception = await Assert.ThrowsAsync<AggregateException>(() => testObj.StoreNoiseLevelsForDateAsync("foo", "bar", dateStr, TestContext.CancellationToken));
            Assert.HasCount(3, exception.InnerExceptions);

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device1", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device2", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device3", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();


            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreNoiseLevelsForDate_HandlesExceptionCorrectly()
        {
            string dateStr = "2023-09-11";
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                    out Mock<IDBClient> dbClient,
                                                    out Mock<IMqttClient> mqttClient,
                                                    out Mock<IAlertIngressPort> messageClient);

            httpClient.Setup(c => c.GetAsync(It.IsRegex(string.Format("\\/dataForDate\\?userID=foo&date={0}&token=bar&instrumentID=*", dateStr)), It.IsAny<CancellationToken>())).
                   Throws(new IOException());

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            AggregateException exception = await Assert.ThrowsAsync<AggregateException>(() => testObj.StoreNoiseLevelsForDateAsync("foo", "bar", dateStr, TestContext.CancellationToken));
            Assert.HasCount(3, exception.InnerExceptions);

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device1",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is IOException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device2",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is IOException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device3",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is IOException)), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();


            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestCheckForOfflineMonitors_OneFailingMonitorDoesNotAbortTheFleet()
        {
            AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IAlertIngressPort> messageClient);

            dbClient.Setup(c => c.ReadRules(null)).Returns(AirQFixture.OfflineRules());
            List<NoiseMonitorDto> monitors = AirQFixture.MonitorDtos(DateTime.UtcNow.AddMinutes(-25 * 60), NoiseMonitorStatus.ACTIVE);
            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<DateTime?>())).Returns(monitors);

            // Monitor 2 of 3 fails; monitor 3 must still be evaluated and marked.
            messageClient.Setup(c => c.AcceptAsync(
                    It.Is<AlertSignal>(signal => signal.SerialId == "Device2"),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException());

            AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
                () => testObj.CheckForOfflineMonitorsAsync(TestContext.CancellationToken));
            Assert.HasCount(1, exception.InnerExceptions);

            foreach (string serialId in new[] { "Device1", "Device2", "Device3" })
            {
                messageClient.Verify(c => c.AcceptAsync(
                    It.Is<AlertSignal>(signal => signal.SerialId == serialId && signal.AlertType == AlertType.Offline),
                    It.IsAny<CancellationToken>()), Times.Exactly(1));
            }

            dbClient.Verify(c => c.SetMonitorOffline(monitors[0].Id, true), Times.Exactly(1));
            dbClient.Verify(c => c.SetMonitorOffline(monitors[2].Id, true), Times.Exactly(1));
            dbClient.Verify(c => c.SetMonitorOffline(monitors[1].Id, It.IsAny<bool>()), Times.Never);
            dbClient.Verify(c => c.HandleException("CheckForOfflineMonitors SerialId=Device2",
                                    It.Is<Exception>(e => e is IOException)), Times.Exactly(1));

            httpClient.VerifyNoOtherCalls();
            mqttClient.VerifyNoOtherCalls();
        }

        public TestContext TestContext { get; set; } = null!;
    }
}
