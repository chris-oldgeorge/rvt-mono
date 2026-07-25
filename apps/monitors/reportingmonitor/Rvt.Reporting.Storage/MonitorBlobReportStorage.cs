using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;
using Rvt.Storage;

namespace Rvt.Reporting.Storage;

public sealed class MonitorBlobReportStorage(
    IObjectStorageClientFactory storageClients,
    IReportObjectUriResolver uriResolver) : IReportStorage
{
    public async Task<Uri> StoreAsync(RenderedReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        var client = storageClients.GetRequiredClient(
            ReportingStorageResourceNames.Reports);
        await using var stream = new MemoryStream(report.Content, writable: false);
        var result = await client.WriteAsync(
            new StorageWriteRequest(
                StorageObjectKey.Parse(report.FileName),
                stream,
                report.ContentType),
            cancellationToken).ConfigureAwait(false);
        return uriResolver.Resolve(result.Key);
    }
}
