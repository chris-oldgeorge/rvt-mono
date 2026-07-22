using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text.Json;
using AirQ.Api.Db;
using AirQ.Api.Http;
using AirQ.Model.Dto;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Rules.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace AirQMonitorTests
{
    [TestClass]
    public class TestAirQApiException
    {
        public TestAirQApiException()
        {
            var factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestAirQApiException");
        }

        [TestMethod]
        public void TestStoreMonitors_HandlesJsonExceptionCorrectly()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            httpClient.Setup(c => c.GetAsync("/instrumentList?userID=foo&token=bar")).
                Returns(Task<string>.Factory.StartNew(() => "Blah Blah Blah."));

            Assert.Throws<AdapterException>(() => testObj.StoreMonitors("foo", "bar"));

            httpClient.Verify(c => c.GetAsync("/instrumentList?userID=foo&token=bar"), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.HandleException("StoreMonitors", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreMonitors_HandlesAdapterExceptionCorrectly()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            httpClient.Setup(c => c.GetAsync("/instrumentList?userID=foo&token=bar")).
                    Returns(Task<string>.Factory.StartNew(() => AirQFixture.TooManyRequestsJson()));

            Assert.Throws<AdapterException>(() => testObj.StoreMonitors("foo", "bar"));


            httpClient.Verify(c => c.GetAsync("/instrumentList?userID=foo&token=bar"), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.HandleException("StoreMonitors", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreMonitors_HandlesMetaDataAdapterExceptionCorrectly()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            httpClient.Setup(c => c.GetAsync("/instrumentList?userID=foo&token=bar")).
                    Returns(Task<string>.Factory.StartNew(() => AirQFixture.InstrumentsResponseJson()));

            // 2nd monitor should fail to get metadata
            httpClient.Setup(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device1")).
                Returns(Task<string>.Factory.StartNew(() => AirQFixture.MetaDataResponseJson()));

            httpClient.Setup(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device2")).
                Returns(Task<string>.Factory.StartNew(() => AirQFixture.TooManyRequestsJson()));

            httpClient.Setup(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device3")).
                Returns(Task<string>.Factory.StartNew(() => AirQFixture.MetaDataResponseJson()));

            testObj.StoreMonitors("foo", "bar");

            httpClient.Verify(c => c.GetAsync("/instrumentList?userID=foo&token=bar"), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device1"), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device2"), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestMetaData?userID=foo&token=bar&instrumentID=Device3"), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.HandleException("GetMetaData", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
            dbClient.Verify(c => c.WriteMonitorList(It.Is<List<NoiseMonitorDto>>(l => l.Count == 3)));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreMonitors_HandlesExceptionCorrectly()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);


            httpClient.Setup(c => c.GetAsync("/instrumentList?userID=foo&token=bar")).
                    Throws(new IOException());
            httpClient.VerifyNoOtherCalls();

            Assert.Throws<AdapterException>(() => testObj.StoreMonitors("foo", "bar"));

            dbClient.Verify(c => c.HandleException("StoreMonitors",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is AggregateException &&
                                        e.InnerException.InnerException is IOException)), Times.Exactly(1));

            dbClient.VerifyNoOtherCalls();


            mqttClient.VerifyNoOtherCalls();
            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestReadMonitors_HandlesExceptionCorrectly()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Throws(new IOException());

            Assert.Throws<AdapterException>(() => testObj.StoreNoiseLevels("foo", "bar"));

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreNoiseLevels",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is IOException)), Times.Exactly(1));

            httpClient.VerifyNoOtherCalls();
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageService.VerifyNoOtherCalls();
        }


        [TestMethod]
        public void TestStoreNoiseLevels_HandlesJsonExceptionCorrectly()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/latestData\\?userID=foo&token=bar&instrumentID=*"))).
                                Returns(Task<string>.Factory.StartNew(() => "Blah Blah Blah"));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            var exception = Assert.Throws<AggregateException>(() => testObj.StoreNoiseLevels("foo", "bar"));
            Assert.AreEqual(3, exception.InnerExceptions.Count);

            httpClient.Verify(c => c.GetAsync("/latestData?userID=foo&token=bar&instrumentID=Device1"), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestData?userID=foo&token=bar&instrumentID=Device2"), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync("/latestData?userID=foo&token=bar&instrumentID=Device3"), Times.Exactly(1));
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
            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreNoiseLevels_HandlesAdapterExceptionCorrectly()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/latestData\\?userID=foo&token=bar&instrumentID=*"))).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.TooManyRequestsJson()));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            var exception = Assert.Throws<AggregateException>(() => testObj.StoreNoiseLevels("foo", "bar"));
            Assert.AreEqual(3, exception.InnerExceptions.Count);

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
            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreNoiseLevels_HandlesExceptionCorrectly()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/latestData\\?userID=foo&token=bar&instrumentID=*"))).
                    Throws(new IOException());

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));
            var exception = Assert.Throws<AggregateException>(() => testObj.StoreNoiseLevels("foo", "bar"));
            Assert.AreEqual(3, exception.InnerExceptions.Count);

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreNoiseLevels SerialId=Device1",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is AggregateException &&
                                        e.InnerException.InnerException is IOException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreNoiseLevels SerialId=Device2",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is AggregateException &&
                                        e.InnerException.InnerException is IOException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreNoiseLevels SerialId=Device3",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is AggregateException &&
                                        e.InnerException.InnerException is IOException)), Times.Exactly(1));

            dbClient.Verify(c => c.UpdateMonitorStatus("Device1", It.Is<NoiseMonitorStatus>(s => s.ErrorCount == 1)), Times.Exactly(1));
            dbClient.Verify(c => c.UpdateMonitorStatus("Device2", It.Is<NoiseMonitorStatus>(s => s.ErrorCount == 1)), Times.Exactly(1));
            dbClient.Verify(c => c.UpdateMonitorStatus("Device3", It.Is<NoiseMonitorStatus>(s => s.ErrorCount == 1)), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();


            mqttClient.VerifyNoOtherCalls();
            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreNoiseLevelsForYesterday_HandlesJsonExceptionCorrectly()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            var yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            httpClient.Setup(c => c.GetAsync(It.IsRegex(string.Format("\\/dataForDate\\?userID=foo&date={0}&token=bar&instrumentID=*", yesterday)))).
                                Returns(Task<string>.Factory.StartNew(() => "Blah Blah Blah"));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            var exception = Assert.Throws<AggregateException>(() => testObj.StoreAllNoiseLevelsForYesterday("foo", "bar"));
            Assert.AreEqual(3, exception.InnerExceptions.Count);

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device1", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device2", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device3", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));

            dbClient.VerifyNoOtherCalls();


            mqttClient.VerifyNoOtherCalls();
            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreNoiseLevelsForYesterday_HandlesAdapterExceptionCorrectly()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            var yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            httpClient.Setup(c => c.GetAsync(It.IsRegex(string.Format("\\/dataForDate\\?userID=foo&date={0}&token=bar&instrumentID=*", yesterday)))).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.TooManyRequestsJson()));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            var exception = Assert.Throws<AggregateException>(() => testObj.StoreAllNoiseLevelsForYesterday("foo", "bar"));
            Assert.AreEqual(3, exception.InnerExceptions.Count);

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device1", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device2", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device3", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();


            mqttClient.VerifyNoOtherCalls();
            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreNoiseLevelsForYesterday_HandlesExceptionCorrectly()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            var yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            httpClient.Setup(c => c.GetAsync(It.IsRegex(string.Format("\\/dataForDate\\?userID=foo&date={0}&token=bar&instrumentID=*", yesterday)))).
                    Throws(new IOException());
            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            var exception = Assert.Throws<AggregateException>(() => testObj.StoreAllNoiseLevelsForYesterday("foo", "bar"));
            Assert.AreEqual(3, exception.InnerExceptions.Count);

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device1",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is AggregateException &&
                                        e.InnerException.InnerException is IOException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device2",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is AggregateException &&
                                        e.InnerException.InnerException is IOException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device3",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is AggregateException &&
                                        e.InnerException.InnerException is IOException)), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();


            mqttClient.VerifyNoOtherCalls();
            messageService.VerifyNoOtherCalls();
        }


        [TestMethod]
        public void TestStoreNoiseLevelsForDate_HandlesJsonExceptionCorrectly()
        {
            var dateStr = "2023-09-11";
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            httpClient.Setup(c => c.GetAsync(It.IsRegex(string.Format("\\/dataForDate\\?userID=foo&date={0}&token=bar&instrumentID=*", dateStr)))).
                                Returns(Task<string>.Factory.StartNew(() => "Blah!!!"));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            var exception = Assert.Throws<AggregateException>(() => testObj.StoreNoiseLevelsForDate("foo", "bar", dateStr));
            Assert.AreEqual(3, exception.InnerExceptions.Count);

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device1", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device2", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device3", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));

            dbClient.VerifyNoOtherCalls();


            mqttClient.VerifyNoOtherCalls();
            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreNoiseLevelsForDate_HandlesAdapterExceptionCorrectly()
        {
            var dateStr = "2023-09-11";
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<IMessageService> messageService);

            httpClient.Setup(c => c.GetAsync(It.IsRegex(string.Format("\\/dataForDate\\?userID=foo&date={0}&token=bar&instrumentID=*", dateStr)))).
                                Returns(Task<string>.Factory.StartNew(() => AirQFixture.TooManyRequestsJson()));

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            var exception = Assert.Throws<AggregateException>(() => testObj.StoreNoiseLevelsForDate("foo", "bar", dateStr));
            Assert.AreEqual(3, exception.InnerExceptions.Count);

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device1", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device2", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device3", It.Is<AdapterException>(
                                e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();


            mqttClient.VerifyNoOtherCalls();
            messageService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void TestStoreNoiseLevelsForDate_HandlesExceptionCorrectly()
        {
            var dateStr = "2023-09-11";
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                    out Mock<IDBClient> dbClient,
                                                    out Mock<IMqttClient> mqttClient,
                                                    out Mock<IMessageService> messageService);

            httpClient.Setup(c => c.GetAsync(It.IsRegex(string.Format("\\/dataForDate\\?userID=foo&date={0}&token=bar&instrumentID=*", dateStr)))).
                   Throws(new IOException());

            dbClient.Setup(c => c.ReadMonitorList(null)).
                    Returns(AirQFixture.MonitorDtos(AirQFixture.BeforeSampleData, NoiseMonitorStatus.ACTIVE));

            var exception = Assert.Throws<AggregateException>(() => testObj.StoreNoiseLevelsForDate("foo", "bar", dateStr));
            Assert.AreEqual(3, exception.InnerExceptions.Count);

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device1",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is AggregateException &&
                                        e.InnerException.InnerException is IOException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device2",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is AggregateException &&
                                        e.InnerException.InnerException is IOException)), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StoreAllNoiseLevelsForDate SerialId=Device3",
                                    It.Is<AdapterException>(e =>
                                        e.InnerException is AggregateException &&
                                        e.InnerException.InnerException is IOException)), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();


            mqttClient.VerifyNoOtherCalls();
            messageService.VerifyNoOtherCalls();
        }
    }
}
