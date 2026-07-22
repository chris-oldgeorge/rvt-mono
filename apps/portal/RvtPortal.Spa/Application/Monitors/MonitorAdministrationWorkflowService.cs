// File summary: Coordinates monitor detail and mutation workflows so API controllers do not dispatch MediatR directly.
// Major updates:
// - 2026-07-09 pending Moved monitor administration write orchestration out of the API controller.

using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RVT.BusinessLogic.Ports.Storage;
using RVT.DataAccess.Context;
using RvtPortal.Spa.Api;

namespace RvtPortal.Spa.Application.Monitors;

public interface IMonitorAdministrationWorkflowService
{
    // Function summary: Returns an authorized monitor detail by monitor id.
    Task<MonitorDetailResponse?> GetDetailAsync(Guid monitorId, ClaimsPrincipal user, CancellationToken cancellationToken);

    // Function summary: Returns an authorized monitor detail by deployment id.
    Task<MonitorDetailResponse?> GetDeploymentDetailAsync(Guid deploymentId, ClaimsPrincipal user, CancellationToken cancellationToken);

    // Function summary: Updates monitor metadata and optional deployment coordinates.
    Task<MonitorDetailWorkflowResult> UpdateAsync(Guid monitorId, MonitorMutationRequest request, ClaimsPrincipal user, CancellationToken cancellationToken);

    // Function summary: Uploads a current deployment picture and returns refreshed monitor detail.
    Task<MonitorDetailWorkflowResult> UploadPictureAsync(Guid monitorId, IUploadedContent? picture, CancellationToken cancellationToken);

    // Function summary: Sets a monitor fleet number and returns refreshed monitor detail.
    Task<MonitorDetailWorkflowResult> SetFleetNumberAsync(Guid monitorId, string fleetNumber, ClaimsPrincipal user, CancellationToken cancellationToken);

    // Function summary: Assigns a monitor to a contract and returns refreshed monitor detail.
    Task<MonitorDetailWorkflowResult> AssignToContractAsync(Guid monitorId, Guid contractId, ClaimsPrincipal user, CancellationToken cancellationToken);

    // Function summary: Removes the active contract assignment for a monitor.
    Task<MonitorMutationWorkflowResult> RemoveFromContractAsync(Guid monitorId, CancellationToken cancellationToken);

    // Function summary: Removes or archives an unattached monitor.
    Task<MonitorRemovalWorkflowResult> RemoveUnattachedAsync(Guid monitorId, string? reason, string? archivedBy, CancellationToken cancellationToken);

    // Function summary: Creates default alert levels for eligible monitors.
    Task<DefaultMonitorsResponse> CreateDefaultAlertLevelsAsync(CancellationToken cancellationToken);
}

public sealed class MonitorDetailWorkflowResult
{
    public bool NotFound { get; init; }
    public Guid? MissingId { get; init; }
    public MonitorDetailResponse? Detail { get; init; }
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();

    // Function summary: Builds a missing monitor result.
    public static MonitorDetailWorkflowResult Missing(Guid? missingId = null)
    {
        return new MonitorDetailWorkflowResult { NotFound = true, MissingId = missingId };
    }

    // Function summary: Builds a validation result from command errors.
    public static MonitorDetailWorkflowResult Validation(IReadOnlyDictionary<string, string[]> errors)
    {
        return new MonitorDetailWorkflowResult { Errors = errors };
    }
}

public sealed class MonitorMutationWorkflowResult
{
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();

    // Function summary: Builds a validation result from command errors.
    public static MonitorMutationWorkflowResult Validation(IReadOnlyDictionary<string, string[]> errors)
    {
        return new MonitorMutationWorkflowResult { Errors = errors };
    }
}

public sealed class MonitorRemovalWorkflowResult
{
    public bool NotFound { get; init; }
    public MonitorRemovalResponse? Response { get; init; }
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();
}

public sealed class MonitorAdministrationWorkflowService : IMonitorAdministrationWorkflowService
{
    private readonly RVTDbContext domainContext;
    private readonly IMediator mediator;
    private readonly IMonitorAdministrationReadService monitorReads;
    private readonly IMonitorReadAuthorizationService authorizationService;

    // Function summary: Initializes monitor workflows with command dispatch, read models, and detail authorization.
    public MonitorAdministrationWorkflowService(
        RVTDbContext domainContext,
        IMediator mediator,
        IMonitorAdministrationReadService monitorReads,
        IMonitorReadAuthorizationService authorizationService)
    {
        this.domainContext = domainContext;
        this.mediator = mediator;
        this.monitorReads = monitorReads;
        this.authorizationService = authorizationService;
    }

    // Function summary: Returns an authorized monitor detail by monitor id.
    public Task<MonitorDetailResponse?> GetDetailAsync(Guid monitorId, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        return BuildAuthorizedDetailAsync(monitorId, null, user, cancellationToken);
    }

    // Function summary: Returns an authorized monitor detail by deployment id.
    public async Task<MonitorDetailResponse?> GetDeploymentDetailAsync(Guid deploymentId, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var monitorId = await domainContext.Deployments
            .AsNoTracking()
            .Where(deployment => deployment.Id == deploymentId)
            .Select(deployment => (Guid?)deployment.MonitorId)
            .SingleOrDefaultAsync(cancellationToken);
        return monitorId.HasValue
            ? await BuildAuthorizedDetailAsync(monitorId.Value, deploymentId, user, cancellationToken)
            : null;
    }

    // Function summary: Updates monitor metadata and optional deployment coordinates.
    public async Task<MonitorDetailWorkflowResult> UpdateAsync(
        Guid monitorId,
        MonitorMutationRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateMonitorCommand(monitorId, request), cancellationToken);
        return await BuildDetailResultAsync(result, monitorId, result.DeploymentId, user, cancellationToken);
    }

    // Function summary: Uploads a current deployment picture and returns refreshed monitor detail.
    public async Task<MonitorDetailWorkflowResult> UploadPictureAsync(Guid monitorId, IUploadedContent? picture, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UploadMonitorPictureCommand(monitorId, picture), cancellationToken);
        return result.NotFound
            ? MonitorDetailWorkflowResult.Missing(monitorId)
            : new MonitorDetailWorkflowResult
            {
                Detail = result.Detail,
                Errors = result.Errors
            };
    }

    // Function summary: Sets a monitor fleet number and returns refreshed monitor detail.
    public async Task<MonitorDetailWorkflowResult> SetFleetNumberAsync(
        Guid monitorId,
        string fleetNumber,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SetMonitorFleetNumberCommand(monitorId, fleetNumber), cancellationToken);
        return await BuildDetailResultAsync(result, monitorId, null, user, cancellationToken);
    }

    // Function summary: Assigns a monitor to a contract and returns refreshed monitor detail.
    public async Task<MonitorDetailWorkflowResult> AssignToContractAsync(
        Guid monitorId,
        Guid contractId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AssignMonitorToContractCommand(monitorId, contractId), cancellationToken);
        if (result.NotFound)
        {
            return MonitorDetailWorkflowResult.Missing(monitorId);
        }

        if (result.Errors.Count > 0)
        {
            return MonitorDetailWorkflowResult.Validation(result.Errors);
        }

        if (!result.DeploymentId.HasValue)
        {
            return MonitorDetailWorkflowResult.Missing(monitorId);
        }

        var detail = await BuildAuthorizedDetailAsync(monitorId, result.DeploymentId, user, cancellationToken);
        return detail == null
            ? MonitorDetailWorkflowResult.Missing(monitorId)
            : new MonitorDetailWorkflowResult { Detail = detail };
    }

    // Function summary: Removes the active contract assignment for a monitor.
    public async Task<MonitorMutationWorkflowResult> RemoveFromContractAsync(Guid monitorId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RemoveMonitorFromContractCommand(monitorId), cancellationToken);
        return MonitorMutationWorkflowResult.Validation(result.Errors);
    }

    // Function summary: Removes or archives an unattached monitor.
    public async Task<MonitorRemovalWorkflowResult> RemoveUnattachedAsync(
        Guid monitorId,
        string? reason,
        string? archivedBy,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RemoveUnattachedMonitorCommand(monitorId, reason, archivedBy), cancellationToken);
        return new MonitorRemovalWorkflowResult
        {
            NotFound = result.NotFound,
            Response = result.Response,
            Errors = result.Errors
        };
    }

    // Function summary: Creates default alert levels for eligible monitors.
    public Task<DefaultMonitorsResponse> CreateDefaultAlertLevelsAsync(CancellationToken cancellationToken)
    {
        return mediator.Send(new CreateDefaultMonitorAlertLevelsCommand(), cancellationToken);
    }

    // Function summary: Converts a monitor command result into an authorized detail workflow result.
    private async Task<MonitorDetailWorkflowResult> BuildDetailResultAsync(
        MonitorMutationCommandResult result,
        Guid monitorId,
        Guid? deploymentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (result.NotFound)
        {
            return MonitorDetailWorkflowResult.Missing(result.MissingId ?? monitorId);
        }

        if (result.Errors.Count > 0)
        {
            return MonitorDetailWorkflowResult.Validation(result.Errors);
        }

        var detail = await BuildAuthorizedDetailAsync(monitorId, deploymentId, user, cancellationToken);
        return detail == null
            ? MonitorDetailWorkflowResult.Missing(monitorId)
            : new MonitorDetailWorkflowResult { Detail = detail };
    }

    // Function summary: Builds detail and enforces the same read authorization used by monitor detail queries.
    private async Task<MonitorDetailResponse?> BuildAuthorizedDetailAsync(
        Guid monitorId,
        Guid? deploymentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var detail = await monitorReads.GetDetailAsync(monitorId, deploymentId, user, cancellationToken);
        return detail != null && await authorizationService.CanReadAsync(detail, user, cancellationToken)
            ? detail
            : null;
    }
}
