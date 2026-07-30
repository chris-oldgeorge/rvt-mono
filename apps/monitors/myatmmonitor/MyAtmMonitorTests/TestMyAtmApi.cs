using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Delivery;
using MyAtm.Model;
using MyAtm.Model.Config;
using MyAtm.Model.Dto;
using MyAtm.Model.Json;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
namespace MyAtmMonitorTests
{

    [TestClass]
    public class TestMyAtmApi
    {

        public TestMyAtmApi()
        {
            ILoggerFactory factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestMyAtmApi");
        }


        [TestMethod]
        public async Task TestStoreDustLevels_EmptyRules_Success()
        {
            MyAtmApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<INotificationDeliveryService> messageClient);

            int customerId = 656;
            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/api\\/customers\\/" + customerId + "\\/devices\\/.*\\/measurements" + Regex.Escape(TestUtil.MEASUREMENT_SELECT)), TestContext.CancellationToken)).
                                 Returns(Task<string>.Factory.StartNew(() => MyAtmFixture.MeasurementsResponseJson(Period.Minutes1), TestContext.CancellationToken));

            List<DustMonitorDto> monitors = MyAtmFixture.CustomerDeviceDtos(null);
            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<int>(), null)).
                    Returns(monitors);

            dbClient.Setup(c => c.ReadRules(It.IsAny<string>())).
                Returns([]);

            await testObj.StoreDustLevelsAsync<DeviceMeasurement>(customerId, Period.Minutes1, TestContext.CancellationToken);

            httpClient.Verify(c => c.GetAsync(It.IsRegex(TestUtil.MeasurementPageRequestPattern(656, "11111", "", TestUtil.MEASUREMENT_SELECT)), TestContext.CancellationToken), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync(It.IsRegex(TestUtil.MeasurementPageRequestPattern(656, "22222", "", TestUtil.MEASUREMENT_SELECT)), TestContext.CancellationToken), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(It.IsAny<int>(), null), Times.Exactly(1));
            DateTime expectedDateTime = DateTime.Parse("2023-09-25T10:29:00");
            dbClient.Verify(c => c.CommitDustImportAsync(It.Is<MyAtmDustImportCommit>(commit =>
                commit.Monitor.SerialId == "11111" &&
                commit.Measurements.Count > 0 &&
                TestUtil.VerifyDustDto(commit.Measurements[0], "11111") &&
                commit.Watermark == expectedDateTime), It.IsAny<CancellationToken>()), Times.Once);
            dbClient.Verify(c => c.ReadRules("11111", Period.Minutes1), Times.Exactly(1));
            dbClient.Verify(c => c.CommitDustImportAsync(It.Is<MyAtmDustImportCommit>(commit =>
                commit.Monitor.SerialId == "22222" &&
                commit.Measurements.Count > 0 &&
                TestUtil.VerifyDustDto(commit.Measurements[0], "22222") &&
                commit.Watermark == expectedDateTime), It.IsAny<CancellationToken>()), Times.Once);
            dbClient.Verify(c => c.ReadRules("22222", Period.Minutes1), Times.Exactly(1));
            //foreach(var m in monitors)
            //{
            //    dbClient.Verify(c => c.SetMonitorOffline(m.Id, false), Times.Exactly(1));
            //}
            dbClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();

        }

        [TestMethod]
        public async Task TestStoreDustLevels_ImportsEveryMeasurementPageAndAdvancesFinalWatermark()
        {
            Mock<IHttpClient> httpClient = new();
            Mock<IDBClient> dbClient = new();
            Mock<IMqttClient> mqttClient = new();
            Mock<INotificationDeliveryService> messageClient = new();
            MyAtmMonitorOptions options = new()
            {
                MeasurementPageSize = 2,
                MaxPagesPerMonitorPerRun = 2
            };
            MyAtmApi testObj = new(httpClient.Object, dbClient.Object, mqttClient.Object, messageClient.Object, false, options);
            int customerId = 656;
            DateTime startTime = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            string firstPage = MyAtmFixture.MeasurementsResponseJson(2, startTime);
            string secondPage = MyAtmFixture.MeasurementsResponseJson(1, startTime.AddMinutes(2));
            string firstRequest = "/api/customers/656/devices/11111/measurements" + TestUtil.MEASUREMENT_SELECT +
                               "&$filter=timestamp gt 1970-01-01T00:00:00.0000000Z&$orderby=timestamp asc&$top=2";
            string secondRequest = "/api/customers/656/devices/11111/measurements" + TestUtil.MEASUREMENT_SELECT +
                                "&$filter=timestamp gt 2024-01-01T00:01:00.0000000Z&$orderby=timestamp asc&$top=2";

            httpClient.SetupSequence(client => client.GetAsync(It.IsRegex("/api/customers/656/devices/11111/measurements"), TestContext.CancellationToken))
                .ReturnsAsync(firstPage)
                .ReturnsAsync(secondPage);
            dbClient.Setup(client => client.ReadMonitorList(customerId, null))
                .Returns(MyAtmFixture.CustomerDeviceDtos(null, singleItem: true));
            dbClient.Setup(client => client.ReadRules(It.IsAny<string>(), It.IsAny<Period>()))
                .Returns([]);
            dbClient.Setup(client => client.CommitDustImportAsync(It.IsAny<MyAtmDustImportCommit>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DustImportCommitResult(Array.Empty<MonitorDeliveryRequest>()));
            dbClient.Setup(client => client.ClaimNextDueAsync(
                    MonitorDeliveryProducers.MyAtm,
                    It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MonitorDeliveryMessage?)null);

            await testObj.StoreDustLevelsAsync<DeviceMeasurement>(customerId, Period.Minutes1, TestContext.CancellationToken);

            httpClient.Verify(client => client.GetAsync(firstRequest, It.IsAny<CancellationToken>()), Times.Once);
            httpClient.Verify(client => client.GetAsync(secondRequest, It.IsAny<CancellationToken>()), Times.Once);
            dbClient.Verify(client => client.CommitDustImportAsync(It.Is<MyAtmDustImportCommit>(commit =>
                commit.Measurements.Count == 2 && commit.Watermark == startTime.AddMinutes(1)), It.IsAny<CancellationToken>()), Times.Once);
            dbClient.Verify(client => client.CommitDustImportAsync(It.Is<MyAtmDustImportCommit>(commit =>
                commit.Measurements.Count == 1 && commit.Watermark == startTime.AddMinutes(2)), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task TestStoreDustLevels_SuccessfulCommitDoesNotDispatchOutbox()
        {
            Mock<IHttpClient> httpClient = new();
            Mock<IDBClient> dbClient = new();
            Mock<IMqttClient> mqttClient = new();
            Mock<INotificationDeliveryService> messageClient = new();
            MyAtmMonitorOptions options = new()
            {
                OutboxBatchSize = 2
            };
            MyAtmApi testObj = new(
                httpClient.Object,
                dbClient.Object,
                mqttClient.Object,
                messageClient.Object,
                testLocal: false,
                options);
            httpClient.Setup(client => client.GetAsync(
                    It.IsRegex("/api/customers/656/devices/11111/measurements"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(MyAtmFixture.MeasurementsResponseJson(Period.Minutes1));
            dbClient.Setup(client => client.ReadMonitorList(656, null))
                .Returns(MyAtmFixture.CustomerDeviceDtos(null, singleItem: true));
            dbClient.Setup(client => client.ReadRules("11111", Period.Minutes1))
                .Returns([]);
            dbClient.Setup(client => client.CommitDustImportAsync(
                    It.IsAny<MyAtmDustImportCommit>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DustImportCommitResult(Array.Empty<MonitorDeliveryRequest>()));

            await testObj.StoreDustLevelsAsync<DeviceMeasurement>(656, Period.Minutes1, TestContext.CancellationToken);

            dbClient.Verify(client => client.ClaimNextDueAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()), Times.Never);
            dbClient.Verify(client => client.CompleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                null,
                It.IsAny<CancellationToken>()), Times.Never);
            mqttClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreDustLevels_FailedCommitDoesNotDispatchOutbox()
        {
            MyAtmApi testObj = TestUtil.CreateApiAndMocks(
                out Mock<IHttpClient> httpClient,
                out Mock<IDBClient> dbClient,
                out Mock<IMqttClient> mqttClient,
                out Mock<INotificationDeliveryService> messageClient);
            httpClient.Setup(client => client.GetAsync(
                    It.IsRegex("/api/customers/656/devices/11111/measurements"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(MyAtmFixture.MeasurementsResponseJson(Period.Minutes1));
            dbClient.Setup(client => client.ReadMonitorList(656, null))
                .Returns(MyAtmFixture.CustomerDeviceDtos(null, singleItem: true));
            dbClient.Setup(client => client.ReadRules("11111", Period.Minutes1))
                .Returns([]);
            dbClient.Setup(client => client.CommitDustImportAsync(
                    It.IsAny<MyAtmDustImportCommit>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("commit failed"));

            await Assert.ThrowsExactlyAsync<MyAtmJobAggregateException>(
                () => testObj.StoreDustLevelsAsync<DeviceMeasurement>(656, Period.Minutes1, TestContext.CancellationToken));

            dbClient.Verify(client => client.ClaimNextDueAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()), Times.Never);
            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow(Period.Minutes15, "/15min")]
        [DataRow(Period.Hours1, "/hourly")]
        [DataRow(Period.Hours24, "/daily")]
        public async Task TestStoreAverageDustLevels_Success(Period period, string urlSuffix)
        {
            MyAtmApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<INotificationDeliveryService> messageClient);
            int customerId = 656;
            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/api\\/customers\\/" + customerId + "\\/devices\\/.*\\/measurements"), TestContext.CancellationToken)).Returns(Task<string>.Factory.StartNew(() => MyAtmFixture.MeasurementsResponseJson(period), TestContext.CancellationToken));

            List<DustMonitorDto> monitors = MyAtmFixture.CustomerDeviceDtos(null);
            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<int>(), null)).Returns(monitors);
            DateTime dt = DateTime.Parse("2023-09-25T10:29:00");
            await testObj.StoreDustLevelsAsync<AvgDeviceMeasurement>(customerId, period, TestContext.CancellationToken);

            httpClient.Verify(c => c.GetAsync(It.IsRegex(TestUtil.MeasurementPageRequestPattern(656, "11111", urlSuffix)), TestContext.CancellationToken), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync(It.IsRegex(TestUtil.MeasurementPageRequestPattern(656, "22222", urlSuffix)), TestContext.CancellationToken), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(It.IsAny<int>(), null), Times.Exactly(1));
            DateTime expectedDateTime = DateTime.Parse("2023-10-04T16:00:00");
            dbClient.Verify(c => c.CommitDustImportAsync(It.Is<MyAtmDustImportCommit>(commit =>
                commit.Monitor.SerialId == "11111" &&
                commit.Measurements.Count > 0 &&
                TestUtil.VerifyDustDto(commit.Measurements[0], "11111") &&
                commit.Watermark == expectedDateTime &&
                commit.Period == period), It.IsAny<CancellationToken>()), Times.Once);
            dbClient.Verify(c => c.CommitDustImportAsync(It.Is<MyAtmDustImportCommit>(commit =>
                commit.Monitor.SerialId == "22222" &&
                commit.Measurements.Count > 0 &&
                TestUtil.VerifyDustDto(commit.Measurements[0], "22222") &&
                commit.Watermark == expectedDateTime &&
                commit.Period == period), It.IsAny<CancellationToken>()), Times.Once);
            switch (period)
            {
                case Period.Minutes1:
                    dbClient.Verify(c => c.ReadRules("11111", Period.Minutes1), Times.Exactly(1));
                    dbClient.Verify(c => c.ReadRules("22222", Period.Minutes1), Times.Exactly(1));
                    break;
                case Period.Minutes15:
                    dbClient.Verify(c => c.ReadRules("11111", Period.Minutes15), Times.Exactly(1));
                    dbClient.Verify(c => c.ReadRules("22222", Period.Minutes15), Times.Exactly(1));
                    break;
                case Period.Hours1:
                    dbClient.Verify(c => c.ReadRules("11111", Period.Hours1), Times.Exactly(1));
                    dbClient.Verify(c => c.ReadRules("22222", Period.Hours1), Times.Exactly(1));
                    break;
                case Period.Hours8:
                    break;
                case Period.Hours24:
                    dbClient.Verify(c => c.ReadRules("11111", Period.Hours24), Times.Exactly(1));
                    dbClient.Verify(c => c.ReadRules("22222", Period.Hours24), Times.Exactly(1));
                    break;
                default:
                    break;
            }

            //foreach (var m in monitors)
            //{
            //    dbClient.Verify(c => c.SetMonitorOffline(m.Id, false), Times.Exactly(1));
            //}
            dbClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();

        }

        [TestMethod]
        public async Task TestStoreDustLevels_TruncatedByTimestamp_Success()
        {
            MyAtmApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                     out Mock<IDBClient> dbClient,
                                                     out Mock<IMqttClient> mqttClient,
                                                     out Mock<INotificationDeliveryService> messageClient);

            int customerId = 656;
            httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/api\\/customers\\/" + customerId + "\\/devices\\/.*\\/measurements" + Regex.Escape(TestUtil.MEASUREMENT_SELECT)), TestContext.CancellationToken)).
                                 Returns(Task<string>.Factory.StartNew(() => MyAtmFixture.MeasurementsResponseJson(Period.Minutes1), TestContext.CancellationToken));

            DateTime expectedDateTime = DateTime.UtcNow;
            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<int>(), null)).
                    Returns(MyAtmFixture.CustomerDeviceDtos(expectedDateTime));
            dbClient.Setup(c => c.ReadRules(It.IsAny<string>())).
                    Returns([]);

            await testObj.StoreDustLevelsAsync<DeviceMeasurement>(customerId, Period.Minutes1, TestContext.CancellationToken);

            httpClient.Verify(c => c.GetAsync(It.IsRegex(TestUtil.MeasurementPageRequestPattern(656, "11111", "", TestUtil.MEASUREMENT_SELECT)), TestContext.CancellationToken), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync(It.IsRegex(TestUtil.MeasurementPageRequestPattern(656, "22222", "", TestUtil.MEASUREMENT_SELECT)), TestContext.CancellationToken), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(It.IsAny<int>(), null), Times.Exactly(1));
            dbClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }


        private static MonitorDeliveryMessage CreateDataInsertedDelivery()
        {
            MonitorDeliveryPayloadV1 payload = new(
                Guid.Empty,
                new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc),
                "11111",
                656,
                "fleet-1",
                Rvt.Monitor.Common.Notifications.AlertType.Alert,
                "pm10",
                42);
            return new MonitorDeliveryMessage(
                Guid.NewGuid(),
                MonitorDeliveryProducers.MyAtm,
                null,
                null,
                $"delivery-{Guid.NewGuid():N}",
                MonitorDeliveryKind.MqttDataInserted,
                string.Empty,
                1,
                JsonSerializer.Serialize(payload),
                1,
                Guid.NewGuid());
        }

        public TestContext TestContext { get; set; } = null!;
    }
}
