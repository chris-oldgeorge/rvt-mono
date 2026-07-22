using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Rvt.Monitor.Common.Storage;

public sealed class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient containerClient;
    private readonly string prefix;

    public AzureBlobStorageService(BlobStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Container))
        {
            throw new InvalidOperationException("Azure Blob storage requires a non-empty blob container.");
        }

        prefix = NormalizePrefix(options.Prefix);
        var serviceClient = CreateServiceClient(options);
        containerClient = serviceClient.GetBlobContainerClient(options.Container.Trim());
    }

    public async Task<BlobStorageWriteResult> WriteAsync(
        BlobStorageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var objectName = BlobObjectName.Normalize(request.ObjectName);
        var blobClient = containerClient.GetBlobClient(GetProviderObjectName(objectName));

        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await using var stream = new MemoryStream(request.Content, writable: false);
        var uploadOptions = new BlobUploadOptions();
        if (!string.IsNullOrWhiteSpace(request.ContentType))
        {
            uploadOptions.HttpHeaders = new BlobHttpHeaders { ContentType = request.ContentType };
        }

        await blobClient.UploadAsync(stream, uploadOptions, cancellationToken);
        return new BlobStorageWriteResult(objectName, blobClient.Uri.AbsoluteUri);
    }

    public async Task DeleteAsync(string objectName, CancellationToken cancellationToken = default)
    {
        var normalizedObjectName = BlobObjectName.Normalize(objectName);
        await containerClient
            .GetBlobClient(GetProviderObjectName(normalizedObjectName))
            .DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private static BlobServiceClient CreateServiceClient(BlobStorageOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.AzureConnectionString))
        {
            return new BlobServiceClient(options.AzureConnectionString);
        }

        if (!string.IsNullOrWhiteSpace(options.AzureServiceUri))
        {
            if (!Uri.TryCreate(options.AzureServiceUri, UriKind.Absolute, out var serviceUri))
            {
                throw new InvalidOperationException("RVT__BLOB_SERVICE_URI must be an absolute URI.");
            }

            return new BlobServiceClient(serviceUri, new DefaultAzureCredential());
        }

        throw new InvalidOperationException(
            "Azure Blob storage requires RVT__BLOB_CONNECTION_STRING or RVT__BLOB_SERVICE_URI.");
    }

    private string GetProviderObjectName(string objectName)
    {
        return string.IsNullOrEmpty(prefix) ? objectName : $"{prefix}/{objectName}";
    }

    private static string NormalizePrefix(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : BlobObjectName.Normalize(value);
    }
}
