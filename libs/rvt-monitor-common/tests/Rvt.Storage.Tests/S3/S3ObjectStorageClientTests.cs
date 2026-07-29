using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Moq;
using Rvt.Storage.S3;

namespace Rvt.Storage.Tests.S3;

[TestClass]
public sealed class S3ObjectStorageClientTests
{
    [TestMethod]
    public async Task WriteAsync_StreamsOriginalContentWithPrefixAndContentType()
    {
        MemoryStream content = new([1, 2, 3], writable: false);
        StorageObjectKey key = StorageObjectKey.Parse("clips/sample.wav");
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        Mock<IAmazonS3> s3 = new(MockBehavior.Strict);
        PutObjectRequest? capturedRequest = null;
        s3.Setup(client => client.PutObjectAsync(
                It.IsAny<PutObjectRequest>(),
                cancellationToken))
            .Callback<PutObjectRequest, CancellationToken>(
                (request, _) => capturedRequest = request)
            .ReturnsAsync(new PutObjectResponse());
        S3ObjectStorageClient client = CreateClient(s3, prefix: "tenant-a");

        StorageWriteResult result = await client.WriteAsync(
            new StorageWriteRequest(key, content, "audio/wav"),
            cancellationToken);

        Assert.AreSame(key, result.Key);
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual("recordings", capturedRequest.BucketName);
        Assert.AreEqual("tenant-a/clips/sample.wav", capturedRequest.Key);
        Assert.AreSame(content, capturedRequest.InputStream);
        Assert.IsFalse(capturedRequest.AutoCloseStream);
        Assert.AreEqual("audio/wav", capturedRequest.ContentType);
        Assert.IsTrue(content.CanRead);
        s3.VerifyAll();
    }

    [TestMethod]
    public async Task WriteAsync_WhenProviderCancelsWithoutCallerCancellation_TranslatesUnavailable()
    {
        const string providerMessage = "configured-provider-timeout";
        MemoryStream content = new([1], writable: false);
        StorageObjectKey key = StorageObjectKey.Parse("sample.wav");
        using CancellationTokenSource cancellation = new();
        Mock<IAmazonS3> s3 = new(MockBehavior.Strict);
        s3.Setup(client => client.PutObjectAsync(
                It.IsAny<PutObjectRequest>(),
                cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(providerMessage));
        S3ObjectStorageClient client = CreateClient(s3);

        await AssertUnavailableProviderCancellationAsync(
            () => client.WriteAsync(
                new StorageWriteRequest(key, content),
                cancellation.Token),
            key,
            providerMessage);

        s3.VerifyAll();
    }

    [TestMethod]
    public async Task OpenReadAsync_ReturnsStreamingContentMetadataAndResponseLease()
    {
        DisposeCountingStream content = new([4, 5, 6]);
        StorageObjectKey key = StorageObjectKey.Parse("sample.wav");
        GetObjectResponse response = new()
        {
            ResponseStream = content,
        };
        response.Headers.ContentType = "audio/wav";
        response.Headers.ContentLength = 3;
        Mock<IAmazonS3> s3 = new(MockBehavior.Strict);
        s3.Setup(client => client.GetObjectAsync(
                It.Is<GetObjectRequest>(request =>
                    request.BucketName == "recordings"
                    && request.Key == "tenant-a/sample.wav"),
                TestContext.CancellationToken))
            .ReturnsAsync(response);
        S3ObjectStorageClient client = CreateClient(s3, prefix: "tenant-a");

        StorageReadResult? result = await client.OpenReadAsync(key, TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.AreSame(content, result.Content);
        Assert.AreEqual("audio/wav", result.ContentType);
        Assert.AreEqual(3, result.Length);
        await result.DisposeAsync();
        Assert.AreEqual(2, content.DisposeCount);
        s3.VerifyAll();
    }

    [TestMethod]
    public async Task OpenReadAsync_WhenS3ReturnsNoSuchKey_ReturnsNull()
    {
        StorageObjectKey key = StorageObjectKey.Parse("missing.wav");
        Mock<IAmazonS3> s3 = new(MockBehavior.Strict);
        s3.Setup(client => client.GetObjectAsync(
                It.IsAny<GetObjectRequest>(),
                TestContext.CancellationToken))
            .ThrowsAsync(CreateException(
                HttpStatusCode.BadRequest,
                errorCode: "NoSuchKey"));
        S3ObjectStorageClient client = CreateClient(s3);

        StorageReadResult? result = await client.OpenReadAsync(key, TestContext.CancellationToken);

        Assert.IsNull(result);
        s3.VerifyAll();
    }

    [TestMethod]
    public async Task OpenReadAsync_WhenS3Returns404_ReturnsNull()
    {
        StorageObjectKey key = StorageObjectKey.Parse("missing.wav");
        Mock<IAmazonS3> s3 = new(MockBehavior.Strict);
        s3.Setup(client => client.GetObjectAsync(
                It.IsAny<GetObjectRequest>(),
                TestContext.CancellationToken))
            .ThrowsAsync(CreateException(
                HttpStatusCode.NotFound,
                errorCode: "ProviderNotFound"));
        S3ObjectStorageClient client = CreateClient(s3);

        StorageReadResult? result = await client.OpenReadAsync(key, TestContext.CancellationToken);

        Assert.IsNull(result);
        s3.VerifyAll();
    }

    [TestMethod]
    public async Task OpenReadAsync_WhenProviderCancelsWithoutCallerCancellation_TranslatesUnavailable()
    {
        const string providerMessage = "configured-provider-timeout";
        StorageObjectKey key = StorageObjectKey.Parse("sample.wav");
        using CancellationTokenSource cancellation = new();
        Mock<IAmazonS3> s3 = new(MockBehavior.Strict);
        s3.Setup(client => client.GetObjectAsync(
                It.IsAny<GetObjectRequest>(),
                cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(providerMessage));
        S3ObjectStorageClient client = CreateClient(s3);

        await AssertUnavailableProviderCancellationAsync(
            () => client.OpenReadAsync(key, cancellation.Token),
            key,
            providerMessage);

        s3.VerifyAll();
    }

    [TestMethod]
    public async Task DeleteIfExistsAsync_WhenMetadataIsMissing_ReturnsFalseWithoutDelete()
    {
        StorageObjectKey key = StorageObjectKey.Parse("missing.wav");
        Mock<IAmazonS3> s3 = new(MockBehavior.Strict);
        s3.Setup(client => client.GetObjectMetadataAsync(
                It.Is<GetObjectMetadataRequest>(request =>
                    request.BucketName == "recordings"
                    && request.Key == "tenant-a/missing.wav"),
                TestContext.CancellationToken))
            .ThrowsAsync(CreateException(
                HttpStatusCode.NotFound,
                errorCode: "NoSuchKey"));
        S3ObjectStorageClient client = CreateClient(s3, prefix: "tenant-a");

        bool deleted = await client.DeleteIfExistsAsync(key, TestContext.CancellationToken);

        Assert.IsFalse(deleted);
        s3.VerifyAll();
    }

    [TestMethod]
    public async Task DeleteIfExistsAsync_WhenMetadataExists_DeletesExpectedObject()
    {
        StorageObjectKey key = StorageObjectKey.Parse("sample.wav");
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        Mock<IAmazonS3> s3 = new(MockBehavior.Strict);
        MockSequence sequence = new();
        s3.InSequence(sequence)
            .Setup(client => client.GetObjectMetadataAsync(
                It.Is<GetObjectMetadataRequest>(request =>
                    request.BucketName == "recordings"
                    && request.Key == "tenant-a/sample.wav"),
                cancellationToken))
            .ReturnsAsync(new GetObjectMetadataResponse());
        s3.InSequence(sequence)
            .Setup(client => client.DeleteObjectAsync(
                It.Is<DeleteObjectRequest>(request =>
                    request.BucketName == "recordings"
                    && request.Key == "tenant-a/sample.wav"),
                cancellationToken))
            .ReturnsAsync(new DeleteObjectResponse());
        S3ObjectStorageClient client = CreateClient(s3, prefix: "tenant-a");

        bool deleted = await client.DeleteIfExistsAsync(key, cancellationToken);

        Assert.IsTrue(deleted);
        s3.VerifyAll();
    }

    [TestMethod]
    public async Task DeleteIfExistsAsync_WhenProviderCancelsWithoutCallerCancellation_TranslatesUnavailable()
    {
        const string providerMessage = "configured-provider-timeout";
        StorageObjectKey key = StorageObjectKey.Parse("sample.wav");
        using CancellationTokenSource cancellation = new();
        Mock<IAmazonS3> s3 = new(MockBehavior.Strict);
        MockSequence sequence = new();
        s3.InSequence(sequence)
            .Setup(client => client.GetObjectMetadataAsync(
                It.IsAny<GetObjectMetadataRequest>(),
                cancellation.Token))
            .ReturnsAsync(new GetObjectMetadataResponse());
        s3.InSequence(sequence)
            .Setup(client => client.DeleteObjectAsync(
                It.IsAny<DeleteObjectRequest>(),
                cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(providerMessage));
        S3ObjectStorageClient client = CreateClient(s3);

        await AssertUnavailableProviderCancellationAsync(
            () => client.DeleteIfExistsAsync(key, cancellation.Token),
            key,
            providerMessage);

        s3.VerifyAll();
    }

    [TestMethod]
    [DataRow(HttpStatusCode.Forbidden, StorageFailureKind.AccessDenied)]
    [DataRow(HttpStatusCode.BadRequest, StorageFailureKind.InvalidRequest)]
    [DataRow(HttpStatusCode.Conflict, StorageFailureKind.Conflict)]
    [DataRow(HttpStatusCode.RequestTimeout, StorageFailureKind.Unavailable)]
    [DataRow((HttpStatusCode)429, StorageFailureKind.Unavailable)]
    [DataRow(HttpStatusCode.ServiceUnavailable, StorageFailureKind.Unavailable)]
    public async Task DeleteIfExistsAsync_TranslatesS3StatusSafely(
        HttpStatusCode status,
        StorageFailureKind expectedKind)
    {
        const string providerBody = "configured-provider-response-body";
        const string innerText = "configured-inner-exception-text";
        StorageObjectKey key = StorageObjectKey.Parse("sample.wav");
        Mock<IAmazonS3> s3 = new(MockBehavior.Strict);
        s3.Setup(client => client.GetObjectMetadataAsync(
                It.IsAny<GetObjectMetadataRequest>(),
                TestContext.CancellationToken))
            .ThrowsAsync(CreateException(
                status,
                providerBody,
                new InvalidOperationException(innerText)));
        S3ObjectStorageClient client = CreateClient(s3);

        ObjectStorageException exception = await Assert.ThrowsExactlyAsync<ObjectStorageException>(() =>
            client.DeleteIfExistsAsync(key, TestContext.CancellationToken));

        Assert.AreEqual(expectedKind, exception.Kind);
        Assert.AreEqual("recordings-resource", exception.ResourceName);
        Assert.AreSame(key, exception.Key);
        Assert.DoesNotContain(providerBody, exception.Message);
        Assert.DoesNotContain(innerText, exception.Message);
        s3.VerifyAll();
    }

    [TestMethod]
    public async Task DeleteIfExistsAsync_WhenCallerCancels_PropagatesOperationCanceledException()
    {
        StorageObjectKey key = StorageObjectKey.Parse("sample.wav");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Mock<IAmazonS3> s3 = new(MockBehavior.Strict);
        s3.Setup(client => client.GetObjectMetadataAsync(
                It.IsAny<GetObjectMetadataRequest>(),
                cancellation.Token))
            .Returns(Task.FromCanceled<GetObjectMetadataResponse>(cancellation.Token));
        S3ObjectStorageClient client = CreateClient(s3);

        TaskCanceledException exception = await Assert.ThrowsExactlyAsync<TaskCanceledException>(() =>
            client.DeleteIfExistsAsync(key, cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        s3.VerifyAll();
    }

    [TestMethod]
    public void GetObjectUri_ReturnsS3UriWithSeparatelyEscapedPathSegments()
    {
        Mock<IAmazonS3> s3 = new(MockBehavior.Strict);
        S3ObjectStorageClient client = CreateClient(s3, prefix: "prefix");

        Uri uri = client.GetObjectUri(
            StorageObjectKey.Parse("escaped name.pdf"));

        Assert.AreEqual(
            "s3://recordings/prefix/escaped%20name.pdf",
            uri.AbsoluteUri);
    }

    [TestMethod]
    public void Dispose_DisposesAmazonS3Client()
    {
        Mock<IAmazonS3> s3 = new(MockBehavior.Strict);
        s3.Setup(client => client.Dispose());
        S3ObjectStorageClient client = CreateClient(s3);

        client.Dispose();

        s3.VerifyAll();
    }

    private static S3ObjectStorageClient CreateClient(
        Mock<IAmazonS3> s3,
        string prefix = "") =>
        new("recordings-resource", s3.Object, "recordings", prefix);

    private static async Task AssertUnavailableProviderCancellationAsync(
        Func<Task> operation,
        StorageObjectKey expectedKey,
        string providerMessage)
    {
        ObjectStorageException exception =
            await Assert.ThrowsExactlyAsync<ObjectStorageException>(operation);

        Assert.AreEqual(StorageFailureKind.Unavailable, exception.Kind);
        Assert.AreEqual("recordings-resource", exception.ResourceName);
        Assert.AreSame(expectedKey, exception.Key);
        Assert.IsInstanceOfType<OperationCanceledException>(
            exception.InnerException);
        Assert.DoesNotContain(providerMessage, exception.Message);
    }

    private static AmazonS3Exception CreateException(
        HttpStatusCode status,
        string message = "provider-message",
        Exception? innerException = null,
        string errorCode = "ProviderErrorCode") =>
        new(
            message,
            innerException ?? new InvalidOperationException("provider-inner"),
            ErrorType.Unknown,
            errorCode,
            "provider-request-id",
            status);

    private sealed class DisposeCountingStream(byte[] buffer) : MemoryStream(buffer)
    {
        public int DisposeCount { get; private set; }

        protected override void Dispose(bool disposing)
        {
            DisposeCount++;
            base.Dispose(disposing);
        }
    }

    public TestContext TestContext { get; set; } = null!;
}
