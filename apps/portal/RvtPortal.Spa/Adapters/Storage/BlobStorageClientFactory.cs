// File summary: Creates Azure Blob container clients for portal storage adapters.
// Major updates:
// - 2026-07-23 Centralized connection-string and managed-identity storage client construction.

using Azure.Identity;
using Azure.Storage.Blobs;

namespace RvtPortal.Spa.Adapters.Storage;

public interface IBlobStorageClientFactory
{
    // Function summary: Returns a container client from the configured storage mode, or null when blob storage is disabled.
    BlobContainerClient? CreateContainerClient(string containerName);
}

public sealed class BlobStorageClientFactory : IBlobStorageClientFactory
{
    private readonly IConfiguration configuration;

    // Function summary: Initializes blob-client construction from host configuration.
    public BlobStorageClientFactory(IConfiguration configuration) => this.configuration = configuration;

    public BlobContainerClient? CreateContainerClient(string containerName)
    {
        var connectionString = configuration["BlobStorage:blobConnectionString"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new BlobContainerClient(connectionString, containerName);
        }

        var serviceUri = configuration["BlobStorage:blobServiceUri"];
        return Uri.TryCreate(serviceUri, UriKind.Absolute, out var uri)
            ? new BlobServiceClient(uri, new DefaultAzureCredential()).GetBlobContainerClient(containerName)
            : null;
    }
}
