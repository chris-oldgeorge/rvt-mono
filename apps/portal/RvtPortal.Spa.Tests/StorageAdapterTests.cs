// File summary: Verifies storage adapters preserve protected content across failed side-effect boundaries.
// Major updates:
// - 2026-07-08 pending Added failed-replacement coverage for customer-logo storage.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using RVT.BusinessLogic.Ports.Storage;
using RvtPortal.Spa.Adapters.Storage;

namespace RvtPortal.Spa.Tests;

public sealed class StorageAdapterTests
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    // Function summary: Verifies failed logo replacement keeps the previously stored logo intact.
    public async Task CustomerLogoStorage_PreservesExistingLogoWhenReplacementCopyFails()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"rvt-logo-storage-{Guid.NewGuid():N}");
        try
        {
            var siteId = Guid.NewGuid();
            var storage = new CustomerLogoStorage(new TestWebHostEnvironment(contentRoot));
            var originalBytes = PngPayload(1, 2, 3, 4);
            await storage.SaveAsync(
                siteId,
                new MemoryUploadedContent("old-logo.png", "image/png", originalBytes),
                CancellationToken.None);

            await Assert.ThrowsAsync<IOException>(() => storage.SaveAsync(
                siteId,
                new ThrowingUploadedContent("new-logo.png", "image/png", PngPayload(9, 8, 7, 6)),
                CancellationToken.None));

            var stored = await storage.OpenReadAsync(siteId, CancellationToken.None);
            Assert.NotNull(stored);
            await using var storedStream = stored.Stream;
            using var buffer = new MemoryStream();
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
        return [.. PngHeader, .. body];
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
}
