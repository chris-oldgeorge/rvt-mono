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
        var content = new MemoryStream([1, 2, 3], writable: false);
        var key = StorageObjectKey.Parse("clips/sample.wav");
        var cancellationToken = new CancellationTokenSource().Token;
        var s3 = new Mock<IAmazonS3>(MockBehavior.Strict);
        PutObjectRequest? capturedRequest = null;
        s3.Setup(client => client.PutObjectAsync(
                It.IsAny<PutObjectRequest>(),
                cancellationToken))
            .Callback<PutObjectRequest, CancellationToken>(
                (request, _) => capturedRequest = request)
            .ReturnsAsync(new PutObjectResponse());
        var client = CreateClient(s3, prefix: "tenant-a");

        var result = await client.WriteAsync(
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
    public async Task OpenReadAsync_ReturnsStreamingContentMetadataAndResponseLease()
    {
        var content = new DisposeCountingStream([4, 5, 6]);
        var key = StorageObjectKey.Parse("sample.wav");
        var response = new GetObjectResponse
        {
            ResponseStream = content,
        };
        response.Headers.ContentType = "audio/wav";
        response.Headers.ContentLength = 3;
        var s3 = new Mock<IAmazonS3>(MockBehavior.Strict);
        s3.Setup(client => client.GetObjectAsync(
                It.Is<GetObjectRequest>(request =>
                    request.BucketName == "recordings"
                    && request.Key == "tenant-a/sample.wav"),
                CancellationToken.None))
            .ReturnsAsync(response);
        var client = CreateClient(s3, prefix: "tenant-a");

        var result = await client.OpenReadAsync(key);

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
        var key = StorageObjectKey.Parse("missing.wav");
        var s3 = new Mock<IAmazonS3>(MockBehavior.Strict);
        s3.Setup(client => client.GetObjectAsync(
                It.IsAny<GetObjectRequest>(),
                CancellationToken.None))
            .ThrowsAsync(CreateException(
                HttpStatusCode.BadRequest,
                errorCode: "NoSuchKey"));
        var client = CreateClient(s3);

        var result = await client.OpenReadAsync(key);

        Assert.IsNull(result);
        s3.VerifyAll();
    }

    [TestMethod]
    public async Task OpenReadAsync_WhenS3Returns404_ReturnsNull()
    {
        var key = StorageObjectKey.Parse("missing.wav");
        var s3 = new Mock<IAmazonS3>(MockBehavior.Strict);
        s3.Setup(client => client.GetObjectAsync(
                It.IsAny<GetObjectRequest>(),
                CancellationToken.None))
            .ThrowsAsync(CreateException(
                HttpStatusCode.NotFound,
                errorCode: "ProviderNotFound"));
        var client = CreateClient(s3);

        var result = await client.OpenReadAsync(key);

        Assert.IsNull(result);
        s3.VerifyAll();
    }

    [TestMethod]
    public async Task DeleteIfExistsAsync_WhenMetadataIsMissing_ReturnsFalseWithoutDelete()
    {
        var key = StorageObjectKey.Parse("missing.wav");
        var s3 = new Mock<IAmazonS3>(MockBehavior.Strict);
        s3.Setup(client => client.GetObjectMetadataAsync(
                It.Is<GetObjectMetadataRequest>(request =>
                    request.BucketName == "recordings"
                    && request.Key == "tenant-a/missing.wav"),
                CancellationToken.None))
            .ThrowsAsync(CreateException(
                HttpStatusCode.NotFound,
                errorCode: "NoSuchKey"));
        var client = CreateClient(s3, prefix: "tenant-a");

        var deleted = await client.DeleteIfExistsAsync(key);

        Assert.IsFalse(deleted);
        s3.VerifyAll();
    }

    [TestMethod]
    public async Task DeleteIfExistsAsync_WhenMetadataExists_DeletesExpectedObject()
    {
        var key = StorageObjectKey.Parse("sample.wav");
        var cancellationToken = new CancellationTokenSource().Token;
        var s3 = new Mock<IAmazonS3>(MockBehavior.Strict);
        var sequence = new MockSequence();
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
        var client = CreateClient(s3, prefix: "tenant-a");

        var deleted = await client.DeleteIfExistsAsync(key, cancellationToken);

        Assert.IsTrue(deleted);
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
        var key = StorageObjectKey.Parse("sample.wav");
        var s3 = new Mock<IAmazonS3>(MockBehavior.Strict);
        s3.Setup(client => client.GetObjectMetadataAsync(
                It.IsAny<GetObjectMetadataRequest>(),
                CancellationToken.None))
            .ThrowsAsync(CreateException(
                status,
                providerBody,
                new InvalidOperationException(innerText)));
        var client = CreateClient(s3);

        var exception = await Assert.ThrowsExactlyAsync<ObjectStorageException>(() =>
            client.DeleteIfExistsAsync(key));

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
        var key = StorageObjectKey.Parse("sample.wav");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var s3 = new Mock<IAmazonS3>(MockBehavior.Strict);
        s3.Setup(client => client.GetObjectMetadataAsync(
                It.IsAny<GetObjectMetadataRequest>(),
                cancellation.Token))
            .Returns(Task.FromCanceled<GetObjectMetadataResponse>(cancellation.Token));
        var client = CreateClient(s3);

        var exception = await Assert.ThrowsExactlyAsync<TaskCanceledException>(() =>
            client.DeleteIfExistsAsync(key, cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        s3.VerifyAll();
    }

    [TestMethod]
    public void GetObjectUri_ReturnsS3UriWithSeparatelyEscapedPathSegments()
    {
        var s3 = new Mock<IAmazonS3>(MockBehavior.Strict);
        var client = CreateClient(s3, prefix: "prefix");

        var uri = client.GetObjectUri(
            StorageObjectKey.Parse("escaped name.pdf"));

        Assert.AreEqual(
            "s3://recordings/prefix/escaped%20name.pdf",
            uri.AbsoluteUri);
    }

    [TestMethod]
    public void Dispose_DisposesAmazonS3Client()
    {
        var s3 = new Mock<IAmazonS3>(MockBehavior.Strict);
        s3.Setup(client => client.Dispose());
        var client = CreateClient(s3);

        client.Dispose();

        s3.VerifyAll();
    }

    private static S3ObjectStorageClient CreateClient(
        Mock<IAmazonS3> s3,
        string prefix = "") =>
        new("recordings-resource", s3.Object, "recordings", prefix);

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
}
