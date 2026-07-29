using RvtPortal.Application.Sites.Ports;
using RvtPortal.Spa.Adapters.Archive;

namespace RvtPortal.Spa.Adapters.Sites;

public sealed class SiteArchiveAdapter(
    ISiteArchiveService archiveService,
    ILogger<SiteArchiveAdapter> logger)
    : ISiteArchivePort
{
    private const string ExportFailureMessage =
        "The site archive could not be created, so the site was not archived. Please try again.";
    private const string CleanupFailureMessage =
        "The duplicate site archive could not be removed. The site remains archived; contact support.";

    public async Task<SiteArchiveExportResult> ExportAsync(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        try
        {
            string url = await archiveService.Process(siteId, cancellationToken);
            return SiteArchiveExportResult.Success(url);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to export the site archive for site {SiteId}.",
                siteId);
            return SiteArchiveExportResult.Failed(ExportFailureMessage);
        }
    }

    public async Task<SiteArchiveCleanupResult> CleanupSupersededAsync(
        Guid siteId,
        string durableArchiveUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            await archiveService.DeleteSupersededAsync(
                siteId,
                durableArchiveUrl,
                cancellationToken);
            return SiteArchiveCleanupResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to remove a superseded site archive for site {SiteId}.",
                siteId);
            return SiteArchiveCleanupResult.Failed(CleanupFailureMessage);
        }
    }
}
