using Moq;
using Rvt.Storage;
using Svantek.Api;
using Svantek.Api.Db;
using Svantek.Api.Http;
using Svantek.Api.Storage;
using Svantek.Api.UseCases;

namespace SvantekMonitorTests;

[TestClass]
public sealed class TestCheckForSoundRecordingStorage
{
    [TestMethod]
    public async Task RunAsync_MatchingWav_DownloadsAndWritesThroughStoragePort()
    {
        Guid notificationId = Guid.Parse("4cb38822-3497-4650-bac0-82da974c1d28");
        DateTime notificationTime = new(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        byte[] soundContent = [82, 73, 70, 70, 1, 2, 3, 4];
        string filesResponse = """
            {
              "status": "ok",
              "files": [
                ["20260713_09_59_30.WAV", 3, "20260713", 2048, "SV307", "station-123", "2026-07-13 10:00:00", 0, 1]
              ],
              "files_size": 1
            }
            """;

        Mock<IHttpClient> httpClient = new();
        Mock<IDBClient> dbClient = new();
        RecordingObjectStorageClient storage = new();
        using CancellationTokenSource cancellation = new();

        dbClient.Setup(client => client.ReadLatestNotificationAsync(cancellation.Token)).ReturnsAsync(
            [
                new(notificationId, Guid.NewGuid(), "F1", "12345", 7, 3, notificationTime, 900)
            ]);
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

        CheckForSoundRecordingsHandler handler = new(
            dbClient.Object,
            dbClient.Object,
            new SvantekHttpGateway(httpClient.Object, "test-api-key"),
            TestObjectStorageFactory.ForSoundRecordings(storage));

        await handler.RunAsync(cancellation.Token);

        httpClient.Verify(client => client.GetByteArrayAsync(
            "projects-get-data.php",
            It.IsAny<MultipartFormDataContent>(),
            cancellation.Token), Times.Once);
        Assert.HasCount(1, storage.Writes);
        Assert.AreEqual($"{notificationId}.wav", storage.Writes[0].Key.Value);
        CollectionAssert.AreEqual(soundContent, storage.Writes[0].Content);
        Assert.AreEqual("audio/wav", storage.Writes[0].ContentType);
        Assert.AreEqual(cancellation.Token, storage.Writes[0].CancellationToken);
        dbClient.Verify(client => client.WriteSoundFileAsync(
            notificationId,
            $"{notificationId}.wav",
            cancellation.Token), Times.Once);
    }

    [TestMethod]
    public async Task RunAsync_EmptyVendorRow_RecordsCompactNotificationIdentifier_AndThrowsAggregate()
    {
        Guid notificationId = Guid.Parse("4cb38822-3497-4650-bac0-82da974c1d28");
        DateTime notificationTime = new(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        string identifier = $"sound:{notificationId}";
        const string filesResponse = """
            {"status":"ok","files":[[]],"files_size":1}
            """;
        Mock<IHttpClient> httpClient = new(MockBehavior.Strict);
        Mock<IDBClient> dbClient = new(MockBehavior.Strict);
        RecordingObjectStorageClient storage = new();
        dbClient.Setup(client => client.ReadLatestNotificationAsync(CancellationToken.None)).ReturnsAsync(
            [
                new(notificationId, Guid.NewGuid(), "F1", "12345", 7, 3, notificationTime, 900)
            ]);
        dbClient.Setup(client => client.HandleException(
            identifier,
            It.IsAny<InvalidDataException>()));
        httpClient.Setup(client => client.PostAsync(
                "projects-get-data.php",
                It.IsAny<HttpContent>(),
                CancellationToken.None))
            .ReturnsAsync(filesResponse);
        CheckForSoundRecordingsHandler handler = new(
            dbClient.Object,
            dbClient.Object,
            new SvantekHttpGateway(httpClient.Object, "test-api-key"),
            TestObjectStorageFactory.ForSoundRecordings(storage));

        SvantekJobAggregateException aggregate = await Assert.ThrowsExactlyAsync<SvantekJobAggregateException>(
            () => handler.RunAsync(TestContext.CancellationToken));

        Assert.AreEqual("CheckForSoundRecordings", aggregate.JobName);
        Assert.HasCount(1, aggregate.Failures);
        Assert.AreEqual(identifier, aggregate.Failures[0].Message);
        Assert.IsLessThanOrEqualTo(64, identifier.Length, "DBClient error tags are limited to 64 characters.");
        Assert.Contains(notificationId.ToString(), identifier);
        Assert.IsEmpty(storage.Writes);
        httpClient.VerifyAll();
        dbClient.VerifyAll();
    }

    [TestMethod]
    public async Task RunAsync_MalformedNonWavRow_IsValidatedBeforeFileTypeFiltering()
    {
        Guid notificationId = Guid.Parse("4cb38822-3497-4650-bac0-82da974c1d28");
        DateTime notificationTime = new(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        string identifier = $"sound:{notificationId}";
        const string filesResponse = """
            {
              "status":"ok",
              "files":[
                ["README.txt", 3, "20260713", 2048, 307, "station-123", "2026-07-13 10:00:00", 0, 1]
              ],
              "files_size":1
            }
            """;
        Mock<IHttpClient> httpClient = new(MockBehavior.Strict);
        Mock<IDBClient> dbClient = new(MockBehavior.Strict);
        RecordingObjectStorageClient storage = new();
        dbClient.Setup(client => client.ReadLatestNotificationAsync(CancellationToken.None)).ReturnsAsync(
            [
                new(notificationId, Guid.NewGuid(), "F1", "12345", 7, 3, notificationTime, 900)
            ]);
        dbClient.Setup(client => client.HandleException(
            identifier,
            It.IsAny<InvalidDataException>()));
        httpClient.Setup(client => client.PostAsync(
                "projects-get-data.php",
                It.IsAny<HttpContent>(),
                CancellationToken.None))
            .ReturnsAsync(filesResponse);
        CheckForSoundRecordingsHandler handler = new(
            dbClient.Object,
            dbClient.Object,
            new SvantekHttpGateway(httpClient.Object, "test-api-key"),
            TestObjectStorageFactory.ForSoundRecordings(storage));

        SvantekJobAggregateException aggregate = await Assert.ThrowsExactlyAsync<SvantekJobAggregateException>(
            () => handler.RunAsync(TestContext.CancellationToken));

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
        Guid notificationId = Guid.Parse("4cb38822-3497-4650-bac0-82da974c1d28");
        DateTime notificationTime = new(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        const string filesResponse = """
            {
              "status": "ok",
              "files": [
                ["20260713_09_59_30.wav", 3, "20260713", 2048, "SV307", "station-123", "2026-07-13 10:00:00", 0, 1]
              ],
              "files_size": 1
            }
            """;
        Mock<IHttpClient> httpClient = new(MockBehavior.Strict);
        Mock<IDBClient> dbClient = new(MockBehavior.Strict);
        RecordingObjectStorageClient storage = new();
        dbClient.Setup(client => client.ReadLatestNotificationAsync(CancellationToken.None)).ReturnsAsync(
            [
                new(notificationId, Guid.NewGuid(), "F1", "12345", 7, 3, notificationTime, 900)
            ]);
        httpClient.Setup(client => client.PostAsync(
                "projects-get-data.php",
                It.IsAny<HttpContent>(),
                CancellationToken.None))
            .ReturnsAsync(filesResponse);
        CheckForSoundRecordingsHandler handler = new(
            dbClient.Object,
            dbClient.Object,
            new SvantekHttpGateway(httpClient.Object, "test-api-key"),
            TestObjectStorageFactory.ForSoundRecordings(storage));

        await handler.RunAsync(TestContext.CancellationToken);

        Assert.IsEmpty(storage.Writes);
        httpClient.VerifyAll();
        dbClient.VerifyAll();
    }

    public TestContext TestContext { get; set; } = null!;
}

internal sealed class RecordingObjectStorageClient : IObjectStorageClient
{
    public Uri GetObjectUri(StorageObjectKey key) =>
        new($"file:///recordings/{key.Value}");

    public List<StorageWrite> Writes { get; } = [];

    public async Task<StorageWriteResult> WriteAsync(
        StorageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        using MemoryStream buffer = new();
        await request.Content.CopyToAsync(buffer, cancellationToken);
        Writes.Add(new(
            request.Key,
            buffer.ToArray(),
            request.ContentType,
            cancellationToken));
        return new StorageWriteResult(request.Key);
    }

    public Task<StorageReadResult?> OpenReadAsync(
        StorageObjectKey key,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<StorageReadResult?>(null);

    public Task<bool> DeleteIfExistsAsync(
        StorageObjectKey key,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    internal sealed record StorageWrite(
        StorageObjectKey Key,
        byte[] Content,
        string? ContentType,
        CancellationToken CancellationToken);
}

internal static class TestObjectStorageFactory
{
    internal static IObjectStorageClientFactory ForSoundRecordings(
        IObjectStorageClient client) =>
        new ObjectStorageClientFactory(
        [
            new ObjectStorageClientRegistration(
                SvantekStorageComposition.SoundRecordingsResource,
                client),
        ]);
}
