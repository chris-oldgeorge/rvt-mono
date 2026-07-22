// File summary: Provides monitor inventory, options, assignment, detail, picture, and removal-impact read workflows for the portal API.
// Major updates:
// - 2026-07-09 pending Moved monitor administration read/query logic out of MonitorsController.
// - 2026-07-22 pending Scoped protected pictures and option metadata to the actor's authorized tenant graph.

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RVT.BusinessLogic.Application;
using RVT.BusinessLogic.Ports.Storage;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Sites;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Application.Monitors;

public interface IMonitorAdministrationReadService
{
    // Function summary: Returns a role-scoped paged monitor inventory result.
    Task<MonitorInventoryResult> QueryAsync(
        MonitorInventoryRequest request,
        PortalUserContext actor,
        CancellationToken cancellationToken);

    // Function summary: Returns monitor edit and assignment option lists.
    Task<MonitorOptionsModel> OptionsAsync(PortalUserContext actor, CancellationToken cancellationToken);

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

    // Function summary: Returns removal impact counts for a monitor, or null when the monitor is missing.
    Task<MonitorRemovalImpactResponse?> GetRemovalImpactAsync(Guid monitorId, CancellationToken cancellationToken);
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

public sealed class MonitorOptionsModel
{
    public List<MonitorOptionModel> MonitorTypes { get; init; } = [];
    public List<MonitorOptionModel> Contracts { get; init; } = [];
    public List<MonitorOptionModel> Sites { get; init; } = [];
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
    private readonly RVTDbContext domainContext;
    private readonly IMonitorPictureStorage pictureStorage;
    private readonly IMonitorDetailReader detailReader;
    private readonly IMonitorListReader monitorListReader;
    private readonly IMonitorRemovalImpactReader impactReader;
    private readonly TimeProvider timeProvider;

    // Function summary: Initializes the monitor read service with domain readers and storage ports.
    public MonitorAdministrationReadService(
        RVTDbContext domainContext,
        IMonitorPictureStorage pictureStorage,
        IMonitorDetailReader detailReader,
        IMonitorListReader monitorListReader,
        IMonitorRemovalImpactReader impactReader,
        TimeProvider timeProvider)
    {
        this.domainContext = domainContext;
        this.pictureStorage = pictureStorage;
        this.detailReader = detailReader;
        this.monitorListReader = monitorListReader;
        this.impactReader = impactReader;
        this.timeProvider = timeProvider;
    }

    // Function summary: Returns a role-scoped paged monitor inventory result.
    public async Task<MonitorInventoryResult> QueryAsync(
        MonitorInventoryRequest request,
        PortalUserContext actor,
        CancellationToken cancellationToken)
    {
        var state = MonitorListStates.Normalize(request.State);
        if (IsInstallerOnly(actor))
        {
            state = MonitorListStates.Installer;
        }

        if (!CanUseState(actor, state))
        {
            return new MonitorInventoryResult { Forbidden = true };
        }

        var result = await monitorListReader.QueryAsync(new MonitorListQuery(
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

    // Function summary: Returns monitor edit and assignment option lists.
    public async Task<MonitorOptionsModel> OptionsAsync(PortalUserContext actor, CancellationToken cancellationToken)
    {
        var visibleSiteIds = VisibleSiteIdsQuery(actor);
        var contracts = await domainContext.Contracts
            .AsNoTracking()
            .Include(contract => contract.Site)
            .Where(contract => contract.SiteiD != null && visibleSiteIds.Contains(contract.SiteiD.Value))
            .OrderBy(contract => contract.ContractNumber)
            .Select(contract => new MonitorOptionModel
            {
                Value = contract.Id.ToString(),
                Label = contract.Site == null
                    ? contract.ContractNumber
                    : $"{contract.ContractNumber} - {contract.Site.SiteName}"
            })
            .ToListAsync(cancellationToken);
        var sites = await domainContext.Sites
            .AsNoTracking()
            .Where(site => !site.Archived && visibleSiteIds.Contains(site.Id))
            .OrderBy(site => site.SiteName)
            .Select(site => new MonitorOptionModel { Value = site.Id.ToString(), Label = site.SiteName })
            .ToListAsync(cancellationToken);

        return new MonitorOptionsModel
        {
            MonitorTypes = Enum.GetValues<MonitorTypeEnum>()
                .Select(value => new MonitorOptionModel { Value = value.ToString(), Label = value.ToString() })
                .ToList(),
            Contracts = contracts,
            Sites = sites
        };
    }

    // Function summary: Returns monitor assignment context for a site and optional selected contract.
    public async Task<MonitorAssignmentContextResult> GetAssignmentContextAsync(
        Guid siteId,
        Guid? contractId,
        PortalUserContext actor,
        CancellationToken cancellationToken)
    {
        var site = await domainContext.Sites
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == siteId, cancellationToken);
        if (site == null)
        {
            return MonitorAssignmentContextResult.Problem(MonitorAssignmentContextStatus.SiteNotFound);
        }

        var contracts = await domainContext.Contracts
            .AsNoTracking()
            .Where(contract => contract.SiteiD == siteId)
            .OrderBy(contract => contract.ContractNumber)
            .ToListAsync(cancellationToken);
        if (contracts.Count == 0)
        {
            return MonitorAssignmentContextResult.Problem(MonitorAssignmentContextStatus.SiteHasNoContracts);
        }

        var selectedContract = SelectAssignmentContract(contracts, contractId);
        if (contractId.HasValue && selectedContract == null)
        {
            return MonitorAssignmentContextResult.Problem(MonitorAssignmentContextStatus.ContractNotAssignedToSite);
        }

        var lists = await monitorListReader.BuildAssignmentListsAsync(
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
            Contracts = contracts
                .Select(contract => new MonitorOptionModel { Value = contract.Id.ToString(), Label = contract.ContractNumber })
                .ToList(),
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
        var monitor = await FindMonitorAsync(monitorId, cancellationToken);
        if (monitor == null)
        {
            return null;
        }

        var deployment = deploymentId.HasValue
            ? await FindDeploymentAsync(deploymentId.Value, cancellationToken)
            : null;
        deployment ??= await FindCurrentDeploymentAsync(monitorId, cancellationToken);
        return await detailReader.BuildAsync(monitor, deployment, user, cancellationToken);
    }

    // Function summary: Opens a protected monitor deployment picture after read authorization.
    public async Task<MonitorPictureModel?> GetPictureAsync(
        Guid monitorId,
        ClaimsPrincipal user,
        PortalUserContext actor,
        CancellationToken cancellationToken)
    {
        var monitor = await FindMonitorAsync(monitorId, cancellationToken);
        if (monitor == null)
        {
            return null;
        }

        var deployment = await FindCurrentDeploymentAsync(monitorId, cancellationToken);
        if (deployment == null)
        {
            return null;
        }

        var detail = await detailReader.BuildAsync(monitor, deployment, user, cancellationToken);
        if (!await CanReadMonitorAsync(detail, actor, cancellationToken))
        {
            return null;
        }

        var picture = await pictureStorage.OpenReadAsync(deployment.PictureLink, cancellationToken);
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
        var result = await monitorListReader.QueryUnattachedAsync(new MonitorListQuery(
            request.MonitorType,
            MonitorListStates.All,
            request.SearchText,
            request.Sort,
            request.SortDir,
            request.Page,
            request.PageSize,
            BuildRoleContext(actor)), cancellationToken);
        var enrichedRows = new List<UnattachedMonitorListItem>();
        foreach (var row in result.Results)
        {
            var impact = await impactReader.BuildAsync(row.Id, row.SerialId, cancellationToken);
            enrichedRows.Add(BuildUnattachedListItem(row, impact));
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

    // Function summary: Returns removal impact counts for a monitor, or null when the monitor is missing.
    public async Task<MonitorRemovalImpactResponse?> GetRemovalImpactAsync(Guid monitorId, CancellationToken cancellationToken)
    {
        var monitor = await FindMonitorAsync(monitorId, cancellationToken);
        return monitor == null
            ? null
            : await impactReader.BuildAsync(monitorId, monitor.SerialId, cancellationToken);
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

    // Function summary: Finds one deployment with site and contract details.
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

    // Function summary: Finds one active monitor.
    private Task<MonitorEntity?> FindMonitorAsync(Guid monitorId, CancellationToken cancellationToken)
    {
        return domainContext.MonitorsList
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

        var siteIds = await domainContext.SiteUsers
            .AsNoTracking()
            .Where(ActiveSiteAssignment.ForUser(actor.UserId.Value, timeProvider.GetUtcNow().UtcDateTime))
            .Select(siteUser => siteUser.SiteId)
            .ToListAsync(cancellationToken);
        return siteIds.ToHashSet();
    }

    // Function summary: Builds the site-id graph visible to the actor for monitor option metadata.
    private IQueryable<Guid> VisibleSiteIdsQuery(PortalUserContext actor)
    {
        if (actor.IsAdmin)
        {
            return domainContext.Sites.Select(site => site.Id);
        }

        if (actor.IsInstaller)
        {
            return actor.CompanyId.HasValue
                ? domainContext.Contracts
                    .Where(contract => contract.SiteiD != null && contract.CompanyId == actor.CompanyId.Value)
                    .Select(contract => contract.SiteiD!.Value)
                : domainContext.Sites.Where(_ => false).Select(site => site.Id);
        }

        return IsCompanyUser(actor) && actor.UserId.HasValue
            ? domainContext.SiteUsers
                .Where(ActiveSiteAssignment.ForUser(actor.UserId.Value, timeProvider.GetUtcNow().UtcDateTime))
                .Select(siteUser => siteUser.SiteId)
            : domainContext.Sites.Where(_ => false).Select(site => site.Id);
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
