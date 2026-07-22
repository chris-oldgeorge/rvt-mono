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
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace MyAtmMonitorTests
{

    [TestClass]
    public class TestMyAtmApiExceptions
    {
        public TestMyAtmApiExceptions()
        {
            var factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestMyAtmApiExceptions");
        }

        [TestMethod]
        public async Task TestStoreDevices_HandlesJsonExceptionCorrectly()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageClient);
            httpClient.Setup(c => c.GetAsync("/api/customers/987/devices?$skip=0&$top=100")).
                    Returns(Task<string>.Factory.StartNew(() => "Blah Blah Blah."));

            var aggregate = await Assert.ThrowsExactlyAsync<MyAtmJobAggregateException>(() => testObj.StoreMonitorsAsync(987));
            Assert.IsInstanceOfType<AdapterException>(aggregate.Failures.Single().Exception);

            httpClient.Verify(c => c.GetAsync("/api/customers/987/devices?$skip=0&$top=100"), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.HandleException("StoreMonitors page=1", It.Is<AdapterException>(
                                e => e.InnerException is JsonException)), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreDevices_HandlesExceptionCorrectly()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                         out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageClient);

            httpClient.Setup(c => c.GetAsync("/api/customers/987/devices?$skip=0&$top=100")).
                    Throws(new IOException());

            var aggregate = await Assert.ThrowsExactlyAsync<MyAtmJobAggregateException>(() => testObj.StoreMonitorsAsync(987));
            Assert.IsInstanceOfType<AdapterException>(aggregate.Failures.Single().Exception);
            httpClient.Verify(c => c.GetAsync("/api/customers/987/devices?$skip=0&$top=100"), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.HandleException("StoreMonitors page=1",
                                    It.Is<AdapterException>(e => e.InnerException is IOException)), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void ReadMonitorsList_HandlesExceptionCorrectly()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                         out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageClient);

            var customerId = 656;
            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<int>(), null)).
                    Throws(new IOException());

            Assert.ThrowsExactly<IOException>(() => testObj.StoreDustLevels<DeviceMeasurement>(customerId, Period.Minutes1));

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
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                         out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageClient);

            var customerId = 987;
            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/api\\/customers\\/" + customerId + "\\/devices\\/.*\\/measurements" + Regex.Escape(TestUtil.MEASUREMENT_SELECT)))).
                                 Returns(Task<string>.Factory.StartNew(() => "Blah !!!"));

            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<int>(), null)).
                    Returns(MyAtmFixture.CustomerDeviceDtos(DateTime.UtcNow));

            var exception = await Assert.ThrowsExactlyAsync<MyAtmJobAggregateException>(
                () => testObj.StoreDustLevelsAsync<DeviceMeasurement>(customerId, Period.Minutes1));
            Assert.AreEqual(2, exception.Failures.Count);

            httpClient.Verify(c => c.GetAsync(It.IsRegex(TestUtil.MeasurementPageRequestPattern(987, "11111", "", TestUtil.MEASUREMENT_SELECT))), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync(It.IsRegex(TestUtil.MeasurementPageRequestPattern(987, "22222", "", TestUtil.MEASUREMENT_SELECT))), Times.Exactly(1));
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
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                         out Mock<IMqttClient> mqttClient, out Mock<IMessageService> messageClient);

            var customerId = 987;
            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/api\\/customers\\/" + customerId + "\\/devices\\/.*\\/measurements" + Regex.Escape(TestUtil.MEASUREMENT_SELECT)))).
                                 Throws(new IOException());

            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<int>(), null)).
                    Returns(MyAtmFixture.CustomerDeviceDtos(DateTime.UtcNow));

            var exception = await Assert.ThrowsExactlyAsync<MyAtmJobAggregateException>(
                () => testObj.StoreDustLevelsAsync<DeviceMeasurement>(customerId, Period.Minutes1));
            Assert.AreEqual(2, exception.Failures.Count);

            httpClient.Verify(c => c.GetAsync(It.IsRegex(TestUtil.MeasurementPageRequestPattern(987, "11111", "", TestUtil.MEASUREMENT_SELECT))), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync(It.IsRegex(TestUtil.MeasurementPageRequestPattern(987, "22222", "", TestUtil.MEASUREMENT_SELECT))), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(It.IsAny<int>(), null), Times.Exactly(1));
            dbClient.Verify(c => c.HandleException(It.Is<string>(tag => tag.StartsWith("StoreDustLevels SerialId=")), It.Is<Exception>(exception => exception is AdapterException)), Times.Exactly(2));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }
    }
}
