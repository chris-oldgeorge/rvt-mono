using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;

namespace Rvt.Reporting.Storage.AzureBlob;

/// <summary>
/// Stores generated report PDFs in Azure Blob Storage using managed identity or a local connection string.
/// Major updates: 2026-06-24 extracted Blob upload behind ACS-compatible storage adapter.
/// </summary>
public sealed class AzureBlobReportStorage : IReportStorage
{
    private readonly AzureBlobReportStorageOptions _options;

    public AzureBlobReportStorage(IOptions<AzureBlobReportStorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<Uri> StoreAsync(RenderedReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        var containerClient = CreateContainerClient();
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken).ConfigureAwait(false);

        var blobName = $"{_options.ReportPrefix.TrimEnd('/')}/{report.FileName}";
        var blobClient = containerClient.GetBlobClient(blobName);
        await using var stream = new MemoryStream(report.Content, writable: false);
        await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = report.ContentType }, cancellationToken: cancellationToken).ConfigureAwait(false);
        return blobClient.Uri;
    }

    private BlobContainerClient CreateContainerClient()
    {
        if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            return new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
        }

        if (!string.IsNullOrWhiteSpace(_options.ServiceUri))
        {
            return new BlobContainerClient(new Uri($"{_options.ServiceUri.TrimEnd('/')}/{_options.ContainerName}"), new DefaultAzureCredential());
        }

        throw new InvalidOperationException("Blob storage must be configured with a service URI or connection string.");
    }
}

public sealed class AzureBlobReportStorageOptions
{
    public string? ServiceUri { get; set; }

    public string? ConnectionString { get; set; }

    public string ContainerName { get; set; } = "pdfreports";

    public string ReportPrefix { get; set; } = "rvtreports";
}
