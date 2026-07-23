// File summary: Implements the application-owned Sites read port with materialized EF Core queries.
// Major updates:
// - 2026-07-23 Extracted Sites filtering, counting, sorting, paging, and projections behind ISiteReadPort.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Application.Common;
using RvtPortal.Application.Sites;
using RvtPortal.Application.Sites.Ports;

namespace RvtPortal.Spa.Adapters.Sites;

public sealed class EfSiteReadAdapter(RVTDbContext domainContext) : ISiteReadPort
{
    // Site detail shows only the newest open alerts; the limit is applied in SQL.
    private const int SiteDetailNotificationLimit = 20;

    private static readonly string[] DayNames =
        ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    public Task<bool> ExistsAsync(
        Guid siteId,
        SiteAccessScope scope,
        CancellationToken cancellationToken)
    {
        return VisibleSites(scope).AnyAsync(site => site.Id == siteId, cancellationToken);
    }

    public async Task<PagedResult<SiteListModel>> QueryAsync(
        SiteAccessScope scope,
        SiteQuery query,
        CancellationToken cancellationToken)
    {
        var sites = ApplySiteFilters(VisibleSites(scope), query);
        var total = await sites.CountAsync(cancellationToken);
        var pageSites = await ApplySiteSort(
                sites
                    .Include(site => site.Contracts)
                    .ThenInclude(contract => contract.Company),
                query.Page.Sort,
                query.Page.SortDir)
            .Skip((query.Page.Page - 1) * query.Page.PageSize)
            .Take(query.Page.PageSize)
            .ToListAsync(cancellationToken);
        var pageItems = pageSites
            .Select(BuildSiteListItem)
            .ToList();
        await PopulateSiteCountersAsync(pageItems, cancellationToken);
        return new PagedResult<SiteListModel>
        {
            Results = pageItems,
            Total = total,
            Page = query.Page.Page,
            PageSize = query.Page.PageSize,
            SearchText = query.Page.SearchText ?? "",
            Sort = query.Page.Sort,
            SortDir = query.Page.SortDir
        };
    }

    public Task<SiteOptionsModel> OptionsAsync(
        Guid? companyId,
        CancellationToken cancellationToken)
    {
        return BuildOptionsAsync(companyId, cancellationToken);
    }

    public async Task<SiteDetailModel?> GetAsync(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        var site = await LoadSiteDetailAsync(siteId, cancellationToken);
        return site == null
            ? null
            : await BuildSiteDetailAsync(site, cancellationToken);
    }

    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF query predicate; ToLower() is the only case-insensitive form that translates on Npgsql and runs on the InMemory test provider. See docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF query predicate; see docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF query predicate; StringComparison does not translate on Npgsql. See docs/development/portal/sonar/globalization-suppressions.md")]
    public async Task<PagedResult<SiteMonitorModel>> QueryMonitorsAsync(
        Guid siteId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        // Search, sort, and paging all run in SQL now; only the requested page is materialized. Previously the
        // site's entire monitor set was loaded and then filtered, sorted, and paged in memory.
        var query = ActiveSiteDeployments(siteId);
        if (!string.IsNullOrWhiteSpace(page.SearchText))
        {
            var search = page.SearchText.Trim().ToLower();
            query = query.Where(deployment =>
                (deployment.Monitor.FleetNr != null && deployment.Monitor.FleetNr.ToLower().Contains(search)) ||
                (deployment.Monitor.SerialId != null && deployment.Monitor.SerialId.ToLower().Contains(search)) ||
                (deployment.Contract.ContractNumber != null && deployment.Contract.ContractNumber.ToLower().Contains(search)));
        }

        var total = await query.CountAsync(cancellationToken);
        var ordered = page.SortDir == PageSortDirections.Descending
            ? query.OrderByDescending(deployment => deployment.Monitor.FleetNr ?? deployment.Monitor.SerialId)
            : query.OrderBy(deployment => deployment.Monitor.FleetNr ?? deployment.Monitor.SerialId);
        var monitors = await ProjectSiteMonitors(
                ordered
                    .Skip((page.Page - 1) * page.PageSize)
                    .Take(page.PageSize))
            .ToListAsync(cancellationToken);

        return BuildPagedResult(monitors, total, page, "fleetNumber");
    }

    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF query predicate; ToLower() is the only case-insensitive form that translates on Npgsql and runs on the InMemory test provider. See docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF query predicate; see docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF query predicate; StringComparison does not translate on Npgsql. See docs/development/portal/sonar/globalization-suppressions.md")]
    public async Task<PagedResult<SiteNotificationModel>> QueryOpenNotificationsAsync(
        Guid siteId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var pageRequest = page with
        {
            PageSize = Math.Min(page.PageSize, 20),
            SortDir = PageSortDirections.Descending
        };

        // The site's active deployments are the display source for each row (fleet number, contract). There is
        // at most one per monitor on a site, so this set is small and stays in memory; the notifications
        // themselves - the unbounded side - are filtered, counted, sorted, and paged in SQL.
        var deploymentByMonitor = await LoadActiveDeploymentsByMonitorAsync(siteId, cancellationToken);
        if (deploymentByMonitor.Count == 0)
        {
            return BuildPagedResult<SiteNotificationModel>(
                [],
                0,
                pageRequest,
                "notificationTime");
        }

        var monitorIds = deploymentByMonitor.Keys.ToList();
        var query = domainContext.Notifications
            .AsNoTracking()
            .Where(notification => monitorIds.Contains(notification.MonitorId)
                && notification.ClosedTime == null
                && notification.AlertType == AlertTypeEnum.Alert);

        if (!string.IsNullOrWhiteSpace(pageRequest.SearchText))
        {
            var search = pageRequest.SearchText.Trim();

            // Fleet number and contract number live on the deployment, not the notification. Resolve which
            // monitors match those fields from the small deployment map, then let SQL match either that monitor
            // set or the notification's own AlertField - the same OR the in-memory filter applied.
            var matchingMonitorIds = deploymentByMonitor
                .Where(entry => Contains(entry.Value.Monitor.FleetNr, search)
                    || Contains(entry.Value.Contract.ContractNumber, search))
                .Select(entry => entry.Key)
                .ToList();
            var loweredSearch = search.ToLower();
            query = query.Where(notification =>
                matchingMonitorIds.Contains(notification.MonitorId)
                || (notification.AlertField != null && notification.AlertField.ToLower().Contains(loweredSearch)));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(notification => notification.NotificationTime)
            .Skip((pageRequest.Page - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize)
            .ToListAsync(cancellationToken);

        var notifications = rows
            .Select(notification => BuildSiteNotification(
                notification,
                deploymentByMonitor[notification.MonitorId]))
            .ToList();
        return BuildPagedResult(notifications, total, pageRequest, "notificationTime");
    }

    public async Task<SiteNotificationSettingsData?> GetNotificationSettingsAsync(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        var site = await domainContext.Sites
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == siteId, cancellationToken);
        if (site == null)
        {
            return null;
        }

        var siteUsers = await domainContext.SiteUsers
            .AsNoTracking()
            .Where(item => item.SiteId == siteId)
            .OrderByDescending(item => item.SiteContact)
            .ToListAsync(cancellationToken);
        var siteUserIds = siteUsers.Select(siteUser => siteUser.Id).ToList();
        var settings = await domainContext.NotificationSettings
            .AsNoTracking()
            .Where(item => siteUserIds.Contains(item.SiteUserId))
            .ToDictionaryAsync(item => item.SiteUserId, item => item, cancellationToken);
        var assignments = siteUsers.Select(siteUser =>
        {
            settings.TryGetValue(siteUser.Id, out var setting);
            return new SiteNotificationAssignment(
                siteUser.Id,
                siteId,
                siteUser.UserId,
                siteUser.SiteContact,
                setting?.Email ?? false,
                setting?.SMS ?? false,
                FormatTime(setting?.StartTime),
                FormatTime(setting?.EndTime));
        }).ToList();
        return new SiteNotificationSettingsData(siteId, site.SiteName, assignments);
    }

    public async Task<SiteMutationValidationData> GetMutationValidationDataAsync(
        SiteMutation request,
        Guid? currentSiteId,
        CancellationToken cancellationToken)
    {
        var siteName = request.SiteName.Trim();
        var duplicateSiteName = await domainContext.Sites
            .AsNoTracking()
            .AnyAsync(
                site => site.Id != currentSiteId &&
                    site.SiteName == siteName,
                cancellationToken);
        var companyExists = await domainContext.Companies
            .AsNoTracking()
            .AnyAsync(
                company => company.Id == request.CompanyId,
                cancellationToken);

        if (!request.ContractId.HasValue ||
            request.ContractId.Value == Guid.Empty)
        {
            return new SiteMutationValidationData(
                duplicateSiteName,
                companyExists,
                ContractExists: false,
                ContractIsUnassigned: false,
                ContractBelongsToCompany: false);
        }

        var contract = await domainContext.Contracts
            .AsNoTracking()
            .Where(item => item.Id == request.ContractId.Value)
            .Select(item => new
            {
                item.CompanyId,
                item.SiteiD
            })
            .SingleOrDefaultAsync(cancellationToken);
        return new SiteMutationValidationData(
            duplicateSiteName,
            companyExists,
            ContractExists: contract is not null,
            ContractIsUnassigned: contract is not null &&
                !contract.SiteiD.HasValue,
            ContractBelongsToCompany: contract is not null &&
                contract.CompanyId == request.CompanyId);
    }

    public Task<SiteNotificationSettingTarget?>
        GetNotificationSettingTargetAsync(
            Guid siteId,
            Guid siteUserId,
            CancellationToken cancellationToken)
    {
        return domainContext.SiteUsers
            .AsNoTracking()
            .Where(item => item.Id == siteUserId && item.SiteId == siteId)
            .Select(item => new SiteNotificationSettingTarget(
                item.Id,
                item.SiteId,
                item.UserId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private IQueryable<Site> VisibleSites(SiteAccessScope scope)
    {
        var sites = domainContext.Sites.AsNoTracking();
        return scope.Kind switch
        {
            SiteAccessScopeKind.All => sites,
            SiteAccessScopeKind.Assigned when scope.UserId.HasValue =>
                sites.Where(site => domainContext.SiteUsers.Any(siteUser =>
                    siteUser.SiteId == site.Id
                    && siteUser.UserId == scope.UserId.Value
                    && siteUser.StartDate <= scope.NowUtc
                    && (!siteUser.EndDate.HasValue || siteUser.EndDate.Value >= scope.NowUtc))),
            _ => sites.Where(_ => false)
        };
    }

    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF query predicate; ToLower() is the only case-insensitive form that translates on Npgsql and runs on the InMemory test provider. See docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF query predicate; see docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF query predicate; StringComparison does not translate on Npgsql. See docs/development/portal/sonar/globalization-suppressions.md")]
    private static IQueryable<Site> ApplySiteFilters(
        IQueryable<Site> query,
        SiteQuery request)
    {
        if (request.IncludeArchived != true)
        {
            query = query.Where(site => !site.Archived);
        }

        if (request.CompanyId.HasValue)
        {
            query = query.Where(site =>
                site.Contracts.Any(contract =>
                    contract.CompanyId == request.CompanyId.Value));
        }

        if (string.IsNullOrWhiteSpace(request.Page.SearchText))
        {
            return query;
        }

        var search = request.Page.SearchText.Trim().ToLower();
        return query.Where(site =>
            (site.SiteName != null && site.SiteName.ToLower().Contains(search)) ||
            (((site.AddressLine1 ?? "") + " " +
                (site.AddressLine2 ?? "") + " " +
                (site.Postcode ?? "") + " " +
                (site.City ?? "")).ToLower().Contains(search)) ||
            site.Contracts.Any(contract =>
                (contract.ContractNumber != null && contract.ContractNumber.ToLower().Contains(search)) ||
                contract.Company.CompanyName.ToLower().Contains(search)));
    }

    private static IQueryable<Site> ApplySiteSort(
        IQueryable<Site> sites,
        string sort,
        string sortDir)
    {
        var descending = sortDir == PageSortDirections.Descending;
        return sort.ToLowerInvariant() switch
        {
            "sitename" => descending
                ? sites.OrderByDescending(site => site.SiteName)
                : sites.OrderBy(site => site.SiteName),
            "companyname" => descending
                ? sites.OrderByDescending(site =>
                    site.Contracts.Select(contract => contract.Company.CompanyName).Min())
                : sites.OrderBy(site =>
                    site.Contracts.Select(contract => contract.Company.CompanyName).Min()),
            "contracts" => descending
                ? sites.OrderByDescending(site =>
                    site.Contracts.Select(contract => contract.ContractNumber).Min())
                : sites.OrderBy(site =>
                    site.Contracts.Select(contract => contract.ContractNumber).Min()),
            "siteaddress" => descending
                ? sites.OrderByDescending(site =>
                    (site.AddressLine1 ?? "") + " " +
                    (site.AddressLine2 ?? "") + " " +
                    (site.Postcode ?? "") + " " +
                    (site.City ?? ""))
                : sites.OrderBy(site =>
                    (site.AddressLine1 ?? "") + " " +
                    (site.AddressLine2 ?? "") + " " +
                    (site.Postcode ?? "") + " " +
                    (site.City ?? "")),
            _ => descending
                ? sites.OrderByDescending(site => site.CreateDate)
                : sites.OrderBy(site => site.CreateDate)
        };
    }

    private async Task<SiteDetailModel> BuildSiteDetailAsync(
        Site site,
        CancellationToken cancellationToken)
    {
        var item = BuildSiteListItem(site);
        await PopulateSiteCountersAsync([item], cancellationToken);
        var options = await BuildOptionsAsync(item.CompanyId, cancellationToken);
        var archive = await domainContext.SiteArchived
            .AsNoTracking()
            .Where(entry => entry.SiteId == site.Id)
            .OrderByDescending(entry => entry.CreateDate)
            .Select(entry =>
                new SiteArchiveModel(entry.CreateDate, entry.CreatedBy, entry.PictureLink))
            .FirstOrDefaultAsync(cancellationToken);

        return new SiteDetailModel
        {
            Id = item.Id,
            SiteName = item.SiteName,
            Archived = item.Archived,
            CreateDate = item.CreateDate,
            AddressLine1 = item.AddressLine1,
            AddressLine2 = item.AddressLine2,
            Postcode = item.Postcode,
            City = item.City,
            County = item.County,
            SiteAddress = item.SiteAddress,
            Contracts = item.Contracts,
            CompanyName = item.CompanyName,
            CompanyId = item.CompanyId,
            SiteContact = item.SiteContact,
            MonitorCount = item.MonitorCount,
            OpenNotificationCount = item.OpenNotificationCount,
            StartTime = FormatTime(site.StartTime),
            EndTime = FormatTime(site.EndTime),
            SatStartTime = FormatTime(site.SatStartTime),
            SatEndTime = FormatTime(site.SatEndTime),
            SunStartTime = FormatTime(site.SunStartTime),
            SunEndTime = FormatTime(site.SunEndTime),
            OperatingHours = BuildOperatingHoursResponse(site),
            ContractList = site.Contracts?
                .OrderBy(contract => contract.ContractNumber)
                .Select(BuildContractItem)
                .ToList() ?? [],
            Monitors = await BuildMonitorItemsAsync(site.Id, cancellationToken),
            OpenNotifications = await BuildOpenNotificationItemsAsync(
                site.Id,
                SiteDetailNotificationLimit,
                cancellationToken),
            Archive = archive,
            Companies = options.Companies,
            AvailableContracts = options.Contracts,
            CanManage = false,
            HasCustomerLogo = false
        };
    }

    private async Task<SiteOptionsModel> BuildOptionsAsync(
        Guid? companyId,
        CancellationToken cancellationToken)
    {
        var companies = await domainContext.Companies
            .AsNoTracking()
            .OrderBy(company => company.CompanyName)
            .Select(company =>
                new SiteOptionModel(company.Id.ToString(), company.CompanyName))
            .ToListAsync(cancellationToken);
        var contractsQuery = domainContext.Contracts
            .AsNoTracking()
            .Where(contract => contract.SiteiD == null);
        if (companyId.HasValue)
        {
            contractsQuery = contractsQuery.Where(contract =>
                contract.CompanyId == companyId.Value);
        }

        var contracts = await contractsQuery
            .OrderBy(contract => contract.ContractNumber)
            .Select(contract =>
                new SiteOptionModel(contract.Id.ToString(), contract.ContractNumber))
            .ToListAsync(cancellationToken);
        return new SiteOptionsModel
        {
            Companies = companies,
            Contracts = contracts
        };
    }

    private async Task PopulateSiteCountersAsync(
        IEnumerable<SiteListModel> sites,
        CancellationToken cancellationToken)
    {
        // This used to issue about three queries per site on the page. It is now two queries for the whole
        // page, with the per-site tallies computed over the (page-bounded) rows they return.
        var rows = sites as IList<SiteListModel> ?? sites.ToList();
        var siteIds = rows.Select(site => site.Id).ToList();
        if (siteIds.Count == 0)
        {
            return;
        }

        // One row per active deployment, so counting rows per site preserves the original MonitorCount, which
        // counted deployments rather than distinct monitors.
        var deployments = await domainContext.Deployments
            .AsNoTracking()
            .Where(deployment => deployment.EndDate == null
                && deployment.Contract.SiteiD != null
                && siteIds.Contains(deployment.Contract.SiteiD.Value))
            .Select(deployment => new
            {
                SiteId = deployment.Contract.SiteiD!.Value,
                deployment.MonitorId
            })
            .ToListAsync(cancellationToken);

        var monitorIds = deployments
            .Select(deployment => deployment.MonitorId)
            .Distinct()
            .ToList();
        var openAlertsByMonitor = monitorIds.Count == 0
            ? []
            : await domainContext.Notifications
                .AsNoTracking()
                .Where(notification => monitorIds.Contains(notification.MonitorId)
                    && notification.ClosedTime == null
                    && notification.AlertType == AlertTypeEnum.Alert)
                .GroupBy(notification => notification.MonitorId)
                .Select(group => new { MonitorId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(
                    item => item.MonitorId,
                    item => item.Count,
                    cancellationToken);

        var deploymentsBySite = deployments
            .GroupBy(deployment => deployment.SiteId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var site in rows)
        {
            if (!deploymentsBySite.TryGetValue(site.Id, out var siteDeployments))
            {
                site.MonitorCount = 0;
                site.OpenNotificationCount = 0;
                continue;
            }

            site.MonitorCount = siteDeployments.Count;

            // The open-alert count is per distinct monitor, matching the original Distinct() on monitor ids.
            site.OpenNotificationCount = siteDeployments
                .Select(deployment => deployment.MonitorId)
                .Distinct()
                .Sum(monitorId =>
                    openAlertsByMonitor.TryGetValue(monitorId, out var count)
                        ? count
                        : 0);
        }
    }

    private Task<List<SiteMonitorModel>> BuildMonitorItemsAsync(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        return ProjectSiteMonitors(
                ActiveSiteDeployments(siteId)
                    .OrderBy(deployment => deployment.Monitor.FleetNr))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Deployment> ActiveSiteDeployments(Guid siteId)
    {
        return domainContext.Deployments
            .AsNoTracking()
            .Where(deployment =>
                deployment.Contract.SiteiD == siteId && deployment.EndDate == null);
    }

    private static IQueryable<SiteMonitorModel> ProjectSiteMonitors(
        IQueryable<Deployment> deployments)
    {
        return deployments.Select(deployment => new SiteMonitorModel(
            deployment.MonitorId,
            deployment.Id,
            deployment.Monitor.FleetNr,
            deployment.Monitor.SerialId,
            deployment.Monitor.FleetNr ?? deployment.Monitor.SerialId,
            deployment.Monitor.TypeOfMonitor.ToString(),
            deployment.ContractId,
            deployment.Contract.ContractNumber,
            deployment.Monitor.LastDataTime15Min
                ?? deployment.Monitor.LastDataTime1Min
                ?? deployment.Monitor.LastDataTime1Hour
                ?? deployment.Monitor.LastDataTime24Hour,
            false,
            deployment.Lat,
            deployment.Lng,
            deployment.What3words));
    }

    private async Task<List<SiteNotificationModel>> BuildOpenNotificationItemsAsync(
        Guid siteId,
        int limit,
        CancellationToken cancellationToken)
    {
        var deploymentByMonitor = await LoadActiveDeploymentsByMonitorAsync(
            siteId,
            cancellationToken);
        if (deploymentByMonitor.Count == 0)
        {
            return [];
        }

        var monitorIds = deploymentByMonitor.Keys.ToList();

        // The caller only shows the newest few rows, so the limit belongs in SQL rather than after loading
        // every open alert on the site.
        var notifications = await domainContext.Notifications
            .AsNoTracking()
            .Where(notification => monitorIds.Contains(notification.MonitorId)
                && notification.ClosedTime == null
                && notification.AlertType == AlertTypeEnum.Alert)
            .OrderByDescending(notification => notification.NotificationTime)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return notifications
            .Select(notification => BuildSiteNotification(
                notification,
                deploymentByMonitor[notification.MonitorId]))
            .ToList();
    }

    private async Task<Dictionary<Guid, Deployment>> LoadActiveDeploymentsByMonitorAsync(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        var deployments = await ActiveSiteDeployments(siteId)
            .Include(deployment => deployment.Contract)
            .Include(deployment => deployment.Monitor)
            .ToListAsync(cancellationToken);

        return deployments
            .GroupBy(deployment => deployment.MonitorId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(deployment => deployment.StartDate)
                    .First());
    }

    private static SiteNotificationModel BuildSiteNotification(
        Notification notification,
        Deployment deployment)
    {
        return new SiteNotificationModel(
            notification.Id,
            notification.MonitorId,
            deployment.Monitor.FleetNr,
            deployment.Monitor.SerialId,
            deployment.Monitor.TypeOfMonitor.ToString(),
            notification.AlertType.ToString(),
            notification.AlertField,
            notification.LimitOn,
            notification.Level,
            notification.NotificationTime,
            deployment.ContractId,
            deployment.Contract.ContractNumber);
    }

    private static PagedResult<T> BuildPagedResult<T>(
        IReadOnlyList<T> pageItems,
        int total,
        PageRequest page,
        string responseSort)
    {
        return new PagedResult<T>
        {
            Results = pageItems.ToList(),
            Total = total,
            Page = page.Page,
            PageSize = page.PageSize,
            SearchText = page.SearchText ?? "",
            Sort = responseSort,
            SortDir = page.SortDir
        };
    }

    private static SiteListModel BuildSiteListItem(Site site)
    {
        var contracts = site.Contracts ?? [];
        var company = contracts
            .Select(contract => contract.Company)
            .FirstOrDefault(company => company != null);
        return new SiteListModel
        {
            Id = site.Id,
            SiteName = site.SiteName,
            Archived = site.Archived,
            CreateDate = site.CreateDate,
            AddressLine1 = site.AddressLine1,
            AddressLine2 = site.AddressLine2,
            Postcode = site.Postcode,
            City = site.City,
            County = site.County,
            SiteAddress = BuildAddress(site),
            Contracts = JoinSummary(contracts
                .OrderBy(contract => contract.ContractNumber)
                .Select(contract => contract.ContractNumber)),
            CompanyName = company?.CompanyName,
            CompanyId = company?.Id,
            SiteContact = null
        };
    }

    private static SiteContractModel BuildContractItem(Contract contract)
    {
        return new SiteContractModel(
            contract.Id,
            contract.ContractNumber,
            contract.OnHireDate,
            contract.OffHireDate,
            contract.CompanyId,
            contract.Company?.CompanyName,
            contract.SiteiD,
            contract.Site?.SiteName);
    }

    private async Task<Site?> LoadSiteDetailAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await domainContext.Sites
            .AsNoTracking()
            .Include(item => item.Contracts)
            .ThenInclude(contract => contract.Company)
            .Include(item => item.OperatingHours)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    private static List<SiteOperatingHoursModel> BuildOperatingHoursResponse(Site site)
    {
        var persisted = site.OperatingHours
            .GroupBy(hours => hours.DayOfWeek)
            .ToDictionary(group => group.Key, group => group.First());
        return Enumerable.Range(1, 7)
            .Select(day =>
            {
                if (persisted.TryGetValue(day, out var hours))
                {
                    return new SiteOperatingHoursModel(
                        day,
                        DayNames[day - 1],
                        FormatTime(hours.StartTime),
                        FormatTime(hours.EndTime),
                        hours.IsClosed
                            || (!hours.StartTime.HasValue && !hours.EndTime.HasValue));
                }

                var legacy = LegacyOperatingHoursForDay(site, day);
                return new SiteOperatingHoursModel(
                    day,
                    DayNames[day - 1],
                    FormatTime(legacy.StartTime),
                    FormatTime(legacy.EndTime),
                    !legacy.StartTime.HasValue && !legacy.EndTime.HasValue);
            })
            .ToList();
    }

    private static string BuildAddress(Site site)
    {
        return string.Join(
            " ",
            new[] { site.AddressLine1, site.AddressLine2, site.Postcode, site.City }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string? JoinSummary(IEnumerable<string?> values)
    {
        var list = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
        return list.Count == 0 ? null : string.Join(", ", list);
    }

    private static bool Contains(string? value, string search)
    {
        return value?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private static string? FormatTime(TimeSpan? value)
    {
        return value?.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }

    private static (TimeSpan? StartTime, TimeSpan? EndTime) LegacyOperatingHoursForDay(
        Site site,
        int day)
    {
        return day switch
        {
            6 => (site.SatStartTime, site.SatEndTime),
            7 => (site.SunStartTime, site.SunEndTime),
            _ => (site.StartTime, site.EndTime)
        };
    }
}
