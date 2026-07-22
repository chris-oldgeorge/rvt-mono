// File summary: Handles CQRS queries for monitor detail read models.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-09 pending Added MediatR query handler for critical monitor detail reads.
// - 2026-06-10 pending Removed redundant async/await from deployment lookup helpers.

using MediatR;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Application.Monitors;

public sealed record GetMonitorDetailQuery(Guid Id, bool IsDeploymentId = false) : IRequest<MonitorDetailResponse?>;

public sealed class GetMonitorDetailQueryHandler : IRequestHandler<GetMonitorDetailQuery, MonitorDetailResponse?>
{
    private readonly RVTDbContext domainContext;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IMonitorDetailReader detailReader;
    private readonly IMonitorReadAuthorizationService authorizationService;

    // Function summary: Initializes dependencies used to load and authorize monitor detail read models.
    public GetMonitorDetailQueryHandler(
        RVTDbContext domainContext,
        IHttpContextAccessor httpContextAccessor,
        IMonitorDetailReader detailReader,
        IMonitorReadAuthorizationService authorizationService)
    {
        this.domainContext = domainContext;
        this.httpContextAccessor = httpContextAccessor;
        this.detailReader = detailReader;
        this.authorizationService = authorizationService;
    }

    // Function summary: Handles monitor detail queries by monitor id or deployment id.
    public async Task<MonitorDetailResponse?> Handle(GetMonitorDetailQuery request, CancellationToken cancellationToken)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user == null)
        {
            return null;
        }

        MonitorEntity? monitor;
        Deployment? deployment;
        if (request.IsDeploymentId)
        {
            deployment = await FindDeploymentAsync(request.Id, cancellationToken);
            monitor = deployment?.Monitor;
        }
        else
        {
            monitor = await domainContext.MonitorsList
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.Id && !item.Archived, cancellationToken);
            deployment = monitor == null ? null : await FindCurrentDeploymentAsync(request.Id, cancellationToken);
        }

        if (monitor == null)
        {
            return null;
        }

        var detail = await detailReader.BuildAsync(monitor, deployment, user, cancellationToken);
        return await authorizationService.CanReadAsync(detail, user, cancellationToken) ? detail : null;
    }

    // Function summary: Finds the current deployment for a monitor detail query.
    private Task<Deployment?> FindCurrentDeploymentAsync(Guid monitorId, CancellationToken cancellationToken)
    {
        return domainContext.Deployments
            .AsNoTracking()
            .Include(deployment => deployment.Contract)
            .ThenInclude(contract => contract.Company)
            .Include(deployment => deployment.Contract)
            .ThenInclude(contract => contract.Site)
            .Include(deployment => deployment.Monitor)
            .Where(deployment => deployment.MonitorId == monitorId && deployment.EndDate == null)
            .OrderByDescending(deployment => deployment.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // Function summary: Finds a historical deployment for a monitor detail query.
    private Task<Deployment?> FindDeploymentAsync(Guid deploymentId, CancellationToken cancellationToken)
    {
        return domainContext.Deployments
            .AsNoTracking()
            .Include(deployment => deployment.Contract)
            .ThenInclude(contract => contract.Company)
            .Include(deployment => deployment.Contract)
            .ThenInclude(contract => contract.Site)
            .Include(deployment => deployment.Monitor)
            .SingleOrDefaultAsync(deployment => deployment.Id == deploymentId, cancellationToken);
    }
}
