using Moq;
using Rvt.Monitor.Common.Storage;
using Svantek.Api;
using Svantek.Api.Db;
using Svantek.Api.Http;
using Svantek.Api.UseCases;
using SvantekMonitor.model.dto;

namespace SvantekMonitorTests;

[TestClass]
public sealed class TestCheckForSoundRecordingStorage
{
    [TestMethod]
    public async Task RunAsync_MatchingWav_DownloadsAndWritesThroughStoragePort()
    {
        var notificationId = Guid.Parse("4cb38822-3497-4650-bac0-82da974c1d28");
        var notificationTime = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        var soundContent = new byte[] { 82, 73, 70, 70, 1, 2, 3, 4 };
        var filesResponse = """
            {
              "status": "ok",
              "files": [
                ["20260713_09_59_30.WAV", 3, "20260713", 2048, "SV307", "station-123", "2026-07-13 10:00:00", 0, 1]
              ],
              "files_size": 1
            }
            """;

        var httpClient = new Mock<IHttpClient>();
        var dbClient = new Mock<IDBClient>();
        var storage = new RecordingBlobStorageService();
        using var cancellation = new CancellationTokenSource();

        dbClient.Setup(client => client.ReadLatestNotificationAsync(cancellation.Token)).ReturnsAsync(
            new List<NoiseNotificationLatest>
            {
                new(notificationId, Guid.NewGuid(), "F1", "12345", 7, 3, notificationTime, 900)
            });
        dbClient.Setup(client => client.WriteSoundFileAsync(
                notificationId,
                $"{notificationId}.wav",
                cancellation.Token))
            .ReturnsAsync(true);
        httpClient.Setup(client => client.PostAsync(
                It.IsAny<string>(),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(filesResponse);
        httpClient.Setup(client => client.GetByteArrayAsync(
                "projects-get-data.php",
                It.IsAny<MultipartFormDataContent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(soundContent);

        var handler = new CheckForSoundRecordingsHandler(
            dbClient.Object,
            dbClient.Object,
            new SvantekHttpGateway(httpClient.Object, "test-api-key"),
            storage);

        await handler.RunAsync(cancellation.Token);

        httpClient.Verify(client => client.GetByteArrayAsync(
            "projects-get-data.php",
            It.IsAny<MultipartFormDataContent>(),
            cancellation.Token), Times.Once);
        Assert.HasCount(1, storage.Writes);
        Assert.AreEqual($"{notificationId}.wav", storage.Writes[0].Request.ObjectName);
        CollectionAssert.AreEqual(soundContent, storage.Writes[0].Request.Content);
        Assert.AreEqual("audio/wav", storage.Writes[0].Request.ContentType);
        Assert.AreEqual(cancellation.Token, storage.Writes[0].CancellationToken);
        dbClient.Verify(client => client.WriteSoundFileAsync(
            notificationId,
            $"{notificationId}.wav",
            cancellation.Token), Times.Once);
    }

    [TestMethod]
    public async Task RunAsync_EmptyVendorRow_RecordsCompactNotificationIdentifier_AndThrowsAggregate()
    {
        var notificationId = Guid.Parse("4cb38822-3497-4650-bac0-82da974c1d28");
        var notificationTime = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        var identifier = $"sound:{notificationId}";
        const string filesResponse = """
            {"status":"ok","files":[[]],"files_size":1}
            """;
        var httpClient = new Mock<IHttpClient>(MockBehavior.Strict);
        var dbClient = new Mock<IDBClient>(MockBehavior.Strict);
        var storage = new RecordingBlobStorageService();
        dbClient.Setup(client => client.ReadLatestNotificationAsync(CancellationToken.None)).ReturnsAsync(
            new List<NoiseNotificationLatest>
            {
                new(notificationId, Guid.NewGuid(), "F1", "12345", 7, 3, notificationTime, 900)
            });
        dbClient.Setup(client => client.HandleException(
            identifier,
            It.IsAny<InvalidDataException>()));
        httpClient.Setup(client => client.PostAsync(
                "projects-get-data.php",
                It.IsAny<HttpContent>(),
                CancellationToken.None))
            .ReturnsAsync(filesResponse);
        var handler = new CheckForSoundRecordingsHandler(
            dbClient.Object,
            dbClient.Object,
            new SvantekHttpGateway(httpClient.Object, "test-api-key"),
            storage);

        var aggregate = await Assert.ThrowsExactlyAsync<SvantekJobAggregateException>(
            () => handler.RunAsync());

        Assert.AreEqual("CheckForSoundRecordings", aggregate.JobName);
        Assert.HasCount(1, aggregate.Failures);
        Assert.AreEqual(identifier, aggregate.Failures[0].Message);
        Assert.IsLessThanOrEqualTo(64, identifier.Length, "DBClient error tags are limited to 64 characters.");
        StringAssert.Contains(identifier, notificationId.ToString());
        Assert.IsEmpty(storage.Writes);
        httpClient.VerifyAll();
        dbClient.VerifyAll();
    }

    [TestMethod]
    public async Task RunAsync_MalformedNonWavRow_IsValidatedBeforeFileTypeFiltering()
    {
        var notificationId = Guid.Parse("4cb38822-3497-4650-bac0-82da974c1d28");
        var notificationTime = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        var identifier = $"sound:{notificationId}";
        const string filesResponse = """
            {
              "status":"ok",
              "files":[
                ["README.txt", 3, "20260713", 2048, 307, "station-123", "2026-07-13 10:00:00", 0, 1]
              ],
              "files_size":1
            }
            """;
        var httpClient = new Mock<IHttpClient>(MockBehavior.Strict);
        var dbClient = new Mock<IDBClient>(MockBehavior.Strict);
        var storage = new RecordingBlobStorageService();
        dbClient.Setup(client => client.ReadLatestNotificationAsync(CancellationToken.None)).ReturnsAsync(
            new List<NoiseNotificationLatest>
            {
                new(notificationId, Guid.NewGuid(), "F1", "12345", 7, 3, notificationTime, 900)
            });
        dbClient.Setup(client => client.HandleException(
            identifier,
            It.IsAny<InvalidDataException>()));
        httpClient.Setup(client => client.PostAsync(
                "projects-get-data.php",
                It.IsAny<HttpContent>(),
                CancellationToken.None))
            .ReturnsAsync(filesResponse);
        var handler = new CheckForSoundRecordingsHandler(
            dbClient.Object,
            dbClient.Object,
            new SvantekHttpGateway(httpClient.Object, "test-api-key"),
            storage);

        var aggregate = await Assert.ThrowsExactlyAsync<SvantekJobAggregateException>(
            () => handler.RunAsync());

        Assert.HasCount(1, aggregate.Failures);
        Assert.AreEqual(identifier, aggregate.Failures[0].Message);
        Assert.IsInstanceOfType<InvalidDataException>(aggregate.Failures[0].InnerException);
        Assert.IsEmpty(storage.Writes);
        httpClient.VerifyAll();
        dbClient.VerifyAll();
    }

    [TestMethod]
    public async Task RunAsync_LowercaseWav_IsExcludedByOrdinalCaseSensitiveFilter()
    {
        var notificationId = Guid.Parse("4cb38822-3497-4650-bac0-82da974c1d28");
        var notificationTime = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        const string filesResponse = """
            {
              "status": "ok",
              "files": [
                ["20260713_09_59_30.wav", 3, "20260713", 2048, "SV307", "station-123", "2026-07-13 10:00:00", 0, 1]
              ],
              "files_size": 1
            }
            """;
        var httpClient = new Mock<IHttpClient>(MockBehavior.Strict);
        var dbClient = new Mock<IDBClient>(MockBehavior.Strict);
        var storage = new RecordingBlobStorageService();
        dbClient.Setup(client => client.ReadLatestNotificationAsync(CancellationToken.None)).ReturnsAsync(
            new List<NoiseNotificationLatest>
            {
                new(notificationId, Guid.NewGuid(), "F1", "12345", 7, 3, notificationTime, 900)
            });
        httpClient.Setup(client => client.PostAsync(
                "projects-get-data.php",
                It.IsAny<HttpContent>(),
                CancellationToken.None))
            .ReturnsAsync(filesResponse);
        var handler = new CheckForSoundRecordingsHandler(
            dbClient.Object,
            dbClient.Object,
            new SvantekHttpGateway(httpClient.Object, "test-api-key"),
            storage);

        await handler.RunAsync();

        Assert.IsEmpty(storage.Writes);
        httpClient.VerifyAll();
        dbClient.VerifyAll();
    }
}

internal sealed class RecordingBlobStorageService : IBlobStorageService
{
    public List<StorageWrite> Writes { get; } = new();

    public Task<BlobStorageWriteResult> WriteAsync(
        BlobStorageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        Writes.Add(new StorageWrite(request, cancellationToken));
        return Task.FromResult(new BlobStorageWriteResult(request.ObjectName));
    }

    public Task DeleteAsync(string objectName, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    internal sealed record StorageWrite(
        BlobStorageWriteRequest Request,
        CancellationToken CancellationToken);
}
