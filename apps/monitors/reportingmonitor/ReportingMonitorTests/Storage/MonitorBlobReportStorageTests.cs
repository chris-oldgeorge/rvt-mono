using Rvt.Monitor.Common.Storage;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Storage;

namespace ReportingMonitorTests.Storage;

public sealed class MonitorBlobReportStorageTests
{
    [Fact]
    public async Task StoreAsync_WritesRenderedReportThroughCommonBlobService()
    {
        var blobStorage = new RecordingBlobStorageService
        {
            Result = new BlobStorageWriteResult(
                "report.pdf",
                "https://storage.example.test/rvtreports/report.pdf")
        };
        var storage = new MonitorBlobReportStorage(blobStorage);
        var report = new RenderedReport("report.pdf", "application/pdf", [1, 2, 3]);

        var uri = await storage.StoreAsync(report, CancellationToken.None);

        Assert.Equal("https://storage.example.test/rvtreports/report.pdf", uri.AbsoluteUri);
        var request = Assert.IsType<BlobStorageWriteRequest>(blobStorage.Request);
        Assert.Equal("report.pdf", request.ObjectName);
        Assert.Equal("application/pdf", request.ContentType);
        Assert.Equal([1, 2, 3], request.Content);
    }

    [Fact]
    public async Task StoreAsync_ThrowsWhenProviderDoesNotReturnAnAbsoluteUri()
    {
        var blobStorage = new RecordingBlobStorageService
        {
            Result = new BlobStorageWriteResult("report.pdf")
        };
        var storage = new MonitorBlobReportStorage(blobStorage);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.StoreAsync(
                new RenderedReport("report.pdf", "application/pdf", [1]),
                CancellationToken.None));

        Assert.Contains("absolute report URI", exception.Message, StringComparison.Ordinal);
    }

    private sealed class RecordingBlobStorageService : IBlobStorageService
    {
        public BlobStorageWriteRequest? Request { get; private set; }

        public BlobStorageWriteResult Result { get; init; } = new("report.pdf");

        public Task<BlobStorageWriteResult> WriteAsync(
            BlobStorageWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(Result);
        }

        public Task DeleteAsync(string objectName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

