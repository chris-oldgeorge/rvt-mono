// File summary: Provides per-request temporary workspaces for site archive exports.
// Major updates:
// - 2026-07-09 pending Added unique archive workspace creation to prevent concurrent archive collisions.

namespace RvtPortal.Spa.Adapters.Archive;

internal interface ISiteArchiveWorkspaceFactory
{
    // Function summary: Creates a unique temporary workspace for one archive request.
    SiteArchiveWorkspace Create(Guid siteId);
}

internal sealed class SiteArchiveWorkspaceFactory : ISiteArchiveWorkspaceFactory
{
    // Function summary: Creates a unique workspace and blob name for a site archive request.
    public SiteArchiveWorkspace Create(Guid siteId)
    {
        var archiveId = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
        var rootPath = Path.Combine(Path.GetTempPath(), "rvt-site-archives", siteId.ToString("N"), archiveId);
        var filesPath = Path.Combine(rootPath, "files");
        var blobName = $"{siteId:N}/{archiveId}.zip";
        var zipPath = Path.Combine(rootPath, $"{archiveId}.zip");

        return new SiteArchiveWorkspace(rootPath, filesPath, zipPath, blobName);
    }
}

internal sealed class SiteArchiveWorkspace : IAsyncDisposable
{
    // Function summary: Initializes this type with the paths used by one archive request.
    public SiteArchiveWorkspace(string rootPath, string filesPath, string zipPath, string blobName)
    {
        RootPath = rootPath;
        FilesPath = filesPath;
        ZipPath = zipPath;
        BlobName = blobName;
    }

    public string RootPath { get; }

    public string FilesPath { get; }

    public string ZipPath { get; }

    public string BlobName { get; }

    // Function summary: Removes the temporary archive workspace after the request completes.
    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, true);
        }

        return ValueTask.CompletedTask;
    }
}
