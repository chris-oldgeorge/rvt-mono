// File summary: Exposes API endpoints used by the React portal for report workflows.
// Major updates:
// - 2026-07-09 pending Routed report list/detail reads through the report application service.
// - 2026-06-24 pending Moved report search, sort, and paging into EF query composition for production-scale lists.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RvtPortal.Spa.Data;
using RvtPortal.Spa.UseCases.Reports;

namespace RvtPortal.Spa.Api;

[ApiController]
[Authorize(Roles = RoleAuthorization.AdminRoles)]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportApplicationService reports;

    // Function summary: Initializes this HTTP adapter with report read workflows.
    public ReportsController(IReportApplicationService reports)
    {
        this.reports = reports;
    }

    [HttpGet]
    [ProducesResponseType(typeof(QueryReportsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Queries reports through the report application service.
    public async Task<ActionResult<QueryReportsResponse>> Query([FromQuery] QueryReportsRequest request)
    {
        ReportQueryResult result = await reports.QueryAsync(
            new ReportQuery(
                request.SearchText,
                request.Sort,
                request.GetNormalizedSortDir(),
                request.GetNormalizedPage(),
                request.GetNormalizedPageSize()),
            HttpContext.RequestAborted);
        return !string.IsNullOrWhiteSpace(result.InvalidSort)
            ? InvalidSort(result.InvalidSort, result.AllowedSortFields)
            : result.Response!;
    }

    // Function summary: Builds the invalid-sort problem response while preserving the existing report API contract.
    private BadRequestObjectResult InvalidSort(string requestedSort, IEnumerable<string> allowedSortFields) =>
        ApiProblems.InvalidSort(HttpContext, requestedSort, allowedSortFields, "reports");
}
