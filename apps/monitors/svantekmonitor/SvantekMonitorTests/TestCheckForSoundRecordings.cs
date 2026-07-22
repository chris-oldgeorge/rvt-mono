using Moq;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Mqtt;
using Svantek.Api;
using Svantek.Api.Db;
using Svantek.Api.Http;
using SvantekMonitor.model.dto;

namespace SvantekMonitorTests
{
    // Summary: Regression tests for the sound-recording check, in particular the per-run file-list cache.
    // Major updates:
    // - 2026-07-13: Added after the singleton conversion; each run must re-fetch the vendor file list.
    [TestClass]
    public class TestCheckForSoundRecordings
    {
        private const string EmptyFilesResponse = "{\"status\":\"ok\",\"files\":[],\"files_size\":0}";

        [TestMethod]
        public async Task TestCheckForSoundRecordings_FileListRefetchedEachRun_CachedWithinRun()
        {
            var httpClient = new Mock<IHttpClient>();
            var dbClient = new Mock<IDBClient>();
            var mqttClient = new Mock<IMqttClient>();
            var emailClient = new Mock<IMessageService>();
            var storage = new RecordingBlobStorageService();
            var testObj = new SvantekApi(
                httpClient.Object,
                dbClient.Object,
                mqttClient.Object,
                emailClient.Object,
                "test-api-key",
                storage,
                testLocal: false);

            var notificationTime = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
            // Two unresolved alerts on the same monitor and day - within one run they must share a single fetch.
            var alerts = new List<NoiseNotificationLatest>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "F1", "12345", 7, 3, notificationTime, 900),
                new(Guid.NewGuid(), Guid.NewGuid(), "F1", "12345", 7, 3, notificationTime.AddHours(1), 900)
            };
            dbClient.Setup(c => c.ReadLatestNotificationAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(alerts);
            httpClient.Setup(c => c.PostAsync(
                    It.IsAny<string>(),
                    It.IsAny<HttpContent>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(EmptyFilesResponse);

            // Two consecutive runs on the same (singleton) api instance: the file list must be fetched
            // once per run - not served from a process-lifetime cache - or recordings uploaded to the
            // vendor between runs would never be found.
            await testObj.CheckForSoundRecordingsAsync();
            await testObj.CheckForSoundRecordingsAsync();

            httpClient.Verify(c => c.PostAsync(
                It.IsAny<string>(),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
            dbClient.Verify(
                c => c.ReadLatestNotificationAsync(It.IsAny<CancellationToken>()),
                Times.Exactly(2));
            dbClient.Verify(
                c => c.WriteSoundFileAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            Assert.IsEmpty(storage.Writes);
        }
    }
}
