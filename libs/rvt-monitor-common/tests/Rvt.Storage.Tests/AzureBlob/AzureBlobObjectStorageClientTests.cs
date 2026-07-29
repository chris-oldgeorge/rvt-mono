using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Moq;
using Rvt.Storage.AzureBlob;

namespace Rvt.Storage.Tests.AzureBlob;

[TestClass]
public sealed class AzureBlobObjectStorageClientTests
{
    [TestMethod]
    public async Task WriteAsync_CreatesContainerAndStreamsOriginalContentWithPrefixAndHeaders()
    {
        MemoryStream content = new([1, 2, 3], writable: false);
        StorageObjectKey key = StorageObjectKey.Parse("clips/sample.wav");
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        Mock<BlobContainerClient> container = new(MockBehavior.Strict);
        Mock<BlobClient> blob = new(MockBehavior.Strict);
        BlobUploadOptions? capturedOptions = null;
        MockSequence sequence = new();
        container
            .InSequence(sequence)
            .Setup(client => client.GetBlobClient("tenant-a/clips/sample.wav"))
            .Returns(blob.Object);
        container
            .InSequence(sequence)
            .Setup(client => client.CreateIfNotExistsAsync(
                PublicAccessType.None,
                null,
                null,
                cancellationToken))
            .ReturnsAsync((Response<BlobContainerInfo>)null!);
        blob
            .InSequence(sequence)
            .Setup(client => client.UploadAsync(
                content,
                It.IsAny<BlobUploadOptions>(),
                cancellationToken))
            .Callback<Stream, BlobUploadOptions, CancellationToken>(
                (_, options, _) => capturedOptions = options)
            .ReturnsAsync((Response<BlobContentInfo>)null!);
        AzureBlobObjectStorageClient client = CreateClient(container, "tenant-a");

        StorageWriteResult result = await client.WriteAsync(
            new StorageWriteRequest(key, content, "audio/wav"),
            cancellationToken);

        Assert.AreSame(key, result.Key);
        Assert.IsNotNull(capturedOptions);
        Assert.IsNull(capturedOptions.Conditions);
        Assert.IsNotNull(capturedOptions.HttpHeaders);
        Assert.AreEqual("audio/wav", capturedOptions.HttpHeaders.ContentType);
        container.VerifyAll();
        blob.VerifyAll();
    }

    [TestMethod]
    public async Task WriteAsync_WhenContentTypeIsBlank_OmitsHttpHeaders()
    {
        MemoryStream content = new([1], writable: false);
        StorageObjectKey key = StorageObjectKey.Parse("sample.wav");
        Mock<BlobContainerClient> container = new(MockBehavior.Strict);
        Mock<BlobClient> blob = new(MockBehavior.Strict);
        BlobUploadOptions? capturedOptions = null;
        container
            .Setup(client => client.GetBlobClient("sample.wav"))
            .Returns(blob.Object);
        container
            .Setup(client => client.CreateIfNotExistsAsync(
                PublicAccessType.None,
                null,
                null,
                TestContext.CancellationToken))
            .ReturnsAsync((Response<BlobContainerInfo>)null!);
        blob
            .Setup(client => client.UploadAsync(
                content,
                It.IsAny<BlobUploadOptions>(),
                TestContext.CancellationToken))
            .Callback<Stream, BlobUploadOptions, CancellationToken>(
                (_, options, _) => capturedOptions = options)
            .ReturnsAsync((Response<BlobContentInfo>)null!);
        AzureBlobObjectStorageClient client = CreateClient(container);

        await client.WriteAsync(new StorageWriteRequest(key, content, " "), TestContext.CancellationToken);

        Assert.IsNotNull(capturedOptions);
        Assert.IsNull(capturedOptions.HttpHeaders);
        container.VerifyAll();
        blob.VerifyAll();
    }

    [TestMethod]
    public async Task WriteAsync_WhenProviderCancelsWithoutCallerCancellation_TranslatesUnavailable()
    {
        const string providerMessage = "configured-provider-timeout";
        MemoryStream content = new([1], writable: false);
        StorageObjectKey key = StorageObjectKey.Parse("sample.wav");
        using CancellationTokenSource cancellation = new();
        Mock<BlobContainerClient> container = new(MockBehavior.Strict);
        Mock<BlobClient> blob = new(MockBehavior.Strict);
        container
            .Setup(client => client.GetBlobClient("sample.wav"))
            .Returns(blob.Object);
        container
            .Setup(client => client.CreateIfNotExistsAsync(
                PublicAccessType.None,
                null,
                null,
                cancellation.Token))
            .ReturnsAsync((Response<BlobContainerInfo>)null!);
        blob
            .Setup(client => client.UploadAsync(
                content,
                It.IsAny<BlobUploadOptions>(),
                cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(providerMessage));
        AzureBlobObjectStorageClient client = CreateClient(container);

        await AssertUnavailableProviderCancellationAsync(
            () => client.WriteAsync(
                new StorageWriteRequest(key, content),
                cancellation.Token),
            key,
            providerMessage);

        container.VerifyAll();
        blob.VerifyAll();
    }

    [TestMethod]
    public async Task OpenReadAsync_ReturnsStreamingContentMetadataAndResponseLease()
    {
        MemoryStream content = new([4, 5, 6], writable: false);
        StorageObjectKey key = StorageObjectKey.Parse("sample.wav");
        BlobDownloadDetails details = BlobsModelFactory.BlobDownloadDetails(
            contentLength: 3,
            contentType: "audio/wav");
        BlobDownloadStreamingResult download = BlobsModelFactory.BlobDownloadStreamingResult(content, details);
        Mock<Response> rawResponse = new(MockBehavior.Strict);
        rawResponse.Setup(response => response.Dispose());
        Response<BlobDownloadStreamingResult> response = Response.FromValue(download, rawResponse.Object);
        Mock<BlobContainerClient> container = new(MockBehavior.Strict);
        Mock<BlobClient> blob = new(MockBehavior.Strict);
        container
            .Setup(client => client.GetBlobClient("tenant-a/sample.wav"))
            .Returns(blob.Object);
        blob
            .Setup(client => client.DownloadStreamingAsync(
                It.IsAny<BlobDownloadOptions>(),
                TestContext.CancellationToken))
            .ReturnsAsync(response);
        AzureBlobObjectStorageClient client = CreateClient(container, "tenant-a");

        StorageReadResult? result = await client.OpenReadAsync(key, TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.AreSame(content, result.Content);
        Assert.AreEqual("audio/wav", result.ContentType);
        Assert.AreEqual(3, result.Length);
        await result.DisposeAsync();
        Assert.IsFalse(content.CanRead);
        rawResponse.Verify(responseValue => responseValue.Dispose(), Times.Once);
        container.VerifyAll();
        blob.VerifyAll();
    }

    [TestMethod]
    public async Task OpenReadAsync_WhenAzureReturns404_ReturnsNull()
    {
        StorageObjectKey key = StorageObjectKey.Parse("missing.wav");
        Mock<BlobContainerClient> container = new(MockBehavior.Strict);
        Mock<BlobClient> blob = new(MockBehavior.Strict);
        container
            .Setup(client => client.GetBlobClient("missing.wav"))
            .Returns(blob.Object);
        blob
            .Setup(client => client.DownloadStreamingAsync(
                It.IsAny<BlobDownloadOptions>(),
                TestContext.CancellationToken))
            .ThrowsAsync(new RequestFailedException(404, "provider-body"));
        AzureBlobObjectStorageClient client = CreateClient(container);

        StorageReadResult? result = await client.OpenReadAsync(key, TestContext.CancellationToken);

        Assert.IsNull(result);
        container.VerifyAll();
        blob.VerifyAll();
    }

    [TestMethod]
    public async Task OpenReadAsync_WhenProviderCancelsWithoutCallerCancellation_TranslatesUnavailable()
    {
        const string providerMessage = "configured-provider-timeout";
        StorageObjectKey key = StorageObjectKey.Parse("sample.wav");
        using CancellationTokenSource cancellation = new();
        Mock<BlobContainerClient> container = new(MockBehavior.Strict);
        Mock<BlobClient> blob = new(MockBehavior.Strict);
        container
            .Setup(client => client.GetBlobClient("sample.wav"))
            .Returns(blob.Object);
        blob
            .Setup(client => client.DownloadStreamingAsync(
                It.IsAny<BlobDownloadOptions>(),
                cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(providerMessage));
        AzureBlobObjectStorageClient client = CreateClient(container);

        await AssertUnavailableProviderCancellationAsync(
            () => client.OpenReadAsync(key, cancellation.Token),
            key,
            providerMessage);

        container.VerifyAll();
        blob.VerifyAll();
    }

    [TestMethod]
    public async Task DeleteIfExistsAsync_ReturnsAzureResult()
    {
        StorageObjectKey key = StorageObjectKey.Parse("sample.wav");
        Mock<Response> rawResponse = new(MockBehavior.Strict);
        Response<bool> response = Response.FromValue(true, rawResponse.Object);
        Mock<BlobContainerClient> container = new(MockBehavior.Strict);
        Mock<BlobClient> blob = new(MockBehavior.Strict);
        container
            .Setup(client => client.GetBlobClient("sample.wav"))
            .Returns(blob.Object);
        blob
            .Setup(client => client.DeleteIfExistsAsync(
                DeleteSnapshotsOption.None,
                null,
                TestContext.CancellationToken))
            .ReturnsAsync(response);
        AzureBlobObjectStorageClient client = CreateClient(container);

        bool deleted = await client.DeleteIfExistsAsync(key, TestContext.CancellationToken);

        Assert.IsTrue(deleted);
        container.VerifyAll();
        blob.VerifyAll();
    }

    [TestMethod]
    public async Task DeleteIfExistsAsync_WhenProviderCancelsWithoutCallerCancellation_TranslatesUnavailable()
    {
        const string providerMessage = "configured-provider-timeout";
        StorageObjectKey key = StorageObjectKey.Parse("sample.wav");
        using CancellationTokenSource cancellation = new();
        Mock<BlobContainerClient> container = new(MockBehavior.Strict);
        Mock<BlobClient> blob = new(MockBehavior.Strict);
        container
            .Setup(client => client.GetBlobClient("sample.wav"))
            .Returns(blob.Object);
        blob
            .Setup(client => client.DeleteIfExistsAsync(
                DeleteSnapshotsOption.None,
                null,
                cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(providerMessage));
        AzureBlobObjectStorageClient client = CreateClient(container);

        await AssertUnavailableProviderCancellationAsync(
            () => client.DeleteIfExistsAsync(key, cancellation.Token),
            key,
            providerMessage);

        container.VerifyAll();
        blob.VerifyAll();
    }

    [TestMethod]
    [DataRow(403, StorageFailureKind.AccessDenied)]
    [DataRow(409, StorageFailureKind.Conflict)]
    [DataRow(408, StorageFailureKind.Unavailable)]
    [DataRow(429, StorageFailureKind.Unavailable)]
    [DataRow(500, StorageFailureKind.Unavailable)]
    [DataRow(503, StorageFailureKind.Unavailable)]
    [DataRow(599, StorageFailureKind.Unavailable)]
    public async Task DeleteIfExistsAsync_TranslatesAzureStatusSafely(
        int status,
        StorageFailureKind expectedKind)
    {
        const string providerBody = "configured-provider-response-body";
        const string innerText = "configured-inner-exception-text";
        StorageObjectKey key = StorageObjectKey.Parse("sample.wav");
        Mock<BlobContainerClient> container = new(MockBehavior.Strict);
        Mock<BlobClient> blob = new(MockBehavior.Strict);
        container
            .Setup(client => client.GetBlobClient("sample.wav"))
            .Returns(blob.Object);
        blob
            .Setup(client => client.DeleteIfExistsAsync(
                DeleteSnapshotsOption.None,
                null,
                TestContext.CancellationToken))
            .ThrowsAsync(new RequestFailedException(
                status,
                providerBody,
                "ProviderErrorCode",
                new InvalidOperationException(innerText)));
        AzureBlobObjectStorageClient client = CreateClient(container);

        ObjectStorageException exception = await Assert.ThrowsExactlyAsync<ObjectStorageException>(() =>
            client.DeleteIfExistsAsync(key, TestContext.CancellationToken));

        Assert.AreEqual(expectedKind, exception.Kind);
        Assert.AreEqual("recordings", exception.ResourceName);
        Assert.AreSame(key, exception.Key);
        Assert.DoesNotContain(providerBody, exception.Message);
        Assert.DoesNotContain(innerText, exception.Message);
        container.VerifyAll();
        blob.VerifyAll();
    }

    [TestMethod]
    public async Task DeleteIfExistsAsync_WhenCallerCancels_PropagatesOperationCanceledException()
    {
        StorageObjectKey key = StorageObjectKey.Parse("sample.wav");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Mock<BlobContainerClient> container = new(MockBehavior.Strict);
        Mock<BlobClient> blob = new(MockBehavior.Strict);
        container
            .Setup(client => client.GetBlobClient("sample.wav"))
            .Returns(blob.Object);
        blob
            .Setup(client => client.DeleteIfExistsAsync(
                DeleteSnapshotsOption.None,
                null,
                cancellation.Token))
            .Returns(Task.FromCanceled<Response<bool>>(cancellation.Token));
        AzureBlobObjectStorageClient client = CreateClient(container);

        TaskCanceledException exception = await Assert.ThrowsExactlyAsync<TaskCanceledException>(() =>
            client.DeleteIfExistsAsync(key, cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        container.VerifyAll();
        blob.VerifyAll();
    }

    [TestMethod]
    public void GetObjectUri_ReturnsUriFromPrefixedBlobClient()
    {
        StorageObjectKey key = StorageObjectKey.Parse("sample.wav");
        Uri expectedUri =
            new("https://storage.example.test/recordings/tenant-a/sample.wav");
        Mock<BlobContainerClient> container = new(MockBehavior.Strict);
        Mock<BlobClient> blob = new(MockBehavior.Strict);
        container
            .Setup(client => client.GetBlobClient("tenant-a/sample.wav"))
            .Returns(blob.Object);
        blob.SetupGet(client => client.Uri).Returns(expectedUri);
        AzureBlobObjectStorageClient client = CreateClient(container, "tenant-a");

        Uri result = client.GetObjectUri(key);

        Assert.AreEqual(expectedUri, result);
        container.VerifyAll();
        blob.VerifyAll();
    }

    private static AzureBlobObjectStorageClient CreateClient(
        Mock<BlobContainerClient> container,
        string prefix = "") =>
        new("recordings", container.Object, prefix);

    private static async Task AssertUnavailableProviderCancellationAsync(
        Func<Task> operation,
        StorageObjectKey expectedKey,
        string providerMessage)
    {
        ObjectStorageException exception =
            await Assert.ThrowsExactlyAsync<ObjectStorageException>(operation);

        Assert.AreEqual(StorageFailureKind.Unavailable, exception.Kind);
        Assert.AreEqual("recordings", exception.ResourceName);
        Assert.AreSame(expectedKey, exception.Key);
        Assert.IsInstanceOfType<OperationCanceledException>(
            exception.InnerException);
        Assert.DoesNotContain(providerMessage, exception.Message);
    }

    public TestContext TestContext { get; set; } = null!;
}
