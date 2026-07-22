// File summary: Exposes API endpoints used by the React portal for alert-level workflows.
// Major updates:
// - 2026-07-09 pending Routed alert-level write workflows through the alert-level application service.
// - 2026-07-09 pending Routed alert-level read workflows through the alert-level application service.
// - 2026-06-25 pending Hid vibration averaging-period labels/options while keeping persisted compatibility values.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-26 pending Routed alert-level write workflows through transactional MediatR commands.

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVT.BusinessLogic.Application;
using RvtPortal.Spa.Application.AlertLevels;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Api;

[ApiController]
[Authorize(Roles = RoleAuthorization.AdminRoles + "," + RoleNames.CompanyUser)]
[Route("api/alert-levels")]
public class AlertLevelsController : ControllerBase
{
    private readonly IAlertLevelApplicationService alertLevels;
    private readonly ICurrentUserContextFactory currentUserContextFactory;

    // Function summary: Initializes this HTTP adapter with alert-level application workflows.
    public AlertLevelsController(
        IAlertLevelApplicationService alertLevels,
        ICurrentUserContextFactory currentUserContextFactory)
    {
        this.alertLevels = alertLevels;
        this.currentUserContextFactory = currentUserContextFactory;
    }

    [HttpGet]
    [ProducesResponseType(typeof(QueryAlertLevelsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Queries alert levels for a visible monitor through the alert-level application service.
    public async Task<ActionResult<QueryAlertLevelsResponse>> Query([FromQuery] QueryAlertLevelsRequest request)
    {
        var result = await alertLevels.QueryAsync(
            await CreateUserContextAsync(),
            new AlertLevelQuery(
                request.MonitorId,
                request.SearchText,
                request.Sort,
                request.GetNormalizedSortDir(),
                request.GetNormalizedPage(),
                request.GetNormalizedPageSize()),
            HttpContext.RequestAborted);

        if (result.MissingMonitor)
        {
            return BadRequest(ApiProblems.Create(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "Monitor is required",
                "A monitorId query parameter must be supplied."));
        }
        if (!string.IsNullOrWhiteSpace(result.InvalidSort))
        {
            return InvalidSort(result.InvalidSort, result.ValidSorts);
        }
        if (result.NotFound || result.Response == null)
        {
            return MonitorNotFound(request.MonitorId ?? Guid.Empty);
        }

        return result.Response;
    }

    [HttpGet("options")]
    [ProducesResponseType(typeof(AlertLevelOptionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Returns alert-level form options for a visible monitor.
    public async Task<ActionResult<AlertLevelOptionsResponse>> Options([FromQuery, Required] Guid monitorId)
    {
        var options = await alertLevels.OptionsAsync(await CreateUserContextAsync(), monitorId, HttpContext.RequestAborted);
        return options == null ? MonitorNotFound(monitorId) : options;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EntityResponse<AlertLevelItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Retrieves a visible alert level by id.
    public async Task<ActionResult<EntityResponse<AlertLevelItem>>> Get(Guid id)
    {
        var item = await alertLevels.GetAsync(await CreateUserContextAsync(), id, HttpContext.RequestAborted);
        return item == null ? AlertLevelNotFound(id) : new EntityResponse<AlertLevelItem> { Item = item };
    }

    [HttpPost]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(EntityResponse<AlertLevelItem>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Creates an alert level through the alert-level application service.
    public async Task<ActionResult<EntityResponse<AlertLevelItem>>> Create(AlertLevelMutationRequest request)
    {
        var result = await alertLevels.CreateAsync(request, HttpContext.RequestAborted);
        if (result.NotFound)
        {
            return MonitorNotFound(request.MonitorId);
        }

        AddCommandErrors(result.Errors);
        if (!ModelState.IsValid || result.Item == null || !result.AlertLevelId.HasValue)
        {
            return ValidationProblem(ModelState);
        }

        var response = new EntityResponse<AlertLevelItem> { Item = result.Item };
        return CreatedAtAction(nameof(Get), new { id = result.AlertLevelId.Value }, response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(EntityResponse<AlertLevelItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Updates an alert level through the alert-level application service.
    public async Task<ActionResult<EntityResponse<AlertLevelItem>>> Update(Guid id, AlertLevelMutationRequest request)
    {
        var result = await alertLevels.UpdateAsync(id, request, HttpContext.RequestAborted);
        if (result.NotFound)
        {
            return AlertLevelNotFound(id);
        }

        AddCommandErrors(result.Errors);
        if (!ModelState.IsValid || result.Item == null)
        {
            return ValidationProblem(ModelState);
        }

        return new EntityResponse<AlertLevelItem> { Item = result.Item };
    }

    [HttpPut("monitors/{monitorId:guid}/vibration")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(VibrationAlertLevelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Updates vibration alert levels through the alert-level application service.
    public async Task<ActionResult<VibrationAlertLevelResponse>> UpdateVibration(Guid monitorId, VibrationAlertLevelMutationRequest request)
    {
        var result = await alertLevels.UpdateVibrationAsync(monitorId, request, HttpContext.RequestAborted);
        if (result.NotFound)
        {
            return MonitorNotFound(monitorId);
        }

        AddCommandErrors(result.Errors);
        if (!ModelState.IsValid || result.Response == null)
        {
            return ValidationProblem(ModelState);
        }

        return result.Response;
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(MutationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Deletes an alert level through the alert-level application service.
    public async Task<ActionResult<MutationResponse>> Delete(Guid id)
    {
        var result = await alertLevels.DeleteAsync(id, HttpContext.RequestAborted);
        if (result.NotFound)
        {
            return AlertLevelNotFound(id);
        }

        return new MutationResponse
        {
            Id = id,
            Message = "Alert level has been deleted."
        };
    }

    // Function summary: Creates a transport-neutral current-user context from the authenticated HTTP user.
    private Task<PortalUserContext> CreateUserContextAsync()
    {
        return currentUserContextFactory.CreateAsync(User, HttpContext.RequestAborted);
    }

    // Function summary: Maps command validation errors into the API model-state response.
    private void AddCommandErrors(IReadOnlyDictionary<string, string[]> errors)
    {
        foreach (var error in errors)
        {
            foreach (var message in error.Value)
            {
                ModelState.AddModelError(error.Key, message);
            }
        }
    }

    // Function summary: Builds the invalid-sort problem response while preserving the existing contract.
    private ObjectResult InvalidSort(string sort, IEnumerable<string> validSorts)
    {
        return Problem(
            detail: $"Sort '{sort}' is not supported. Use one of: {string.Join(", ", validSorts)}.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid sort field");
    }

    // Function summary: Builds the not-found response used for hidden or missing monitor alert-level resources.
    private NotFoundObjectResult MonitorNotFound(Guid id)
    {
        return NotFound(new ProblemDetails
        {
            Title = "Monitor not found",
            Detail = $"Monitor '{id}' was not found or is not visible to the current user.",
            Status = StatusCodes.Status404NotFound
        });
    }

    // Function summary: Builds the not-found response used for hidden or missing alert levels.
    private NotFoundObjectResult AlertLevelNotFound(Guid id)
    {
        return NotFound(new ProblemDetails
        {
            Title = "Alert level not found",
            Detail = $"Alert level '{id}' was not found or is not visible to the current user.",
            Status = StatusCodes.Status404NotFound
        });
    }
}
