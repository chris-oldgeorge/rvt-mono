using System.Globalization;
using System.Text.Json;
using AirQ.Api;
using AirQ.Api.Db;
using AirQ.Api.Http;
using AirQ.Model.Dto;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
namespace AirQMonitorTests;

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
                                                 out Mock<IMessageService> messageService);

        httpClient.Setup(c => c.GetAsync("/instrumentList?userID=foo&token=bar", It.IsAny<CancellationToken>())).
            Returns(Task<string>.Factory.StartNew(() => "Blah Blah Blah.", TestContext.CancellationToken));

        await Assert.ThrowsAsync<AdapterException>(() => testObj.StoreMonitorsAsync("foo", "bar", TestContext.CancellationToken));

        httpClient.Verify(c => c.GetAsync("/instrumentList?userID=foo&token=bar", It.IsAny<CancellationToken>()), Times.Exactly(1));
        httpClient.VerifyNoOtherCalls();

        dbClient.Verify(c => c.HandleException("StoreMonitors", It.Is<AdapterException>(
                            e => e.InnerException is JsonException)), Times.Exactly(1));
        dbClient.VerifyNoOtherCalls();

        mqttClient.VerifyNoOtherCalls();
        messageService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestStoreMonitors_HandlesAdapterExceptionCorrectly()
    {
        AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageService);

        httpClient.Setup(c => c.GetAsync("/instrumentList?userID=foo&token=bar", It.IsAny<CancellationToken>())).
                Returns(Task<string>.Factory.StartNew(() => AirQFixture.TooManyRequestsJson(), TestContext.CancellationToken));

        await Assert.ThrowsAsync<AdapterException>(() => testObj.StoreMonitorsAsync("foo", "bar", TestContext.CancellationToken));


        httpClient.Verify(c => c.GetAsync("/instrumentList?userID=foo&token=bar", It.IsAny<CancellationToken>()), Times.Exactly(1));
        httpClient.VerifyNoOtherCalls();

        dbClient.Verify(c => c.HandleException("StoreMonitors", It.Is<AdapterException>(
                            e => "Too many requests!".Equals(e.Message))), Times.Exactly(1));
        dbClient.VerifyNoOtherCalls();

        mqttClient.VerifyNoOtherCalls();
        messageService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestStoreMonitors_HandlesMetaDataAdapterExceptionCorrectly()
    {
        AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageService);

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
        messageService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestStoreMonitors_HandlesExceptionCorrectly()
    {
        AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageService);


        httpClient.Setup(c => c.GetAsync("/instrumentList?userID=foo&token=bar", It.IsAny<CancellationToken>())).
                Throws(new IOException());
        httpClient.VerifyNoOtherCalls();

        await Assert.ThrowsAsync<AdapterException>(() => testObj.StoreMonitorsAsync("foo", "bar", TestContext.CancellationToken));

        dbClient.Verify(c => c.HandleException("StoreMonitors",
                                It.Is<AdapterException>(e =>
                                    e.InnerException is IOException)), Times.Exactly(1));

        dbClient.VerifyNoOtherCalls();


        mqttClient.VerifyNoOtherCalls();
        messageService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestReadMonitors_HandlesExceptionCorrectly()
    {
        AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageService);

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
        messageService.VerifyNoOtherCalls();
    }


    [TestMethod]
    public async Task TestStoreNoiseLevels_HandlesJsonExceptionCorrectly()
    {
        AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageService);

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
        messageService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestStoreNoiseLevels_HandlesAdapterExceptionCorrectly()
    {
        AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageService);

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
        messageService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestStoreNoiseLevels_HandlesExceptionCorrectly()
    {
        AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageService);

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
        messageService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestStoreNoiseLevelsForYesterday_HandlesJsonExceptionCorrectly()
    {
        AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageService);

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
        messageService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestStoreNoiseLevelsForYesterday_HandlesAdapterExceptionCorrectly()
    {
        AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageService);

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
        messageService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestStoreNoiseLevelsForYesterday_HandlesExceptionCorrectly()
    {
        AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageService);

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
        messageService.VerifyNoOtherCalls();
    }


    [TestMethod]
    public async Task TestStoreNoiseLevelsForDate_HandlesJsonExceptionCorrectly()
    {
        string dateStr = "2023-09-11";
        AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageService);

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
        messageService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestStoreNoiseLevelsForDate_HandlesAdapterExceptionCorrectly()
    {
        string dateStr = "2023-09-11";
        AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageService);

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
        messageService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestStoreNoiseLevelsForDate_HandlesExceptionCorrectly()
    {
        string dateStr = "2023-09-11";
        AirQApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                out Mock<IDBClient> dbClient,
                                                out Mock<IMqttClient> mqttClient,
                                                out Mock<IMessageService> messageService);

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
        messageService.VerifyNoOtherCalls();
    }

    public TestContext TestContext { get; set; } = null!;
}
