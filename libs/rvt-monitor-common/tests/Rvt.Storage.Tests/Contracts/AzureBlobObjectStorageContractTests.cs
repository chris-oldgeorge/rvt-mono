using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Moq;
using Rvt.Storage.AzureBlob;

namespace Rvt.Storage.Tests.Contracts;

[TestClass]
public sealed class AzureBlobObjectStorageContractTests : ObjectStorageClientContractTests
{
    protected override Task<IObjectStorageClientFixture> CreateFixtureAsync() =>
        Task.FromResult<IObjectStorageClientFixture>(new AzureBlobFixture());

    private sealed class AzureBlobFixture : IObjectStorageClientFixture
    {
        private readonly Mock<BlobContainerClient> container =
            new(MockBehavior.Strict);
        private readonly Dictionary<string, StoredObject> objects =
            new(StringComparer.Ordinal);

        public AzureBlobFixture()
        {
            container
                .Setup(client => client.GetBlobClient(It.IsAny<string>()))
                .Returns((string providerKey) => CreateBlobClient(providerKey));
            container
                .Setup(client => client.CreateIfNotExistsAsync(
                    PublicAccessType.None,
                    null,
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns((
                    PublicAccessType _,
                    IDictionary<string, string>? _,
                    BlobContainerEncryptionScopeOptions? _,
                    CancellationToken cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult((Response<BlobContainerInfo>)null!);
                });
            Client = new AzureBlobObjectStorageClient(
                "contract-recordings",
                container.Object,
                "fixture-root");
        }

        public IObjectStorageClient Client { get; }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private BlobClient CreateBlobClient(string providerKey)
        {
            var blob = new Mock<BlobClient>(MockBehavior.Strict);
            blob
                .Setup(client => client.UploadAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<BlobUploadOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    Stream content,
                    BlobUploadOptions options,
                    CancellationToken cancellationToken) =>
                    StoreAsync(providerKey, content, options, cancellationToken));
            blob
                .Setup(client => client.DownloadStreamingAsync(
                    It.IsAny<BlobDownloadOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    BlobDownloadOptions _,
                    CancellationToken cancellationToken) =>
                    DownloadAsync(providerKey, cancellationToken));
            blob
                .Setup(client => client.DeleteIfExistsAsync(
                    DeleteSnapshotsOption.None,
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns((
                    DeleteSnapshotsOption _,
                    BlobRequestConditions? _,
                    CancellationToken cancellationToken) =>
                    DeleteAsync(providerKey, cancellationToken));
            return blob.Object;
        }

        private async Task<Response<BlobContentInfo>> StoreAsync(
            string providerKey,
            Stream content,
            BlobUploadOptions options,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            objects[providerKey] = new StoredObject(
                buffer.ToArray(),
                options.HttpHeaders?.ContentType);
            return null!;
        }

        private Task<Response<BlobDownloadStreamingResult>> DownloadAsync(
            string providerKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!objects.TryGetValue(providerKey, out var storedObject))
            {
                return Task.FromException<Response<BlobDownloadStreamingResult>>(
                    new RequestFailedException(404, "Object is missing."));
            }

            var content = new MemoryStream(storedObject.Content, writable: false);
            var details = BlobsModelFactory.BlobDownloadDetails(
                contentLength: storedObject.Content.Length,
                contentType: storedObject.ContentType);
            var result = BlobsModelFactory.BlobDownloadStreamingResult(content, details);
            var rawResponse = new Mock<Response>(MockBehavior.Strict);
            rawResponse.Setup(response => response.Dispose());
            return Task.FromResult(Response.FromValue(result, rawResponse.Object));
        }

        private Task<Response<bool>> DeleteAsync(
            string providerKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var deleted = objects.Remove(providerKey);
            return Task.FromResult(
                Response.FromValue(deleted, new Mock<Response>(MockBehavior.Strict).Object));
        }

        private sealed record StoredObject(byte[] Content, string? ContentType);
    }
}
