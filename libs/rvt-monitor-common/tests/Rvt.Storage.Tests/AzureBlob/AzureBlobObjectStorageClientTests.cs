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
        var content = new MemoryStream([1, 2, 3], writable: false);
        var key = StorageObjectKey.Parse("clips/sample.wav");
        var cancellationToken = new CancellationTokenSource().Token;
        var container = new Mock<BlobContainerClient>(MockBehavior.Strict);
        var blob = new Mock<BlobClient>(MockBehavior.Strict);
        BlobUploadOptions? capturedOptions = null;
        var sequence = new MockSequence();
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
        var client = CreateClient(container, "tenant-a");

        var result = await client.WriteAsync(
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
        var content = new MemoryStream([1], writable: false);
        var key = StorageObjectKey.Parse("sample.wav");
        var container = new Mock<BlobContainerClient>(MockBehavior.Strict);
        var blob = new Mock<BlobClient>(MockBehavior.Strict);
        BlobUploadOptions? capturedOptions = null;
        container
            .Setup(client => client.GetBlobClient("sample.wav"))
            .Returns(blob.Object);
        container
            .Setup(client => client.CreateIfNotExistsAsync(
                PublicAccessType.None,
                null,
                null,
                CancellationToken.None))
            .ReturnsAsync((Response<BlobContainerInfo>)null!);
        blob
            .Setup(client => client.UploadAsync(
                content,
                It.IsAny<BlobUploadOptions>(),
                CancellationToken.None))
            .Callback<Stream, BlobUploadOptions, CancellationToken>(
                (_, options, _) => capturedOptions = options)
            .ReturnsAsync((Response<BlobContentInfo>)null!);
        var client = CreateClient(container);

        await client.WriteAsync(new StorageWriteRequest(key, content, " "));

        Assert.IsNotNull(capturedOptions);
        Assert.IsNull(capturedOptions.HttpHeaders);
        container.VerifyAll();
        blob.VerifyAll();
    }

    [TestMethod]
    public async Task OpenReadAsync_ReturnsStreamingContentMetadataAndResponseLease()
    {
        var content = new MemoryStream([4, 5, 6], writable: false);
        var key = StorageObjectKey.Parse("sample.wav");
        var details = BlobsModelFactory.BlobDownloadDetails(
            contentLength: 3,
            contentType: "audio/wav");
        var download = BlobsModelFactory.BlobDownloadStreamingResult(content, details);
        var rawResponse = new Mock<Response>(MockBehavior.Strict);
        rawResponse.Setup(response => response.Dispose());
        var response = Response.FromValue(download, rawResponse.Object);
        var container = new Mock<BlobContainerClient>(MockBehavior.Strict);
        var blob = new Mock<BlobClient>(MockBehavior.Strict);
        container
            .Setup(client => client.GetBlobClient("tenant-a/sample.wav"))
            .Returns(blob.Object);
        blob
            .Setup(client => client.DownloadStreamingAsync(
                It.IsAny<BlobDownloadOptions>(),
                CancellationToken.None))
            .ReturnsAsync(response);
        var client = CreateClient(container, "tenant-a");

        var result = await client.OpenReadAsync(key);

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
        var key = StorageObjectKey.Parse("missing.wav");
        var container = new Mock<BlobContainerClient>(MockBehavior.Strict);
        var blob = new Mock<BlobClient>(MockBehavior.Strict);
        container
            .Setup(client => client.GetBlobClient("missing.wav"))
            .Returns(blob.Object);
        blob
            .Setup(client => client.DownloadStreamingAsync(
                It.IsAny<BlobDownloadOptions>(),
                CancellationToken.None))
            .ThrowsAsync(new RequestFailedException(404, "provider-body"));
        var client = CreateClient(container);

        var result = await client.OpenReadAsync(key);

        Assert.IsNull(result);
        container.VerifyAll();
        blob.VerifyAll();
    }

    [TestMethod]
    public async Task DeleteIfExistsAsync_ReturnsAzureResult()
    {
        var key = StorageObjectKey.Parse("sample.wav");
        var rawResponse = new Mock<Response>(MockBehavior.Strict);
        var response = Response.FromValue(true, rawResponse.Object);
        var container = new Mock<BlobContainerClient>(MockBehavior.Strict);
        var blob = new Mock<BlobClient>(MockBehavior.Strict);
        container
            .Setup(client => client.GetBlobClient("sample.wav"))
            .Returns(blob.Object);
        blob
            .Setup(client => client.DeleteIfExistsAsync(
                DeleteSnapshotsOption.None,
                null,
                CancellationToken.None))
            .ReturnsAsync(response);
        var client = CreateClient(container);

        var deleted = await client.DeleteIfExistsAsync(key);

        Assert.IsTrue(deleted);
        container.VerifyAll();
        blob.VerifyAll();
    }

    [DataTestMethod]
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
        var key = StorageObjectKey.Parse("sample.wav");
        var container = new Mock<BlobContainerClient>(MockBehavior.Strict);
        var blob = new Mock<BlobClient>(MockBehavior.Strict);
        container
            .Setup(client => client.GetBlobClient("sample.wav"))
            .Returns(blob.Object);
        blob
            .Setup(client => client.DeleteIfExistsAsync(
                DeleteSnapshotsOption.None,
                null,
                CancellationToken.None))
            .ThrowsAsync(new RequestFailedException(
                status,
                providerBody,
                "ProviderErrorCode",
                new InvalidOperationException(innerText)));
        var client = CreateClient(container);

        var exception = await Assert.ThrowsExactlyAsync<ObjectStorageException>(() =>
            client.DeleteIfExistsAsync(key));

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
        var key = StorageObjectKey.Parse("sample.wav");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var container = new Mock<BlobContainerClient>(MockBehavior.Strict);
        var blob = new Mock<BlobClient>(MockBehavior.Strict);
        container
            .Setup(client => client.GetBlobClient("sample.wav"))
            .Returns(blob.Object);
        blob
            .Setup(client => client.DeleteIfExistsAsync(
                DeleteSnapshotsOption.None,
                null,
                cancellation.Token))
            .Returns(Task.FromCanceled<Response<bool>>(cancellation.Token));
        var client = CreateClient(container);

        var exception = await Assert.ThrowsExactlyAsync<TaskCanceledException>(() =>
            client.DeleteIfExistsAsync(key, cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        container.VerifyAll();
        blob.VerifyAll();
    }

    [TestMethod]
    public void GetObjectUri_ReturnsUriFromPrefixedBlobClient()
    {
        var key = StorageObjectKey.Parse("sample.wav");
        var expectedUri =
            new Uri("https://storage.example.test/recordings/tenant-a/sample.wav");
        var container = new Mock<BlobContainerClient>(MockBehavior.Strict);
        var blob = new Mock<BlobClient>(MockBehavior.Strict);
        container
            .Setup(client => client.GetBlobClient("tenant-a/sample.wav"))
            .Returns(blob.Object);
        blob.SetupGet(client => client.Uri).Returns(expectedUri);
        var client = CreateClient(container, "tenant-a");

        var result = client.GetObjectUri(key);

        Assert.AreEqual(expectedUri, result);
        container.VerifyAll();
        blob.VerifyAll();
    }

    private static AzureBlobObjectStorageClient CreateClient(
        Mock<BlobContainerClient> container,
        string prefix = "") =>
        new("recordings", container.Object, prefix);
}
