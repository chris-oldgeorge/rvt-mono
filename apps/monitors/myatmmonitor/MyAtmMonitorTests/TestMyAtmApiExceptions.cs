// File summary: Verifies MyAtm API exception handling and failure logging paths.
// Major updates:
// - 2026-06-18: Realigned expectations with paged monitor listing and direct measurement exception logging.
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Model.Json;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
namespace MyAtmMonitorTests
{

    [TestClass]
    public class TestMyAtmApiExceptions
    {
        public TestMyAtmApiExceptions()
        {
            ILoggerFactory factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestMyAtmApiExceptions");
        }

        [TestMethod]
        public async Task TestStoreDevices_HandlesJsonExceptionCorrectly()
        {
            MyAtmApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient, out Mock<INotificationDeliveryService> messageClient);
            httpClient.Setup(c => c.GetAsync("/api/customers/987/devices?$skip=0&$top=100", TestContext.CancellationToken)).
                    Returns(Task<string>.Factory.StartNew(() => "Blah Blah Blah.", TestContext.CancellationToken));

            MyAtmJobAggregateException aggregate = await Assert.ThrowsExactlyAsync<MyAtmJobAggregateException>(() => testObj.StoreMonitorsAsync(987, TestContext.CancellationToken));
            Assert.IsInstanceOfType<AdapterException>(aggregate.Failures.Single().Exception);

            httpClient.Verify(c => c.GetAsync("/api/customers/987/devices?$skip=0&$top=100", TestContext.CancellationToken), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.HandleException("StoreMonitors page=1", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreDevices_HandlesExceptionCorrectly()
        {
            MyAtmApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                         out Mock<IMqttClient> mqttClient, out Mock<INotificationDeliveryService> messageClient);

            httpClient.Setup(c => c.GetAsync("/api/customers/987/devices?$skip=0&$top=100", TestContext.CancellationToken)).
                    Throws(new IOException());

            MyAtmJobAggregateException aggregate = await Assert.ThrowsExactlyAsync<MyAtmJobAggregateException>(() => testObj.StoreMonitorsAsync(987, TestContext.CancellationToken));
            Assert.IsInstanceOfType<AdapterException>(aggregate.Failures.Single().Exception);
            httpClient.Verify(c => c.GetAsync("/api/customers/987/devices?$skip=0&$top=100", TestContext.CancellationToken), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.HandleException("StoreMonitors page=1",
                                    It.Is<AdapterException>(e => e.InnerException is IOException)), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task ReadMonitorsList_HandlesExceptionCorrectly()
        {
            MyAtmApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                         out Mock<IMqttClient> mqttClient, out Mock<INotificationDeliveryService> messageClient);

            int customerId = 656;
            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<int>(), null)).
                    Throws(new IOException());

            await Assert.ThrowsExactlyAsync<IOException>(() => testObj.StoreDustLevelsAsync<DeviceMeasurement>(customerId, Period.Minutes1, TestContext.CancellationToken));

            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(It.IsAny<int>(), null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("ReadMonitors",
                                    It.Is<Exception>(e =>
                                        e is IOException)), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreDustLevels_HandlesJsonExceptionCorrectly()
        {
            MyAtmApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                         out Mock<IMqttClient> mqttClient, out Mock<INotificationDeliveryService> messageClient);

            int customerId = 987;
            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/api\\/customers\\/" + customerId + "\\/devices\\/.*\\/measurements" + Regex.Escape(TestUtil.MEASUREMENT_SELECT)), TestContext.CancellationToken)).
                                 Returns(Task<string>.Factory.StartNew(() => "Blah !!!", TestContext.CancellationToken));

            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<int>(), null)).
                    Returns(MyAtmFixture.CustomerDeviceDtos(DateTime.UtcNow));

            MyAtmJobAggregateException exception = await Assert.ThrowsExactlyAsync<MyAtmJobAggregateException>(
                () => testObj.StoreDustLevelsAsync<DeviceMeasurement>(customerId, Period.Minutes1, TestContext.CancellationToken));
            Assert.HasCount(2, exception.Failures);

            httpClient.Verify(c => c.GetAsync(It.IsRegex(TestUtil.MeasurementPageRequestPattern(987, "11111", "", TestUtil.MEASUREMENT_SELECT)), TestContext.CancellationToken), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync(It.IsRegex(TestUtil.MeasurementPageRequestPattern(987, "22222", "", TestUtil.MEASUREMENT_SELECT)), TestContext.CancellationToken), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(It.IsAny<int>(), null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException(It.Is<string>(tag => tag.StartsWith("StoreDustLevels SerialId=")), It.Is<Exception>(exception => exception is AdapterException)), Times.Exactly(2));
            dbClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreDustLevels_HandlesExceptionCorrectly()
        {
            MyAtmApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                         out Mock<IMqttClient> mqttClient, out Mock<INotificationDeliveryService> messageClient);

            int customerId = 987;
            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/api\\/customers\\/" + customerId + "\\/devices\\/.*\\/measurements" + Regex.Escape(TestUtil.MEASUREMENT_SELECT)), TestContext.CancellationToken)).
                                 Throws(new IOException());

            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<int>(), null)).
                    Returns(MyAtmFixture.CustomerDeviceDtos(DateTime.UtcNow));

            MyAtmJobAggregateException exception = await Assert.ThrowsExactlyAsync<MyAtmJobAggregateException>(
                () => testObj.StoreDustLevelsAsync<DeviceMeasurement>(customerId, Period.Minutes1, TestContext.CancellationToken));
            Assert.HasCount(2, exception.Failures);

            httpClient.Verify(c => c.GetAsync(It.IsRegex(TestUtil.MeasurementPageRequestPattern(987, "11111", "", TestUtil.MEASUREMENT_SELECT)), TestContext.CancellationToken), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync(It.IsRegex(TestUtil.MeasurementPageRequestPattern(987, "22222", "", TestUtil.MEASUREMENT_SELECT)), TestContext.CancellationToken), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(It.IsAny<int>(), null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException(It.Is<string>(tag => tag.StartsWith("StoreDustLevels SerialId=")), It.Is<Exception>(exception => exception is AdapterException)), Times.Exactly(2));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        public TestContext TestContext { get; set; } = null!;
    }
}
