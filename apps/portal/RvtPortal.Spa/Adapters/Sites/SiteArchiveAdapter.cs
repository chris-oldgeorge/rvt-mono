using RvtPortal.Application.Sites.Ports;
using RvtPortal.Spa.Adapters.Archive;

namespace RvtPortal.Spa.Adapters.Sites;

public sealed class SiteArchiveAdapter(ISiteArchiveService archiveService)
    : ISiteArchivePort
{
    private const string ExportFailureMessage =
        "The site archive could not be created, so the site was not archived. Please try again.";

    public async Task<SiteArchiveExportResult> ExportAsync(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = await archiveService.Process(siteId, cancellationToken);
            return SiteArchiveExportResult.Success(url);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return SiteArchiveExportResult.Failed(ExportFailureMessage);
        }
    }
}
