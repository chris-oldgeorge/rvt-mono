// File summary: Stores and streams monitor deployment pictures through protected API routes.
// Major updates:
// - 2026-07-08 pending Moved monitor-picture storage behind a business-layer storage port.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-09 pending Added local and Azure Blob-backed monitor picture storage for AKS-ready parity.
// - 2026-06-25 pending Derived stored and served content type from the validated extension instead of client input.
// - 2026-07-08 pending Added stored-picture delete support for failed database-save compensation.

using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using RVT.BusinessLogic.Ports.Storage;

namespace RvtPortal.Spa.Adapters.Storage;

public sealed class MonitorPictureStorage : IMonitorPictureStorage
{
    private const string LocalPrefix = "monitor-pictures/";
    private const string BlobPrefix = "blob://";
    private readonly IConfiguration configuration;
    private readonly IWebHostEnvironment environment;

    // Function summary: Initializes this type with storage configuration and host paths.
    public MonitorPictureStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        this.configuration = configuration;
        this.environment = environment;
    }

    public async Task<string> SaveAsync(Guid deploymentId, IUploadedContent picture, CancellationToken cancellationToken)
    {
        var fileName = $"{deploymentId:N}{Path.GetExtension(picture.FileName).ToLowerInvariant()}";
        var blobClient = BuildBlobContainerClient();
        if (blobClient != null)
        {
            await blobClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            var blob = blobClient.GetBlobClient(fileName);
            await using var input = picture.OpenReadStream();
            // Store the content type derived from the validated extension rather than
            // the client-supplied ContentType, which only had to start with "image/".
            await blob.UploadAsync(
                input,
                new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = ContentTypeForPicture(fileName) } },
                cancellationToken);
            return $"{BlobPrefix}{ContainerName()}/{fileName}";
        }

        var directory = Path.Combine(ContentRoot(), "App_Data", "monitor-pictures");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        await using var stream = File.Create(path);
        await picture.CopyToAsync(stream, cancellationToken);
        return $"{LocalPrefix}{fileName}";
    }

    public async Task DeleteAsync(string? storedLink, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storedLink))
        {
            return;
        }

        if (TryParseBlobReference(storedLink, out var blobName))
        {
            var blobClient = BuildBlobContainerClient()?.GetBlobClient(blobName);
            if (blobClient != null)
            {
                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            }

            return;
        }

        if (!IsLocalPictureLink(storedLink))
        {
            return;
        }

        var fileName = Path.GetFileName(storedLink);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var protectedPath = Path.Combine(ContentRoot(), "App_Data", "monitor-pictures", fileName);
        if (File.Exists(protectedPath))
        {
            File.Delete(protectedPath);
        }
    }

    public async Task<StoredContentFile?> OpenReadAsync(string? storedLink, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storedLink))
        {
            return null;
        }

        if (TryParseBlobReference(storedLink, out var blobName))
        {
            var blobClient = BuildBlobContainerClient()?.GetBlobClient(blobName);
            if (blobClient == null || !await blobClient.ExistsAsync(cancellationToken))
            {
                return null;
            }

            var download = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            // Serve the content type derived from the known extension allowlist rather than
            // trusting whatever was stored on the blob, so an unexpected stored content type
            // cannot be reflected back to the browser.
            return new StoredContentFile(download.Value.Content, ContentTypeForPicture(blobName), blobName);
        }

        if (!IsLocalPictureLink(storedLink))
        {
            return null;
        }

        var fileName = Path.GetFileName(storedLink);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var protectedPath = Path.Combine(ContentRoot(), "App_Data", "monitor-pictures", fileName);
        var path = File.Exists(protectedPath)
            ? protectedPath
            : LegacyStaticPath(fileName);
        if (path == null)
        {
            return null;
        }

        return new StoredContentFile(File.OpenRead(path), ContentTypeForPicture(path), fileName);
    }

    public string? BuildProtectedLink(Guid monitorId, string? storedLink)
    {
        if (string.IsNullOrWhiteSpace(storedLink))
        {
            return null;
        }

        return IsLocalPictureLink(storedLink) || TryParseBlobReference(storedLink, out _)
            ? $"/api/monitors/{monitorId}/picture"
            : storedLink;
    }

    private BlobContainerClient? BuildBlobContainerClient()
    {
        var connectionString = configuration["BlobStorage:blobConnectionString"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new BlobContainerClient(connectionString, ContainerName());
        }

        var serviceUri = configuration["BlobStorage:blobServiceUri"];
        return string.IsNullOrWhiteSpace(serviceUri)
            ? null
            : new BlobServiceClient(new Uri(serviceUri), new DefaultAzureCredential()).GetBlobContainerClient(ContainerName());
    }

    private string ContainerName()
    {
        return configuration["BlobStorage:MonitorImagesContainer"] ?? "monitor-pictures";
    }

    private string ContentRoot()
    {
        return string.IsNullOrWhiteSpace(environment.ContentRootPath)
            ? AppContext.BaseDirectory
            : environment.ContentRootPath;
    }

    private string? LegacyStaticPath(string fileName)
    {
        var webRoot = string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(AppContext.BaseDirectory, "wwwroot")
            : environment.WebRootPath;
        var legacyPath = Path.Combine(webRoot, "monitor-pictures", fileName);
        return File.Exists(legacyPath) ? legacyPath : null;
    }

    private static bool IsLocalPictureLink(string storedLink)
    {
        return storedLink.TrimStart('/').StartsWith(LocalPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseBlobReference(string storedLink, out string blobName)
    {
        blobName = "";
        if (!storedLink.StartsWith(BlobPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = storedLink[BlobPrefix.Length..];
        var separator = remainder.IndexOf('/', StringComparison.Ordinal);
        if (separator < 0 || separator == remainder.Length - 1)
        {
            return false;
        }

        blobName = remainder[(separator + 1)..];
        return !string.IsNullOrWhiteSpace(blobName);
    }

    private static string ContentTypeForPicture(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
