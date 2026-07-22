// File summary: Coordinates site business workflows for the SPA API without depending on HTTP transport types.
// Major updates:
// - 2026-07-08 pending Pushed site list filtering, counting, sorting, and paging into EF queries.
// - 2026-07-05 pending Moved core site query, detail, mutation, archive, and visibility rules out of the controller.
// - 2026-07-05 pending Added site monitor, open-notification, and notification-setting application workflows.

// File summary: Coordinates site list, detail, mutation, archive, monitoring, and notification-setting workflows.
// Major updates:
// - 2026-07-22 pending Enforced inclusive active assignment windows for company-user site visibility.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RvtPortal.Spa.Adapters.Archive;
using RVT.BusinessLogic.Application;
using RVT.BusinessLogic.Application.Paging;
using RVT.BusinessLogic.Application.Users;
using RVT.DataAccess.Context;
using RVT.BusinessLogic.Sites;
using RVT.Entities;

namespace RvtPortal.Spa.Application.Sites;

public interface ISiteApplicationService
{
    // Function summary: Returns paged sites after applying user visibility, filters, sorting, and counters.
    Task<ApplicationResult<PagedResult<SiteListModel>>> QueryAsync(PortalUserContext user, SiteQuery request, CancellationToken cancellationToken);

    // Function summary: Returns company and unassigned-contract choices for site edit screens.
    Task<ApplicationResult<SiteOptionsModel>> OptionsAsync(Guid? companyId, CancellationToken cancellationToken);

    // Function summary: Returns a site detail model after enforcing current-user visibility.
    Task<ApplicationResult<SiteDetailModel>> GetAsync(PortalUserContext user, Guid id, CancellationToken cancellationToken);

    // Function summary: Returns paged active monitor rows for a visible site.
    Task<ApplicationResult<PagedResult<SiteMonitorModel>>> QueryMonitorsAsync(PortalUserContext user, Guid siteId, PageRequest page, CancellationToken cancellationToken);

    // Function summary: Returns paged open alert rows for a visible site.
    Task<ApplicationResult<PagedResult<SiteNotificationModel>>> QueryOpenNotificationsAsync(PortalUserContext user, Guid siteId, PageRequest page, CancellationToken cancellationToken);

    // Function summary: Validates and creates a site with its initial contract and operating hours.
    Task<ApplicationResult<SiteDetailModel>> CreateAsync(PortalUserContext user, SiteMutation request, CancellationToken cancellationToken);

    // Function summary: Validates and updates mutable site fields and operating hours.
    Task<ApplicationResult<SiteDetailModel>> UpdateAsync(PortalUserContext user, Guid id, SiteMutation request, CancellationToken cancellationToken);

    // Function summary: Archives a site idempotently and returns the updated detail model.
    Task<ApplicationResult<SiteDetailModel>> ArchiveAsync(PortalUserContext user, Guid id, string createdBy, CancellationToken cancellationToken);

    // Function summary: Evaluates whether the current user can read the requested site.
    Task<bool> CanReadSiteAsync(PortalUserContext user, Guid id, CancellationToken cancellationToken);

    // Function summary: Evaluates whether the current user can manage the requested site.
    Task<bool> CanManageSiteAsync(PortalUserContext user, Guid id, CancellationToken cancellationToken);

    // Function summary: Returns notification settings for users assigned to a visible site.
    Task<ApplicationResult<SiteNotificationSettingsModel>> GetNotificationSettingsAsync(PortalUserContext user, Guid siteId, CancellationToken cancellationToken);

    // Function summary: Validates and updates one assigned user's notification settings for a site.
    Task<ApplicationResult<SiteNotificationSettingModel>> UpdateNotificationSettingAsync(PortalUserContext user, Guid siteId, Guid siteUserId, SiteNotificationSettingMutation request, CancellationToken cancellationToken);
}

public sealed class SiteApplicationService : ISiteApplicationService
{
    public const string DefaultSort = "createDate";
    public const string MonitorSort = "fleetNumber";
    public const string NotificationSort = "notificationTime";

    // Site detail shows only the newest open alerts; the limit is applied in SQL.
    private const int SiteDetailNotificationLimit = 20;

    public static readonly IReadOnlySet<string> SortFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "siteName",
        "companyName",
        "contracts",
        "createDate",
        "siteAddress"
    };

    public static readonly IReadOnlySet<string> MonitorSortFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        MonitorSort
    };

    public static readonly IReadOnlySet<string> NotificationSortFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        NotificationSort
    };

    private static readonly string[] DayNames = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];
    private readonly RVTDbContext domainContext;
    private readonly IPortalUserDirectory userDirectory;
    private readonly ISiteArchiveService archiveService;
    private readonly TimeProvider timeProvider;

    // Function summary: Initializes this type with the data context needed for site workflows.
    public SiteApplicationService(
        RVTDbContext domainContext,
        IPortalUserDirectory userDirectory,
        ISiteArchiveService archiveService,
        TimeProvider timeProvider)
    {
        this.domainContext = domainContext;
        this.userDirectory = userDirectory;
        this.archiveService = archiveService;
        this.timeProvider = timeProvider;
    }

    // Function summary: Returns paged sites after applying user visibility, filters, sorting, paging, and counters.
    public async Task<ApplicationResult<PagedResult<SiteListModel>>> QueryAsync(
        PortalUserContext user,
        SiteQuery request,
        CancellationToken cancellationToken)
    {
        var query = ApplySiteFilters(GetVisibleSitesQuery(user), request);
        var total = await query.CountAsync(cancellationToken);
        var pageSites = await ApplySiteSort(
                query
                    .Include(site => site.Contracts)
                    .ThenInclude(contract => contract.Company),
                request.Page.Sort,
                request.Page.SortDir)
            .Skip((request.Page.Page - 1) * request.Page.PageSize)
            .Take(request.Page.PageSize)
            .ToListAsync(cancellationToken);
        var pageItems = pageSites
            .Select(BuildSiteListItem)
            .ToList();
        await PopulateSiteCountersAsync(pageItems, cancellationToken);
        return ApplicationResult<PagedResult<SiteListModel>>.Success(new PagedResult<SiteListModel>
        {
            Results = pageItems,
            Total = total,
            Page = request.Page.Page,
            PageSize = request.Page.PageSize,
            SearchText = request.Page.SearchText ?? "",
            Sort = request.Page.Sort,
            SortDir = request.Page.SortDir
        });
    }

    // Function summary: Applies site list filters in EF before counting or materializing rows.
    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF query predicate; ToLower() is the only case-insensitive form that translates on Npgsql and runs on the InMemory test provider. See docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF query predicate; see docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF query predicate; StringComparison does not translate on Npgsql. See docs/development/portal/sonar/globalization-suppressions.md")]
    private static IQueryable<Site> ApplySiteFilters(IQueryable<Site> query, SiteQuery request)
    {
        if (request.IncludeArchived != true)
        {
            query = query.Where(site => !site.Archived);
        }

        if (request.CompanyId.HasValue)
        {
            query = query.Where(site => site.Contracts.Any(contract => contract.CompanyId == request.CompanyId.Value));
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

    // Function summary: Applies the supported site list sort fields while rows remain queryable.
    private static IQueryable<Site> ApplySiteSort(IQueryable<Site> sites, string sort, string sortDir)
    {
        var descending = sortDir == PageSortDirections.Descending;
        return sort.ToLowerInvariant() switch
        {
            "sitename" => descending ? sites.OrderByDescending(site => site.SiteName) : sites.OrderBy(site => site.SiteName),
            "companyname" => descending
                ? sites.OrderByDescending(site => site.Contracts.Select(contract => contract.Company.CompanyName).Min())
                : sites.OrderBy(site => site.Contracts.Select(contract => contract.Company.CompanyName).Min()),
            "contracts" => descending
                ? sites.OrderByDescending(site => site.Contracts.Select(contract => contract.ContractNumber).Min())
                : sites.OrderBy(site => site.Contracts.Select(contract => contract.ContractNumber).Min()),
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
            _ => descending ? sites.OrderByDescending(site => site.CreateDate) : sites.OrderBy(site => site.CreateDate)
        };
    }

    // Function summary: Returns company and unassigned-contract choices for site edit screens.
    public async Task<ApplicationResult<SiteOptionsModel>> OptionsAsync(Guid? companyId, CancellationToken cancellationToken)
    {
        return ApplicationResult<SiteOptionsModel>.Success(await BuildOptionsAsync(companyId, cancellationToken));
    }

    // Function summary: Returns a site detail model after enforcing current-user visibility.
    public async Task<ApplicationResult<SiteDetailModel>> GetAsync(PortalUserContext user, Guid id, CancellationToken cancellationToken)
    {
        if (!await CanReadSiteAsync(user, id, cancellationToken))
        {
            return ApplicationResult<SiteDetailModel>.NotFound($"Site '{id}' was not found.");
        }

        var site = await LoadSiteDetailAsync(id, cancellationToken);
        return site == null
            ? ApplicationResult<SiteDetailModel>.NotFound($"Site '{id}' was not found.")
            : ApplicationResult<SiteDetailModel>.Success(await BuildSiteDetailAsync(user, site, cancellationToken));
    }

    // Function summary: Returns paged active monitor rows for a visible site.
    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF query predicate; ToLower() is the only case-insensitive form that translates on Npgsql and runs on the InMemory test provider. See docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF query predicate; see docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF query predicate; StringComparison does not translate on Npgsql. See docs/development/portal/sonar/globalization-suppressions.md")]
    public async Task<ApplicationResult<PagedResult<SiteMonitorModel>>> QueryMonitorsAsync(
        PortalUserContext user,
        Guid siteId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        if (!await CanReadSiteAsync(user, siteId, cancellationToken))
        {
            return ApplicationResult<PagedResult<SiteMonitorModel>>.NotFound($"Site '{siteId}' was not found.");
        }

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

    // Function summary: Returns paged open alert rows for a visible site.
    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF query predicate; ToLower() is the only case-insensitive form that translates on Npgsql and runs on the InMemory test provider. See docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF query predicate; see docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF query predicate; StringComparison does not translate on Npgsql. See docs/development/portal/sonar/globalization-suppressions.md")]
    public async Task<ApplicationResult<PagedResult<SiteNotificationModel>>> QueryOpenNotificationsAsync(
        PortalUserContext user,
        Guid siteId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        if (!await CanReadSiteAsync(user, siteId, cancellationToken))
        {
            return ApplicationResult<PagedResult<SiteNotificationModel>>.NotFound($"Site '{siteId}' was not found.");
        }

        var pageRequest = page with { PageSize = Math.Min(page.PageSize, 20), SortDir = PageSortDirections.Descending };

        // The site's active deployments are the display source for each row (fleet number, contract). There is
        // at most one per monitor on a site, so this set is small and stays in memory; the notifications
        // themselves - the unbounded side - are filtered, counted, sorted, and paged in SQL.
        var deploymentByMonitor = await LoadActiveDeploymentsByMonitorAsync(siteId, cancellationToken);
        if (deploymentByMonitor.Count == 0)
        {
            return BuildPagedResult<SiteNotificationModel>([], 0, pageRequest, "notificationTime");
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
            .Select(notification => BuildSiteNotification(notification, deploymentByMonitor[notification.MonitorId]))
            .ToList();
        return BuildPagedResult(notifications, total, pageRequest, "notificationTime");
    }

    // Function summary: Validates and creates a site with its initial contract and operating hours.
    public async Task<ApplicationResult<SiteDetailModel>> CreateAsync(
        PortalUserContext user,
        SiteMutation request,
        CancellationToken cancellationToken)
    {
        var errors = await ValidateSiteAsync(request, null, requireContract: true, cancellationToken);
        if (errors.Count > 0)
        {
            return ApplicationResult<SiteDetailModel>.Validation(errors.ToArray());
        }

        var site = new Site
        {
            SiteName = request.SiteName.Trim(),
            AddressLine1 = EmptyToNull(request.AddressLine1),
            AddressLine2 = EmptyToNull(request.AddressLine2),
            Postcode = EmptyToNull(request.Postcode),
            City = EmptyToNull(request.City),
            County = EmptyToNull(request.County),
            StartTime = ParseOptionalTime(request.StartTime),
            EndTime = ParseOptionalTime(request.EndTime),
            SatStartTime = ParseOptionalTime(request.SatStartTime),
            SatEndTime = ParseOptionalTime(request.SatEndTime),
            SunStartTime = ParseOptionalTime(request.SunStartTime),
            SunEndTime = ParseOptionalTime(request.SunEndTime),
            CreateDate = DateTime.UtcNow,
            Contracts = [],
            OperatingHours = BuildSiteOperatingHours(request)
        };
        // Stage the site and the contract link together, then commit once. EF assigns the Guid key when the
        // entity is added, so the contract can be pointed at the new site before any round-trip. The previous
        // two-save version could leave a site with no contract attached if the second save failed.
        var contract = await domainContext.Contracts.SingleAsync(item => item.Id == request.ContractId, cancellationToken);
        domainContext.Sites.Add(site);
        contract.SiteiD = site.Id;
        await domainContext.SaveChangesAsync(cancellationToken);
        var created = await LoadSiteDetailAsync(site.Id, cancellationToken);
        return ApplicationResult<SiteDetailModel>.Success(await BuildSiteDetailAsync(user, created!, cancellationToken));
    }

    // Function summary: Validates and updates mutable site fields and operating hours.
    public async Task<ApplicationResult<SiteDetailModel>> UpdateAsync(
        PortalUserContext user,
        Guid id,
        SiteMutation request,
        CancellationToken cancellationToken)
    {
        var site = await domainContext.Sites
            .Include(item => item.OperatingHours)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (site == null)
        {
            return ApplicationResult<SiteDetailModel>.NotFound($"Site '{id}' was not found.");
        }

        var errors = await ValidateSiteAsync(request, id, requireContract: false, cancellationToken);
        if (errors.Count > 0)
        {
            return ApplicationResult<SiteDetailModel>.Validation(errors.ToArray());
        }

        site.SiteName = request.SiteName.Trim();
        site.AddressLine1 = EmptyToNull(request.AddressLine1);
        site.AddressLine2 = EmptyToNull(request.AddressLine2);
        site.Postcode = EmptyToNull(request.Postcode);
        site.City = EmptyToNull(request.City);
        site.County = EmptyToNull(request.County);
        site.StartTime = ParseOptionalTime(request.StartTime);
        site.EndTime = ParseOptionalTime(request.EndTime);
        site.SatStartTime = ParseOptionalTime(request.SatStartTime);
        site.SatEndTime = ParseOptionalTime(request.SatEndTime);
        site.SunStartTime = ParseOptionalTime(request.SunStartTime);
        site.SunEndTime = ParseOptionalTime(request.SunEndTime);
        UpdateSiteOperatingHours(site, request);
        await domainContext.SaveChangesAsync(cancellationToken);
        var updated = await LoadSiteDetailAsync(id, cancellationToken);
        return ApplicationResult<SiteDetailModel>.Success(await BuildSiteDetailAsync(user, updated!, cancellationToken));
    }

    // Function summary: Archives a site idempotently and returns the updated detail model.
    public async Task<ApplicationResult<SiteDetailModel>> ArchiveAsync(
        PortalUserContext user,
        Guid id,
        string createdBy,
        CancellationToken cancellationToken)
    {
        var site = await domainContext.Sites.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (site == null)
        {
            return ApplicationResult<SiteDetailModel>.NotFound($"Site '{id}' was not found.");
        }

        if (!site.Archived)
        {
            // Build the archive export first, and only mark the site archived if it succeeds. The export runs
            // outside any transaction (it streams data and uploads a blob over many round-trips), and its failure
            // must not leave the site marked archived with no archive behind it.
            //
            // TODO: SiteArchiveService talks to Azure blob storage directly. Re-point it at the rvt-common
            // IStorage port once that abstraction is available, so the archive's blob I/O goes through the same
            // storage port as the rest of the app instead of a bespoke BlobServiceClient.
            string archiveUrl;
            try
            {
                archiveUrl = await archiveService.Process(id, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return ApplicationResult<SiteDetailModel>.ExternalServiceUnavailable(
                    "The site archive could not be created, so the site was not archived. Please try again.");
            }

            site.Archived = true;
            domainContext.SiteArchived.Add(new SiteArchived
            {
                SiteId = id,
                CreateDate = DateTime.UtcNow,
                CreatedBy = createdBy,
                PictureLink = archiveUrl
            });
            await domainContext.SaveChangesAsync(cancellationToken);
        }

        var archived = await LoadSiteDetailAsync(id, cancellationToken);
        return ApplicationResult<SiteDetailModel>.Success(await BuildSiteDetailAsync(user, archived!, cancellationToken));
    }

    // Function summary: Evaluates whether the current user can read the requested site.
    public async Task<bool> CanReadSiteAsync(PortalUserContext user, Guid id, CancellationToken cancellationToken)
    {
        if (user.IsAdmin)
        {
            return await domainContext.Sites.AnyAsync(site => site.Id == id, cancellationToken);
        }

        if (!user.UserId.HasValue)
        {
            return false;
        }

        var activeAssignments = domainContext.SiteUsers
            .Where(ActiveSiteAssignment.ForUser(user.UserId.Value, timeProvider.GetUtcNow().UtcDateTime));
        return await activeAssignments.AnyAsync(siteUser => siteUser.SiteId == id, cancellationToken);
    }

    // Function summary: Evaluates whether the current user can manage the requested site.
    public async Task<bool> CanManageSiteAsync(PortalUserContext user, Guid id, CancellationToken cancellationToken)
    {
        return user.IsAdmin && await domainContext.Sites.AnyAsync(site => site.Id == id, cancellationToken);
    }

    // Function summary: Returns notification settings for users assigned to a visible site.
    public async Task<ApplicationResult<SiteNotificationSettingsModel>> GetNotificationSettingsAsync(
        PortalUserContext user,
        Guid siteId,
        CancellationToken cancellationToken)
    {
        if (!await CanReadSiteAsync(user, siteId, cancellationToken))
        {
            return ApplicationResult<SiteNotificationSettingsModel>.NotFound($"Site '{siteId}' was not found.");
        }

        return ApplicationResult<SiteNotificationSettingsModel>.Success(await BuildNotificationSettingsAsync(user, siteId, cancellationToken));
    }

    // Function summary: Validates and updates one assigned user's notification settings for a site.
    public async Task<ApplicationResult<SiteNotificationSettingModel>> UpdateNotificationSettingAsync(
        PortalUserContext user,
        Guid siteId,
        Guid siteUserId,
        SiteNotificationSettingMutation request,
        CancellationToken cancellationToken)
    {
        var siteUser = await domainContext.SiteUsers.SingleOrDefaultAsync(
            item => item.Id == siteUserId && item.SiteId == siteId,
            cancellationToken);
        if (siteUser == null || !await CanReadSiteAsync(user, siteId, cancellationToken))
        {
            return ApplicationResult<SiteNotificationSettingModel>.NotFound($"Site '{siteId}' was not found.");
        }

        if (user.IsCompanyUser && !user.IsAdmin && user.UserId != siteUser.UserId)
        {
            return ApplicationResult<SiteNotificationSettingModel>.Forbidden();
        }

        var errors = new List<ApplicationError>();
        var start = TryParseOptionalTime(request.StartTime, nameof(SiteNotificationSettingMutation.StartTime), errors);
        var end = TryParseOptionalTime(request.EndTime, nameof(SiteNotificationSettingMutation.EndTime), errors);
        ValidateTimePair(nameof(SiteNotificationSettingMutation.StartTime), start, end, errors);
        if (errors.Count > 0)
        {
            return ApplicationResult<SiteNotificationSettingModel>.Validation(errors.ToArray());
        }

        var settings = await domainContext.NotificationSettings.SingleOrDefaultAsync(
            item => item.SiteUserId == siteUserId,
            cancellationToken);
        if (settings == null)
        {
            settings = new NotificationSettings { SiteUserId = siteUserId };
            domainContext.NotificationSettings.Add(settings);
        }

        settings.Email = request.Email;
        settings.SMS = request.Sms;
        settings.StartTime = start;
        settings.EndTime = end;
        await domainContext.SaveChangesAsync(cancellationToken);

        var updated = await BuildNotificationSettingsAsync(user, siteId, cancellationToken);
        return ApplicationResult<SiteNotificationSettingModel>.Success(updated.Settings.Single(item => item.SiteUserId == siteUserId));
    }

    // Function summary: Runs all site mutation validation and returns transport-neutral errors.
    private async Task<List<ApplicationError>> ValidateSiteAsync(
        SiteMutation request,
        Guid? currentId,
        bool requireContract,
        CancellationToken cancellationToken)
    {
        var errors = new List<ApplicationError>();
        await ValidateSiteNameAsync(request, currentId, errors, cancellationToken);
        ValidateSiteFields(request, errors);
        await ValidateSiteContractAsync(request, requireContract, errors, cancellationToken);
        await ValidateSiteCompanyAsync(request.CompanyId, errors, cancellationToken);
        return errors;
    }

    // Function summary: Validates required, length, and uniqueness rules for site names.
    private async Task ValidateSiteNameAsync(
        SiteMutation request,
        Guid? currentId,
        List<ApplicationError> errors,
        CancellationToken cancellationToken)
    {
        var siteName = request.SiteName?.Trim();
        if (string.IsNullOrWhiteSpace(siteName))
        {
            errors.Add(new ApplicationError(nameof(SiteMutation.SiteName), "The Site Name is required"));
            return;
        }

        if (siteName.Length > 100)
        {
            errors.Add(new ApplicationError(nameof(SiteMutation.SiteName), "Site name must be 100 characters or fewer."));
            return;
        }

        if (await domainContext.Sites.AnyAsync(site =>
            site.Id != currentId &&
            site.SiteName == siteName,
            cancellationToken))
        {
            errors.Add(new ApplicationError(nameof(SiteMutation.SiteName), "The Site Name is already registered"));
        }
    }

    // Function summary: Validates address lengths and legacy/daily operating-hour time pairs.
    private static void ValidateSiteFields(SiteMutation request, List<ApplicationError> errors)
    {
        ValidateMaxLength(nameof(SiteMutation.AddressLine1), request.AddressLine1, 100, errors);
        ValidateMaxLength(nameof(SiteMutation.AddressLine2), request.AddressLine2, 100, errors);
        ValidateMaxLength(nameof(SiteMutation.Postcode), request.Postcode, 10, errors);
        ValidateMaxLength(nameof(SiteMutation.City), request.City, 30, errors);
        ValidateMaxLength(nameof(SiteMutation.County), request.County, 30, errors);
        ValidateTimePair(nameof(SiteMutation.StartTime), TryParseOptionalTime(request.StartTime, nameof(SiteMutation.StartTime), errors), TryParseOptionalTime(request.EndTime, nameof(SiteMutation.EndTime), errors), errors);
        ValidateTimePair(nameof(SiteMutation.SatStartTime), TryParseOptionalTime(request.SatStartTime, nameof(SiteMutation.SatStartTime), errors), TryParseOptionalTime(request.SatEndTime, nameof(SiteMutation.SatEndTime), errors), errors);
        ValidateTimePair(nameof(SiteMutation.SunStartTime), TryParseOptionalTime(request.SunStartTime, nameof(SiteMutation.SunStartTime), errors), TryParseOptionalTime(request.SunEndTime, nameof(SiteMutation.SunEndTime), errors), errors);
        ValidateOperatingHours(request, errors);
    }

    // Function summary: Validates the initial contract assignment when site creation requires one.
    private async Task ValidateSiteContractAsync(
        SiteMutation request,
        bool requireContract,
        List<ApplicationError> errors,
        CancellationToken cancellationToken)
    {
        if (!requireContract)
        {
            return;
        }

        if (!request.ContractId.HasValue || request.ContractId.Value == Guid.Empty)
        {
            errors.Add(new ApplicationError(nameof(SiteMutation.ContractId), "The Contract is Required"));
            return;
        }

        var contract = await domainContext.Contracts.SingleOrDefaultAsync(item => item.Id == request.ContractId.Value, cancellationToken);
        ValidateSelectedContract(request, contract, errors);
    }

    // Function summary: Validates contract existence, company ownership, and unassigned state.
    private static void ValidateSelectedContract(SiteMutation request, Contract? contract, List<ApplicationError> errors)
    {
        if (contract == null)
        {
            errors.Add(new ApplicationError(nameof(SiteMutation.ContractId), "The Contract is Required"));
            return;
        }

        if (contract.CompanyId != request.CompanyId)
        {
            errors.Add(new ApplicationError(nameof(SiteMutation.ContractId), "The Contract must belong to the selected company."));
            return;
        }

        if (contract.SiteiD.HasValue)
        {
            errors.Add(new ApplicationError(nameof(SiteMutation.ContractId), "The Contract is already assigned to a site."));
        }
    }

    // Function summary: Validates that the selected company exists.
    private async Task ValidateSiteCompanyAsync(Guid companyId, List<ApplicationError> errors, CancellationToken cancellationToken)
    {
        if (!await domainContext.Companies.AnyAsync(company => company.Id == companyId, cancellationToken))
        {
            errors.Add(new ApplicationError(nameof(SiteMutation.CompanyId), "The Company is required"));
        }
    }

    // Function summary: Builds the complete site detail model expected by the SPA API mapper.
    private async Task<SiteDetailModel> BuildSiteDetailAsync(
        PortalUserContext user,
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
            .Select(entry => new SiteArchiveModel(entry.CreateDate, entry.CreatedBy, entry.PictureLink))
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
            ContractList = site.Contracts?.OrderBy(contract => contract.ContractNumber).Select(BuildContractItem).ToList() ?? [],
            Monitors = await BuildMonitorItemsAsync(site.Id, cancellationToken),
            OpenNotifications = await BuildOpenNotificationItemsAsync(site.Id, SiteDetailNotificationLimit, cancellationToken),
            Archive = archive,
            Companies = options.Companies,
            AvailableContracts = options.Contracts,
            CanManage = user.IsAdmin
        };
    }

    // Function summary: Builds site form options for companies and currently unassigned contracts.
    private async Task<SiteOptionsModel> BuildOptionsAsync(Guid? companyId, CancellationToken cancellationToken)
    {
        var companies = await domainContext.Companies
            .AsNoTracking()
            .OrderBy(company => company.CompanyName)
            .Select(company => new SiteOptionModel(company.Id.ToString(), company.CompanyName))
            .ToListAsync(cancellationToken);
        var contractsQuery = domainContext.Contracts
            .AsNoTracking()
            .Where(contract => contract.SiteiD == null);
        if (companyId.HasValue)
        {
            contractsQuery = contractsQuery.Where(contract => contract.CompanyId == companyId.Value);
        }

        var contracts = await contractsQuery
            .OrderBy(contract => contract.ContractNumber)
            .Select(contract => new SiteOptionModel(contract.Id.ToString(), contract.ContractNumber))
            .ToListAsync(cancellationToken);
        return new SiteOptionsModel
        {
            Companies = companies,
            Contracts = contracts
        };
    }

    // Function summary: Applies admin/company-user site visibility to the base site query.
    private IQueryable<Site> GetVisibleSitesQuery(PortalUserContext user)
    {
        var query = domainContext.Sites.AsNoTracking().AsQueryable();
        if (!user.IsCompanyUser || user.IsAdmin)
        {
            return query;
        }

        if (!user.UserId.HasValue)
        {
            return query.Where(_ => false);
        }

        var activeAssignments = domainContext.SiteUsers
            .Where(ActiveSiteAssignment.ForUser(user.UserId.Value, timeProvider.GetUtcNow().UtcDateTime));
        return query.Where(site => activeAssignments.Any(siteUser => siteUser.SiteId == site.Id));
    }

    // Function summary: Adds active monitor and open alert counts to site list rows.
    private async Task PopulateSiteCountersAsync(IEnumerable<SiteListModel> sites, CancellationToken cancellationToken)
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
            .Select(deployment => new { SiteId = deployment.Contract.SiteiD!.Value, deployment.MonitorId })
            .ToListAsync(cancellationToken);

        var monitorIds = deployments.Select(deployment => deployment.MonitorId).Distinct().ToList();
        var openAlertsByMonitor = monitorIds.Count == 0
            ? []
            : await domainContext.Notifications
                .AsNoTracking()
                .Where(notification => monitorIds.Contains(notification.MonitorId)
                    && notification.ClosedTime == null
                    && notification.AlertType == AlertTypeEnum.Alert)
                .GroupBy(notification => notification.MonitorId)
                .Select(group => new { MonitorId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.MonitorId, item => item.Count, cancellationToken);

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
                .Sum(monitorId => openAlertsByMonitor.TryGetValue(monitorId, out var count) ? count : 0);
        }
    }

    // Function summary: Builds active monitor rows embedded in site detail responses.
    private Task<List<SiteMonitorModel>> BuildMonitorItemsAsync(Guid siteId, CancellationToken cancellationToken)
    {
        return ProjectSiteMonitors(ActiveSiteDeployments(siteId).OrderBy(deployment => deployment.Monitor.FleetNr))
            .ToListAsync(cancellationToken);
    }

    // Function summary: The site's currently active deployments, as a composable query.
    private IQueryable<Deployment> ActiveSiteDeployments(Guid siteId)
    {
        return domainContext.Deployments
            .AsNoTracking()
            .Where(deployment => deployment.Contract.SiteiD == siteId && deployment.EndDate == null);
    }

    // Function summary: Projects deployments into site monitor rows, fetching only the columns the model needs.
    private static IQueryable<SiteMonitorModel> ProjectSiteMonitors(IQueryable<Deployment> deployments)
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
            deployment.Monitor.LastDataTime15Min ?? deployment.Monitor.LastDataTime1Min ?? deployment.Monitor.LastDataTime1Hour ?? deployment.Monitor.LastDataTime24Hour,
            false,
            deployment.Lat,
            deployment.Lng,
            deployment.What3words));
    }

    // Function summary: Builds the newest open alert rows embedded in site detail responses.
    private async Task<List<SiteNotificationModel>> BuildOpenNotificationItemsAsync(
        Guid siteId,
        int limit,
        CancellationToken cancellationToken)
    {
        var deploymentByMonitor = await LoadActiveDeploymentsByMonitorAsync(siteId, cancellationToken);
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
            .Select(notification => BuildSiteNotification(notification, deploymentByMonitor[notification.MonitorId]))
            .ToList();
    }

    // Function summary: Maps each monitor on the site to its most recent active deployment.
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
            .ToDictionary(group => group.Key, group => group.OrderByDescending(deployment => deployment.StartDate).First());
    }

    // Function summary: Projects one notification plus its owning deployment into a site notification row.
    private static SiteNotificationModel BuildSiteNotification(Notification notification, Deployment deployment)
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

    // Function summary: Builds notification-setting rows for users assigned to a site.
    private async Task<SiteNotificationSettingsModel> BuildNotificationSettingsAsync(
        PortalUserContext user,
        Guid siteId,
        CancellationToken cancellationToken)
    {
        var site = await domainContext.Sites.AsNoTracking().SingleOrDefaultAsync(item => item.Id == siteId, cancellationToken);
        if (site == null)
        {
            return new SiteNotificationSettingsModel { SiteId = siteId };
        }

        var siteUsers = await domainContext.SiteUsers
            .AsNoTracking()
            .Where(item => item.SiteId == siteId)
            .OrderByDescending(item => item.SiteContact)
            .ToListAsync(cancellationToken);
        if (user.IsCompanyUser && !user.IsAdmin)
        {
            siteUsers = user.UserId.HasValue
                ? siteUsers.Where(item => item.UserId == user.UserId.Value).ToList()
                : [];
        }

        var users = (await userDirectory.ListUsersAsync(cancellationToken))
            .ToDictionary(profile => profile.UserId, profile => profile);
        var siteUserIds = siteUsers.Select(siteUser => siteUser.Id).ToList();
        var settings = await domainContext.NotificationSettings
            .AsNoTracking()
            .Where(item => siteUserIds.Contains(item.SiteUserId))
            .ToDictionaryAsync(item => item.SiteUserId, item => item, cancellationToken);
        return new SiteNotificationSettingsModel
        {
            SiteId = siteId,
            SiteName = site.SiteName,
            Settings = siteUsers.Select(siteUser =>
            {
                users.TryGetValue(siteUser.UserId, out var userProfile);
                settings.TryGetValue(siteUser.Id, out var setting);
                return new SiteNotificationSettingModel(
                    siteUser.Id,
                    siteId,
                    siteUser.UserId,
                    userProfile?.Email ?? "",
                    userProfile?.Name,
                    siteUser.SiteContact,
                    setting?.Email ?? false,
                    setting?.SMS ?? false,
                    FormatTime(setting?.StartTime),
                    FormatTime(setting?.EndTime));
            }).ToList()
        };
    }

    // Function summary: Builds a transport-neutral page from rows the database already filtered, sorted, and paged.
    private static ApplicationResult<PagedResult<T>> BuildPagedResult<T>(
        IReadOnlyList<T> pageItems,
        int total,
        PageRequest page,
        string responseSort)
    {
        return ApplicationResult<PagedResult<T>>.Success(new PagedResult<T>
        {
            Results = pageItems.ToList(),
            Total = total,
            Page = page.Page,
            PageSize = page.PageSize,
            SearchText = page.SearchText ?? "",
            Sort = responseSort,
            SortDir = page.SortDir
        });
    }

    // Function summary: Projects a site entity into the business-layer list model.
    private static SiteListModel BuildSiteListItem(Site site)
    {
        var contracts = site.Contracts ?? [];
        var company = contracts.Select(contract => contract.Company).FirstOrDefault(company => company != null);
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
            Contracts = JoinSummary(contracts.OrderBy(contract => contract.ContractNumber).Select(contract => contract.ContractNumber)),
            CompanyName = company?.CompanyName,
            CompanyId = company?.Id,
            SiteContact = null
        };
    }

    // Function summary: Projects a contract entity into the site detail contract model.
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

    // Function summary: Loads a site with related contracts, company, and operating hours for detail output.
    private async Task<Site?> LoadSiteDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        return await domainContext.Sites
            .AsNoTracking()
            .Include(item => item.Contracts)
            .ThenInclude(contract => contract.Company)
            .Include(item => item.OperatingHours)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    // Function summary: Creates persisted operating-hour entities from daily or legacy mutation fields.
    private static List<SiteOperatingHours> BuildSiteOperatingHours(SiteMutation request)
    {
        return NormalizeOperatingHours(request)
            .Select(hours => new SiteOperatingHours
            {
                DayOfWeek = hours.DayOfWeek,
                StartTime = hours.IsClosed ? null : ParseOptionalTime(hours.StartTime),
                EndTime = hours.IsClosed ? null : ParseOptionalTime(hours.EndTime),
                IsClosed = hours.IsClosed || (string.IsNullOrWhiteSpace(hours.StartTime) && string.IsNullOrWhiteSpace(hours.EndTime))
            })
            .ToList();
    }

    // Function summary: Replaces persisted operating-hour rows for an updated site.
    private static void UpdateSiteOperatingHours(Site site, SiteMutation request)
    {
        site.OperatingHours.Clear();
        foreach (var hours in BuildSiteOperatingHours(request))
        {
            hours.SiteId = site.Id;
            site.OperatingHours.Add(hours);
        }
    }

    // Function summary: Builds Monday-Sunday operating hours using daily rows with legacy fallbacks.
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
                        hours.IsClosed || (!hours.StartTime.HasValue && !hours.EndTime.HasValue));
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

    // Function summary: Validates explicit daily operating-hour rows for uniqueness and valid time ranges.
    private static void ValidateOperatingHours(SiteMutation request, List<ApplicationError> errors)
    {
        if (request.OperatingHours is not { Count: > 0 })
        {
            return;
        }

        var seenDays = new HashSet<int>();
        foreach (var hours in request.OperatingHours)
        {
            var key = $"{nameof(SiteMutation.OperatingHours)}[{hours.DayOfWeek}]";
            if (hours.DayOfWeek is < 1 or > 7 || !seenDays.Add(hours.DayOfWeek))
            {
                errors.Add(new ApplicationError(key, "Operating hours must use unique days from 1 to 7."));
                continue;
            }

            if (hours.IsClosed)
            {
                continue;
            }

            var start = TryParseOptionalTime(hours.StartTime, key, errors);
            var end = TryParseOptionalTime(hours.EndTime, key, errors);
            ValidateTimePair(key, start, end, errors);
        }
    }

    // Function summary: Normalizes explicit or legacy hours into a full seven-day schedule.
    private static List<SiteOperatingHoursMutation> NormalizeOperatingHours(SiteMutation request)
    {
        var supplied = request.OperatingHours is { Count: > 0 }
            ? request.OperatingHours
            : LegacyOperatingHours(request);
        var byDay = supplied
            .Where(hours => hours.DayOfWeek is >= 1 and <= 7)
            .GroupBy(hours => hours.DayOfWeek)
            .ToDictionary(group => group.Key, group => group.First());
        return Enumerable.Range(1, 7)
            .Select(day => byDay.TryGetValue(day, out var hours)
                ? hours
                : new SiteOperatingHoursMutation(day, null, null, true))
            .ToList();
    }

    // Function summary: Converts weekday/Saturday/Sunday legacy fields into daily operating-hour rows.
    private static List<SiteOperatingHoursMutation> LegacyOperatingHours(SiteMutation request)
    {
        return
        [
            new(1, request.StartTime, request.EndTime, IsLegacyHoursClosed(request.StartTime, request.EndTime)),
            new(2, request.StartTime, request.EndTime, IsLegacyHoursClosed(request.StartTime, request.EndTime)),
            new(3, request.StartTime, request.EndTime, IsLegacyHoursClosed(request.StartTime, request.EndTime)),
            new(4, request.StartTime, request.EndTime, IsLegacyHoursClosed(request.StartTime, request.EndTime)),
            new(5, request.StartTime, request.EndTime, IsLegacyHoursClosed(request.StartTime, request.EndTime)),
            new(6, request.SatStartTime, request.SatEndTime, IsLegacyHoursClosed(request.SatStartTime, request.SatEndTime)),
            new(7, request.SunStartTime, request.SunEndTime, IsLegacyHoursClosed(request.SunStartTime, request.SunEndTime))
        ];
    }

    // Function summary: Builds the compact display address used by site list rows.
    private static string BuildAddress(Site site)
    {
        return string.Join(" ", new[] { site.AddressLine1, site.AddressLine2, site.Postcode, site.City }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    // Function summary: Builds a short comma-separated summary of distinct non-empty values.
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

    // Function summary: Performs case-insensitive search matching for in-memory site projections.
    private static bool Contains(string? value, string search)
    {
        return value?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    // Function summary: Trims optional text fields and stores blanks as null.
    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    // Function summary: Parses optional mutation times and records validation errors instead of throwing.
    private static TimeSpan? TryParseOptionalTime(string? value, string key, List<ApplicationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        errors.Add(new ApplicationError(key, "Time values must use HH:mm format."));
        return null;
    }

    // Function summary: Parses optional validated time values for persistence.
    private static TimeSpan? ParseOptionalTime(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : TimeSpan.Parse(value, CultureInfo.InvariantCulture);
    }

    // Function summary: Formats persisted time values for existing API contracts.
    private static string? FormatTime(TimeSpan? value)
    {
        return value?.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }

    // Function summary: Treats empty legacy start/end fields as a closed day.
    private static bool IsLegacyHoursClosed(string? startTime, string? endTime)
    {
        return string.IsNullOrWhiteSpace(startTime) && string.IsNullOrWhiteSpace(endTime);
    }

    // Function summary: Selects the legacy grouped hours that correspond to a specific day.
    private static (TimeSpan? StartTime, TimeSpan? EndTime) LegacyOperatingHoursForDay(Site site, int day)
    {
        return day switch
        {
            6 => (site.SatStartTime, site.SatEndTime),
            7 => (site.SunStartTime, site.SunEndTime),
            _ => (site.StartTime, site.EndTime)
        };
    }

    // Function summary: Validates that optional start/end times are paired and ordered.
    private static void ValidateTimePair(string key, TimeSpan? start, TimeSpan? end, List<ApplicationError> errors)
    {
        if (start.HasValue != end.HasValue)
        {
            errors.Add(new ApplicationError(key, "You need to set both start and end time"));
            return;
        }

        if (start.HasValue && start.Value >= end.GetValueOrDefault())
        {
            errors.Add(new ApplicationError(key, "Start time needs to be before end time"));
        }
    }

    // Function summary: Records a validation error when an optional field exceeds its maximum length.
    private static void ValidateMaxLength(string key, string? value, int maxLength, List<ApplicationError> errors)
    {
        if (value?.Length > maxLength)
        {
            errors.Add(new ApplicationError(key, $"{key} must be {maxLength} characters or fewer."));
        }
    }
}
