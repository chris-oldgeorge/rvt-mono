// File summary: Exposes API endpoints used by the React portal for report workflows.
// Major updates:
// - 2026-07-09 pending Routed report list/detail reads through the report application service.
// - 2026-06-24 pending Moved report search, sort, and paging into EF query composition for production-scale lists.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RvtPortal.Spa.Application.Reports;
using RvtPortal.Spa.Data;

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
        var result = await reports.QueryAsync(
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

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EntityResponse<ReportListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Retrieves report detail by id.
    public async Task<ActionResult<EntityResponse<ReportListItem>>> Get(Guid id)
    {
        var report = await reports.GetAsync(id, HttpContext.RequestAborted);
        return report == null ? ReportNotFound(id) : new EntityResponse<ReportListItem> { Item = report };
    }

    // Function summary: Builds the invalid-sort problem response while preserving the existing report API contract.
    private BadRequestObjectResult InvalidSort(string requestedSort, IEnumerable<string> allowedSortFields)
    {
        var problem = ApiProblems.Create(
            HttpContext,
            StatusCodes.Status400BadRequest,
            "Invalid sort field",
            $"Sort field '{requestedSort}' is not supported for reports.");
        problem.Extensions["allowedSortFields"] = allowedSortFields.OrderBy(field => field, StringComparer.OrdinalIgnoreCase).ToArray();
        return BadRequest(problem);
    }

    // Function summary: Builds the report not-found response.
    private NotFoundObjectResult ReportNotFound(Guid id)
    {
        return NotFound(ApiProblems.Create(
            HttpContext,
            StatusCodes.Status404NotFound,
            "Report not found",
            $"Report '{id}' was not found."));
    }
}
