using Rvt.Monitor.Common.Storage;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;

namespace Rvt.Reporting.Storage;

public sealed class MonitorBlobReportStorage(IBlobStorageService blobStorage) : IReportStorage
{
    public async Task<Uri> StoreAsync(RenderedReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        var result = await blobStorage.WriteAsync(
            new BlobStorageWriteRequest(report.FileName, report.Content, report.ContentType),
            cancellationToken).ConfigureAwait(false);

        return Uri.TryCreate(result.Uri, UriKind.Absolute, out var uri)
            ? uri
            : throw new InvalidOperationException(
                "The blob storage provider did not return an absolute report URI.");
    }
}

