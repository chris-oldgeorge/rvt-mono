using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Omnidots.Api.Db;
using Omnidots.Api.Http;
using Omnidots.Api.UseCases;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using AlertActivityTimeDto = Rvt.Monitor.Common.Notifications.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Notifications.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Notifications.RvtContactDto;
namespace OmnidotsAdapterTests
{


    [TestClass]
    public class TestOmnidotsApiException
    {
        public TestOmnidotsApiException()
        {
            var factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestOmnidotsApiException");
        }

        [TestMethod]
        public async Task TestAuthenticate_BadJson_ThrowsCorrectException()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                    out Mock<IDBClient> dbClient,
                                                    out Mock<IMqttClient> mqttClient,
                                                    out Mock<IMessageService> messageClient);
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)), It.IsAny<CancellationToken>())).
                Returns(OmnidotsFixture.StringTask("blah"));

            var exception = await Assert.ThrowsExactlyAsync<AdapterException>(async () =>
            {
                await testObj.AuthenticateAsync();
            });

            Assert.AreEqual("Failed ! Invalid ErrorResponse", exception.Message);
            Assert.IsInstanceOfType(exception.InnerException, typeof(JsonException));

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate",
                It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
            messageClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestAuthenticate_ErrorJson_ThrowsCorrectException()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageClient);

            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)), It.IsAny<CancellationToken>())).
                Returns(OmnidotsFixture.StringTask(OmnidotsFixture.ErrorJson()));

            var exception = await Assert.ThrowsExactlyAsync<AdapterException>(async () =>
            {
                await testObj.AuthenticateAsync();
            });
            Assert.AreEqual("Failed ! error message='Some error message.'", exception.Message);

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate",
             It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreMonitors_BadJson_ThrowsCorrectException()
        {

            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageClient);

            var token = "XXX";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)), It.IsAny<CancellationToken>())).
            Returns(OmnidotsFixture.AuthenticateTask(token));

            var measuringPointsUrl = string.Format("/api/v1/list_measuring_points?token={0}", token);
            httpClient.Setup(c => c.GetAsync(measuringPointsUrl, It.IsAny<CancellationToken>())).
                Returns(OmnidotsFixture.StringTask("bang"));

            var exception = await Assert.ThrowsExactlyAsync<AdapterException>(async () =>
            {
                await testObj.StoreMonitorsAsync();
            });
            Assert.AreEqual("Failed ! Invalid ErrorResponse", exception.Message);
            Assert.IsInstanceOfType(exception.InnerException, typeof(JsonException));

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate",
                It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync(measuringPointsUrl, It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStoreMonitors_ErrorJson_ThrowsCorrectException()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageClient);

            var token = "XXX";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate",
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)), It.IsAny<CancellationToken>())).
            Returns(OmnidotsFixture.AuthenticateTask(token));

            var measuringPointsUrl = string.Format("/api/v1/list_measuring_points?token={0}", token);
            httpClient.Setup(c => c.GetAsync(measuringPointsUrl, It.IsAny<CancellationToken>())).
                Returns(OmnidotsFixture.StringTask(OmnidotsFixture.ErrorJson()));

            var exception = await Assert.ThrowsExactlyAsync<AdapterException>(async () =>
            {

                await testObj.StoreMonitorsAsync();
            });
            Assert.AreEqual("Failed ! error message='Some error message.'", exception.Message);

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate",
                It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.Verify(c => c.GetAsync(measuringPointsUrl, It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.VerifyNoOtherCalls();

            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStorePeakRecords_BadJson_ThrowsCorrectException()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageClient);

            var token = "hghjadg";
            var authUrl = "/api/v1/user/authenticate";
            httpClient.Setup(c => c.PostAsync(authUrl,
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)), It.IsAny<CancellationToken>())).
                    Returns(OmnidotsFixture.AuthenticateTask(token));

            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(OmnidotsFixture.MonitorsList(2));

            var peakRecordsUrl = string.Format("/api/v1/get_peak_records?token={0}", token);
            httpClient.Setup(c => c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl)), It.IsAny<CancellationToken>())).
                Returns(OmnidotsFixture.StringTask("Blahh"));


            var exception = await Assert.ThrowsExactlyAsync<OmnidotsImportException>(() => testObj.StorePeakRecordsAsync(OmnidotsFixture.MINUTES_ELAPSED));
            Assert.AreEqual("StorePeakRecords", exception.Operation);
            CollectionAssert.AreEqual(new[] { "1", "2" }, exception.Failures.Select(failure => failure.SerialId).ToArray());

            httpClient.Verify(c => c.PostAsync(authUrl, It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.Verify(c =>
                c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl)), It.IsAny<CancellationToken>()), Times.Exactly(2));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));

            //"StorePeakRecords serialId={}"
            dbClient.Verify(c => c.HandleException("StorePeakRecords serialId=1", It.IsAny<AdapterException>()),
                Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StorePeakRecords serialId=2", It.IsAny<AdapterException>()),
                Times.Exactly(1));
            var cursorQueries = dbClient.As<IOmnidotsImportCursorQueries>();
            cursorQueries.Verify(
                c => c.ReadImportCursor(It.IsAny<string>(), OmnidotsMeasurementSeries.Peak), Times.Exactly(2));
            cursorQueries.Verify(
                c => c.ReadLatestMeasurementTime(It.IsAny<string>(), OmnidotsMeasurementSeries.Peak), Times.Exactly(2));
            //dbClient.Verify(c => c.ReadSiteTimes(It.IsAny<Guid>()), Times.Exactly(2));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task TestStorePeakRecords_ErrorJson_ThrowsCorrectException()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                  out Mock<IMessageService> messageClient);

            var token = "hghjadg";
            var authUrl = "/api/v1/user/authenticate";
            httpClient.Setup(c => c.PostAsync(authUrl,
                It.Is<HttpContent>(c => TestUtil.VerifyAuthenticateForm(c)), It.IsAny<CancellationToken>())).
                    Returns(OmnidotsFixture.AuthenticateTask(token));

            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(OmnidotsFixture.MonitorsList(2));

            var peakRecordsUrl = string.Format("/api/v1/get_peak_records?token={0}", token);
            httpClient.Setup(c => c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl)), It.IsAny<CancellationToken>())).
                Returns(OmnidotsFixture.StringTask(OmnidotsFixture.ErrorJson()));


            var exception = await Assert.ThrowsExactlyAsync<OmnidotsImportException>(() => testObj.StorePeakRecordsAsync(OmnidotsFixture.MINUTES_ELAPSED));
            Assert.AreEqual("StorePeakRecords", exception.Operation);
            CollectionAssert.AreEqual(new[] { "1", "2" }, exception.Failures.Select(failure => failure.SerialId).ToArray());

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate",
             It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            httpClient.Verify(c =>
                c.GetAsync(It.Is<string>(s => s.StartsWith(peakRecordsUrl)), It.IsAny<CancellationToken>()), Times.Exactly(2));
            httpClient.VerifyNoOtherCalls();

            dbClient.Verify(c => c.ReadMonitorList(null), Times.Exactly(1));

            dbClient.Verify(c => c.HandleException("StorePeakRecords serialId=1", It.IsAny<AdapterException>()),
                Times.Exactly(1));
            dbClient.Verify(c => c.HandleException("StorePeakRecords serialId=2", It.IsAny<AdapterException>()),
                Times.Exactly(1));
            var cursorQueries = dbClient.As<IOmnidotsImportCursorQueries>();
            cursorQueries.Verify(
                c => c.ReadImportCursor(It.IsAny<string>(), OmnidotsMeasurementSeries.Peak), Times.Exactly(2));
            cursorQueries.Verify(
                c => c.ReadLatestMeasurementTime(It.IsAny<string>(), OmnidotsMeasurementSeries.Peak), Times.Exactly(2));
            //dbClient.Verify(c => c.ReadSiteTimes(It.IsAny<Guid>()), Times.Exactly(2));
            dbClient.VerifyNoOtherCalls();

            mqttClient.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task StorePeakRecords_FirstMonitorFails_AttemptsSecondAndThrowsAggregateFailure()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageClient);
            var token = "peak-token";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.AuthenticateTask(token));
            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(OmnidotsFixture.MonitorsList(2));
            httpClient.Setup(c => c.GetAsync(It.Is<string>(url =>
                    url.StartsWith("/api/v1/get_peak_records", StringComparison.Ordinal) &&
                    url.Contains("measuring_point_id=1", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.StringTask("invalid-json"));
            httpClient.Setup(c => c.GetAsync(It.Is<string>(url =>
                    url.StartsWith("/api/v1/get_peak_records", StringComparison.Ordinal) &&
                    url.Contains("measuring_point_id=2", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.StringTask("{\"ok\":true,\"samples\":[]}"));

            await AssertAggregateFailure(
                () => testObj.StorePeakRecordsAsync(OmnidotsFixture.MINUTES_ELAPSED),
                "StorePeakRecords",
                "1");

            httpClient.Verify(c => c.GetAsync(It.Is<string>(url =>
                url.StartsWith("/api/v1/get_peak_records", StringComparison.Ordinal) &&
                url.Contains("measuring_point_id=2", StringComparison.Ordinal)), It.IsAny<CancellationToken>()), Times.Once);
            dbClient.Verify(c => c.HandleException("StorePeakRecords serialId=1", It.IsAny<AdapterException>()), Times.Once);
            dbClient.Verify(c => c.HandleException("StorePeakRecords serialId=2", It.IsAny<Exception>()), Times.Never);
        }

        [TestMethod]
        public async Task StoreVeffRecords_FirstMonitorFails_AttemptsSecondAndThrowsAggregateFailure()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageClient);
            var token = "veff-token";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.AuthenticateTask(token));
            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(OmnidotsFixture.MonitorsList(2));
            httpClient.Setup(c => c.GetAsync(It.Is<string>(url =>
                    url.StartsWith("/api/v1/get_veff_records", StringComparison.Ordinal) &&
                    url.Contains("measuring_point_id=1", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.StringTask("invalid-json"));
            httpClient.Setup(c => c.GetAsync(It.Is<string>(url =>
                    url.StartsWith("/api/v1/get_veff_records", StringComparison.Ordinal) &&
                    url.Contains("measuring_point_id=2", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.StringTask("{\"ok\":true,\"samples\":[]}"));

            await AssertAggregateFailure(
                () => testObj.StoreVeffRecordsAsync(TimeSpan.FromHours(2)),
                "StoreVeffRecords",
                "1");

            httpClient.Verify(c => c.GetAsync(It.Is<string>(url =>
                url.StartsWith("/api/v1/get_veff_records", StringComparison.Ordinal) &&
                url.Contains("measuring_point_id=2", StringComparison.Ordinal)), It.IsAny<CancellationToken>()), Times.Once);
            dbClient.Verify(c => c.HandleException("StoreVeffRecords serialId=1", It.IsAny<AdapterException>()), Times.Once);
            dbClient.Verify(c => c.HandleException("StoreVeffRecords serialId=2", It.IsAny<Exception>()), Times.Never);
        }

        [TestMethod]
        public async Task StoreVdvRecords_FirstMonitorFails_AttemptsSecondAndThrowsAggregateFailure()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageClient);
            var token = "vdv-token";
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.AuthenticateTask(token));
            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(OmnidotsFixture.MonitorsList(2));
            httpClient.Setup(c => c.GetAsync(It.Is<string>(url =>
                    url.StartsWith("/api/v1/get_vdv_records", StringComparison.Ordinal) &&
                    url.Contains("measuring_point_id=1", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.StringTask("invalid-json"));
            httpClient.Setup(c => c.GetAsync(It.Is<string>(url =>
                    url.StartsWith("/api/v1/get_vdv_records", StringComparison.Ordinal) &&
                    url.Contains("measuring_point_id=2", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.StringTask("{\"ok\":true,\"samples\":[]}"));

            await AssertAggregateFailure(
                () => testObj.StoreVdvRecordsAsync(TimeSpan.FromHours(2)),
                "StoreVdvRecords",
                "1");

            httpClient.Verify(c => c.GetAsync(It.Is<string>(url =>
                url.StartsWith("/api/v1/get_vdv_records", StringComparison.Ordinal) &&
                url.Contains("measuring_point_id=2", StringComparison.Ordinal)), It.IsAny<CancellationToken>()), Times.Once);
            dbClient.Verify(c => c.HandleException("StoreVdvRecords serialId=1", It.IsAny<AdapterException>()), Times.Once);
            dbClient.Verify(c => c.HandleException("StoreVdvRecords serialId=2", It.IsAny<Exception>()), Times.Never);
        }

        [TestMethod]
        public async Task StoreTraces_FirstMonitorFails_AttemptsSecondAndThrowsAggregateFailure()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageClient);
            var monitors = OmnidotsFixture.MonitorsList(1, serialIdIn: 23422);
            monitors.Add(OmnidotsFixture.MonitorsList(1, serialIdIn: 23422).Single());
            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<DateTime?>())).Returns(monitors);
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.AuthenticateTask("trace-token"));
            httpClient.SetupSequence(c => c.GetAsync(It.Is<string>(url =>
                    url.StartsWith("/api/v1/get_traces_list", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.StringTask("invalid-json"))
                .Returns(OmnidotsFixture.StringTask("{\"ok\":true,\"traces\":[]}"));

            await AssertAggregateFailure(
                () => testObj.StoreTracesAsync(DateTime.UtcNow.AddMinutes(-5)),
                "StoreTraces",
                "23423");

            httpClient.Verify(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Once);
            httpClient.Verify(c => c.GetAsync(It.Is<string>(url =>
                url.StartsWith("/api/v1/get_traces_list", StringComparison.Ordinal)), It.IsAny<CancellationToken>()), Times.Exactly(2));
            dbClient.Verify(c => c.HandleException("Failed to read traces for serialId=23423", It.IsAny<AdapterException>()), Times.Once);
        }

        [TestMethod]
        [DataRow("StorePeakRecords", "/api/v1/get_peak_records")]
        [DataRow("StoreVeffRecords", "/api/v1/get_veff_records")]
        [DataRow("StoreVdvRecords", "/api/v1/get_vdv_records")]
        public async Task Import_WhenRecordingFailureThrows_AttemptsSecondAndPreservesBothFailures(
            string operation,
            string endpoint)
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageClient);
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.AuthenticateTask("recording-token"));
            dbClient.Setup(c => c.ReadMonitorList(null)).Returns(OmnidotsFixture.MonitorsList(2));
            httpClient.Setup(c => c.GetAsync(It.Is<string>(url =>
                    url.StartsWith(endpoint, StringComparison.Ordinal) &&
                    url.Contains("measuring_point_id=1", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.StringTask("invalid-json"));
            httpClient.Setup(c => c.GetAsync(It.Is<string>(url =>
                    url.StartsWith(endpoint, StringComparison.Ordinal) &&
                    url.Contains("measuring_point_id=2", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.StringTask("{\"ok\":true,\"samples\":[]}"));
            var recordingException = new InvalidOperationException("database-password=secret-value");
            dbClient.Setup(c => c.HandleException($"{operation} serialId=1", It.IsAny<AdapterException>()))
                .Throws(recordingException);

            Func<Task> import = operation switch
            {
                "StorePeakRecords" => () => testObj.StorePeakRecordsAsync(OmnidotsFixture.MINUTES_ELAPSED),
                "StoreVeffRecords" => () => testObj.StoreVeffRecordsAsync(TimeSpan.FromHours(2)),
                "StoreVdvRecords" => () => testObj.StoreVdvRecordsAsync(TimeSpan.FromHours(2)),
                _ => throw new AssertFailedException($"Unexpected operation '{operation}'.")
            };

            var exception = await Assert.ThrowsExactlyAsync<OmnidotsImportException>(import);

            AssertRecordingFailureContext(exception, operation, "1", recordingException);
            httpClient.Verify(c => c.GetAsync(It.Is<string>(url =>
                url.StartsWith(endpoint, StringComparison.Ordinal) &&
                url.Contains("measuring_point_id=2", StringComparison.Ordinal)), It.IsAny<CancellationToken>()), Times.Once);
            dbClient.Verify(c => c.HandleException($"{operation} serialId=1", It.IsAny<AdapterException>()), Times.Once);
        }

        [TestMethod]
        public async Task StoreTraces_WhenRecordingFailureThrows_AttemptsSecondAndPreservesBothFailures()
        {
            var testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IMessageService> messageClient);
            var monitors = OmnidotsFixture.MonitorsList(1, serialIdIn: 23422);
            monitors.Add(OmnidotsFixture.MonitorsList(1, serialIdIn: 23422).Single());
            dbClient.Setup(c => c.ReadMonitorList(It.IsAny<DateTime?>())).Returns(monitors);
            httpClient.Setup(c => c.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.AuthenticateTask("trace-recording-token"));
            httpClient.SetupSequence(c => c.GetAsync(It.Is<string>(url =>
                    url.StartsWith("/api/v1/get_traces_list", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
                .Returns(OmnidotsFixture.StringTask("invalid-json"))
                .Returns(OmnidotsFixture.StringTask("{\"ok\":true,\"traces\":[]}"));
            var recordingException = new InvalidOperationException("database-password=secret-value");
            dbClient.Setup(c => c.HandleException("Failed to read traces for serialId=23423", It.IsAny<AdapterException>()))
                .Throws(recordingException);

            var exception = await Assert.ThrowsExactlyAsync<OmnidotsImportException>(() => testObj.StoreTracesAsync(DateTime.UtcNow.AddMinutes(-5)));

            AssertRecordingFailureContext(exception, "StoreTraces", "23423", recordingException);
            httpClient.Verify(c => c.GetAsync(It.Is<string>(url =>
                url.StartsWith("/api/v1/get_traces_list", StringComparison.Ordinal)), It.IsAny<CancellationToken>()), Times.Exactly(2));
            dbClient.Verify(c => c.HandleException(
                "Failed to read traces for serialId=23423",
                It.IsAny<AdapterException>()), Times.Once);
        }

        private static async Task AssertAggregateFailure(Func<Task> action, string operation, string failedSerialId)
        {
            var exception = await Assert.ThrowsExactlyAsync<OmnidotsImportException>(action);
            Assert.AreEqual(operation, exception.Operation);
            CollectionAssert.AreEqual(
                new[] { failedSerialId },
                exception.Failures.Select(failure => failure.SerialId).ToArray());
        }

        private static void AssertRecordingFailureContext(
            OmnidotsImportException exception,
            string operation,
            string failedSerialId,
            Exception recordingException)
        {
            Assert.AreEqual(operation, exception.Operation);
            Assert.HasCount(1, exception.Failures);
            Assert.AreEqual(failedSerialId, exception.Failures[0].SerialId);
            Assert.IsFalse(exception.Message.Contains("secret-value", StringComparison.Ordinal));
            Assert.IsFalse(exception.ToString().Contains("secret-value", StringComparison.Ordinal));
            Assert.IsNull(exception.InnerException);

            var failure = exception.Failures[0];
            Assert.IsInstanceOfType<AdapterException>(failure.Exception);
            Assert.AreSame(recordingException, failure.RecordingException);
        }

    }
}
