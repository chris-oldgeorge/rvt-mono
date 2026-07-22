// File summary: Exposes API endpoints used by the React portal for monitors controller workflows.
// Major updates:
// - 2026-07-09 pending Refined generated endpoint comments after controller workflow cleanup.
// - 2026-07-09 pending Routed monitor detail and mutation workflows through the monitor workflow service.
// - 2026-07-09 pending Routed monitor administration reads through an application service.
// - 2026-07-08 pending Wrapped picture uploads at the HTTP boundary for storage port usage.
// - 2026-06-25 pending Narrowed private assignment helper collection types for CA1859 cleanup.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-08 pending Added admin workflow for removing or archiving unattached monitors.
// - 2026-06-09 pending Added legacy monitor-detail summaries and deployment picture upload.
// - 2026-06-09 pending Hardened monitor picture upload/storage and coordinate validation.
// - 2026-06-09 pending Added latest average and battery summaries to monitor details.
// - 2026-06-09 pending Preferred live measurement data for latest monitor readings.
// - 2026-06-09 pending Switched detail summaries and pictures to shared services for installer parity and Blob storage.
// - 2026-06-09 pending Routed critical detail and picture upload workflows through MediatR CQRS handlers.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-10 pending Removed redundant async/await from deployment lookup helpers.
// - 2026-06-25 pending Moved monitor assignment and unattached-removal writes into transactional MediatR commands.
// - 2026-06-25 pending Routed monitor inventory lists through a database-backed paged reader.
// - 2026-06-26 pending Passed installer company assignment into monitor list authorization.
// - 2026-06-26 pending Routed residual monitor mutation and default-alert writes through transactional MediatR commands.

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVT.BusinessLogic.Application;
using RvtPortal.Spa.Api.Mappers;
using RvtPortal.Spa.Adapters.Storage;
using RvtPortal.Spa.Application.Monitors;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Api;

[ApiController]
[Authorize(Roles = RoleAuthorization.AdminRoles + "," + RoleNames.CompanyUser + "," + RoleNames.RVTInstaller)]
[Route("api/monitors")]
public class MonitorsController : ControllerBase
{
    // Function summary: Defines the public monitor sort aliases accepted by list endpoints.
    private static readonly IReadOnlyDictionary<string, string> SortFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["fleetNumber"] = "fleetNumber",
        ["serialId"] = "serialId",
        ["typeOfMonitor"] = "typeOfMonitor",
        ["siteName"] = "siteName",
        ["contractNumber"] = "contractNumber",
        ["lastDataTime"] = "lastDataTime"
    };
    private const int MaxPictureBytes = 5 * 1024 * 1024;
    private const int MaxPictureRequestBytes = MaxPictureBytes + 32 * 1024;

    private readonly IMonitorAdministrationReadService monitorReads;
    private readonly IMonitorAdministrationWorkflowService monitorWorkflows;
    private readonly ICurrentUserContextFactory currentUsers;

    // Function summary: Initializes this HTTP adapter with monitor read and workflow use cases.
    public MonitorsController(
        IMonitorAdministrationReadService monitorReads,
        IMonitorAdministrationWorkflowService monitorWorkflows,
        ICurrentUserContextFactory currentUsers)
    {
        this.monitorReads = monitorReads;
        this.monitorWorkflows = monitorWorkflows;
        this.currentUsers = currentUsers;
    }

    [HttpGet]
    [ProducesResponseType(typeof(QueryMonitorsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    // Function summary: Returns the paged monitor inventory visible to the current user.
    public async Task<ActionResult<QueryMonitorsResponse>> Query([FromQuery] QueryMonitorsRequest request)
    {
        var requestedSort = string.IsNullOrWhiteSpace(request.Sort) ? "fleetNumber" : request.Sort.Trim();
        if (!SortFields.ContainsKey(requestedSort))
        {
            return InvalidSort(requestedSort, SortFields.Keys);
        }

        var page = request.GetNormalizedPage();
        var pageSize = request.GetNormalizedPageSize();
        var sortDir = request.GetNormalizedSortDir();
        var result = await monitorReads.QueryAsync(new MonitorInventoryRequest(
            request.MonitorType,
            request.State,
            request.SearchText,
            requestedSort,
            sortDir,
            page,
            pageSize), await CreateActorAsync(), HttpContext.RequestAborted);
        if (result.Forbidden)
        {
            return Forbid();
        }

        return MonitorApiMapper.ToQueryResponse(result);
    }

    [HttpGet("options")]
    [ProducesResponseType(typeof(MonitorOptionsResponse), StatusCodes.Status200OK)]
    // Function summary: Returns monitor form and filter options.
    public async Task<ActionResult<MonitorOptionsResponse>> Options()
    {
        var options = await monitorReads.OptionsAsync(HttpContext.RequestAborted);
        return MonitorApiMapper.ToOptionsResponse(options);
    }

    [HttpGet("assignment")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(MonitorAssignmentContextResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Returns context needed before assigning a monitor to a site contract.
    public async Task<ActionResult<MonitorAssignmentContextResponse>> Assignment([FromQuery, Required] Guid siteId, [FromQuery] Guid? contractId = null)
    {
        var result = await monitorReads.GetAssignmentContextAsync(siteId, contractId, await CreateActorAsync(), HttpContext.RequestAborted);
        switch (result.Status)
        {
            case MonitorAssignmentContextStatus.Found:
                return MonitorApiMapper.ToAssignmentResponse(result.Context!);
            case MonitorAssignmentContextStatus.SiteNotFound:
                return SiteNotFound(siteId);
            case MonitorAssignmentContextStatus.SiteHasNoContracts:
                ModelState.AddModelError(nameof(siteId), "The site does not have any contracts to assign monitors to.");
                return ValidationProblem(ModelState);
            case MonitorAssignmentContextStatus.ContractNotAssignedToSite:
                ModelState.AddModelError(nameof(contractId), "The selected contract is not assigned to this site.");
                return ValidationProblem(ModelState);
            default:
                return SiteNotFound(siteId);
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EntityResponse<MonitorDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Retrieves authorized monitor detail through the monitor workflow service.
    public async Task<ActionResult<EntityResponse<MonitorDetailResponse>>> Get(Guid id)
    {
        var detail = await monitorWorkflows.GetDetailAsync(id, User, HttpContext.RequestAborted);
        if (detail == null)
        {
            return MonitorNotFound(id);
        }

        return new EntityResponse<MonitorDetailResponse> { Item = detail };
    }

    [HttpGet("deployments/{deploymentId:guid}")]
    [ProducesResponseType(typeof(EntityResponse<MonitorDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Retrieves authorized deployment detail through the monitor workflow service.
    public async Task<ActionResult<EntityResponse<MonitorDetailResponse>>> GetDeployment(Guid deploymentId)
    {
        var detail = await monitorWorkflows.GetDeploymentDetailAsync(deploymentId, User, HttpContext.RequestAborted);
        if (detail == null)
        {
            return MonitorNotFound(deploymentId);
        }

        return new EntityResponse<MonitorDetailResponse> { Item = detail };
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(EntityResponse<MonitorDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Updates monitor metadata through the monitor workflow service.
    public async Task<ActionResult<EntityResponse<MonitorDetailResponse>>> Update(Guid id, MonitorMutationRequest request)
    {
        var result = await monitorWorkflows.UpdateAsync(id, request, User, HttpContext.RequestAborted);
        return MonitorDetailResult(id, result);
    }

    [HttpPost("{id:guid}/picture")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [RequestSizeLimit(MaxPictureRequestBytes)]
    [ProducesResponseType(typeof(EntityResponse<MonitorDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Uploads a deployment location picture for the monitor detail workflow.
    public async Task<ActionResult<EntityResponse<MonitorDetailResponse>>> UploadPicture(Guid id, IFormFile picture)
    {
        var result = await monitorWorkflows.UploadPictureAsync(id, new FormFileUpload(picture), HttpContext.RequestAborted);
        return MonitorDetailResult(id, result);
    }

    [HttpGet("{id:guid}/picture")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Streams a monitor deployment picture after applying monitor read authorization.
    public async Task<IActionResult> GetPicture(Guid id)
    {
        var picture = await monitorReads.GetPictureAsync(id, User, await CreateActorAsync(), HttpContext.RequestAborted);
        if (picture == null)
        {
            return MonitorNotFound(id);
        }

        return File(picture.Stream, picture.ContentType, picture.FileName);
    }

    [HttpPut("{id:guid}/fleet-number")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(EntityResponse<MonitorDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Sets a monitor fleet number through the monitor workflow service.
    public async Task<ActionResult<EntityResponse<MonitorDetailResponse>>> SetFleetNumber(Guid id, FleetNumberMutationRequest request)
    {
        var result = await monitorWorkflows.SetFleetNumberAsync(id, request.FleetNumber, User, HttpContext.RequestAborted);
        return MonitorDetailResult(id, result);
    }

    [HttpPost("{id:guid}/contract-assignment")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(EntityResponse<MonitorDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Assigns a monitor to a contract through the monitor workflow service.
    public async Task<ActionResult<EntityResponse<MonitorDetailResponse>>> AddToContract(Guid id, MonitorAssignmentRequest request)
    {
        var result = await monitorWorkflows.AssignToContractAsync(id, request.ContractId, User, HttpContext.RequestAborted);
        return MonitorDetailResult(id, result);
    }

    [HttpDelete("{id:guid}/contract-assignment")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(MutationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Removes the active monitor contract assignment through the monitor workflow service.
    public async Task<ActionResult<MutationResponse>> RemoveFromContract(Guid id)
    {
        var result = await monitorWorkflows.RemoveFromContractAsync(id, HttpContext.RequestAborted);
        AddModelErrors(result.Errors);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return new MutationResponse
        {
            Id = id,
            Message = "Monitor contract assignment has been removed."
        };
    }

    [HttpGet("unattached")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(QueryUnattachedMonitorsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Retrieves unattached monitor removal candidates for RVT administrators.
    public async Task<ActionResult<QueryUnattachedMonitorsResponse>> QueryUnattached([FromQuery] QueryMonitorsRequest request)
    {
        var requestedSort = string.IsNullOrWhiteSpace(request.Sort) ? "fleetNumber" : request.Sort.Trim();
        if (!SortFields.ContainsKey(requestedSort))
        {
            return InvalidSort(requestedSort, SortFields.Keys);
        }

        var page = request.GetNormalizedPage();
        var pageSize = request.GetNormalizedPageSize();
        var sortDir = request.GetNormalizedSortDir();
        var result = await monitorReads.QueryUnattachedAsync(new MonitorInventoryRequest(
            request.MonitorType,
            MonitorListStates.All,
            request.SearchText,
            requestedSort,
            sortDir,
            page,
            pageSize), await CreateActorAsync(), HttpContext.RequestAborted);
        return MonitorApiMapper.ToUnattachedQueryResponse(result);
    }

    [HttpGet("{id:guid}/removal-impact")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(MonitorRemovalImpactResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Retrieves related data counts used before removing an unattached monitor.
    public async Task<ActionResult<MonitorRemovalImpactResponse>> GetRemovalImpact(Guid id)
    {
        var impact = await monitorReads.GetRemovalImpactAsync(id, HttpContext.RequestAborted);
        return impact == null ? MonitorNotFound(id) : impact;
    }

    [HttpDelete("{id:guid}/unattached")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(MonitorRemovalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Removes or archives an unattached monitor through the monitor workflow service.
    public async Task<ActionResult<MonitorRemovalResponse>> RemoveUnattached(Guid id, MonitorRemovalRequest request)
    {
        var actor = await CreateActorAsync();
        var result = await monitorWorkflows.RemoveUnattachedAsync(
            id,
            request.Reason,
            actor.UserName ?? actor.UserId?.ToString(),
            HttpContext.RequestAborted);
        if (result.NotFound)
        {
            return MonitorNotFound(id);
        }

        AddModelErrors(result.Errors);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (result.Response == null)
        {
            return MonitorNotFound(id);
        }

        return result.Response;
    }

    [HttpPost("default-alert-levels")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(DefaultMonitorsResponse), StatusCodes.Status200OK)]
    // Function summary: Creates default monitor alert levels through the monitor workflow service.
    public async Task<ActionResult<DefaultMonitorsResponse>> DefaultMonitors()
    {
        return await monitorWorkflows.CreateDefaultAlertLevelsAsync(HttpContext.RequestAborted);
    }

    // Function summary: Maps monitor detail workflow results to the existing API response shape.
    private ActionResult<EntityResponse<MonitorDetailResponse>> MonitorDetailResult(Guid id, MonitorDetailWorkflowResult result)
    {
        if (result.NotFound)
        {
            return MonitorNotFound(result.MissingId ?? id);
        }

        AddModelErrors(result.Errors);
        if (!ModelState.IsValid || result.Detail == null)
        {
            return ValidationProblem(ModelState);
        }

        return new EntityResponse<MonitorDetailResponse> { Item = result.Detail };
    }

    // Function summary: Copies command validation errors into MVC model state.
    private void AddModelErrors(IReadOnlyDictionary<string, string[]> errors)
    {
        foreach (var error in errors)
        {
            foreach (var message in error.Value)
            {
                ModelState.AddModelError(error.Key, message);
            }
        }
    }

    // Function summary: Builds the current portal user context for monitor read workflows.
    private Task<PortalUserContext> CreateActorAsync()
    {
        return currentUsers.CreateAsync(User, HttpContext.RequestAborted);
    }

    // Function summary: Builds the existing invalid-sort problem response for monitor endpoints.
    private ObjectResult InvalidSort(string sort, IEnumerable<string> validSorts)
    {
        return Problem(
            detail: $"Sort '{sort}' is not supported. Use one of: {string.Join(", ", validSorts)}.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid sort field");
    }

    // Function summary: Builds the existing not-found response for missing or unauthorized monitors.
    private NotFoundObjectResult MonitorNotFound(Guid id)
    {
        return NotFound(new ProblemDetails
        {
            Title = "Monitor not found",
            Detail = $"Monitor '{id}' was not found or is not visible to the current user.",
            Status = StatusCodes.Status404NotFound
        });
    }

    // Function summary: Builds the existing not-found response for missing sites.
    private NotFoundObjectResult SiteNotFound(Guid id)
    {
        return NotFound(new ProblemDetails
        {
            Title = "Site not found",
            Detail = $"Site '{id}' was not found.",
            Status = StatusCodes.Status404NotFound
        });
    }

}
