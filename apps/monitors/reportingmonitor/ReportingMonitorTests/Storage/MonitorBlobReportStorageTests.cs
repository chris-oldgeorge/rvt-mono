using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Storage;
using Rvt.Storage;

namespace ReportingMonitorTests.Storage;

public sealed class MonitorBlobReportStorageTests
{
    [Fact]
    public async Task StoreAsync_WritesRenderedReportThroughNamedStreamingClientAndReturnsResolverUri()
    {
        var client = new RecordingObjectStorageClient();
        var factory = new RecordingObjectStorageClientFactory(client);
        var resolvedUri = new Uri("s3://report-bucket/rvtreports/report.pdf");
        var resolver = new RecordingReportObjectUriResolver(resolvedUri);
        var storage = new MonitorBlobReportStorage(factory, resolver);
        var report = new RenderedReport("report.pdf", "application/pdf", [1, 2, 3]);
        using var cancellation = new CancellationTokenSource();

        Uri uri = await storage.StoreAsync(report, cancellation.Token);

        Assert.Equal(resolvedUri, uri);
        Assert.Equal(ReportingStorageResourceNames.Reports, factory.ResourceName);
        Assert.Equal("report.pdf", client.Request?.Key.Value);
        Assert.Equal("application/pdf", client.Request?.ContentType);
        Assert.Equal([1, 2, 3], client.Content);
        Assert.Equal(cancellation.Token, client.CancellationToken);
        Assert.Equal("report.pdf", resolver.Key?.Value);
    }

    [Fact]
    public async Task StoreAsync_ResolvesTheProviderResultKeyWithoutAProviderUriSurface()
    {
        var client = new RecordingObjectStorageClient
        {
            Result = new StorageWriteResult(StorageObjectKey.Parse("provider-result.pdf")),
        };
        var resolvedUri = new Uri("https://storage.example.test/rvtreports/provider-result.pdf");
        var resolver = new RecordingReportObjectUriResolver(resolvedUri);
        var storage = new MonitorBlobReportStorage(
            new RecordingObjectStorageClientFactory(client),
            resolver);

        Uri uri = await storage.StoreAsync(
            new RenderedReport("report.pdf", "application/pdf", [1]),
            CancellationToken.None);

        Assert.Equal(resolvedUri, uri);
        Assert.Equal("provider-result.pdf", resolver.Key?.Value);
        Assert.Equal(
            ["Key"],
            typeof(StorageWriteResult)
                .GetProperties()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
    }

    private sealed class RecordingObjectStorageClientFactory(IObjectStorageClient client)
        : IObjectStorageClientFactory
    {
        public string? ResourceName { get; private set; }

        public IObjectStorageClient GetRequiredClient(string resourceName)
        {
            ResourceName = resourceName;
            return client;
        }
    }

    private sealed class RecordingObjectStorageClient : IObjectStorageClient
    {
        public Uri GetObjectUri(StorageObjectKey key) =>
            new($"https://reports.example.test/{key.Value}");

        public StorageWriteRequest? Request { get; private set; }

        public byte[]? Content { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public StorageWriteResult Result { get; init; } =
            new(StorageObjectKey.Parse("report.pdf"));

        public async Task<StorageWriteResult> WriteAsync(
            StorageWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            CancellationToken = cancellationToken;
            using var buffer = new MemoryStream();
            await request.Content.CopyToAsync(buffer, cancellationToken);
            Content = buffer.ToArray();
            return Result;
        }

        public Task<StorageReadResult?> OpenReadAsync(
            StorageObjectKey key,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteIfExistsAsync(
            StorageObjectKey key,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingReportObjectUriResolver(Uri uri) : IReportObjectUriResolver
    {
        public StorageObjectKey? Key { get; private set; }

        public Uri Resolve(StorageObjectKey key)
        {
            Key = key;
            return uri;
        }
    }
}
