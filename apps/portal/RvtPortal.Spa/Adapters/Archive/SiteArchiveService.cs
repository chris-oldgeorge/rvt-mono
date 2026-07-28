// File summary: Orchestrates site archive export creation, blob download, zipping, upload, and cleanup.
// Major updates:
// - 2026-07-09 pending Split archive SQL, provider execution, temp workspaces, and streamed CSV writing into dedicated components.
// - 2026-07-08 pending Added business archive port and removed configuration-reading parameterless construction.
// - 2026-06-29 pending Removed archive export dead assignments and stale comments for Sonar maintainability.
// - 2026-06-26 pending Scoped monitor-bound archive exports to effective deployment/contract ownership windows.
// - 2026-06-25 pending Escaped CSV export fields (RFC 4180) and neutralized spreadsheet formula injection.
// - 2026-06-25 pending Initialized BlobStorage string properties to satisfy non-nullable contract.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-08 pending Replaced concatenated archive SQL execution with EF Core parameterized query execution.
// - 2026-06-08 pending Updated archive export SQL to canonical database names for the naming refactor.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Added relational database support.
// - 2026-06-26 pending Awaited archive file/blob APIs to remove blocking async workflows.
// - 2026-07-08 pending Replaced console archive error output with trace logging during cleanup review.

using System.Data.Common;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using RvtPortal.Spa.Adapters.Storage;

namespace RvtPortal.Spa.Adapters.Archive;

public class BlobStorage
{
    public string blobConnectionString { get; set; } = string.Empty;
    public string MonitorImagesContainer { get; set; } = string.Empty;
    public string ArchiveContainer { get; set; } = string.Empty;
    public string ReportContainer { get; set; } = string.Empty;
    public string ReportFolder { get; set; } = string.Empty;
}

public interface ISiteArchiveService
{
    // Function summary: Builds a site archive export and returns its stored archive URL; throws if it fails.
    Task<string> Process(Guid siteId, CancellationToken cancellationToken);

    // Function summary: Reconciles the stable candidate against durable archive metadata.
    Task DeleteSupersededAsync(
        Guid siteId,
        string durableArchiveUrl,
        CancellationToken cancellationToken);
}

internal class SiteArchiveService : ISiteArchiveService
{
    private readonly ISiteArchiveQueryCatalog queryCatalog;
    private readonly ISiteArchiveQueryExecutor queryExecutor;
    private readonly ISiteArchiveCsvWriter csvWriter;
    private readonly ISiteArchiveWorkspaceFactory workspaceFactory;
    private readonly IBlobStorageClientFactory blobStorageClientFactory;
    private readonly BlobStorage config;

    // Function summary: Initializes this type with archive export collaborators resolved through dependency injection.
    public SiteArchiveService(
        ISiteArchiveQueryCatalog queryCatalog,
        ISiteArchiveQueryExecutor queryExecutor,
        ISiteArchiveCsvWriter csvWriter,
        ISiteArchiveWorkspaceFactory workspaceFactory,
        IBlobStorageClientFactory blobStorageClientFactory,
        IConfiguration configuration)
    {
        this.queryCatalog = queryCatalog;
        this.queryExecutor = queryExecutor;
        this.csvWriter = csvWriter;
        this.workspaceFactory = workspaceFactory;
        this.blobStorageClientFactory = blobStorageClientFactory;
        config = new BlobStorage();
        configuration.GetSection("BlobStorage").Bind(config);
    }

    // Function summary: Builds CSV archive files for a site, zips them, uploads the archive, and returns the archive URL.
    public async Task<string> Process(Guid siteId, CancellationToken cancellationToken)
    {
        // Failures propagate. A swallowed failure that returns an empty URL is worse than throwing: the caller
        // marks the site archived and reports success even though no archive exists. The caller decides what
        // a failed export means (ArchiveAsync returns 503 and does not archive). The workspace is disposed on
        // the way out either way.
        await using SiteArchiveWorkspace workspace = workspaceFactory.Create(siteId);
        Directory.CreateDirectory(workspace.RootPath);
        Directory.CreateDirectory(workspace.FilesPath);

        foreach (ArchiveCsvExport export in queryCatalog.CsvExports)
        {
            await export.WriteAsync(queryExecutor, csvWriter, workspace.FilesPath, siteId, cancellationToken);
        }

        await DownloadReportsAsync(workspace.FilesPath, siteId, cancellationToken);
        return await ZipAndUpload(workspace.FilesPath, workspace.ZipPath, workspace.BlobName, cancellationToken);
    }

    // Function summary: Streams report links and downloads linked report blobs into the archive workspace.
    private async Task DownloadReportsAsync(string filesDirectory, Guid siteId, CancellationToken cancellationToken)
    {
        await foreach (ReportArchiveRow? report in queryExecutor
            .StreamAsync<ReportArchiveRow>(queryCatalog.ReportLinksSql, siteId, cancellationToken)
            .WithCancellation(cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(report.ReportLink))
            {
                string filename = Path.GetFileName(new Uri(report.ReportLink).AbsolutePath);
                await BlobToFolder(filename, filesDirectory, cancellationToken);
            }
        }
    }

    // Function summary: Creates an archive zip from exported files, uploads it to blob storage, and returns its URL.
    public async Task<string> ZipAndUpload(string filesPath, string zipFilePath, string blobName, CancellationToken cancellationToken)
    {
        await System.IO.Compression.ZipFile.CreateFromDirectoryAsync(filesPath, zipFilePath, cancellationToken);

        BlobContainerClient monitorContainerClient = RequiredContainer(config.ArchiveContainer);

        BlobClient blobClient = monitorContainerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(zipFilePath, true, cancellationToken);

        BlockBlobClient blob = monitorContainerClient.GetBlockBlobClient(blobName);
        return blob.Uri.AbsoluteUri;
    }

    public async Task DeleteSupersededAsync(
        Guid siteId,
        string durableArchiveUrl,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(
                durableArchiveUrl,
                UriKind.Absolute,
                out Uri? durableUri)
            || (durableUri.Scheme != Uri.UriSchemeHttps
                && durableUri.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(durableUri.UserInfo)
            || !string.IsNullOrEmpty(durableUri.Fragment))
        {
            throw new InvalidOperationException(
                "The durable archive URL is not a verifiable absolute blob URL.");
        }

        BlobUriBuilder durableArchive;
        try
        {
            durableArchive = new BlobUriBuilder(durableUri);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or UriFormatException)
        {
            throw new InvalidOperationException(
                "The durable archive URL is not a verifiable absolute blob URL.",
                exception);
        }

        BlobContainerClient archiveContainer = RequiredContainer(config.ArchiveContainer);
        BlobClient candidateBlob = archiveContainer.GetBlobClient(
            $"{siteId:N}/site-archive.zip");
        BlobUriBuilder candidateArchive = new(candidateBlob.Uri);
        if (!IdentifiesConfiguredContainer(
                durableUri,
                durableArchive,
                candidateBlob.Uri,
                candidateArchive))
        {
            throw new InvalidOperationException(
                "The durable archive URL does not identify a blob in the configured archive account and container.");
        }

        if (string.Equals(
                durableArchive.BlobName,
                candidateArchive.BlobName,
                StringComparison.Ordinal))
        {
            return;
        }

        await candidateBlob.DeleteIfExistsAsync(
            DeleteSnapshotsOption.IncludeSnapshots,
            cancellationToken: cancellationToken);
    }

    private static bool IdentifiesConfiguredContainer(
        Uri durableUri,
        BlobUriBuilder durableArchive,
        Uri candidateUri,
        BlobUriBuilder candidateArchive)
    {
        return string.Equals(
                durableUri.Scheme,
                candidateUri.Scheme,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                durableUri.IdnHost,
                candidateUri.IdnHost,
                StringComparison.OrdinalIgnoreCase)
            && durableUri.Port == candidateUri.Port
            && !string.IsNullOrWhiteSpace(durableArchive.AccountName)
            && string.Equals(
                durableArchive.AccountName,
                candidateArchive.AccountName,
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(durableArchive.BlobContainerName)
            && string.Equals(
                durableArchive.BlobContainerName,
                candidateArchive.BlobContainerName,
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(durableArchive.BlobName);
    }

    // Function summary: Creates a provider-specific site id parameter for focused archive security tests.
    private DbParameter CreateSiteIdParameter(Guid siteId)
    {
        return queryExecutor.CreateSiteIdParameter(siteId);
    }

    // Function summary: Downloads a report blob into the archive workspace.
    private async Task BlobToFolder(string blobName, string downloadFolder, CancellationToken cancellationToken)
    {
        string downloadFilePath = Path.Combine(downloadFolder, blobName);
        BlobContainerClient containerClient = RequiredContainer(config.ReportContainer);
        BlobClient blobClient = containerClient.GetBlobClient($"{config.ReportFolder}/{blobName}");

        if (await blobClient.ExistsAsync(cancellationToken))
        {
            Response<BlobDownloadInfo> response = await blobClient.DownloadAsync(cancellationToken);
            await using FileStream fileStream = File.Create(downloadFilePath);
            await response.Value.Content.CopyToAsync(fileStream, cancellationToken);
        }
    }

    // Function summary: Resolves a required archive container through the shared blob-client factory.
    private BlobContainerClient RequiredContainer(string containerName)
    {
        return blobStorageClientFactory.CreateContainerClient(containerName)
            ?? throw new InvalidOperationException("Blob storage is not configured for site archives.");
    }
}
