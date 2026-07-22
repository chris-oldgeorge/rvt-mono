// File summary: Exposes internal report-content assets to the reporting service through shared-key protected APIs.
// Major updates:
// - 2026-07-09 pending Routed report-content asset fetches through an application service.
// - 2026-07-08 pending Read customer logos through the business-layer storage port.
// - 2026-06-24 pending Added customer-logo fetch endpoint for report rendering.
// - 2026-06-25 pending Bound the internal-key header via [FromHeader] model binding instead of reading Request.Headers (S6932).
// - 2026-06-25 pending Compared the internal key in constant time (hash + FixedTimeEquals).

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RvtPortal.Spa.Application.ReportContent;

namespace RvtPortal.Spa.Api;

[ApiController]
[AllowAnonymous]
[Route("api/report-content")]
public sealed class ReportContentController : ControllerBase
{
    private const string InternalKeyHeader = "X-RVT-Internal-Key";
    private readonly IReportContentApplicationService reportContent;

    // Function summary: Initializes this controller with the application service that owns report-content fetch workflows.
    public ReportContentController(IReportContentApplicationService reportContent)
    {
        this.reportContent = reportContent;
    }

    [HttpGet("sites/{siteId:guid}/customer-logo")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    // Function summary: Streams a site customer logo to the trusted reporting service.
    public async Task<IActionResult> CustomerLogo(
        Guid siteId,
        [FromHeader(Name = InternalKeyHeader)] string? internalKey,
        CancellationToken cancellationToken)
    {
        var result = await reportContent.GetCustomerLogoAsync(siteId, internalKey, cancellationToken);
        if (result.File is not null)
        {
            return File(result.File.Stream, result.File.ContentType, result.File.FileName);
        }

        return result.Failure switch
        {
            ReportContentFailureKind.ServiceUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable),
            ReportContentFailureKind.Unauthorized => Unauthorized(),
            _ => NotFound()
        };
    }
}
