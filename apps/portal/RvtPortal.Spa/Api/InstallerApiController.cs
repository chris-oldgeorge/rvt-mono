// File summary: Exposes API endpoints used by the React portal for installer api controller workflows.
// Major updates:
// - 2026-07-09 pending Routed installer monitor detail and deployment-location writes through the installer application service.
// - 2026-07-09 pending Routed what3words conversion configuration and HTTP access through the installer application service.
// - 2026-07-09 pending Routed installer monitor list, deployment detail, and status reads through the installer application service.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-09 pending Kept installer monitor detail notifications aligned with SPA legacy parity links.
// - 2026-06-09 pending Returned protected monitor picture links for installer detail responses.
// - 2026-06-09 pending Reused shared monitor summaries so installer detail matches main SPA detail metrics.
// - 2026-06-09 pending Routed installer monitor detail reads through MediatR CQRS query handlers.
// - 2026-06-10 pending Removed redundant async/await from deployment lookup helpers.
// - 2026-06-26 pending Routed installer deployment-location writes through transactional MediatR commands.
// - 2026-06-26 pending Scoped installer monitor/status/deployment access to the installer's assigned company.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVT.BusinessLogic.Application;
using RvtPortal.Spa.Api.Mappers;
using RvtPortal.Spa.Application.Installers;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Api;

[ApiController]
[Authorize(Roles = RoleAuthorization.AdminRoles + "," + RoleNames.RVTInstaller)]
[Route("api/installer")]
public class InstallerApiController : ControllerBase
{
    private readonly IInstallerApplicationService installer;
    private readonly ICurrentUserContextFactory currentUserContextFactory;

    // Function summary: Initializes this HTTP adapter with installer application workflows.
    public InstallerApiController(
        IInstallerApplicationService installer,
        ICurrentUserContextFactory currentUserContextFactory)
    {
        this.installer = installer;
        this.currentUserContextFactory = currentUserContextFactory;
    }

    [HttpGet("monitors")]
    [ProducesResponseType(typeof(QueryMonitorsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Queries installer-visible monitors through the installer application service.
    public async Task<ActionResult<QueryMonitorsResponse>> Monitors([FromQuery] QueryMonitorsRequest request)
    {
        var query = new InstallerMonitorQuery(
            request.MonitorType,
            request.SearchText,
            request.GetNormalizedSortDir(),
            request.GetNormalizedPage(),
            request.GetNormalizedPageSize());
        var result = await installer.QueryMonitorsAsync(
            await CreateUserContextAsync(),
            query,
            HttpContext.RequestAborted);
        return MonitorApiMapper.ToQueryResponse(result);
    }

    [HttpGet("monitors/{id:guid}")]
    [ProducesResponseType(typeof(EntityResponse<MonitorDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Retrieves installer-visible monitor detail through the installer application service.
    public async Task<ActionResult<EntityResponse<MonitorDetailResponse>>> GetMonitor(Guid id)
    {
        var detail = await installer.GetMonitorDetailAsync(await CreateUserContextAsync(), User, id, HttpContext.RequestAborted);
        if (detail == null)
        {
            return MonitorNotFound(id);
        }

        return new EntityResponse<MonitorDetailResponse>
        {
            Item = detail
        };
    }

    [HttpPut("deployments/{deploymentId:guid}")]
    [ProducesResponseType(typeof(EntityResponse<MonitorDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Updates deployment data through the installer application service.
    public async Task<ActionResult<EntityResponse<MonitorDetailResponse>>> UpdateDeployment(Guid deploymentId, InstallerDeploymentMutationRequest request)
    {
        var user = await CreateUserContextAsync();
        var result = await installer.UpdateDeploymentAsync(user, User, deploymentId, request, HttpContext.RequestAborted);
        AddModelErrors(result.Errors);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        if (result.NotFound)
        {
            return MonitorNotFound(deploymentId);
        }

        return new EntityResponse<MonitorDetailResponse>
        {
            Item = result.Detail!
        };
    }

    [HttpGet("monitors/{id:guid}/status")]
    [ProducesResponseType(typeof(InstallerMonitorStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Reads installer-visible monitor status through the installer application service.
    public async Task<ActionResult<InstallerMonitorStatusResponse>> MonitorStatus(Guid id)
    {
        var status = await installer.GetMonitorStatusAsync(await CreateUserContextAsync(), id, HttpContext.RequestAborted);
        return status == null
            ? MonitorNotFound(id)
            : InstallerApiMapper.ToStatusResponse(status);
    }

    [HttpGet("what3words/convert")]
    [ProducesResponseType(typeof(What3WordsConvertResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    // Function summary: Converts a word address to coordinates through the installer application service.
    public async Task<ActionResult<What3WordsConvertResponse>> ConvertWhat3Words([FromQuery] string? what3words)
    {
        if (string.IsNullOrWhiteSpace(what3words))
        {
            ModelState.AddModelError(nameof(what3words), "What3words is required.");
            return ValidationProblem(ModelState);
        }

        var result = await installer.ConvertWhat3WordsAsync(what3words, HttpContext.RequestAborted);
        if (result.Value is not null)
        {
            return result.Value;
        }

        return result.Failure == What3WordsConversionFailureKind.ServiceUnavailable
            ? Problem(
                title: "What3words API key is not configured",
                detail: "Configure What3Words:ApiKey before using the conversion endpoint.",
                statusCode: StatusCodes.Status503ServiceUnavailable)
            : Problem(
                title: "What3words conversion failed",
                detail: $"The what3words service returned {result.ExternalStatusCode}.",
                statusCode: StatusCodes.Status502BadGateway);
    }

    // Function summary: Creates a transport-neutral current-user context from the authenticated HTTP user.
    private Task<PortalUserContext> CreateUserContextAsync()
    {
        return currentUserContextFactory.CreateAsync(User, HttpContext.RequestAborted);
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

    // Function summary: Builds the not-found response used for hidden or missing installer monitor resources.
    private NotFoundObjectResult MonitorNotFound(Guid id)
    {
        return NotFound(new ProblemDetails
        {
            Title = "Monitor not found",
            Detail = $"Monitor or deployment '{id}' was not found.",
            Status = StatusCodes.Status404NotFound
        });
    }
}
