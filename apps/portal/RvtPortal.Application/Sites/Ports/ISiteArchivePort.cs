namespace RvtPortal.Application.Sites.Ports;

public sealed record SiteArchiveExportResult(
    bool Succeeded,
    string? ArchiveUrl,
    string? ErrorMessage)
{
    public static SiteArchiveExportResult Success(string url) =>
        new(true, url, null);

    public static SiteArchiveExportResult Failed(string message) =>
        new(false, null, message);
}

public interface ISiteArchivePort
{
    Task<SiteArchiveExportResult> ExportAsync(
        Guid siteId,
        CancellationToken cancellationToken);
}
