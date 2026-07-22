// File summary: Handles transactional CQRS commands for installer deployment mutation workflows.
// Major updates:
// - 2026-06-26 pending Moved installer deployment-location writes behind MediatR transactional commands.

using MediatR;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Common;

namespace RvtPortal.Spa.Application.Installers;

public sealed record UpdateInstallerDeploymentLocationCommand(Guid DeploymentId, InstallerDeploymentMutationRequest Request)
    : IRequest<InstallerDeploymentCommandResult>, ITransactionalRequest;

public sealed class InstallerDeploymentCommandResult : ITransactionOutcome
{
    public bool NotFound { get; set; }
    public Guid? MonitorId { get; set; }
    public Dictionary<string, string[]> Errors { get; } = [];
    public bool ShouldCommit => !NotFound && Errors.Count == 0;
}

public sealed class UpdateInstallerDeploymentLocationCommandHandler
    : IRequestHandler<UpdateInstallerDeploymentLocationCommand, InstallerDeploymentCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional installer deployment-location command handler.
    public UpdateInstallerDeploymentLocationCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Updates deployment location fields for an active deployment.
    public async Task<InstallerDeploymentCommandResult> Handle(
        UpdateInstallerDeploymentLocationCommand request,
        CancellationToken cancellationToken)
    {
        var result = new InstallerDeploymentCommandResult();
        Validate(request.Request, result.Errors);
        if (result.Errors.Count > 0)
        {
            return result;
        }

        var deployment = await domainContext.Deployments
            .Include(item => item.Monitor)
            .SingleOrDefaultAsync(item => item.Id == request.DeploymentId && item.EndDate == null, cancellationToken);
        if (deployment == null)
        {
            result.NotFound = true;
            return result;
        }

        deployment.What3words = EmptyToNull(request.Request.What3words);
        deployment.Location = EmptyToNull(request.Request.Location);
        deployment.Lat = request.Request.Lat;
        deployment.Lng = request.Request.Lng;
        result.MonitorId = deployment.MonitorId;
        return result;
    }

    private static void Validate(InstallerDeploymentMutationRequest request, Dictionary<string, string[]> errors)
    {
        if (request.Lat is < -90 or > 90)
        {
            AddError(errors, nameof(InstallerDeploymentMutationRequest.Lat), "Latitude must be between -90 and 90.");
        }
        if (request.Lng is < -180 or > 180)
        {
            AddError(errors, nameof(InstallerDeploymentMutationRequest.Lng), "Longitude must be between -180 and 180.");
        }
        if (request.Location?.Length > 256)
        {
            AddError(errors, nameof(InstallerDeploymentMutationRequest.Location), "Location must be 256 characters or fewer.");
        }
        if (request.What3words?.Length > 256)
        {
            AddError(errors, nameof(InstallerDeploymentMutationRequest.What3words), "What3words must be 256 characters or fewer.");
        }
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void AddError(Dictionary<string, string[]> errors, string key, string message)
    {
        errors[key] = errors.TryGetValue(key, out var existing)
            ? [.. existing, message]
            : [message];
    }
}
