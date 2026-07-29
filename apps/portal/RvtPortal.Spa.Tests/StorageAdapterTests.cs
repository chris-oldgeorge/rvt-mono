// File summary: Verifies storage adapters preserve protected content across failed side-effect boundaries.
// Major updates:
// - 2026-07-08 pending Added failed-replacement coverage for customer-logo storage.

using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using RVT.BusinessLogic.Ports.Storage;
using RvtPortal.Application.Sites.Ports;
using RvtPortal.Spa.Adapters.Sites;
using RvtPortal.Spa.Adapters.Storage;

namespace RvtPortal.Spa.Tests;

public sealed class StorageAdapterTests
{
    private static readonly byte[] _pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Theory]
    [InlineData("BlobStorage:blobConnectionString", "UseDevelopmentStorage=true")]
    [InlineData("BlobStorage:blobServiceUri", "https://rvtstorage.blob.core.windows.net")]
    // Function summary: Verifies both supported storage authentication modes create container clients through one factory.
    public void BlobStorageClientFactory_CreatesContainerClientForConfiguredStorageMode(string key, string value)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();

        BlobContainerClient? container = new BlobStorageClientFactory(configuration).CreateContainerClient("site-archives");

        Assert.NotNull(container);
        Assert.EndsWith("/site-archives", container.Uri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SiteLogoAdapter_MapsStorageContractsAndValidation()
    {
        RecordingCustomerLogoStorage storage = new();
        SiteLogoAdapter adapter = new(storage);
        Guid siteId = Guid.NewGuid();
        await using MemoryStream content = new(PngPayload(1, 2, 3));

        SiteLogoSaveResult invalid = await adapter.SaveAsync(
            siteId,
            new SiteLogoUpload(
                content,
                content.Length,
                "image/png",
                "customer-logo.png"),
            CancellationToken.None);

        Assert.Equal(SiteLogoSaveOutcome.Invalid, invalid.Outcome);
        Assert.Equal("invalid image", invalid.Message);
        Assert.NotNull(storage.UploadStream);
        Assert.Equal(0x89, storage.UploadStream.ReadByte());
        storage.UploadStream.Dispose();
        Assert.True(content.CanRead);
        Assert.True(await adapter.ExistsAsync(siteId, CancellationToken.None));

        SiteLogoFile? file = await adapter.OpenReadAsync(siteId, CancellationToken.None);
        Assert.NotNull(file);
        Assert.Same(storage.StoredStream, file.Content);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal("stored-logo.png", file.FileName);
    }

    [Fact]
    // Function summary: Verifies failed logo replacement keeps the previously stored logo intact.
    public async Task CustomerLogoStorage_PreservesExistingLogoWhenReplacementCopyFails()
    {
        string contentRoot = Path.Combine(Path.GetTempPath(), $"rvt-logo-storage-{Guid.NewGuid():N}");
        try
        {
            Guid siteId = Guid.NewGuid();
            CustomerLogoStorage storage = new(new TestWebHostEnvironment(contentRoot));
            byte[] originalBytes = PngPayload(1, 2, 3, 4);
            await storage.SaveAsync(
                siteId,
                new MemoryUploadedContent("old-logo.png", "image/png", originalBytes),
                CancellationToken.None);

            await Assert.ThrowsAsync<IOException>(() => storage.SaveAsync(
                siteId,
                new ThrowingUploadedContent("new-logo.png", "image/png", PngPayload(9, 8, 7, 6)),
                CancellationToken.None));

            StoredContentFile? stored = await storage.OpenReadAsync(siteId, CancellationToken.None);
            Assert.NotNull(stored);
            await using Stream storedStream = stored.Stream;
            using MemoryStream buffer = new();
            await storedStream.CopyToAsync(buffer);
            Assert.Equal(originalBytes, buffer.ToArray());
        }
        finally
        {
            if (Directory.Exists(contentRoot))
            {
                Directory.Delete(contentRoot, recursive: true);
            }
        }
    }

    // Function summary: Builds a valid PNG payload with caller-supplied body bytes.
    private static byte[] PngPayload(params byte[] body)
    {
        return [.. _pngHeader, .. body];
    }

    private sealed class MemoryUploadedContent : IUploadedContent
    {
        private readonly byte[] bytes;

        public MemoryUploadedContent(string fileName, string contentType, byte[] bytes)
        {
            FileName = fileName;
            ContentType = contentType;
            this.bytes = bytes;
        }

        public string FileName { get; }
        public string ContentType { get; }
        public long Length => bytes.Length;

        // Function summary: Opens the in-memory upload payload for storage validation.
        public Stream OpenReadStream()
        {
            return new MemoryStream(bytes, writable: false);
        }

        // Function summary: Copies the in-memory upload payload to adapter-owned storage.
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken)
        {
            return target.WriteAsync(bytes, cancellationToken).AsTask();
        }
    }

    private sealed class ThrowingUploadedContent : IUploadedContent
    {
        private readonly byte[] bytes;

        public ThrowingUploadedContent(string fileName, string contentType, byte[] bytes)
        {
            FileName = fileName;
            ContentType = contentType;
            this.bytes = bytes;
        }

        public string FileName { get; }
        public string ContentType { get; }
        public long Length => bytes.Length;

        // Function summary: Opens a valid image header so the test reaches the copy failure boundary.
        public Stream OpenReadStream()
        {
            return new MemoryStream(bytes, writable: false);
        }

        // Function summary: Simulates a storage write failure after validation succeeds.
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken)
        {
            throw new IOException("Simulated storage copy failure.");
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            WebRootPath = Path.Combine(contentRootPath, "wwwroot");
            ContentRootFileProvider = new NullFileProvider();
            WebRootFileProvider = new NullFileProvider();
        }

        public string ApplicationName { get; set; } = "RvtPortal.Spa.Tests";
        public IFileProvider ContentRootFileProvider { get; set; }
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; } = "Testing";
        public IFileProvider WebRootFileProvider { get; set; }
        public string WebRootPath { get; set; }
    }

    private sealed class RecordingCustomerLogoStorage : ICustomerLogoStorage
    {
        public Stream? UploadStream { get; private set; }
        public Stream StoredStream { get; } = new MemoryStream(_pngHeader);

        public Task SaveAsync(
            Guid siteId,
            IUploadedContent logo,
            CancellationToken cancellationToken)
        {
            UploadStream = logo.OpenReadStream();
            throw new StorageValidationException("invalid image");
        }

        public Task<StoredContentFile?> OpenReadAsync(
            Guid siteId,
            CancellationToken cancellationToken) =>
            Task.FromResult<StoredContentFile?>(
                new(StoredStream, "image/png", "stored-logo.png"));

        public Task DeleteAsync(
            Guid siteId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public string? BuildProtectedLink(Guid siteId) =>
            $"/api/sites/{siteId}/customer-logo";
    }
}
