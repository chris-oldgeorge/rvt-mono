using Moq;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Mqtt;
using Svantek.Api;
using Svantek.Api.Db;
using Svantek.Api.Http;
using SvantekMonitor.model.dto;

namespace SvantekMonitorTests;

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
        Mock<IHttpClient> httpClient = new();
        Mock<IDBClient> dbClient = new();
        Mock<IMqttClient> mqttClient = new();
        Mock<IMessageService> emailClient = new();
        RecordingObjectStorageClient storage = new();
        SvantekApi testObj = new(
            httpClient.Object,
            dbClient.Object,
            mqttClient.Object,
            emailClient.Object,
            "test-api-key",
            TestObjectStorageFactory.ForSoundRecordings(storage),
            testLocal: false);

        DateTime notificationTime = new(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        // Two unresolved alerts on the same monitor and day - within one run they must share a single fetch.
        List<NoiseNotificationLatest> alerts =
        [
            new(Guid.NewGuid(), Guid.NewGuid(), "F1", "12345", 7, 3, notificationTime, 900),
            new(Guid.NewGuid(), Guid.NewGuid(), "F1", "12345", 7, 3, notificationTime.AddHours(1), 900)
        ];
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
        await testObj.CheckForSoundRecordingsAsync(TestContext.CancellationToken);
        await testObj.CheckForSoundRecordingsAsync(TestContext.CancellationToken);

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

    public TestContext TestContext { get; set; } = null!;
}
