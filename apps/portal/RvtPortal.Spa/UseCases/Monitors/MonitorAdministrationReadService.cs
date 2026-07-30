// File summary: Provides monitor inventory, options, assignment, detail, picture, and removal-impact read workflows for the portal API.
// Major updates:
// - 2026-07-09 pending Moved monitor administration read/query logic out of MonitorsController.
// - 2026-07-22 pending Scoped protected pictures and option metadata to the actor's authorized tenant graph.

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Application.Identity;
using RvtPortal.Application.Ports.Storage;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.UseCases.Sites;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.UseCases.Monitors;

public interface IMonitorAdministrationReadService
{
    // Function summary: Returns a role-scoped paged monitor inventory result.
    Task<MonitorInventoryResult> QueryAsync(
        MonitorInventoryRequest request,
        PortalUserContext actor,
        CancellationToken cancellationToken);

    // Function summary: Returns monitor assignment context for a site and optional selected contract.
    Task<MonitorAssignmentContextResult> GetAssignmentContextAsync(
        Guid siteId,
        Guid? contractId,
        PortalUserContext actor,
        CancellationToken cancellationToken);

    // Function summary: Rebuilds monitor detail after a read or mutation workflow.
    Task<MonitorDetailResponse?> GetDetailAsync(
        Guid monitorId,
        Guid? deploymentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken);

    // Function summary: Opens a protected monitor deployment picture after read authorization.
    Task<MonitorPictureModel?> GetPictureAsync(
        Guid monitorId,
        ClaimsPrincipal user,
        PortalUserContext actor,
        CancellationToken cancellationToken);

    // Function summary: Returns enriched unattached monitor removal candidates.
    Task<MonitorUnattachedInventoryResult> QueryUnattachedAsync(
        MonitorInventoryRequest request,
        PortalUserContext actor,
        CancellationToken cancellationToken);
}

public sealed record MonitorInventoryRequest(
    MonitorTypeEnum? MonitorType,
    string State,
    string? SearchText,
    string Sort,
    string SortDir,
    int Page,
    int PageSize);

public sealed class MonitorInventoryResult
{
    public bool Forbidden { get; init; }
    public List<MonitorListItem> Results { get; init; } = [];
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public bool HasPreviousPage { get; init; }
    public bool HasNextPage { get; init; }
    public string SearchText { get; init; } = "";
    public string Sort { get; init; } = "";
    public string SortDir { get; init; } = "";
    public string State { get; init; } = MonitorListStates.All;
    public bool IsScopedToCurrentUser { get; init; }
    public bool CanManage { get; init; }
    public bool CanUseInstallerTools { get; init; }
}

public sealed class MonitorUnattachedInventoryResult
{
    public List<UnattachedMonitorListItem> Results { get; init; } = [];
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public bool HasPreviousPage { get; init; }
    public bool HasNextPage { get; init; }
    public string SearchText { get; init; } = "";
    public string Sort { get; init; } = "";
    public string SortDir { get; init; } = "";
    public bool CanRemove { get; init; }
}

public sealed class MonitorOptionModel
{
    public string Value { get; init; } = "";
    public string Label { get; init; } = "";
}

public enum MonitorAssignmentContextStatus
{
    Found,
    SiteNotFound,
    SiteHasNoContracts,
    ContractNotAssignedToSite
}

public sealed class MonitorAssignmentContextResult
{
    public MonitorAssignmentContextStatus Status { get; init; }
    public MonitorAssignmentContextModel? Context { get; init; }

    // Function summary: Creates a found result that carries the assignment context model.
    public static MonitorAssignmentContextResult Found(MonitorAssignmentContextModel context)
    {
        return new MonitorAssignmentContextResult
        {
            Status = MonitorAssignmentContextStatus.Found,
            Context = context
        };
    }

    // Function summary: Creates a non-found or validation result for controller response mapping.
    public static MonitorAssignmentContextResult Problem(MonitorAssignmentContextStatus status)
    {
        return new MonitorAssignmentContextResult { Status = status };
    }
}

public sealed class MonitorAssignmentContextModel
{
    public Guid SiteId { get; init; }
    public string SiteName { get; init; } = "";
    public Guid? ContractId { get; init; }
    public string? ContractNumber { get; init; }
    public List<MonitorOptionModel> Contracts { get; init; } = [];
    public List<MonitorListItem> AvailableMonitors { get; init; } = [];
    public List<MonitorListItem> AssignedMonitors { get; init; } = [];
}

public sealed record MonitorPictureModel(Stream Stream, string ContentType, string FileName);

public sealed class MonitorAdministrationReadService : IMonitorAdministrationReadService
{
    private readonly RVTDbContext _domainContext;
    private readonly IMonitorPictureStorage _pictureStorage;
    private readonly IMonitorDetailReader _detailReader;
    private readonly IMonitorListReader _monitorListReader;
    private readonly IMonitorRemovalImpactReader _impactReader;
    private readonly TimeProvider _timeProvider;

    // Function summary: Initializes the monitor read service with domain readers and storage ports.
    public MonitorAdministrationReadService(
        RVTDbContext domainContext,
        IMonitorPictureStorage pictureStorage,
        IMonitorDetailReader detailReader,
        IMonitorListReader monitorListReader,
        IMonitorRemovalImpactReader impactReader,
        TimeProvider timeProvider)
    {
        _domainContext = domainContext;
        _pictureStorage = pictureStorage;
        _detailReader = detailReader;
        _monitorListReader = monitorListReader;
        _impactReader = impactReader;
        _timeProvider = timeProvider;
    }

    // Function summary: Returns a role-scoped paged monitor inventory result.
    public async Task<MonitorInventoryResult> QueryAsync(
        MonitorInventoryRequest request,
        PortalUserContext actor,
        CancellationToken cancellationToken)
    {
        string state = MonitorListStates.Normalize(request.State);
        if (IsInstallerOnly(actor))
        {
            state = MonitorListStates.Installer;
        }

        if (!CanUseState(actor, state))
        {
            return new MonitorInventoryResult { Forbidden = true };
        }

        MonitorListPage result = await _monitorListReader.QueryAsync(new MonitorListQuery(
            request.MonitorType,
            state,
            request.SearchText,
            request.Sort,
            request.SortDir,
            request.Page,
            request.PageSize,
            BuildRoleContext(actor)), cancellationToken);

        return new MonitorInventoryResult
        {
            Results = result.Results,
            Total = result.Total,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = TotalPages(result.Total, request.PageSize),
            HasPreviousPage = request.Page > 1 && result.Total > 0,
            HasNextPage = request.Page * request.PageSize < result.Total,
            SearchText = request.SearchText ?? "",
            Sort = request.Sort,
            SortDir = request.SortDir,
            State = state,
            IsScopedToCurrentUser = IsCompanyUser(actor),
            CanManage = actor.IsAdmin,
            CanUseInstallerTools = actor.IsAdmin || actor.IsInstaller
        };
    }

    // Function summary: Returns monitor assignment context for a site and optional selected contract.
    public async Task<MonitorAssignmentContextResult> GetAssignmentContextAsync(
        Guid siteId,
        Guid? contractId,
        PortalUserContext actor,
        CancellationToken cancellationToken)
    {
        Site? site = await _domainContext.Sites
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == siteId, cancellationToken);
        if (site == null)
        {
            return MonitorAssignmentContextResult.Problem(MonitorAssignmentContextStatus.SiteNotFound);
        }

        List<Contract> contracts = await _domainContext.Contracts
            .AsNoTracking()
            .Where(contract => contract.SiteiD == siteId)
            .OrderBy(contract => contract.ContractNumber)
            .ToListAsync(cancellationToken);
        if (contracts.Count == 0)
        {
            return MonitorAssignmentContextResult.Problem(MonitorAssignmentContextStatus.SiteHasNoContracts);
        }

        Contract? selectedContract = SelectAssignmentContract(contracts, contractId);
        if (contractId.HasValue && selectedContract == null)
        {
            return MonitorAssignmentContextResult.Problem(MonitorAssignmentContextStatus.ContractNotAssignedToSite);
        }

        MonitorAssignmentLists lists = await _monitorListReader.BuildAssignmentListsAsync(
            siteId,
            selectedContract?.Id,
            BuildRoleContext(actor),
            cancellationToken);

        return MonitorAssignmentContextResult.Found(new MonitorAssignmentContextModel
        {
            SiteId = site.Id,
            SiteName = site.SiteName,
            ContractId = selectedContract?.Id,
            ContractNumber = selectedContract?.ContractNumber,
            Contracts = [.. contracts.Select(contract => new MonitorOptionModel { Value = contract.Id.ToString(), Label = contract.ContractNumber })],
            AvailableMonitors = lists.AvailableMonitors,
            AssignedMonitors = lists.AssignedMonitors
        });
    }

    // Function summary: Rebuilds monitor detail after a read or mutation workflow.
    public async Task<MonitorDetailResponse?> GetDetailAsync(
        Guid monitorId,
        Guid? deploymentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        MonitorEntity? monitor = await FindMonitorAsync(monitorId, cancellationToken);
        if (monitor == null)
        {
            return null;
        }

        Deployment? deployment = deploymentId.HasValue
            ? await FindDeploymentAsync(deploymentId.Value, cancellationToken)
            : null;
        deployment ??= await FindCurrentDeploymentAsync(monitorId, cancellationToken);
        return await _detailReader.BuildAsync(monitor, deployment, user, cancellationToken);
    }

    // Function summary: Opens a protected monitor deployment picture after read authorization.
    public async Task<MonitorPictureModel?> GetPictureAsync(
        Guid monitorId,
        ClaimsPrincipal user,
        PortalUserContext actor,
        CancellationToken cancellationToken)
    {
        MonitorEntity? monitor = await FindMonitorAsync(monitorId, cancellationToken);
        if (monitor == null)
        {
            return null;
        }

        Deployment? deployment = await FindCurrentDeploymentAsync(monitorId, cancellationToken);
        if (deployment == null)
        {
            return null;
        }

        MonitorDetailResponse detail = await _detailReader.BuildAsync(monitor, deployment, user, cancellationToken);
        if (!await CanReadMonitorAsync(detail, actor, cancellationToken))
        {
            return null;
        }

        StoredContentFile? picture = await _pictureStorage.OpenReadAsync(deployment.PictureLink, cancellationToken);
        return picture == null
            ? null
            : new MonitorPictureModel(picture.Stream, picture.ContentType, picture.FileName);
    }

    // Function summary: Returns enriched unattached monitor removal candidates.
    public async Task<MonitorUnattachedInventoryResult> QueryUnattachedAsync(
        MonitorInventoryRequest request,
        PortalUserContext actor,
        CancellationToken cancellationToken)
    {
        MonitorListPage result = await _monitorListReader.QueryUnattachedAsync(new MonitorListQuery(
            request.MonitorType,
            MonitorListStates.All,
            request.SearchText,
            request.Sort,
            request.SortDir,
            request.Page,
            request.PageSize,
            BuildRoleContext(actor)), cancellationToken);
        IReadOnlyDictionary<Guid, MonitorRemovalImpactResponse> impacts = await _impactReader.BuildForPageAsync(
            [.. result.Results.Select(row => new MonitorRemovalImpactKey(row.Id, row.SerialId))],
            cancellationToken);
        List<UnattachedMonitorListItem> enrichedRows = new();
        foreach (MonitorListItem row in result.Results)
        {
            enrichedRows.Add(BuildUnattachedListItem(row, impacts[row.Id]));
        }

        return new MonitorUnattachedInventoryResult
        {
            Results = enrichedRows,
            Total = result.Total,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = TotalPages(result.Total, request.PageSize),
            HasPreviousPage = request.Page > 1 && result.Total > 0,
            HasNextPage = request.Page * request.PageSize < result.Total,
            SearchText = request.SearchText ?? "",
            Sort = request.Sort,
            SortDir = request.SortDir,
            CanRemove = actor.IsAdmin
        };
    }

    // Function summary: Builds the role context passed into database-backed monitor list queries.
    private static MonitorListRoleContext BuildRoleContext(PortalUserContext actor)
    {
        return new MonitorListRoleContext(actor.IsAdmin, actor.IsInstaller, IsCompanyUser(actor), actor.UserId, actor.CompanyId);
    }

    // Function summary: Evaluates whether the actor may use the requested inventory state.
    private static bool CanUseState(PortalUserContext actor, string state)
    {
        if (actor.IsAdmin)
        {
            return true;
        }

        if (state is MonitorListStates.New or MonitorListStates.NotInUse)
        {
            return false;
        }

        if (state == MonitorListStates.Installer)
        {
            return actor.IsInstaller;
        }

        return IsCompanyUser(actor);
    }

    // Function summary: Evaluates whether the actor may read one monitor row.
    private async Task<bool> CanReadMonitorAsync(
        MonitorListItem row,
        PortalUserContext actor,
        CancellationToken cancellationToken)
    {
        if (actor.IsAdmin)
        {
            return true;
        }

        if (actor.IsInstaller)
        {
            return row.IsAssigned &&
                row.CompanyId.HasValue &&
                actor.CompanyId.HasValue &&
                row.CompanyId.Value == actor.CompanyId.Value;
        }

        if (!IsCompanyUser(actor) || !row.SiteId.HasValue)
        {
            return false;
        }

        return (await VisibleSiteIdsAsync(actor, cancellationToken)).Contains(row.SiteId.Value);
    }

    // Function summary: Finds the current deployment for a monitor with site and contract details.
    private Task<Deployment?> FindCurrentDeploymentAsync(Guid monitorId, CancellationToken cancellationToken)
    {
        return _domainContext.Deployments
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

    // Function summary: Finds one deployment with site and contract details.
    private Task<Deployment?> FindDeploymentAsync(Guid deploymentId, CancellationToken cancellationToken)
    {
        return _domainContext.Deployments
            .AsNoTracking()
            .Include(deployment => deployment.Contract)
            .ThenInclude(contract => contract.Company)
            .Include(deployment => deployment.Contract)
            .ThenInclude(contract => contract.Site)
            .Include(deployment => deployment.Monitor)
            .SingleOrDefaultAsync(deployment => deployment.Id == deploymentId, cancellationToken);
    }

    // Function summary: Finds one active monitor.
    private Task<MonitorEntity?> FindMonitorAsync(Guid monitorId, CancellationToken cancellationToken)
    {
        return _domainContext.MonitorsList
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == monitorId && !item.Archived, cancellationToken);
    }

    // Function summary: Returns active site assignments visible to a company user.
    private async Task<HashSet<Guid>> VisibleSiteIdsAsync(PortalUserContext actor, CancellationToken cancellationToken)
    {
        if (!actor.UserId.HasValue)
        {
            return [];
        }

        List<Guid> siteIds = await _domainContext.SiteUsers
            .AsNoTracking()
            .Where(ActiveSiteAssignment.ForUser(actor.UserId.Value, _timeProvider.GetUtcNow().UtcDateTime))
            .Select(siteUser => siteUser.SiteId)
            .ToListAsync(cancellationToken);
        return [.. siteIds];
    }

    // Function summary: Selects the requested contract or the only contract when exactly one exists.
    private static Contract? SelectAssignmentContract(List<Contract> contracts, Guid? contractId)
    {
        if (contractId.HasValue)
        {
            return contracts.SingleOrDefault(contract => contract.Id == contractId.Value);
        }

        return contracts.Count == 1 ? contracts[0] : null;
    }

    // Function summary: Builds unattached monitor removal candidate data for callers.
    private static UnattachedMonitorListItem BuildUnattachedListItem(MonitorListItem row, MonitorRemovalImpactResponse impact)
    {
        return new UnattachedMonitorListItem
        {
            Id = row.Id,
            DeploymentId = row.DeploymentId,
            FleetNumber = row.FleetNumber,
            SerialId = row.SerialId,
            Manufacturer = row.Manufacturer,
            Model = row.Model,
            FirmwareVersion = row.FirmwareVersion,
            TypeOfMonitor = row.TypeOfMonitor,
            ContractId = row.ContractId,
            ContractNumber = row.ContractNumber,
            SiteId = row.SiteId,
            SiteName = row.SiteName,
            CompanyId = row.CompanyId,
            CompanyName = row.CompanyName,
            StartDate = row.StartDate,
            EndDate = row.EndDate,
            LastDataTime = row.LastDataTime,
            IsAssigned = row.IsAssigned,
            IsOffline = row.IsOffline,
            HasAlerts = row.HasAlerts,
            HasCautions = row.HasCautions,
            CanEdit = row.CanEdit,
            CanAssign = row.CanAssign,
            CanInstallerEdit = row.CanInstallerEdit,
            Impact = impact,
            HasRelatedData = impact.HasRelatedData,
            WillArchiveOnRemoval = impact.HasRelatedData
        };
    }

    // Function summary: Calculates page count for a query result.
    private static int TotalPages(int total, int pageSize)
    {
        return total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
    }

    // Function summary: Evaluates whether the actor is scoped only to installer role behavior.
    private static bool IsInstallerOnly(PortalUserContext actor)
    {
        return actor.IsInstaller && !actor.IsAdmin && !IsCompanyUser(actor);
    }

    // Function summary: Evaluates whether the actor is a non-admin company user.
    private static bool IsCompanyUser(PortalUserContext actor)
    {
        return actor.IsCompanyUser && !actor.IsAdmin;
    }
}
