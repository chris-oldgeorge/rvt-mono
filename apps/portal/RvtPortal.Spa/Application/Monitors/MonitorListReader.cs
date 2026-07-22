// File summary: Builds monitor inventory read models with database-side filtering, sorting, counting, and paging.
// Major updates:
// - 2026-06-26 pending Scoped monitor list alert/caution flags to active deployment/contract ownership windows.
// - 2026-06-26 pending Scoped installer monitor inventory reads to the installer's assigned company.
// - 2026-06-26 pending Split base row projection helpers for Sonar complexity cleanup.
// - 2026-06-25 pending Moved monitor inventory and unattached list shaping out of controllers and into EF query projections.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Application.Monitors;

public sealed record MonitorListRoleContext(
    bool IsAdmin,
    bool IsInstaller,
    bool IsCompanyUser,
    Guid? CurrentUserId,
    Guid? CompanyId);

public sealed record MonitorListQuery(
    MonitorTypeEnum? MonitorType,
    string State,
    string? SearchText,
    string Sort,
    string SortDir,
    int Page,
    int PageSize,
    MonitorListRoleContext RoleContext);

public sealed class MonitorListPage
{
    public List<MonitorListItem> Results { get; set; } = [];
    public int Total { get; set; }
}

public sealed class MonitorAssignmentLists
{
    public List<MonitorListItem> AvailableMonitors { get; set; } = [];
    public List<MonitorListItem> AssignedMonitors { get; set; } = [];
}

public interface IMonitorListReader
{
    // Function summary: Builds a paged monitor inventory list from database-side filters.
    Task<MonitorListPage> QueryAsync(MonitorListQuery query, CancellationToken cancellationToken);

    // Function summary: Builds a paged unattached monitor removal candidate list from database-side filters.
    Task<MonitorListPage> QueryUnattachedAsync(MonitorListQuery query, CancellationToken cancellationToken);

    // Function summary: Builds assignment dialog monitor lists without loading the full fleet.
    Task<MonitorAssignmentLists> BuildAssignmentListsAsync(
        Guid siteId,
        Guid? contractId,
        MonitorListRoleContext roleContext,
        CancellationToken cancellationToken);
}

public sealed class MonitorListReader : IMonitorListReader
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the database-backed monitor list reader.
    public MonitorListReader(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Builds a paged monitor inventory list from database-side filters.
    public async Task<MonitorListPage> QueryAsync(MonitorListQuery query, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddHours(-1);
        var rows = BuildBaseRows(query.MonitorType);
        rows = await ApplyRoleVisibilityAsync(rows, query.State, query.RoleContext, cancellationToken);
        rows = ApplyState(rows, query.State, cutoff);
        rows = ApplyInventorySearch(rows, query.SearchText);
        return await BuildPageAsync(rows, query, cutoff, cancellationToken);
    }

    // Function summary: Builds a paged unattached monitor removal candidate list from database-side filters.
    public Task<MonitorListPage> QueryUnattachedAsync(MonitorListQuery query, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddHours(-1);
        var rows = BuildBaseRows(query.MonitorType)
            .Where(row => row.DeploymentId == null && row.SiteId == null);
        rows = ApplyUnattachedSearch(rows, query.SearchText);
        return BuildPageAsync(rows, query, cutoff, cancellationToken);
    }

    // Function summary: Builds assignment dialog monitor lists without loading the full fleet.
    public async Task<MonitorAssignmentLists> BuildAssignmentListsAsync(
        Guid siteId,
        Guid? contractId,
        MonitorListRoleContext roleContext,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddHours(-1);
        var rows = BuildBaseRows(null);
        var availableRows = await ApplyState(rows, MonitorListStates.NotInUse, cutoff)
            .OrderBy(row => row.FleetNumber)
            .ToListAsync(cancellationToken);
        var assignedRows = await rows
            .Where(row => row.SiteId == siteId && (!contractId.HasValue || row.ContractId == contractId.Value))
            .OrderBy(row => row.FleetNumber)
            .ToListAsync(cancellationToken);

        return new MonitorAssignmentLists
        {
            AvailableMonitors = availableRows.Select(row => ToItem(row, roleContext, cutoff)).ToList(),
            AssignedMonitors = assignedRows.Select(row => ToItem(row, roleContext, cutoff)).ToList()
        };
    }

    // Function summary: Builds the shared monitor list projection used by inventory endpoints.
    // internal (not private) so MonitorListReaderSqlTests can compile this query to SQL and prove it translates.
    internal IQueryable<MonitorListRow> BuildBaseRows(MonitorTypeEnum? monitorType)
    {
        return ProjectMonitorRows(AddLatestDataTime(BuildMonitorScope(monitorType)));
    }

    // Function summary: Builds the shared monitor entity scope for list projections.
    private IQueryable<MonitorEntity> BuildMonitorScope(MonitorTypeEnum? monitorType)
    {
        return domainContext.MonitorsList
            .AsNoTracking()
            .Where(monitor => !monitor.Archived)
            .Where(monitor => !monitorType.HasValue || monitor.TypeOfMonitor == monitorType.Value);
    }

    // Function summary: Projects the latest available monitor reading timestamp.
    private static IQueryable<MonitorWithLastDataTime> AddLatestDataTime(IQueryable<MonitorEntity> monitors)
    {
        var withLastOneMinute = monitors.Select(monitor => new
        {
            Monitor = monitor,
            LastDataTime = monitor.LastDataTime15Min == null ||
                (monitor.LastDataTime1Min != null && monitor.LastDataTime1Min > monitor.LastDataTime15Min)
                    ? monitor.LastDataTime1Min
                    : monitor.LastDataTime15Min
        });
        var withLastOneHour = withLastOneMinute.Select(row => new
        {
            row.Monitor,
            LastDataTime = row.LastDataTime == null ||
                (row.Monitor.LastDataTime1Hour != null && row.Monitor.LastDataTime1Hour > row.LastDataTime)
                    ? row.Monitor.LastDataTime1Hour
                    : row.LastDataTime
        });
        var withLastDay = withLastOneHour.Select(row => new
        {
            row.Monitor,
            LastDataTime = row.LastDataTime == null ||
                (row.Monitor.LastDataTime24Hour != null && row.Monitor.LastDataTime24Hour > row.LastDataTime)
                    ? row.Monitor.LastDataTime24Hour
                    : row.LastDataTime
        });

        return withLastDay.Select(row => new MonitorWithLastDataTime
        {
            Monitor = row.Monitor,
            LastDataTime = row.LastDataTime
        });
    }

    // Function summary: Projects monitor and active deployment data into the list row shape.
    private IQueryable<MonitorListRow> ProjectMonitorRows(IQueryable<MonitorWithLastDataTime> rows)
    {
        // Each projected deployment column used to repeat the same "latest active deployment" correlated
        // subquery, so a nine-column row recomputed it nine times. A left-correlated join (SelectMany over a
        // Take(1) + DefaultIfEmpty) resolves it once as an OUTER APPLY that the rest of the row reads from.
        //
        // Note: projecting the deployment into a nested object and then reading its members does NOT work -
        // EF's optimizer pushes the subquery back down into each member access, restoring all nine copies.
        // MonitorListReaderSqlTests asserts the generated SQL orders by start_date at most once.
        return rows.SelectMany(
            row => domainContext.Deployments
                .Where(deployment => deployment.MonitorId == row.Monitor.Id && deployment.EndDate == null)
                .OrderByDescending(deployment => deployment.StartDate)
                .ThenByDescending(deployment => deployment.Id)
                .Take(1)
                .DefaultIfEmpty(),
            (row, current) => new MonitorListRow
            {
                Id = row.Monitor.Id,
                DeploymentId = current == null ? null : (Guid?)current.Id,
                FleetNumber = row.Monitor.FleetNr,
                SerialId = row.Monitor.SerialId,
                Manufacturer = row.Monitor.Manufacturer,
                Model = row.Monitor.Model,
                FirmwareVersion = row.Monitor.FirmwareVersion,
                TypeOfMonitor = row.Monitor.TypeOfMonitor,
                ContractId = current == null ? null : (Guid?)current.ContractId,
                ContractNumber = current == null ? null : current.Contract.ContractNumber,
                SiteId = current == null ? null : current.Contract.SiteiD,
                SiteName = current == null || current.Contract.Site == null ? null : current.Contract.Site.SiteName,
                CompanyId = current == null ? null : (Guid?)current.Contract.CompanyId,
                CompanyName = current == null ? null : current.Contract.Company.CompanyName,
                StartDate = current == null ? null : (DateTime?)current.StartDate,
                EndDate = current == null ? null : current.EndDate,
                LastDataTime = row.LastDataTime,
                HasAlerts = false,
                HasCautions = false
            });
    }

    // Function summary: Applies role visibility before state and paging.
    private async Task<IQueryable<MonitorListRow>> ApplyRoleVisibilityAsync(
        IQueryable<MonitorListRow> rows,
        string state,
        MonitorListRoleContext roleContext,
        CancellationToken cancellationToken)
    {
        if (roleContext.IsAdmin)
        {
            return rows;
        }

        if (roleContext.IsInstaller)
        {
            return state == MonitorListStates.Installer
                ? rows.Where(row => row.DeploymentId != null && row.CompanyId == roleContext.CompanyId)
                : rows.Where(_ => false);
        }

        if (!roleContext.IsCompanyUser || !roleContext.CurrentUserId.HasValue)
        {
            return rows.Where(_ => false);
        }

        var visibleSiteIds = await domainContext.SiteUsers
            .AsNoTracking()
            .Where(siteUser => siteUser.UserId == roleContext.CurrentUserId.Value && siteUser.EndDate == null)
            .Select(siteUser => siteUser.SiteId)
            .ToListAsync(cancellationToken);
        return rows.Where(row => row.SiteId != null && visibleSiteIds.Contains(row.SiteId ?? Guid.Empty));
    }

    // Function summary: Applies monitor inventory state filters to the query.
    private static IQueryable<MonitorListRow> ApplyState(
        IQueryable<MonitorListRow> rows,
        string state,
        DateTime offlineCutoff)
    {
        return state switch
        {
            MonitorListStates.New => rows.Where(row => row.FleetNumber == null || row.FleetNumber == ""),
            MonitorListStates.NotInUse => rows.Where(row => row.FleetNumber != null && row.FleetNumber != "" && row.DeploymentId == null),
            MonitorListStates.Offline => rows.Where(row =>
                row.FleetNumber != null &&
                row.FleetNumber != "" &&
                row.DeploymentId != null &&
                (row.LastDataTime == null || row.LastDataTime < offlineCutoff)),
            MonitorListStates.Online => rows.Where(row =>
                row.FleetNumber != null &&
                row.FleetNumber != "" &&
                row.DeploymentId != null &&
                row.LastDataTime >= offlineCutoff),
            MonitorListStates.Installer => rows.Where(row => row.FleetNumber != null && row.FleetNumber != "" && row.DeploymentId != null),
            _ => rows.Where(row => row.FleetNumber != null && row.FleetNumber != "")
        };
    }

    // Function summary: Applies monitor inventory search filters to the query.
    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF query predicate; ToLower() is the only case-insensitive form that translates on Npgsql and runs on the InMemory test provider. See docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF query predicate; see docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF query predicate; StringComparison does not translate on Npgsql. See docs/development/portal/sonar/globalization-suppressions.md")]
    private static IQueryable<MonitorListRow> ApplyInventorySearch(IQueryable<MonitorListRow> rows, string? searchText)
    {
        var search = NormalizeSearch(searchText);
        if (search == null)
        {
            return rows;
        }

        var matchingTypes = MatchingMonitorTypes(search);
        return rows.Where(row =>
            (row.FleetNumber != null && row.FleetNumber.ToLower().Contains(search)) ||
            row.SerialId.ToLower().Contains(search) ||
            matchingTypes.Contains(row.TypeOfMonitor) ||
            (row.SiteName != null && row.SiteName.ToLower().Contains(search)) ||
            (row.ContractNumber != null && row.ContractNumber.ToLower().Contains(search)) ||
            (row.CompanyName != null && row.CompanyName.ToLower().Contains(search)));
    }

    // Function summary: Applies unattached monitor search filters to the query.
    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF query predicate; ToLower() is the only case-insensitive form that translates on Npgsql and runs on the InMemory test provider. See docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF query predicate; see docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF query predicate; StringComparison does not translate on Npgsql. See docs/development/portal/sonar/globalization-suppressions.md")]
    private static IQueryable<MonitorListRow> ApplyUnattachedSearch(IQueryable<MonitorListRow> rows, string? searchText)
    {
        var search = NormalizeSearch(searchText);
        if (search == null)
        {
            return rows;
        }

        var matchingTypes = MatchingMonitorTypes(search);
        return rows.Where(row =>
            (row.FleetNumber != null && row.FleetNumber.ToLower().Contains(search)) ||
            row.SerialId.ToLower().Contains(search) ||
            matchingTypes.Contains(row.TypeOfMonitor) ||
            row.Manufacturer.ToLower().Contains(search) ||
            row.Model.ToLower().Contains(search));
    }

    // Function summary: Sorts, counts, pages, and materializes monitor list rows.
    private async Task<MonitorListPage> BuildPageAsync(
        IQueryable<MonitorListRow> rows,
        MonitorListQuery query,
        DateTime offlineCutoff,
        CancellationToken cancellationToken)
    {
        var total = await rows.CountAsync(cancellationToken);
        var orderedRows = ApplySort(rows, query.Sort, query.SortDir);
        var skip = (query.Page - 1) * query.PageSize;
        var pageRows = await orderedRows
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        await HydrateNotificationFlagsAsync(pageRows, cancellationToken);

        return new MonitorListPage
        {
            Results = pageRows.Select(row => ToItem(row, query.RoleContext, offlineCutoff)).ToList(),
            Total = total
        };
    }

    // Function summary: Hydrates page-level alert flags using effective deployment/contract ownership windows.
    private async Task HydrateNotificationFlagsAsync(List<MonitorListRow> rows, CancellationToken cancellationToken)
    {
        var deploymentIds = rows
            .Where(row => row.DeploymentId.HasValue)
            .Select(row => row.DeploymentId!.Value)
            .ToList();
        if (deploymentIds.Count == 0)
        {
            return;
        }

        var deployments = await domainContext.Deployments
            .AsNoTracking()
            .Include(deployment => deployment.Contract)
            .Where(deployment => deploymentIds.Contains(deployment.Id))
            .ToListAsync(cancellationToken);
        var deploymentByMonitor = deployments.ToDictionary(deployment => deployment.MonitorId);
        var monitorIds = deploymentByMonitor.Keys.ToList();
        var notifications = await domainContext.Notifications
            .AsNoTracking()
            .Where(notification => monitorIds.Contains(notification.MonitorId) && notification.ClosedTime == null)
            .ToListAsync(cancellationToken);
        var notificationsByMonitor = notifications
            .Where(notification =>
                deploymentByMonitor.TryGetValue(notification.MonitorId, out var deployment) &&
                MonitorOwnershipWindowResolver.ForDeployment(deployment).Contains(notification.NotificationTime))
            .GroupBy(notification => notification.MonitorId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var row in rows)
        {
            if (!notificationsByMonitor.TryGetValue(row.Id, out var monitorNotifications))
            {
                continue;
            }

            row.HasAlerts = monitorNotifications.Any(notification => notification.AlertType == AlertTypeEnum.Alert);
            row.HasCautions = monitorNotifications.Any(notification => notification.AlertType == AlertTypeEnum.Caution);
        }
    }

    // Function summary: Applies monitor list sorting to the query.
    private static IOrderedQueryable<MonitorListRow> ApplySort(IQueryable<MonitorListRow> rows, string sort, string sortDir)
    {
        var descending = sortDir == SortDirections.Descending;
        return sort.ToLowerInvariant() switch
        {
            "serialid" => descending ? rows.OrderByDescending(row => row.SerialId) : rows.OrderBy(row => row.SerialId),
            "typeofmonitor" => descending ? rows.OrderByDescending(row => row.TypeOfMonitor) : rows.OrderBy(row => row.TypeOfMonitor),
            "sitename" => descending ? rows.OrderByDescending(row => row.SiteName) : rows.OrderBy(row => row.SiteName),
            "contractnumber" => descending ? rows.OrderByDescending(row => row.ContractNumber) : rows.OrderBy(row => row.ContractNumber),
            "lastdatatime" => descending ? rows.OrderByDescending(row => row.LastDataTime) : rows.OrderBy(row => row.LastDataTime),
            _ => descending ? rows.OrderByDescending(row => row.FleetNumber) : rows.OrderBy(row => row.FleetNumber)
        };
    }

    // Function summary: Converts a database projection into the API monitor list DTO.
    private static MonitorListItem ToItem(MonitorListRow row, MonitorListRoleContext roleContext, DateTime offlineCutoff)
    {
        var isAssigned = row.DeploymentId.HasValue;
        return new MonitorListItem
        {
            Id = row.Id,
            DeploymentId = row.DeploymentId,
            FleetNumber = row.FleetNumber,
            SerialId = row.SerialId,
            Manufacturer = row.Manufacturer,
            Model = row.Model,
            FirmwareVersion = row.FirmwareVersion,
            TypeOfMonitor = row.TypeOfMonitor.ToString(),
            ContractId = row.ContractId,
            ContractNumber = row.ContractNumber,
            SiteId = row.SiteId,
            SiteName = row.SiteName,
            CompanyId = row.CompanyId,
            CompanyName = row.CompanyName,
            StartDate = row.StartDate,
            EndDate = row.EndDate,
            LastDataTime = row.LastDataTime,
            IsAssigned = isAssigned,
            IsOffline = row.LastDataTime == null || row.LastDataTime < offlineCutoff,
            HasAlerts = row.HasAlerts,
            HasCautions = row.HasCautions,
            CanEdit = roleContext.IsAdmin,
            CanAssign = roleContext.IsAdmin && !isAssigned && !string.IsNullOrWhiteSpace(row.FleetNumber),
            CanInstallerEdit = (roleContext.IsAdmin || roleContext.IsInstaller) && isAssigned
        };
    }

    // Function summary: Normalizes user search input for database-side comparisons.
    private static string? NormalizeSearch(string? searchText)
    {
        return string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim().ToLowerInvariant();
    }

    // Function summary: Finds monitor enum values matching a text search.
    private static MonitorTypeEnum[] MatchingMonitorTypes(string search)
    {
        return Enum.GetValues<MonitorTypeEnum>()
            .Where(value => value.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    internal sealed class MonitorListRow
    {
        public Guid Id { get; set; }
        public Guid? DeploymentId { get; set; }
        public string? FleetNumber { get; set; }
        public string SerialId { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string Model { get; set; } = "";
        public string FirmwareVersion { get; set; } = "";
        public MonitorTypeEnum TypeOfMonitor { get; set; }
        public Guid? ContractId { get; set; }
        public string? ContractNumber { get; set; }
        public Guid? SiteId { get; set; }
        public string? SiteName { get; set; }
        public Guid? CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? LastDataTime { get; set; }
        public bool HasAlerts { get; set; }
        public bool HasCautions { get; set; }
    }

    private sealed class MonitorWithLastDataTime
    {
        public MonitorEntity Monitor { get; set; } = null!;
        public DateTime? LastDataTime { get; set; }
    }
}
