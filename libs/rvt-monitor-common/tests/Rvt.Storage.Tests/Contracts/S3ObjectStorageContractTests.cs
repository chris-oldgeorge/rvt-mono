using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Moq;
using Rvt.Storage.S3;

namespace Rvt.Storage.Tests.Contracts;

[TestClass]
public sealed class S3ObjectStorageContractTests : ObjectStorageClientContractTests
{
    protected override Task<IObjectStorageClientFixture> CreateFixtureAsync() =>
        Task.FromResult<IObjectStorageClientFixture>(new S3Fixture());

    private sealed class S3Fixture : IObjectStorageClientFixture
    {
        private readonly Mock<IAmazonS3> s3 = new(MockBehavior.Strict);
        private readonly Dictionary<string, StoredObject> objects =
            new(StringComparer.Ordinal);
        private readonly S3ObjectStorageClient client;

        public S3Fixture()
        {
            s3.Setup(sdk => sdk.PutObjectAsync(
                    It.IsAny<PutObjectRequest>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    PutObjectRequest request,
                    CancellationToken cancellationToken) =>
                    StoreAsync(request, cancellationToken));
            s3.Setup(sdk => sdk.GetObjectAsync(
                    It.IsAny<GetObjectRequest>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    GetObjectRequest request,
                    CancellationToken cancellationToken) =>
                    DownloadAsync(request, cancellationToken));
            s3.Setup(sdk => sdk.GetObjectMetadataAsync(
                    It.IsAny<GetObjectMetadataRequest>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    GetObjectMetadataRequest request,
                    CancellationToken cancellationToken) =>
                    GetMetadataAsync(request, cancellationToken));
            s3.Setup(sdk => sdk.DeleteObjectAsync(
                    It.IsAny<DeleteObjectRequest>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    DeleteObjectRequest request,
                    CancellationToken cancellationToken) =>
                    DeleteAsync(request, cancellationToken));
            s3.Setup(sdk => sdk.Dispose());
            client = new S3ObjectStorageClient(
                "contract-recordings",
                s3.Object,
                "contract-tests",
                "fixture-root");
        }

        public IObjectStorageClient Client => client;

        public ValueTask DisposeAsync()
        {
            client.Dispose();
            return ValueTask.CompletedTask;
        }

        private async Task<PutObjectResponse> StoreAsync(
            PutObjectRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var buffer = new MemoryStream();
            await request.InputStream.CopyToAsync(buffer, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            objects[GetObjectIdentity(request.BucketName, request.Key)] =
                new StoredObject(buffer.ToArray(), request.ContentType);
            return new PutObjectResponse();
        }

        private Task<GetObjectResponse> DownloadAsync(
            GetObjectRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!objects.TryGetValue(
                    GetObjectIdentity(request.BucketName, request.Key),
                    out var storedObject))
            {
                return Task.FromException<GetObjectResponse>(CreateMissingException());
            }

            var response = new GetObjectResponse
            {
                ResponseStream = new MemoryStream(storedObject.Content, writable: false),
            };
            response.Headers.ContentLength = storedObject.Content.Length;
            if (storedObject.ContentType is not null)
            {
                response.Headers.ContentType = storedObject.ContentType;
            }

            return Task.FromResult(response);
        }

        private Task<GetObjectMetadataResponse> GetMetadataAsync(
            GetObjectMetadataRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return objects.ContainsKey(GetObjectIdentity(request.BucketName, request.Key))
                ? Task.FromResult(new GetObjectMetadataResponse())
                : Task.FromException<GetObjectMetadataResponse>(CreateMissingException());
        }

        private Task<DeleteObjectResponse> DeleteAsync(
            DeleteObjectRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            objects.Remove(GetObjectIdentity(request.BucketName, request.Key));
            return Task.FromResult(new DeleteObjectResponse());
        }

        private static string GetObjectIdentity(string bucket, string key) =>
            $"{bucket}\n{key}";

        private static AmazonS3Exception CreateMissingException() =>
            new(
                "Object is missing.",
                new InvalidOperationException("No object exists."),
                ErrorType.Unknown,
                "NoSuchKey",
                "contract-request-id",
                HttpStatusCode.NotFound);

        private sealed record StoredObject(byte[] Content, string? ContentType);
    }
}
