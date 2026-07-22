// File summary: Exposes report-rule API endpoints as thin HTTP adapters over business-layer use cases.
// Major updates:
// - 2026-07-08 pending Routed report-rule workflows through the business application service for hexagonal-at-the-edges boundaries.
// - 2026-06-26 pending Routed report-rule recipient reads through the CQRS reader.
// - 2026-06-26 pending Marked archived current site options as disabled for report-rule edit forms.
// - 2026-06-25 pending Added Daily as a scheduled report rule option.
// - 2026-06-24 pending Added manual generation requests, paged recipient lists, and database-side rule list querying.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVT.BusinessLogic.Application;
using RVT.BusinessLogic.Application.Paging;
using RVT.BusinessLogic.Reports;
using RvtPortal.Spa.Application.ReportRules;
using RvtPortal.Spa.Api.Mappers;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Api;

[ApiController]
[Authorize(Roles = RoleAuthorization.AdminRoles)]
[Route("api/report-rules")]
public class ReportRulesController : ControllerBase
{
    private readonly IReportRuleApplicationService reportRules;
    private readonly ICurrentUserContextFactory currentUserContextFactory;
    private readonly IApiResultMapper resultMapper;

    // Function summary: Initializes this HTTP adapter with report-rule business use cases and API mappers.
    public ReportRulesController(
        IReportRuleApplicationService reportRules,
        ICurrentUserContextFactory currentUserContextFactory,
        IApiResultMapper resultMapper)
    {
        this.reportRules = reportRules;
        this.currentUserContextFactory = currentUserContextFactory;
        this.resultMapper = resultMapper;
    }

    [HttpGet]
    [ProducesResponseType(typeof(QueryReportRulesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Queries report rules using the business-layer report-rule use case.
    public async Task<ActionResult<QueryReportRulesResponse>> Query([FromQuery] QueryReportRulesRequest request)
    {
        var requestedSort = string.IsNullOrWhiteSpace(request.Sort)
            ? ReportRuleApplicationService.DefaultSort
            : request.Sort.Trim();
        if (!ReportRuleApplicationService.SortFields.Contains(requestedSort))
        {
            return InvalidSort(requestedSort, ReportRuleApplicationService.SortFields);
        }

        var result = await reportRules.QueryAsync(
            new ReportRuleQuery(request.SiteId, BuildPageRequest(request, requestedSort)),
            HttpContext.RequestAborted);
        return resultMapper.ToActionResult(this, result, ReportRuleApiMapper.ToQueryResponse);
    }

    [HttpGet("options")]
    [ProducesResponseType(typeof(ReportRuleOptionsResponse), StatusCodes.Status200OK)]
    // Function summary: Returns report-rule edit options from the business-layer use case.
    public async Task<ActionResult<ReportRuleOptionsResponse>> Options()
    {
        var result = await reportRules.OptionsAsync(HttpContext.RequestAborted);
        return resultMapper.ToActionResult(this, result, ReportRuleApiMapper.ToOptionsResponse);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EntityResponse<ReportRuleDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Retrieves report-rule detail through the business-layer use case.
    public async Task<ActionResult<EntityResponse<ReportRuleDetailResponse>>> Get(Guid id)
    {
        var result = await reportRules.GetAsync(id, HttpContext.RequestAborted);
        return resultMapper.ToActionResult(this, result, ToDetailEntity);
    }

    [HttpPost]
    [ProducesResponseType(typeof(EntityResponse<ReportRuleDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Creates a report rule through the business-layer use case.
    public async Task<ActionResult<EntityResponse<ReportRuleDetailResponse>>> Create(ReportRuleMutationRequest request)
    {
        var user = await currentUserContextFactory.CreateAsync(User, HttpContext.RequestAborted);
        var result = await reportRules.CreateAsync(
            user,
            ReportRuleApiMapper.ToMutation(request),
            HttpContext.RequestAborted);
        if (result.Kind == ApplicationResultKind.Success && result.Value != null)
        {
            var response = ToDetailEntity(result.Value);
            return CreatedAtAction(nameof(Get), new { id = result.Value.Id }, response);
        }

        return resultMapper.ToActionResult(this, result, ToDetailEntity);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EntityResponse<ReportRuleDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Updates a report rule through the business-layer use case.
    public async Task<ActionResult<EntityResponse<ReportRuleDetailResponse>>> Update(Guid id, ReportRuleMutationRequest request)
    {
        var user = await currentUserContextFactory.CreateAsync(User, HttpContext.RequestAborted);
        var result = await reportRules.UpdateAsync(
            user,
            id,
            ReportRuleApiMapper.ToMutation(request),
            HttpContext.RequestAborted);
        return resultMapper.ToActionResult(this, result, ToDetailEntity);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(MutationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Deletes a report rule through the business-layer use case.
    public async Task<ActionResult<MutationResponse>> Delete(Guid id)
    {
        var result = await reportRules.DeleteAsync(id, HttpContext.RequestAborted);
        return resultMapper.ToActionResult(this, result, deletedId => new MutationResponse
        {
            Id = deletedId,
            Message = "Report rule has been deleted."
        });
    }

    [HttpGet("{id:guid}/users")]
    [ProducesResponseType(typeof(EntityResponse<ReportUserAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Retrieves the report-rule assignment summary used by legacy-compatible clients.
    public async Task<ActionResult<EntityResponse<ReportUserAssignmentResponse>>> Users(Guid id)
    {
        var result = await reportRules.GetUsersAsync(id, HttpContext.RequestAborted);
        return resultMapper.ToActionResult(this, result, ToAssignmentEntity);
    }

    [HttpGet("{id:guid}/available-users")]
    [ProducesResponseType(typeof(QueryReportUsersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Retrieves paged users eligible to receive a report rule.
    public async Task<ActionResult<QueryReportUsersResponse>> AvailableUsers(Guid id, [FromQuery] QueryReportUsersRequest request)
    {
        var result = await reportRules.QueryUsersAsync(
            id,
            BuildPageRequest(request, string.IsNullOrWhiteSpace(request.Sort) ? "name" : request.Sort.Trim()),
            assigned: false,
            HttpContext.RequestAborted);
        return resultMapper.ToActionResult(this, result, ReportRuleApiMapper.ToQueryUsersResponse);
    }

    [HttpGet("{id:guid}/assigned-users")]
    [ProducesResponseType(typeof(QueryReportUsersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Retrieves paged users currently assigned to receive a report rule.
    public async Task<ActionResult<QueryReportUsersResponse>> AssignedUsers(Guid id, [FromQuery] QueryReportUsersRequest request)
    {
        var result = await reportRules.QueryUsersAsync(
            id,
            BuildPageRequest(request, string.IsNullOrWhiteSpace(request.Sort) ? "name" : request.Sort.Trim()),
            assigned: true,
            HttpContext.RequestAborted);
        return resultMapper.ToActionResult(this, result, ReportRuleApiMapper.ToQueryUsersResponse);
    }

    [HttpPost("{id:guid}/users")]
    [ProducesResponseType(typeof(EntityResponse<ReportUserAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Assigns one user to a report rule through the business-layer use case.
    public async Task<ActionResult<EntityResponse<ReportUserAssignmentResponse>>> AddUser(Guid id, ReportUserMutationRequest request)
    {
        var result = await reportRules.AddUserAsync(id, request.UserId, HttpContext.RequestAborted);
        return resultMapper.ToActionResult(this, result, ToAssignmentEntity);
    }

    [HttpDelete("{id:guid}/users/{userId:guid}")]
    [ProducesResponseType(typeof(EntityResponse<ReportUserAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Removes one user from a report rule through the business-layer use case.
    public async Task<ActionResult<EntityResponse<ReportUserAssignmentResponse>>> RemoveUser(Guid id, Guid userId)
    {
        var result = await reportRules.RemoveUserAsync(id, userId, HttpContext.RequestAborted);
        return resultMapper.ToActionResult(this, result, ToAssignmentEntity);
    }

    [HttpPost("{id:guid}/generation-requests")]
    [ProducesResponseType(typeof(ReportGenerationRequestResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Accepts a manual generation request and delegates orchestration to the business use case.
    public async Task<ActionResult<ReportGenerationRequestResponse>> RequestGeneration(
        Guid id,
        ReportGenerationRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await reportRules.RequestGenerationAsync(
            id,
            ReportRuleApiMapper.ToGenerationRequest(request),
            cancellationToken);
        if (result.Kind == ApplicationResultKind.Success && result.Value != null)
        {
            return Accepted(ReportRuleApiMapper.ToGenerationResponse(result.Value));
        }

        if (result.Kind == ApplicationResultKind.NotFound)
        {
            return RuleNotFound(id);
        }

        if (result.Kind == ApplicationResultKind.ExternalServiceUnavailable)
        {
            var statusCode = result.StatusCode ?? StatusCodes.Status503ServiceUnavailable;
            return StatusCode(statusCode, ApiProblems.Create(
                HttpContext,
                statusCode,
                "Report generation service unavailable",
                result.Message));
        }

        return resultMapper.ToActionResult(this, result, ReportRuleApiMapper.ToGenerationResponse);
    }

    // Function summary: Builds a normalized transport-neutral page request for business-layer queries.
    private static PageRequest BuildPageRequest(PagedQueryRequest request, string sort)
    {
        return new PageRequest(
            request.SearchText,
            request.GetNormalizedPage(),
            request.GetNormalizedPageSize(),
            sort,
            request.GetNormalizedSortDir());
    }

    // Function summary: Wraps report-rule detail in the existing entity response contract.
    private static EntityResponse<ReportRuleDetailResponse> ToDetailEntity(ReportRuleDetailModel model)
    {
        return new EntityResponse<ReportRuleDetailResponse>
        {
            Item = ReportRuleApiMapper.ToDetailResponse(model)
        };
    }

    // Function summary: Wraps report-recipient assignment in the existing entity response contract.
    private static EntityResponse<ReportUserAssignmentResponse> ToAssignmentEntity(ReportUserAssignmentModel model)
    {
        return new EntityResponse<ReportUserAssignmentResponse>
        {
            Item = ReportRuleApiMapper.ToAssignmentResponse(model)
        };
    }

    // Function summary: Builds the existing problem response for unsupported sort fields.
    private BadRequestObjectResult InvalidSort(string requestedSort, IEnumerable<string> allowedSortFields)
    {
        var problem = ApiProblems.Create(
            HttpContext,
            StatusCodes.Status400BadRequest,
            "Invalid sort field",
            $"Sort field '{requestedSort}' is not supported for report rules.");
        problem.Extensions["allowedSortFields"] = allowedSortFields.OrderBy(field => field, StringComparer.OrdinalIgnoreCase).ToArray();
        return BadRequest(problem);
    }

    // Function summary: Builds the existing not-found problem response for report rules.
    private NotFoundObjectResult RuleNotFound(Guid id)
    {
        return NotFound(ApiProblems.Create(
            HttpContext,
            StatusCodes.Status404NotFound,
            "Report rule not found",
            $"Report rule '{id}' was not found."));
    }
}
